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
/// - Local builds (main branch): Build each platform separately to local Docker
/// - Release builds (tags): Build and push multi-arch manifests to registry
/// - After push to Docker Hub, sync to other registries (Aliyun, Azure)
///
/// Environment variables:
/// - PushToRegistry: true to push to registry, false for local-only builds
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

    Target DockerLogin => _ => _
        .DependsOn(DockerBuild)
        .Requires(() => DockerUsername)
        .Requires(() => DockerPassword)
        .Executes(DockerLoginExecute);

    Target DockerBuild => _ => _
        .DependsOn(Extract)
        .Requires(() => DockerImageName)
        .Produces(OutputDirectory / "docker-image-built.txt")
        .Executes(DockerBuildExecute);

    Target DockerPush => _ => _
        .DependsOn(DockerBuild)
        .Requires(() => DockerUsername)
        .Requires(() => DockerPassword)
        .Executes(() =>
        {
            if (!PushToRegistry)
            {
                Log.Information("PushToRegistry=false: Skipping Docker Hub push (images already pushed by DockerBuild)");
                return;
            }
            DockerPushExecute();
        });

    Target DockerPushAzure => _ => _
        .DependsOn(DockerBuild)
        .Requires(() => AzureAcrUsername)
        .Requires(() => AzureAcrPassword)
        .Requires(() => AzureAcrRegistry)
        .Executes(() =>
        {
            if (!PushToRegistry)
            {
                Log.Information("PushToRegistry=false: Skipping Azure ACR push");
                return;
            }
            DockerPushAzureExecute();
        });

    Target DockerPushAliyun => _ => _
        .DependsOn(DockerBuild)
        .Requires(() => AliyunAcrUsername)
        .Requires(() => AliyunAcrPassword)
        .Requires(() => AliyunAcrRegistry)
        .Executes(() =>
        {
            if (!PushToRegistry)
            {
                Log.Information("PushToRegistry=false: Skipping Aliyun ACR push");
                return;
            }
            DockerPushAliyunExecute();
        });

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

        // Step 2.5: Verify base image availability in registry (only for multi-arch builds with push enabled)
        if (IsMultiArchBuild && PushToRegistry)
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
    /// When PushToRegistry is true, pushes to registry and verifies availability
    /// When PushToRegistry is false, exports to tar and loads to local Docker for local builds
    /// </summary>
    void BuildMultiArchBaseImage()
    {
        Log.Information("Building multi-architecture base image: {BaseTag}", BaseDockerTag);
        Log.Information("  Platforms: {Platforms}", string.Join(", ", TargetDockerPlatforms));
        Log.Information("  Context: {Context}", DockerDeploymentDirectory);
        Log.Information("  Push to registry: {Push}", PushToRegistry);

        // Create multi-arch builder if it doesn't exist
        EnsureBuildxBuilder("hagicode-multiarch");

        var platformsArg = string.Join(",", TargetDockerPlatforms);
        var buildxBaseTag = $"{BaseImageName}:base-{FullVersion}";
        var tempTarPath = OutputDirectory / "base-image.tar";

        if (PushToRegistry)
        {
            // For tag builds: push directly to registry
            var outputType = "registry";

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
                                $"--output {outputType} " +
                                $"--builder hagicode-multiarch " +
                                $"\"{DockerDeploymentDirectory}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            Log.Information("Starting buildx build process (output type: {OutputType})...", outputType);
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Log.Error("Multi-arch base image build failed");
                Log.Error("Exit code: {ExitCode}", process.ExitCode);
                Log.Error("Error: {Error}", error);

                if (error.Contains("unauthorized") || error.Contains("denied") || error.Contains("401"))
                {
                    Log.Error("This appears to be an authentication error. Please check your registry credentials.");
                    throw new Exception($"Failed to build multi-architecture base image (authentication error): {error}");
                }

                throw new Exception($"Failed to build multi-architecture base image: {error}");
            }

            Log.Information("✓ Multi-architecture base image built and pushed to registry: {BaseTag}", BaseDockerTag);
        }
        else
        {
            // For main branch builds: build each platform separately and load to local Docker
            Log.Information("Building base image for each platform separately (local build mode)");

            // Store platform-specific base image tags for later use
            var platformBaseTags = new Dictionary<string, string>();

            foreach (var platform in TargetDockerPlatforms)
            {
                var platformTag = $"{BaseImageName}:base-{FullVersion}-{platform.Replace("/", "-")}";
                var dockerfile = platform.Contains("arm64") ? "Dockerfile.base.arm64" : "Dockerfile.base";

                Log.Information("Building platform {Platform}: {Tag}", platform, platformTag);

                DockerTasks.DockerBuild(s => s
                    .SetPath(DockerDeploymentDirectory)
                    .SetFile(DockerDeploymentDirectory / dockerfile)
                    .SetTag(platformTag)
                    .SetPlatform(platform)
                    .EnableRm()
                    .EnablePull());

                platformBaseTags[platform] = platformTag;
                Log.Information("✓ Platform {Platform} base image built: {Tag}", platform, platformTag);
            }

            Log.Information("✓ All platform base images built locally");
            Log.Information("  Platform-specific tags: {Tags}", string.Join(", ", platformBaseTags.Values));

            // Store for use in application build
            PlatformBaseTags = platformBaseTags;
        }
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
    /// When PushToRegistry is true, pushes to registry
    /// When PushToRegistry is false, builds each platform separately to local Docker
    /// </summary>
    void BuildMultiArchApplicationImage(List<string> tags)
    {
        Log.Information("Building multi-architecture application image");
        Log.Information("  Platforms: {Platforms}", string.Join(", ", TargetDockerPlatforms));
        Log.Information("  Context: {Context}", DockerBuildContext);
        Log.Information("  Push to registry: {Push}", PushToRegistry);

        if (PushToRegistry)
        {
            // For tag builds: use buildx to push multi-arch manifest to registry
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
                Log.Error("Multi-arch application build failed: {Error}", error);
                throw new Exception($"Failed to build multi-architecture application image: {error}");
            }

            Log.Information("✓ Multi-architecture application image built and pushed to registry");
            Log.Debug("Build output: {Output}", output);
        }
        else
        {
            // For main branch builds: build each platform separately to local Docker
            Log.Information("Building application image for each platform separately (local build mode)");

            foreach (var platform in TargetDockerPlatforms)
            {
                Log.Information("Building platform {Platform}", platform);

                // Get the platform-specific base image tag
                var platformBaseTag = PlatformBaseTags.TryGetValue(platform, out var tag)
                    ? tag
                    : BaseDockerTag;

                Log.Information("  Using base image: {BaseTag}", platformBaseTag);

                // Generate platform-specific Dockerfile with correct base image
                var dockerfileContent = GenerateAppDockerfile(platformBaseTag);
                var platformDockerfilePath = DockerBuildContext / $"Dockerfile.{platform.Replace("/", "-")}";
                System.IO.File.WriteAllText(platformDockerfilePath, dockerfileContent);

                DockerTasks.DockerBuild(s => s
                    .SetPath(DockerBuildContext)
                    .SetFile(platformDockerfilePath)
                    .SetTag(tags)
                    .SetPlatform(platform)
                    .SetBuildArg($"VERSION={FullVersion}")
                    .EnableRm()
                    .EnablePull());

                Log.Information("✓ Platform {Platform} application image built", platform);
            }

            Log.Information("✓ All platform application images built locally");
        }
    }

    /// <summary>
    /// Generates the application Dockerfile content
    /// </summary>
    string GenerateAppDockerfile(string baseImageTag = null)
    {
        var templateContent = System.IO.File.ReadAllText(DockerDeploymentDirectory / "Dockerfile.app.template");
        var baseTagToUse = baseImageTag ?? BaseDockerTag;
        return templateContent
            .Replace("{version}", FullVersion)
            .Replace("{build_date}", BuildDate)
            .Replace("{base_image_name}:base", baseTagToUse);
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
            PushImageToRegistry(localImage, AzureAcrRegistry, AzureAcrUsername, AzureAcrPassword, tags);
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
            PushImageToRegistry(localImage, AliyunAcrRegistry, AliyunAcrUsername, AliyunAcrPassword, tags);
        }

        Log.Information("Docker images pushed to Aliyun ACR successfully");
    }

    /// <summary>
    /// Push multi-architecture image manifests to a registry
    /// Pulls from Docker Hub and pushes to target registry (no rebuild)
    /// </summary>
    void PushMultiArchImages(string registry, string username, string password)
    {
        Log.Information("Syncing multi-architecture images to {Registry}", registry);
        Log.Information("  Source: Docker Hub ({SourceTag})", BaseImageName);

        var tags = GetAppDockerTags();

        // Login to target registry
        DockerTasks.DockerLogin(s => s
            .SetServer(registry)
            .SetUsername(username)
            .SetPassword(password));

        // For each tag, pull from Docker Hub, re-tag, and push to target registry
        foreach (var tag in tags)
        {
            // Extract tag part after the registry
            var tagPart = tag.Substring(tag.IndexOf(':') + 1);
            var targetTag = $"{registry}/{DockerImageName}:{tagPart}";

            Log.Information("Syncing {SourceTag} -> {TargetTag}", tag, targetTag);

            // Use docker buildx imagetools to create manifest and push
            // This pulls the multi-arch manifest from Docker Hub and pushes to target
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"buildx imagetools create " +
                                $"--tag \"{targetTag}\" " +
                                $"\"{tag}\"",
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
                Log.Error("Failed to sync multi-arch manifest {Tag}: {Error}", tag, error);
                throw new Exception($"Failed to sync multi-architecture manifest: {error}");
            }

            Log.Information("✓ Synced multi-arch manifest: {TargetTag}", targetTag);
        }

        // Also sync the base image
        var baseTargetTag = $"{registry}/{DockerImageName}:base";
        Log.Information("Syncing base image {SourceTag} -> {TargetTag}", BaseDockerTag, baseTargetTag);

        var baseProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"buildx imagetools create " +
                            $"--tag \"{baseTargetTag}\" " +
                            $"\"{BaseDockerTag}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        baseProcess.Start();
        var baseOutput = baseProcess.StandardOutput.ReadToEnd();
        var baseError = baseProcess.StandardError.ReadToEnd();
        baseProcess.WaitForExit();

        if (baseProcess.ExitCode != 0)
        {
            Log.Error("Failed to sync base image: {Error}", baseError);
            throw new Exception($"Failed to sync base image: {baseError}");
        }

        Log.Information("✓ Synced base image: {TargetTag}", baseTargetTag);
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
