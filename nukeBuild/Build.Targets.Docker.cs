using Nuke.Common;
using Serilog;
using System.Diagnostics;

/// Docker build targets - multi-architecture image building and Edge ACR publishing
///
/// This file is the main entry point for Docker-related build operations.
/// It orchestrates the modular targets defined in separate partial class files:
/// - QemuSetup: QEMU cross-architecture emulation setup
/// - BuildxBuilder: Docker buildx builder management
/// - BaseImageBuild: Base Docker image building
/// - AppImageBuild: Application Docker image building
/// - DockerPush: Image push to registry
///
/// The modular structure allows for independent testing and reusability of each component.
///
/// Configuration priority (highest to lowest):
/// 1. Command line parameters (--DockerImageName, etc.)
/// 2. Environment variables (NUGEX_DockerImageName, etc.)
/// 3. YAML configuration file (build-config.yaml)
/// 4. Default values in code
partial class Build
{
    // ==========================================================================
    // Docker Parameters
    // ==========================================================================

    /// Gets the Docker platform to build for
    [Parameter("Docker platform: linux-amd64, linux-arm64, or all (default: all)")]
    readonly string DockerPlatform = "all";

    /// Gets the Docker image name
    [Parameter("Docker image name (e.g., hagicode/hagicode)")]
    readonly string DockerImageName = "hagicode";

    /// Gets the Docker build timeout in seconds
    [Parameter("Docker build timeout in seconds (default: 3600)")]
    readonly int DockerBuildTimeout = 3600;

    /// Gets the force rebuild flag
    [Parameter("Force rebuild of Docker images")]
    readonly bool DockerForceRebuild = false;

    // ==========================================================================
    // Docker State Properties (with configuration management)
    // ==========================================================================
    /// Gets the effective Edge ACR registry
    /// Priority: Parameter > Environment > Config file > Default

    /// Gets the effective Edge ACR username
    /// Priority: Parameter > Environment > Config file > Default

    string EffectiveAzureAcrUsername => GetEffectiveValue(
        BuildConfig.AzureAcrUsername,
        "NUGEX_AzureAcrUsername",
        AzureAcrUsername);

    /// Gets the effective Edge ACR password
    /// Priority: Parameter > Environment > Config file > Default

    string EffectiveAzureAcrPassword => GetEffectiveValue(
        BuildConfig.AzureAcrPassword,
        "NUGEX_AzureAcrPassword",
        AzureAcrPassword);

    /// Gets the effective Azure ACR namespace
    /// Priority: Parameter > Environment > Config file > Default

    string EffectiveAzureAcrNamespace => GetEffectiveValue(
        BuildConfig.AzureAcrNamespace,
        "NUGEX_AzureAcrNamespace",
        AzureAcrNamespace ?? "");


    /// Gets the effective Aliyun ACR username
    /// Priority: Parameter > Environment > Config file > Default

    string EffectiveAliyunAcrUsername => GetEffectiveValue(
        BuildConfig.AliyunAcrUsername,
        "NUGEX_AliyunAcrUsername",
        AliyunAcrUsername);

    /// Gets the effective Aliyun ACR password
    /// Priority: Parameter > Environment > Config file > Default

    string EffectiveAliyunAcrPassword => GetEffectiveValue(
        BuildConfig.AliyunAcrPassword,
        "NUGEX_AliyunAcrPassword",
        AliyunAcrPassword);

    /// Gets the effective Aliyun ACR namespace
    /// Priority: Parameter > Environment > Config file > Default

    string EffectiveAliyunAcrNamespace => GetEffectiveValue(
        BuildConfig.AliyunAcrNamespace,
        "NUGEX_AliyunAcrNamespace",
        AliyunAcrNamespace ?? "");

    /// Gets the effective DockerHub username
    /// Priority: Parameter > Environment > Config file > Default

    string EffectiveDockerHubUsername => GetEffectiveValue(
        BuildConfig.DockerHubUsername,
        "NUGEX_DockerHubUsername",
        DockerHubUsername);

    /// Gets the effective DockerHub access token
    /// Priority: Parameter > Environment > Config file > Default

    string EffectiveDockerHubToken => GetEffectiveValue(
        BuildConfig.DockerHubToken,
        "NUGEX_DockerHubToken",
        DockerHubToken);

    /// Gets the effective DockerHub namespace
    /// Priority: Parameter > Environment > Config file > Default

    string EffectiveDockerHubNamespace => GetEffectiveValue(
        BuildConfig.DockerHubNamespace,
        "NUGEX_DockerHubNamespace",
        DockerHubNamespace ?? "");

    // ==========================================================================
    // Docker Build Implementation
    // ==========================================================================

    void ExecuteDockerCommand(List<string> arguments, string description)
    {
        var commandLine = $"docker {string.Join(" ", arguments)}";
        Log.Information("Executing: {Command}", commandLine);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = string.Join(" ", arguments),
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process == null)
        {
            throw new Exception($"Failed to start Docker {description}");
        }

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Docker {description} failed with exit code {process.ExitCode}");
        }
    }

    /// <summary>
    /// Retag Docker images to create multiple version tags (full, major.minor, major, latest)
    /// Uses docker buildx imagetools create to efficiently create tags for multi-arch manifests
    /// </summary>
    /// <param name="image">Docker image info (contains Registry and Namespace)</param>
    /// <param name="version">Full version string</param>
    void RetagImages(DockerImageInfo image, string version)
    {
        Log.Information("Starting image retag for version: {Version}", version);

        // Get all version tags using existing GetVersionTags method
        var tags = GetVersionTags(version);

        if (tags.Length <= 1)
        {
            Log.Information("No additional tags to create (only single tag: {Tag})", tags.FirstOrDefault() ?? version);
            return;
        }

        var sourceTag = image.WithTag(version);
        var sourceImage = sourceTag.FullImageNameWithTag;

        Log.Information("Source image: {SourceImage}", sourceImage);
        Log.Information("Creating additional tags: {Tags}", string.Join(", ", tags.Skip(1)));

        // Construct full image name with namespace from DockerImageInfo
        string GetFullImagePath(string tag)
        {
            if (string.IsNullOrEmpty(image.Namespace))
            {
                return $"{image.Registry}/{image.ImageName}:{tag}";
            }
            return $"{image.Registry}/{image.Namespace}/{image.ImageName}:{tag}";
        }

        // Use docker buildx imagetools create to create tags
        // This command creates new tags by copying the manifest without pulling the full image
        try
        {
            foreach (var tag in tags.Skip(1))
            {
                var targetImage = GetFullImagePath(tag);
                Log.Information("Creating tag: {TargetImage}", targetImage);

                var arguments = new List<string>
                {
                    "buildx", "imagetools", "create",
                    "-t", targetImage,
                    sourceImage
                };

                ExecuteDockerCommand(arguments, $"imagetools create tag {tag}");
                Log.Information("Successfully created tag: {Tag}", tag);
            }

            Log.Information("Image retag completed successfully for {Count} additional tags", tags.Length - 1);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to retag images");
            throw;
        }
    }
}