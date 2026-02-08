using Nuke.Common;
using NukeBuild.Adapters;
using Serilog;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Version Monitor target - monitors Azure Blob Storage for new versions and triggers GitHub releases
/// </summary>
partial class Build
{
    Target VersionMonitor => _ => _
        .Requires(() => AzureBlobSasUrl)
        .Executes(VersionMonitorExecute);

    void VersionMonitorExecute()
    {
        Log.Information("Starting Version Monitor");

        var adapter = new AzureBlobAdapter();

        // Download index from Azure
        Log.Information("Downloading index.json from Azure Blob Storage...");
        var index = adapter.DownloadIndexJson(AzureBlobSasUrl);

        if (index == null)
        {
            Log.Error("Failed to download index.json from Azure");
            throw new Exception("Azure index download failed");
        }

        // Get all versions from Azure
        var azureVersions = adapter.GetAllVersions(index);
        Log.Information("Found {Count} versions in Azure: {Versions}",
            azureVersions.Count,
            string.Join(", ", azureVersions));

        // Get GitHub releases
        var githubReleases = GetGitHubReleases(EffectiveGitHubToken, EffectiveGitHubRepository);
        Log.Information("Found {Count} releases on GitHub: {Releases}",
            githubReleases.Count,
            string.Join(", ", githubReleases));

        // Find new versions
        var newVersions = FindNewVersions(azureVersions, githubReleases);

        if (newVersions.Count == 0)
        {
            Log.Information("No new versions to release. All Azure versions are already on GitHub.");
            return;
        }

        Log.Information("Found {Count} new versions to release: {Versions}",
            newVersions.Count,
            string.Join(", ", newVersions));

        // Trigger release for each new version
        foreach (var version in newVersions)
        {
            TriggerReleaseForVersion(version);
        }

        Log.Information("Version Monitor completed successfully");
    }

    List<string> GetGitHubReleases(string token, string repository)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "gh",
                Arguments = "release list --json tagName --jq '.[].tagName'",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment =
                {
                    ["GH_TOKEN"] = token
                }
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                throw new Exception("Failed to start gh process");
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new Exception($"gh release list failed: {error}");
            }

            var releases = output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            Log.Debug("GitHub releases raw output: {Output}", string.Join(", ", releases));
            return releases;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get GitHub releases");
            throw;
        }
    }

    List<string> FindNewVersions(List<string> azureVersions, List<string> githubReleases)
    {
        var newVersions = new List<string>();

        foreach (var version in azureVersions)
        {
            // Check if this version (with or without 'v' prefix) exists in GitHub releases
            var versionWithV = $"v{version}";
            var hasVersion = githubReleases.Contains(version, StringComparer.OrdinalIgnoreCase) ||
                           githubReleases.Contains(versionWithV, StringComparer.OrdinalIgnoreCase);

            if (!hasVersion)
            {
                newVersions.Add(version);
                Log.Debug("Version {Version} is new (not found in GitHub releases)", version);
            }
            else
            {
                Log.Debug("Version {Version} already exists in GitHub releases", version);
            }
        }

        return newVersions;
    }

    void TriggerReleaseForVersion(string version)
    {
        var repository = EffectiveGitHubRepository;
        var dryRun = DryRun;

        Log.Information("Processing version: {Version}", version);

        if (dryRun)
        {
            Log.Warning("[DRY RUN] Would trigger release for version {Version}", version);
            return;
        }

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                version = version
            });

            var processInfo = new ProcessStartInfo
            {
                FileName = "gh",
                Arguments = $"api --method POST -H \"Accept: application/vnd.github.v3+json\" /repos/{repository}/dispatches -f event_type=\"version-monitor-release\" -f client_payload='{payload}'",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment =
                {
                    ["GH_TOKEN"] = EffectiveGitHubToken
                }
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                throw new Exception("Failed to start gh process");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"gh api dispatch failed: {error}");
            }

            Log.Information("Successfully triggered release workflow for version {Version}", version);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to trigger release for version {Version}", version);
            throw;
        }
    }
}
