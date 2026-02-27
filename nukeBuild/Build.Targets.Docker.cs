using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

/// Docker build targets - multi-architecture image building and Edge ACR publishing
///
/// This file implements Docker multi-architecture build capabilities using Docker buildx with QEMU emulation.
/// Supports building for linux/amd64 and linux/arm64 platforms and pushing to Edge ACR.
/// </summary>
partial class Build
{
    // ==========================================================================
    // Docker Parameters
    // ==========================================================================

    /// Gets the Docker platform to build for
    /// </summary>
    [Parameter("Docker platform: linux-amd64, linux-arm64, or all (default: all)")]
    readonly string DockerPlatform = "all";

    /// Gets the Edge ACR username
    /// </summary>
    [Parameter("Edge ACR username")]
    [Secret]
    readonly string AzureAcrUsername = string.Empty;

    /// Gets the Edge ACR password
    /// </summary>
    [Parameter("Edge ACR password")]
    [Secret]
    readonly string AzureAcrPassword = string.Empty;

    /// Gets the Edge ACR registry endpoint
    /// </summary>
    [Parameter("Edge ACR registry endpoint (e.g., hagicode.azurecr.io)")]
    [Secret]
    readonly string AzureAcrRegistry = string.Empty;

    /// Gets the Docker image name
    /// </summary>
    [Parameter("Docker image name (e.g., hagicode/hagicode)")]
    readonly string DockerImageName = "hagicode/hagicode";

    /// Gets the Docker build timeout in seconds
    /// </summary>
    [Parameter("Docker build timeout in seconds (default: 3600)")]
    readonly int DockerBuildTimeout = 3600;

    /// Gets the force rebuild flag
    /// </summary>
    [Parameter("Force rebuild of Docker images")]
    readonly bool DockerForceRebuild = false;

    // ==========================================================================
    // Docker State Properties
    // ==========================================================================

    /// Gets the Docker buildx builder name
    /// </summary>
    string DockerBuilderName => "hagicode-multiarch";

    /// Gets the Docker deployment directory
    /// </summary>
    AbsolutePath DockerDeploymentDirectory => RootDirectory / "docker_deployment";

    /// Gets the Docker base Dockerfile path
    /// </summary>
    AbsolutePath DockerBaseDockerfile => DockerDeploymentDirectory / "Dockerfile.base";

    /// Gets the Docker ARM64 base Dockerfile path
    /// </summary>
    AbsolutePath DockerBaseArm64Dockerfile => DockerDeploymentDirectory / "Dockerfile.base.arm64";

    /// Gets the Docker app template Dockerfile path
    /// </summary>
    AbsolutePath DockerAppTemplateDockerfile => DockerDeploymentDirectory / "Dockerfile.app.template";

    /// Gets the Docker entrypoint script path
    /// </summary>
    AbsolutePath DockerEntrypointScript => DockerDeploymentDirectory / "docker-entrypoint.sh";

    /// Gets the extracted package directory for Docker build
    /// </summary>
    AbsolutePath DockerBuildContext => OutputDirectory / "docker-build-context";

    /// Gets the generated app Dockerfile path
    /// </summary>
    AbsolutePath GeneratedAppDockerfile => DockerBuildContext / "Dockerfile.app";

    /// Gets the Docker output directory
    /// </summary>
    AbsolutePath DockerOutputDirectory => OutputDirectory / "docker-output";

    /// Gets the base image tag
    /// </summary>
    string BaseImageTag => $"{DockerImageName}:base";

    /// Gets the full image tag with version
    /// </summary>
    string GetFullImageTag(string version) => $"{DockerImageName}:{version}";

    /// Gets the registry image tag
    /// </summary>
    string GetRegistryImageTag(string version) => $"{EffectiveAzureAcrRegistry}/{DockerImageName}:{version}";

    /// Gets the registry base image tag
    /// </summary>
    string RegistryBaseImageTag => $"{EffectiveAzureAcrRegistry}/{DockerImageName}:base";

    /// Gets the effective Edge ACR registry from environment variable or parameter
    /// </summary>
    string EffectiveAzureAcrRegistry => Environment.GetEnvironmentVariable("NUGEX_AzureAcrRegistry") ?? AzureAcrRegistry;

    /// Gets the effective Edge ACR username from environment variable or parameter
    /// </summary>
    string EffectiveAzureAcrUsername => Environment.GetEnvironmentVariable("NUGEX_AzureAcrUsername") ?? AzureAcrUsername;

