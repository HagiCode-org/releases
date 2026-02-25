using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Serilog;
using System;

/// <summary>
/// Partial class declarations for Build - shared properties and CI integration
/// </summary>
partial class Build
{
    // ==========================================================================
    // CI/CD Integration
    // ==========================================================================

    GitHubActions GitHubActions => GitHubActions.Instance;

    // ==========================================================================
    // Parameters
    // ==========================================================================

    /// <summary>
    /// Gets or sets whether to build all channels' latest versions
    /// When true, downloads and builds the latest version for each channel (beta, stable, etc.)
    /// </summary>
    [Parameter("Build all channels' latest versions instead of a specific version")]
    bool BuildAllChannels { get; set; }

    /// <summary>
    /// Gets or sets whether to push images to registry during build
    /// When false, images are only built locally (for main branch builds)
    /// When true, images are pushed to registry (for tag/release builds)
    /// Default: false
    /// </summary>
    [Parameter("Push images to registry during build")]
    bool PushToRegistry { get; set; }

    // ==========================================================================
    // State
    // ==========================================================================

    /// <summary>
    /// Stores the versions that were actually downloaded (channel -> version mapping)
    /// </summary>
    List<(string channel, string version)> DownloadedVersions { get; set; } = new();

    /// <summary>
    /// Stores platform-specific base image tags for local builds
    /// Key: platform (e.g., "linux/amd64"), Value: image tag (e.g., "docker.io/newbe36524/hagicode:base-0.1.0-beta.15-linux-amd64")
    /// </summary>
    Dictionary<string, string> PlatformBaseTags { get; set; } = new();

    /// <summary>
    /// Gets the effective version to use for Docker builds
    /// Returns the first downloaded version, or falls back to ReleaseVersion
    /// </summary>
    string EffectiveVersion => DownloadedVersions.Count > 0
        ? DownloadedVersions[0].version
        : ReleaseVersion.TrimStart('v');

    // ==========================================================================
    // Shared Helper Properties
    // ==========================================================================

    /// <summary>
    /// Gets the full version string without 'v' prefix
    /// </summary>
    string FullVersion => EffectiveVersion;

    /// <summary>
    /// Gets the major version (e.g., "1" from "1.2.3")
    /// </summary>
    string MajorVersion
    {
        get
        {
            var parts = FullVersion.Split('.');
            if (parts.Length < 1) throw new Exception($"Invalid version format: {FullVersion}");
            return parts[0];
        }
    }

    /// <summary>
    /// Gets the minor version (e.g., "1.2" from "1.2.3")
    /// </summary>
    string MinorVersion
    {
        get
        {
            var parts = FullVersion.Split('.');
            if (parts.Length < 2) throw new Exception($"Invalid version format: {FullVersion}");
            return $"{parts[0]}.{parts[1]}";
        }
    }

    /// <summary>
    /// Determines if the current version is a pre-release
    /// </summary>
    bool IsPreRelease => FullVersion.Contains("-") ||
                        FullVersion.Contains("alpha") ||
                        FullVersion.Contains("beta") ||
                        FullVersion.Contains("rc");

    /// <summary>
    /// Gets the base Docker image name (e.g., "docker.io/newbe36524/hagicode")
    /// </summary>
    string BaseImageName => $"{DockerRegistry}/{DockerImageName}";

    /// <summary>
    /// Gets the base Docker tag (e.g., "docker.io/newbe36524/hagicode:base")
    /// </summary>
    string BaseDockerTag => $"{BaseImageName}:base";

    /// <summary>
    /// Gets the download directory path
    /// </summary>
    AbsolutePath DownloadDirectory => OutputDirectory / "download";

    /// <summary>
    /// Gets the extracted directory path
    /// </summary>
    AbsolutePath ExtractedDirectory => OutputDirectory / "extracted";

    /// <summary>
    /// Gets the docker build context path
    /// </summary>
    AbsolutePath DockerBuildContext => OutputDirectory / "docker-context";

    /// <summary>
    /// Gets the docker deployment source directory
    /// </summary>
    AbsolutePath DockerDeploymentDirectory => RootDirectory / "docker_deployment";

    /// <summary>
    /// Gets the current build date in ISO format
    /// </summary>
    string BuildDate => DateTime.UtcNow.ToString("yyyy-MM-dd");

    /// <summary>
    /// Gets the GitHub token from CI or parameter
    /// </summary>
    string EffectiveGitHubToken => GitHubActions?.Token ?? GitHubToken;

    /// <summary>
    /// Gets the GitHub repository from CI or parameter
    /// </summary>
    string EffectiveGitHubRepository => GitHubActions?.Repository ?? GitHubRepository;

    /// <summary>
    /// Sets a GitHub Actions output value
    /// </summary>
    void SetGitHubOutput(string name, string value)
    {
        var outputPath = System.Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
        if (!string.IsNullOrEmpty(outputPath))
        {
            System.IO.File.AppendAllText(outputPath, $"{name}={value}\n");
            Log.Debug("Set GitHub output: {Name}={Value}", name, value);
        }
        else
        {
            Log.Warning("GITHUB_OUTPUT environment variable not found. Output '{Name}' will not be set.", name);
        }
    }
}
