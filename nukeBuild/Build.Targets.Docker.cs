using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.Docker;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

/// <summary>
/// Docker targets - builds, logs in, and pushes Docker images
/// Supports multi-architecture builds (linux/amd64, linux/arm64)
///
/// For multi-architecture builds:
/// - Base image is pushed immediately to registry using type=registry output
/// - Base image availability is verified before application build starts
/// - Retry logic with exponential backoff handles registry propagation delays
/// - Configure retry behavior via DOCKER_VERIFY_MAX_RETRIES environment variable (default: 5)
/// </summary>
partial class Build
{
    /// <summary>
    /// Gets the target Docker platforms based on Platform parameter
    /// </summary>
    List<string> TargetDockerPlatforms => Platform.ToLowerInvariant() switch
    {
        "all" => new List<string> { "linux/amd64", "linux/arm64" },
        "linux-arm64" => new List<string> { "linux/arm64" },
        "linux-x64" => new List<string> { "linux/amd64" },
        _ => new List<string> { "linux/amd64" }
    };

    /// <summary>
    /// Gets the base Dockerfile path based on target platform(s)
    /// For multi-arch builds, returns the AMD64 Dockerfile as reference
    /// </summary>
    AbsolutePath BaseDockerfilePath => TargetDockerPlatforms.Count > 1
        ? DockerDeploymentDirectory / "Dockerfile.base"
        : (TargetDockerPlatforms.Contains("linux/arm64")
            ? DockerDeploymentDirectory / "Dockerfile.base.arm64"
            : DockerDeploymentDirectory / "Dockerfile.base");

    /// <summary>
    /// Gets whether this is a multi-architecture build
    /// </summary>
    bool IsMultiArchBuild => TargetDockerPlatforms.Count > 1;

    Target DockerBuild => _ => _
        .DependsOn(Extract)
        .Requires(() => DockerImageName)
        .Produces(OutputDirectory / "docker-image-built.txt")
        .Executes(DockerBuildExecute);

    Target DockerLogin => _ => _
        .DependsOn(DockerBuild)
        .Requires(() => DockerUsername)
        .Requires(() => DockerPassword)
        .Executes(DockerLoginExecute);

    Target DockerPush => _ => _
        .DependsOn(DockerLogin)
        .Requires(() => DockerUsername)
        .Requires(() => DockerPassword)
        .Executes(DockerPushExecute);

    Target DockerPushAzure => _ => _
        .DependsOn(DockerBuild)
        .Requires(() => AzureAcrUsername)
        .Requires(() => AzureAcrPassword)
        .Requires(() => AzureAcrRegistry)
        .Executes(DockerPushAzureExecute);

    Target DockerPushAliyun => _ => _
        .DependsOn(DockerBuild)
        .Requires(() => AliyunAcrUsername)
        .Requires(() => AliyunAcrPassword)
        .Requires(() => AliyunAcrRegistry)
        .Executes(DockerPushAliyunExecute);

    Target DockerPushAll => _ => _
        .DependsOn(DockerPush, DockerPushAliyun, DockerPushAzure)
        .Executes(() =>
        {
            Log.Information("All Docker registry pushes completed");
        });