    /// Gets the effective Edge ACR password from environment variable or parameter
    /// </summary>
    string EffectiveAzureAcrPassword => Environment.GetEnvironmentVariable("NUGEX_AzureAcrPassword") ?? AzureAcrPassword;

    /// Gets the target platforms for multi-arch build
    /// </summary>
    IEnumerable<string> GetTargetPlatforms()
    {
        return DockerPlatform.ToLowerInvariant() switch
        {
            "linux-amd64" => new[] { "linux/amd64" },
            "linux-arm64" => new[] { "linux/arm64" },
            "amd64" => new[] { "linux/amd64" },
            "arm64" => new[] { "linux/arm64" },
            _ => new[] { "linux/amd64", "linux/arm64" }
        };
    }

    // ==========================================================================
    // Docker Targets
    // ==========================================================================

    /// Docker build target - builds Docker images
    /// </summary>
    Target DockerBuild => _ => _
        .DependsOn(Download)
        .Executes(DockerBuildExecute);

    /// Docker push target - pushes Docker images to Edge ACR
    /// </summary>
    Target DockerPush => _ => _
        .DependsOn(DockerBuild)
        .Executes(DockerPushExecute);

    /// Docker release target - builds and pushes Docker images to Edge ACR
    /// </summary>
    Target DockerRelease => _ => _
        .DependsOn(DockerPush)
        .Executes(() =>
        {
            Log.Information("Docker release completed successfully");
        });

    // ==========================================================================
    // Docker Build Implementation
    // ==========================================================================

    void DockerBuildExecute()
    {
        Log.Information("Starting Docker build");

        var version = EffectiveReleaseVersion;
        var platforms = GetTargetPlatforms();

        Log.Information("Building for version: {Version}", version);
        Log.Information("Target platforms: {Platforms}", string.Join(", ", platforms));
        Log.Information("Platform mode: {Platform}", DockerPlatform);

        // Check if Docker is available
        if (!IsDockerAvailable())
        {
            throw new Exception("Docker is not available. Please ensure Docker is installed and running.");
        }

        // Setup QEMU for multi-arch builds
        if (platforms.Count() > 1)
        {
            SetupQemu();
            SetupBuildxBuilder();
        }

        // Build base image
        BuildBaseImage(platforms);

        // Build application image
        BuildApplicationImage(version, platforms);

        Log.Information("Docker build completed");
    }

