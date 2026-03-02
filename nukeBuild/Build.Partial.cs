using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Serilog;

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
    // State
    // ==========================================================================

    /// <summary>
    /// Gets the current build date in ISO format
    /// </summary>
    string BuildDate => DateTime.UtcNow.ToString("yyyy-MM-dd");

    /// <summary>
    /// Gets the download directory path
    /// </summary>
    AbsolutePath DownloadDirectory => OutputDirectory / "download";

    /// <summary>
    /// Gets the list of downloaded zip files
    /// </summary>
    IReadOnlyCollection<AbsolutePath> DownloadedZipFiles =>
        DownloadDirectory.GlobFiles("*.zip");

    /// <summary>
    /// Gets the Docker deployment directory path
    /// </summary>
    AbsolutePath DockerDeploymentDirectory => RootDirectory / "docker_deployment";

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
        var outputPath = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
        if (!string.IsNullOrEmpty(outputPath))
        {
            File.AppendAllText(outputPath, $"{name}={value}\n");
            Log.Debug("Set GitHub output: {Name}={Value}", name, value);
        }
        else
        {
            Log.Warning("GITHUB_OUTPUT environment variable not found. Output '{Name}' will not be set.", name);
        }
    }
}