    /// <summary>
/// Main Docker build execution
/// Builds both base and application Docker images
/// For multi-arch builds, verifies base image availability before building application images
/// </summary>
    void DockerBuildExecute()
    {
        Log.Information("Building Docker images (base + application)");
        Log.Information("Target platforms: {Platforms}", string.Join(", ", TargetDockerPlatforms));

        if (IsMultiArchBuild)
        {
            Log.Information("Multi-architecture build requested - using docker buildx with QEMU");
        }

        var appTags = GetAppDockerTags();
        Log.Information("Application tags: {Tags}", string.Join(", ", appTags));

        // Step 1: Setup QEMU for cross-architecture builds (if multi-arch)
        if (IsMultiArchBuild)
        {
            SetupQemu();
        }

        // Step 2: Build base image(s)
        BuildDockerBaseImage();

        // Step 2.5: Verify base image availability in registry (for multi-arch builds)
        if (IsMultiArchBuild)
        {
            VerifyBaseImageAvailable(BaseDockerTag);
        }

        // Step 3: Generate application Dockerfile from template
        var dockerfileContent = GenerateAppDockerfile();
        PrepareDockerBuildContext(dockerfileContent);

        // Step 4: Build application image
        BuildDockerApplicationImage(appTags);

        // Mark that docker image was built
        System.IO.File.WriteAllText(
            OutputDirectory / "docker-image-built.txt",
            $"Built at {DateTime.UtcNow:O}\nPlatforms: {string.Join(", ", TargetDockerPlatforms)}\nBase: {BaseDockerTag}\nApp: {string.Join(", ", appTags)}");
    }