    bool IsDockerAvailable()
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "docker",
                ArgumentList = { "--version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return false;
            }

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    void SetupQemu()
    {
        Log.Information("Setting up QEMU for cross-architecture builds...");

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "docker",
                ArgumentList =
                {
                    "run",
                    "--privileged",
                    "--rm",
                    "tonistiigi/binfmt",
                    "--install", "all"
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                throw new Exception("Failed to start QEMU setup process");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Log.Warning("QEMU setup may have failed: {Error}", error);
            }
            else
            {
                Log.Information("QEMU setup completed");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "QEMU setup failed, continuing anyway...");
        }
    }

    void SetupBuildxBuilder()
    {
        Log.Information("Setting up Docker buildx builder: {BuilderName}", DockerBuilderName);

        try
        {
            // Check if builder already exists
            var existingBuilder = GetExistingBuilder();

            if (existingBuilder == null)
            {
                Log.Information("Creating new buildx builder: {BuilderName}", DockerBuilderName);

                var processInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    ArgumentList =
                    {
                        "buildx",
                        "create",
                        "--name", DockerBuilderName,
                        "--driver", "docker-container",
                        "--use"
                    },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                {
                    throw new Exception("Failed to start buildx builder creation process");
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Failed to create buildx builder: {error}");
                }

                Log.Information("Buildx builder created successfully");
            }
            else
            {
                Log.Information("Buildx builder already exists: {BuilderName}", DockerBuilderName);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to setup buildx builder, attempting to continue...");
        }
    }

    string? GetExistingBuilder()
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "docker",
                ArgumentList =
                {
                    "buildx",
                    "ls"
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (output.Contains(DockerBuilderName))
            {
                return DockerBuilderName;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    void BuildBaseImage(IEnumerable<string> platforms)
    {
        Log.Information("Building base Docker image...");

        var platformArg = platforms.Count() > 1
            ? "--platform=linux/amd64,linux/arm64"
            : $"--platform={platforms.First()}";

        var buildArgs = new List<string>
        {
            "buildx",
            "build",
            platformArg,
            "--tag", BaseImageTag,
            "--file", DockerBaseDockerfile,
            DockerDeploymentDirectory.ToString()
        };

        // Add registry tag if Edge ACR is configured
        if (!string.IsNullOrEmpty(EffectiveAzureAcrRegistry))
        {
            buildArgs.Add("--tag");
            buildArgs.Add(RegistryBaseImageTag);
        }

        // Add push flag if Edge ACR is configured
        if (!string.IsNullOrEmpty(EffectiveAzureAcrRegistry))
        {
            buildArgs.Add("--output");
            buildArgs.Add("type=registry");
        }

        ExecuteDockerCommand(buildArgs, "base image build");
        Log.Information("Base Docker image built successfully");
    }

    void BuildApplicationImage(string version, IEnumerable<string> platforms)
    {
        Log.Information("Building application Docker image for version {Version}...", version);

        // Prepare build context
        PrepareBuildContext(version);

        var platformArg = platforms.Count() > 1
            ? "--platform=linux/amd64,linux/arm64"
            : $"--platform={platforms.First()}";

        var imageTag = GetFullImageTag(version);
        var registryTag = !string.IsNullOrEmpty(EffectiveAzureAcrRegistry)
            ? GetRegistryImageTag(version)
            : null;

        var buildArgs = new List<string>
        {
            "buildx",
            "build",
            platformArg,
            "--tag", imageTag,
            "--file", GeneratedAppDockerfile,
            DockerBuildContext.ToString()
        };

        // Add registry tag if Edge ACR is configured
        if (registryTag != null)
        {
            buildArgs.Add("--tag");
            buildArgs.Add(registryTag);
        }

        // Add push flag if Edge ACR is configured
        if (!string.IsNullOrEmpty(EffectiveAzureAcrRegistry))
        {
            buildArgs.Add("--output");
            buildArgs.Add("type=registry");
        }

        ExecuteDockerCommand(buildArgs, "application image build");
        Log.Information("Application Docker image built successfully: {Tag}", imageTag);
    }

    void PrepareBuildContext(string version)
    {
        Log.Information("Preparing Docker build context...");

        // Create build context directory
        DockerBuildContext.CreateOrCleanDirectory();
        GeneratedAppDockerfile.Parent.CreateDirectory();

        // Generate application Dockerfile from template
        GenerateAppDockerfile(version);

        // Copy entrypoint script
        File.Copy(DockerEntrypointScript, DockerBuildContext / "docker-entrypoint.sh", true);

        // Extract and copy lib directory
        var extractedDir = DockerBuildContext / "lib";
        ExtractZipFiles(extractedDir);

        Log.Information("Docker build context prepared");
    }

    void GenerateAppDockerfile(string version)
    {
        Log.Information("Generating application Dockerfile for version {Version}...", version);

        var template = File.ReadAllText(DockerAppTemplateDockerfile);
        var dockerfile = template
            .Replace("{version}", version)
            .Replace("{build_date}", BuildDate)
            .Replace("{base_image_name}", BaseImageTag);

        File.WriteAllText(GeneratedAppDockerfile, dockerfile);
        Log.Debug("Generated Dockerfile at: {Path}", GeneratedAppDockerfile);
    }

    void ExtractZipFiles(AbsolutePath targetDirectory)
    {
        targetDirectory.CreateOrCleanDirectory();

        foreach (var zipFile in DownloadedZipFiles)
        {
            Log.Information("Extracting {FileName} to {Directory}...",
                System.IO.Path.GetFileName(zipFile), targetDirectory);

            var processInfo = new ProcessStartInfo
            {
                FileName = "unzip",
                ArgumentList =
                {
                    "-q",
                    "-o", targetDirectory.ToString(),
                    zipFile.ToString()
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                throw new Exception($"Failed to start unzip process for {zipFile}");
            }

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Log.Warning("Failed to extract {FileName}", System.IO.Path.GetFileName(zipFile));
            }
        }
    }

    void ExecuteDockerCommand(List<string> arguments, string description)
    {
        Log.Information("Executing: docker {Command}", string.Join(" ", arguments));

        var processInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = string.Join(" ", arguments),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        if (process == null)
        {
            throw new Exception($"Failed to start Docker {description}");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        // Use a separate thread to read output in real-time
        var outputThread = new Thread(() =>
        {
            var reader = process.StandardOutput;
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line != null)
                {
                    Log.Information(line);
                }
            }
        });
        outputThread.Start();

        process.WaitForExit();
        outputThread.Join();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Docker {description} failed (exit code {process.ExitCode}): {error}");
        }
    }

    // ==========================================================================
    // Docker Push Implementation
    // ==========================================================================

    void DockerPushExecute()
    {
        Log.Information("Pushing Docker images to Edge ACR");

        if (string.IsNullOrEmpty(EffectiveAzureAcrRegistry))
        {
            Log.Warning("Edge ACR registry not configured, skipping push");
            Log.Information("Set AzureAcrRegistry parameter to enable push");
            return;
        }

        var version = EffectiveReleaseVersion;

        // Login to Edge ACR
        LoginToEdgeAcr();

        // Push images
        PushImageToRegistry(GetRegistryImageTag(version));
        VerifyImageInRegistry(GetRegistryImageTag(version));

        // Push additional tags (major, minor, latest)
        PushAdditionalTags(version);

        Log.Information("Docker images pushed to Edge ACR successfully");
    }

    void LoginToEdgeAcr()
    {
        var username = EffectiveAzureAcrUsername;
        var password = EffectiveAzureAcrPassword;
        var registry = EffectiveAzureAcrRegistry;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(registry))
        {
            throw new Exception("Edge ACR credentials not configured. " +
                "Please provide AzureAcrUsername, AzureAcrPassword, and AzureAcrRegistry.");
        }

        Log.Information("Logging in to Edge ACR: {Registry}", registry);

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "docker",
                ArgumentList =
                {
                    "login",
                    "--username", username,
                    "--password-stdin",
                    registry
                },
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                throw new Exception("Failed to start docker login process");
            }

            process.StandardInput.Write(password);
            process.StandardInput.Close();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Failed to login to Edge ACR: {error}");
            }

            Log.Information("Successfully logged in to Edge ACR");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to login to Edge ACR");
            throw;
        }
    }

    void PushImageToRegistry(string imageTag)
    {
        Log.Information("Pushing image: {Image}", imageTag);

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "docker",
                ArgumentList =
                {
                    "push", imageTag
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                throw new Exception($"Failed to start docker push process for {imageTag}");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Failed to push image {imageTag}: {error}");
            }

            Log.Information("Image pushed successfully: {Image}", imageTag);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to push image {Image}", imageTag);
            throw;
        }
    }

    void PushAdditionalTags(string version)
    {
        var versionParts = version.Split('.');
        if (versionParts.Length < 2)
        {
            Log.Information("Version does not follow semver format, skipping additional tags");
            return;
        }

        var registry = EffectiveAzureAcrRegistry;

        // Minor version tag (e.g., v1.2)
        if (versionParts.Length >= 2)
        {
            var minorTag = $"{registry}/{DockerImageName}:{versionParts[0]}.{versionParts[1]}";
            PushImageToRegistry(minorTag);
        }

        // Major version tag (e.g., v1)
        if (versionParts.Length >= 1)
        {
            var majorTag = $"{registry}/{DockerImageName}:{versionParts[0]}";
            PushImageToRegistry(majorTag);
        }

        // Latest tag
        var latestTag = $"{registry}/{DockerImageName}:latest";
        PushImageToRegistry(latestTag);
    }

    /// Verifies that an image is available in the registry with retry logic
    /// </summary>
    bool VerifyImageInRegistry(string imageTag, int maxRetries = 5)
    {
        Log.Information("Verifying image {Image} is available in registry...", imageTag);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (IsImageAvailable(imageTag))
                {
                    Log.Information("Image verified successfully: {Image}", imageTag);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Attempt {Attempt}/{MaxRetries}: Failed to verify image {Image}: {Error}",
                    attempt, maxRetries, imageTag, ex.Message);

                if (attempt < maxRetries)
                {
                    var delay = (int)Math.Pow(2, attempt) * 1000; // Exponential backoff
                    Log.Information("Waiting {Delay}ms before retry...", delay);
                    Thread.Sleep(delay);
                }
            }
        }

        Log.Error("Failed to verify image after {MaxRetries} attempts: {Image}", maxRetries, imageTag);
        return false;
    }

    /// Checks if an image is available in the registry
    /// </summary>
    bool IsImageAvailable(string imageTag)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "docker",
                ArgumentList =
                {
                    "manifest",
                    "inspect",
                    imageTag
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
