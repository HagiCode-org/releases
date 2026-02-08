using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Serilog;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// GitHub Release target - uploads packages to pre-existing GitHub releases
/// </summary>
partial class Build
{
    /// <summary>
    /// Gets the version with 'v' prefix for GitHub release tags
    /// </summary>
    string GitHubReleaseVersion => ReleaseVersion.StartsWith("v") ? ReleaseVersion : $"v{ReleaseVersion}";

    Target GitHubRelease => _ => _
        .DependsOn(Download)
        .Executes(GitHubReleaseExecute);

    void GitHubReleaseExecute()
    {
        Log.Information("Uploading to GitHub Release");

        var token = EffectiveGitHubToken;
        var repository = EffectiveGitHubRepository;

        if (string.IsNullOrEmpty(token))
        {
            Log.Warning("GitHub token not available, skipping release upload");
            return;
        }

        if (string.IsNullOrEmpty(repository))
        {
            throw new Exception("GitHub repository is not specified");
        }

        var zipFiles = DownloadDirectory.GlobFiles("*.zip");
        if (zipFiles.Count == 0)
        {
            throw new Exception("No .zip packages found for release upload");
        }

        CreateGitHubRelease(token, repository, zipFiles);
    }

    void CreateGitHubRelease(string token, string repository, IReadOnlyCollection<AbsolutePath> zipFiles)
    {
        var releaseTag = GitHubReleaseVersion;
        Log.Information("Uploading to release {ReleaseTag} for repository {Repository} with {Count} packages",
            releaseTag, repository, zipFiles.Count);

        // Build gh release upload command with all packages
        var packageArgs = string.Join(" ", zipFiles.Select(f => $"\"{f}\""));
        var result = ProcessTasks.StartShell(
            $"gh release upload {releaseTag} " +
            $"{packageArgs} " +
            $"--repo {repository} " +
            $"--clobber",
            logOutput: true,
            logInvocation: true,
            environmentVariables: new Dictionary<string, string>
            {
                ["GH_TOKEN"] = token
            });

        // Wait for the process to complete and assert zero exit code
        result.AssertZeroExitCode();

        Log.Information("GitHub Release {ReleaseTag} uploaded successfully with {Count} packages", releaseTag, zipFiles.Count);
    }

    string BuildReleaseNotes(IReadOnlyCollection<AbsolutePath> zipFiles)
    {
        return $"Release {ReleaseVersion}\n\n" +
               $"Automated release from CI/CD pipeline.\n\n" +
               $"Packages included:\n" +
               string.Join("\n", zipFiles.Select(f => $"- {System.IO.Path.GetFileName(f)}"));
    }
}