    /// <summary>
    /// Setup QEMU for cross-architecture Docker builds
    /// Enables building ARM64 images on AMD64 hosts and vice versa
    /// </summary>
    void SetupQemu()
    {
        Log.Information("Setting up QEMU for cross-architecture builds");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "run --privileged --rm tonistiigi/binfmt --install all",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            Log.Warning("QEMU setup completed with warnings: {Error}", error);
        }
        else
        {
            Log.Information("✓ QEMU setup completed successfully");
            Log.Debug("QEMU output: {Output}", output);
        }
    }

    void BuildDockerBaseImage()
    {
        if (IsMultiArchBuild)
        {
            BuildMultiArchBaseImage();
            return;
        }

        var targetPlatform = TargetDockerPlatforms.First();
        var dockerfile = targetPlatform.Contains("arm64")
            ? "Dockerfile.base.arm64"
            : "Dockerfile.base";

        Log.Information("Building base image: {BaseTag} (platform: {Platform})", BaseDockerTag, targetPlatform);
        Log.Information("  Context: {Context}", DockerDeploymentDirectory);
        Log.Information("  Dockerfile: {DockerfileBase}", DockerDeploymentDirectory / dockerfile);

        DockerTasks.DockerBuild(s => s
            .SetPath(DockerDeploymentDirectory)
            .SetFile(DockerDeploymentDirectory / dockerfile)
            .SetTag(BaseDockerTag)
            .SetPlatform(targetPlatform)
            .EnableRm()
            .EnablePull());

        Log.Information("✓ Base image built successfully: {BaseTag}", BaseDockerTag);
    }

    /// <summary>
    /// Build multi-architecture base image using buildx
    /// Image is immediately pushed to registry to ensure availability for application builds
    /// </summary>
    void BuildMultiArchBaseImage()
    {
        Log.Information("Building multi-architecture base image: {BaseTag}", BaseDockerTag);
        Log.Information("  Platforms: {Platforms}", string.Join(", ", TargetDockerPlatforms));
        Log.Information("  Context: {Context}", DockerDeploymentDirectory);
        Log.Information("  Output type: registry (push immediately to registry)");

        // Create multi-arch builder if it doesn't exist
        EnsureBuildxBuilder("hagicode-multiarch");

        var platformsArg = string.Join(",", TargetDockerPlatforms);
        var buildxBaseTag = $"{BaseImageName}:base-{FullVersion}";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"buildx build " +
                            $"--platform {platformsArg} " +
                            $"--file \"{DockerDeploymentDirectory / "Dockerfile.base"}\" " +
                            $"--tag \"{buildxBaseTag}\" " +
                            $"--tag \"{BaseDockerTag}\" " +
                            $"--output type=registry " +
                            $"--builder hagicode-multiarch " +
                            $"\"{DockerDeploymentDirectory}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        Log.Information("Starting buildx build process...");
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            Log.Error("Multi-arch base image build failed");
            Log.Error("Exit code: {ExitCode}", process.ExitCode);
            Log.Error("Error: {Error}", error);

            // Check for authentication errors
            if (error.Contains("unauthorized") || error.Contains("denied") || error.Contains("401"))
            {
                Log.Error("This appears to be an authentication error. Please check your registry credentials.");
                throw new Exception($"Failed to build multi-architecture base image (authentication error): {error}");
            }

            throw new Exception($"Failed to build multi-architecture base image: {error}");
        }

        Log.Information("✓ Multi-architecture base image built and pushed to registry: {BaseTag}", BaseDockerTag);
        Log.Information("✓ Additional tag: {BuildxBaseTag}", buildxBaseTag);
        Log.Debug("Build output: {Output}", output);
    }

    /// <summary>
    /// Ensure buildx builder exists for multi-architecture builds
    /// </summary>
    void EnsureBuildxBuilder(string builderName)
    {
        Log.Information("Ensuring buildx builder '{Builder}' exists", builderName);

        // Check if builder exists
        var checkProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "buildx ls",
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };

        checkProcess.Start();
        var output = checkProcess.StandardOutput.ReadToEnd();
        checkProcess.WaitForExit();

        if (output.Contains(builderName))
        {
            Log.Information("Builder '{Builder}' already exists", builderName);
            return;
        }

        // Create builder
        Log.Information("Creating buildx builder '{Builder}'...", builderName);
        var createProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"buildx create --name {builderName} --driver docker-container --use",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        createProcess.Start();
        var createOutput = createProcess.StandardOutput.ReadToEnd();
        var createError = createProcess.StandardError.ReadToEnd();
        createProcess.WaitForExit();

        if (createProcess.ExitCode != 0)
        {
            Log.Warning("Builder creation had issues: {Error}", createError);
        }
        else
        {
            Log.Information("✓ Builder '{Builder}' created successfully", builderName);
        }
    }

    string GenerateAppDockerfile()
    {
        var templateContent = System.IO.File.ReadAllText(DockerDeploymentDirectory / "Dockerfile.app.template");
        return templateContent
            .Replace("{version}", FullVersion)
            .Replace("{build_date}", BuildDate)
            .Replace("{base_image_name}", BaseImageName);
    }

    void PrepareDockerBuildContext(string dockerfileContent)
    {
        DockerBuildContext.CreateDirectory();

        // Write Dockerfile
        var appDockerfilePath = DockerBuildContext / "Dockerfile";
        System.IO.File.WriteAllText(appDockerfilePath, dockerfileContent);

        // Copy entrypoint script
        System.IO.File.Copy(
            DockerDeploymentDirectory / "docker-entrypoint.sh",
            DockerBuildContext / "docker-entrypoint.sh",
            true);

        // Copy lib/ directory (framework-dependent assemblies)
        var libDir = ExtractedDirectory / "lib";
        if (!System.IO.Directory.Exists(libDir))
        {
            throw new Exception($"Required lib/ directory not found in extracted package at {libDir}");
        }

        // Create lib/ subdirectory in build context
        var targetLibDir = DockerBuildContext / "lib";
        targetLibDir.CreateDirectory();
        CopyDirectoryRecursive(libDir, targetLibDir);

        // Copy any .sh scripts from package
        foreach (var scriptFile in ExtractedDirectory.GlobFiles("*.sh"))
        {
            System.IO.File.Copy(scriptFile, DockerBuildContext / scriptFile.Name, true);
        }

        Log.Information("Docker build context prepared at: {Context}", DockerBuildContext);
    }

    void BuildDockerApplicationImage(List<string> tags)
    {
        if (IsMultiArchBuild)
        {
            BuildMultiArchApplicationImage(tags);
            return;
        }

        var targetPlatform = TargetDockerPlatforms.First();
        Log.Information("Building application image (platform: {Platform})", targetPlatform);
        Log.Information("  Context: {Context}", DockerBuildContext);
        Log.Information("  Dockerfile: {Dockerfile}", DockerBuildContext / "Dockerfile");

        DockerTasks.DockerBuild(s => s
            .SetPath(DockerBuildContext)
            .SetFile(DockerBuildContext / "Dockerfile")
            .SetTag(tags)
            .SetPlatform(targetPlatform)
            .SetBuildArg($"VERSION={FullVersion}")
            .EnableRm()
            .EnablePull());

        Log.Information("✓ Application image built successfully");
    }

    /// <summary>
    /// Build multi-architecture application image using buildx
    /// </summary>
    void BuildMultiArchApplicationImage(List<string> tags)
    {
        Log.Information("Building multi-architecture application image");
        Log.Information("  Platforms: {Platforms}", string.Join(", ", TargetDockerPlatforms));
        Log.Information("  Context: {Context}", DockerBuildContext);

        var platformsArg = string.Join(",", TargetDockerPlatforms);
        var tagsArg = string.Join(" ", tags.Select(t => $"--tag \"{t}\""));

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"buildx build " +
                            $"--platform {platformsArg} " +
                            $"{tagsArg} " +
                            $"--file \"{DockerBuildContext / "Dockerfile"}\" " +
                            $"--builder hagicode-multiarch " +
                            $"--output type=image,push=false " +
                            $"--build-arg \"VERSION={FullVersion}\" " +
                            $"\"{DockerBuildContext}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            Log.Error("Multi-arch application build failed: {Error}", error);
            throw new Exception($"Failed to build multi-architecture application image: {error}");
        }

        Log.Information("✓ Multi-architecture application image built successfully");
        Log.Debug("Build output: {Output}", output);
    }

    void DockerLoginExecute()
    {
        Log.Information("Logging in to Docker Hub");

        DockerTasks.DockerLogin(s => s
            .SetServer("docker.io")
            .SetUsername(DockerUsername)
            .SetPassword(DockerPassword));

        Log.Information("Docker login successful");
    }

    void DockerPushExecute()
    {
        Log.Information("Pushing Docker images to Docker Hub");

        if (IsMultiArchBuild)
        {
            PushMultiArchImages("docker.io", DockerUsername, DockerPassword);
        }
        else
        {
            var tags = GetAppDockerTags();
            foreach (var tag in tags)
            {
                Log.Information("Pushing {Tag}", tag);
                DockerTasks.DockerPush(s => s.SetName(tag));
            }
        }

        Log.Information("All Docker images pushed to Docker Hub successfully");
    }

    void DockerPushAzureExecute()
    {
        Log.Information("Pushing Docker images to Azure Container Registry");

        var tags = GetAppDockerTags();

        if (IsMultiArchBuild)
        {
            PushMultiArchImages(AzureAcrRegistry, AzureAcrUsername, AzureAcrPassword);
        }
        else
        {
            var localImage = $"{BaseImageName}:{FullVersion}";
            PushToRegistry(localImage, AzureAcrRegistry, AzureAcrUsername, AzureAcrPassword, tags);
        }

        Log.Information("Docker images pushed to Azure ACR successfully");
    }

    void DockerPushAliyunExecute()
    {
        Log.Information("Pushing Docker images to Aliyun Container Registry");

        var tags = GetAppDockerTags();

        if (IsMultiArchBuild)
        {
            PushMultiArchImages(AliyunAcrRegistry, AliyunAcrUsername, AliyunAcrPassword);
        }
        else
        {
            var localImage = $"{BaseImageName}:{FullVersion}";
            PushToRegistry(localImage, AliyunAcrRegistry, AliyunAcrUsername, AliyunAcrPassword, tags);
        }

        Log.Information("Docker images pushed to Aliyun ACR successfully");
    }

    /// <summary>
    /// Push multi-architecture image manifests to a registry
    /// </summary>
    void PushMultiArchImages(string registry, string username, string password)
    {
        Log.Information("Pushing multi-architecture images to {Registry}", registry);

        var tags = GetAppDockerTags();
        var platformsArg = string.Join(",", TargetDockerPlatforms);

        // Login to registry
        DockerTasks.DockerLogin(s => s
            .SetServer(registry)
            .SetUsername(username)
            .SetPassword(password));

        // Push each tag as a multi-architecture manifest
        foreach (var tag in tags)
        {
            Log.Information("Pushing multi-arch manifest: {Tag}", tag);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"buildx build " +
                                $"--platform {platformsArg} " +
                                $"--tag \"{tag}\" " +
                                $"--file \"{DockerBuildContext / "Dockerfile"}\" " +
                                $"--builder hagicode-multiarch " +
                                $"--output type=registry " +
                                $"--build-arg \"VERSION={FullVersion}\" " +
                                $"\"{DockerBuildContext}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Log.Error("Failed to push multi-arch manifest {Tag}: {Error}", tag, error);
                throw new Exception($"Failed to push multi-architecture manifest: {error}");
            }

            Log.Information("✓ Pushed multi-arch manifest: {Tag}", tag);
        }
    }

    /// <summary>
    /// Check if a Docker image exists in the registry
    /// Uses docker manifest inspect to verify availability
    /// </summary>
    bool IsImageInRegistry(string imageTag)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"manifest inspect \"{imageTag}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        process.WaitForExit();

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        Log.Debug("Manifest inspect result for {Image}: ExitCode={ExitCode}", imageTag, process.ExitCode);

        return process.ExitCode == 0;
    }

    /// <summary>
    /// Verify that a base image is available in the registry before proceeding with dependent builds
    /// Implements retry logic with exponential backoff to handle registry propagation delays
    /// </summary>
    void VerifyBaseImageAvailable(string imageTag, int maxRetries = 5)
    {
        // Allow configuration via environment variable
        var envRetries = Environment.GetEnvironmentVariable("DOCKER_VERIFY_MAX_RETRIES");
        if (!string.IsNullOrEmpty(envRetries) && int.TryParse(envRetries, out var parsedRetries))
        {
            maxRetries = Math.Max(1, Math.Min(20, parsedRetries));
        }

        Log.Information("Verifying base image availability: {Image}", imageTag);
        Log.Information("  Max retries: {MaxRetries}", maxRetries);

        var delay = TimeSpan.FromSeconds(2);
        var totalWaitTime = TimeSpan.Zero;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            if (IsImageInRegistry(imageTag))
            {
                Log.Information("✓ Base image verified in registry");
                if (attempt > 1)
                {
                    Log.Information("  Waited {TotalWaitTime} before verification succeeded", totalWaitTime);
                }
                return;
            }

            // Don't sleep on the last attempt
            if (attempt < maxRetries)
            {
                Log.Warning("Base image not yet available, retrying in {Delay}s (attempt {Attempt}/{Max})",
                    delay.TotalSeconds, attempt, maxRetries);

                Thread.Sleep(delay);
                totalWaitTime += delay;
                delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2); // Exponential backoff
            }
        }

        // Verification failed after all retries
        Log.Error("Base image {Image} not available in registry after {MaxRetries} attempts", imageTag, maxRetries);
        Log.Error("  Total wait time: {TotalWaitTime}", totalWaitTime);
        throw new Exception($"Base image {imageTag} not available in registry after {maxRetries} attempts (total wait: {totalWaitTime.TotalSeconds}s)");
    }

    List<string> GetAppDockerTags()
    {
        var tags = new List<string>
        {
            $"{BaseImageName}:{FullVersion}",
            $"{BaseImageName}:{MinorVersion}",
            $"{BaseImageName}:{MajorVersion}"
        };

        if (!IsPreRelease)
        {
            tags.Add($"{BaseImageName}:latest");
        }

        return tags;
    }
}
