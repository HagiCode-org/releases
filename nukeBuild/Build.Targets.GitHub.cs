using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Serilog;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// GitHub Release target - creates GitHub releases with all platform packages
/// </summary>
partial class Build
{
    Target GitHubRelease => _ => _
        .DependsOn(Download)
        .Executes(GitHubReleaseExecute);

    void GitHubReleaseExecute()
    {
        Log.Information("Creating GitHub Release");

        var token = EffectiveGitHubToken;
        var repository = EffectiveGitHubRepository;

        if (string.IsNullOrEmpty(token))
        {
            Log.Warning("GitHub token not available, skipping release creation");
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
        var prereleaseFlag = IsPreRelease ? "--prerelease" : "";
        var notes = BuildReleaseNotes(zipFiles);

        Log.Information("Creating release {Version} for repository {Repository} with {Count} packages",
            Version, repository, zipFiles.Count);

        // Build gh release command with all packages
        var packageArgs = string.Join(" ", zipFiles.Select(f => $"\"{f}\""));
        var result = ProcessTasks.StartShell(
            $"gh release create {Version} " +
            $"{packageArgs} " +
            $"--title \"{Version}\" " +
            $"--notes \"{notes}\" " +
            $"{prereleaseFlag} " +
            $"--repo {repository}",
            logOutput: true,
            logInvocation: true,
            environmentVariables: new Dictionary<string, string>
            {
                ["GH_TOKEN"] = token
            });

        if (result.ExitCode != 0)
        {
            throw new Exception("GitHub Release creation failed");
        }

        Log.Information("GitHub Release {Version} created successfully with {Count} packages", Version, zipFiles.Count);
    }

    string BuildReleaseNotes(IReadOnlyCollection<AbsolutePath> zipFiles)
    {
        return $"Release {Version}\n\n" +
               $"Automated release from CI/CD pipeline.\n\n" +
               $"Packages included:\n" +
               string.Join("\n", zipFiles.Select(f => $"- {System.IO.Path.GetFileName(f)}"));
    }
}
