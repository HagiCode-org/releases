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
    // Shared Helper Properties
    // ==========================================================================

    /// <summary>
    /// Gets the full version string without 'v' prefix
    /// </summary>
    string FullVersion => Version.TrimStart('v');

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
}
