using Nuke.Common;
using NukeBuild.Adapters;
using Serilog;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Version Monitor target - monitors Azure Blob Storage for new versions and triggers GitHub releases
///
/// Dispatch Verification:
/// - After triggering a repository_dispatch event, this target queries the GitHub Actions API
/// - Confirms that the dispatch successfully created a workflow run
/// - Provides a workflow run URL for tracking the release progress
/// - This catches authentication/permission issues early and provides actionable feedback
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
            // Output to GITHUB_ENV for use in workflow
            SetGitHubOutput("has_new_versions", "false");
            SetGitHubOutput("new_versions", "");
            return;
        }

        Log.Information("Found {Count} new versions to release: {Versions}",
            newVersions.Count,
            string.Join(", ", newVersions));

        // Output to GITHUB_ENV for use in workflow
        SetGitHubOutput("has_new_versions", "true");
        SetGitHubOutput("new_versions", string.Join(", ", newVersions));

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
                ArgumentList =
                {
                    "release",
                    "list",
                    "--json", "tagName",
                    "--jq", ".[].tagName"
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment =
                {
                    ["GH_TOKEN"] = token
                }
            };

            // Log the command for debugging
            var commandArgs = string.Join(" ", "release", "list", "--json", "tagName", "--jq", ".[].tagName");
            Log.Debug("Executing gh command: gh {Args}", commandArgs);

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
            // Build the complete request body as JSON
            var requestBody = JsonSerializer.Serialize(new
            {
                event_type = "version-monitor-release",
                client_payload = new
                {
                    version = version
                }
            });

            var processInfo = new ProcessStartInfo
            {
                FileName = "gh",
                ArgumentList =
                {
                    "api",
                    "--method", "POST",
                    "-H", "Accept: application/vnd.github.v3+json",
                    "-H", "Content-Type: application/json",
                    $"/repos/{repository}/dispatches",
                    "--input", "-"
                },
                RedirectStandardInput = true,
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

            // Write the JSON body to stdin
            process.StandardInput.Write(requestBody);
            process.StandardInput.Close();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"gh api dispatch failed: {error}");
            }

            Log.Information("Successfully triggered release workflow for version {Version}", version);

            // Verify the dispatch created a workflow run
            VerifyDispatchCreated(version, repository);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to trigger release for version {Version}", version);
            throw;
        }
    }

    /// <summary>
    /// Verifies that a repository_dispatch event successfully created a workflow run.
    /// Queries the GitHub Actions API to find a matching run within the last 60 seconds.
    /// </summary>
    /// <param name="version">The version that was dispatched</param>
    /// <param name="repository">The GitHub repository (owner/repo)</param>
    void VerifyDispatchCreated(string version, string repository)
    {
        Log.Debug("Verifying dispatch created workflow run for version {Version}...", version);

        var timeout = TimeSpan.FromSeconds(10);
        var startTime = DateTime.UtcNow;
        var found = false;

        while (DateTime.UtcNow - startTime < timeout && !found)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "gh",
                    ArgumentList =
                    {
                        "api",
                        $"/repos/{repository}/actions/runs",
                        "--jq", ".workflow_runs[] | select(.event == \"repository_dispatch\") | {id, name, created_at, event: .event}"
                    },
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
                    Log.Warning("Failed to start gh process for dispatch verification");
                    break;
                }

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    var error = process.StandardError.ReadToEnd();
                    Log.Warning("gh api query failed: {Error}", error);
                    break;
                }

                // Check if the output contains a run created in the last 60 seconds
                // The response format is JSON lines with id, name, created_at
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        // Parse the created_at timestamp to check if within last 60 seconds
                        if (line.Contains("\"created_at\""))
                        {
                            // Extract the created_at value
                            var createdAtStart = line.IndexOf("\"created_at\": \"", StringComparison.OrdinalIgnoreCase) + 15;
                            var createdAtEnd = line.IndexOf("\"", createdAtStart);
                            if (createdAtStart > 14 && createdAtEnd > createdAtStart)
                            {
                                var createdAtStr = line.Substring(createdAtStart, createdAtEnd - createdAtStart);
                                if (DateTimeOffset.TryParse(createdAtStr, out var createdAt))
                                {
                                    var timeSinceDispatch = DateTime.UtcNow - createdAt.DateTime;
                                    if (timeSinceDispatch.TotalSeconds <= 60)
                                    {
                                        // Extract the run ID
                                        var idStart = line.IndexOf("\"id\": ") + 6;
                                        var idEnd = line.IndexOf(",", idStart);
                                        if (idEnd == -1) idEnd = line.IndexOf("}", idStart);
                                        if (idEnd > idStart)
                                        {
                                            var runId = line.Substring(idStart, idEnd - idStart).Trim();
                                            Log.Information("✓ Dispatch confirmed: https://github.com/{Repository}/actions/runs/{RunId}",
                                                repository, runId);
                                            found = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug("Error parsing workflow run line: {Line}, Error: {Error}", line, ex.Message);
                    }
                }

                if (found)
                {
                    break;
                }

                // Wait 2 seconds before retrying
                System.Threading.Thread.Sleep(2000);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error during dispatch verification retry");
                break;
            }
        }

        if (!found)
        {
            Log.Error("Dispatch may have failed - no workflow run found for version {Version} within {Timeout} seconds",
                version, timeout.TotalSeconds);
            throw new Exception($"Dispatch verification failed for version {version}");
        }
    }
}
