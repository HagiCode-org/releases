using Nuke.Common;
using NukeBuild.Adapters;
using Serilog;
using System.Diagnostics;
using System.Text.Json;

/// <summary>
/// Version Monitor target - monitors Azure Blob Storage for new versions and triggers GitHub releases
///
/// Dispatch Verification:
/// - After triggering a repository_dispatch event, this target queries the GitHub Actions API
/// - Confirms that dispatch successfully created a workflow run
/// - Provides a workflow run URL for tracking release progress
/// - This catches authentication/permission issues early and provides actionable feedback

partial class Build
{
    Target VersionMonitor => _ => _
        .Requires(() => AzureBlobSasUrl)
        .Executes(VersionMonitorExecute);

    void VersionMonitorExecute()
    {
        Log.Information("Starting Version Monitor");

        // Declare variables at method scope to avoid CS0136 errors
        AzureBlobAdapter adapter;
        PackageIndex? index;
        List<string> azureVersions;
        ReleaseVersionMonitorPlan releasePlan;
        List<string> sortedAzureVersions;
        List<string> githubReleases;
        List<string> newVersions;
        List<string> deferredVersions;
        string selectedVersion;

        adapter = new AzureBlobAdapter();

        // Download index from Azure
        Log.Information("Downloading index.json from Azure Blob Storage...");
        index = adapter.DownloadIndexJson(AzureBlobSasUrl);

        if (index == null)
        {
            Log.Error("Failed to download index.json from Azure");
            throw new Exception("Azure index download failed");
        }

        // Get all versions from Azure
        azureVersions = adapter.GetAllVersions(index);
        Log.Information("Found {Count} versions in Azure: {Versions}",
                azureVersions.Count,
                string.Join(", ", azureVersions));

        // Get GitHub releases
        githubReleases = GetGitHubReleases(EffectiveGitHubToken, EffectiveGitHubRepository);
        Log.Information("Found {Count} releases on GitHub: {Releases}",
                githubReleases.Count,
                string.Join(", ", githubReleases));

        releasePlan = ReleaseVersionMonitorPlanner.CreatePlan(azureVersions, githubReleases);
        sortedAzureVersions = releasePlan.SortedAzureVersions.ToList();
        newVersions = releasePlan.NewVersions.ToList();
        deferredVersions = releasePlan.DeferredVersions.ToList();
        selectedVersion = releasePlan.SelectedVersion;

        foreach (var ignoredVersion in releasePlan.IgnoredVersions)
        {
            Log.Warning("Skipping invalid version format: {Version} (must start with digit and contain only letters, numbers, dots, hyphens, or underscores)", ignoredVersion);
        }

        Log.Information("Sorted Azure versions: {Versions}",
            sortedAzureVersions.Count == 0 ? "(none)" : string.Join(", ", sortedAzureVersions));

        // Output latest version and has_new_versions
        var hasNewVersions = releasePlan.HasNewVersions;
        var latestVersion = releasePlan.LatestVersion;

        SetGitHubOutput("has_new_versions", hasNewVersions ? "true" : "false");
        SetGitHubOutput("new_versions", string.Join(", ", newVersions));
        SetGitHubOutput("latest_version", latestVersion);
        SetGitHubOutput("selected_version", selectedVersion);
        SetGitHubOutput("deferred_versions", string.Join(", ", deferredVersions));

        LogVersionSelectionSummary(releasePlan);

        // If ListOnly mode, just output versions without triggering releases
        if (EffectiveListOnly)
        {
            Log.Information(
                "List-only mode enabled - selected version remains {SelectedVersion}; deferred versions remain {DeferredVersions}",
                FormatVersionValue(selectedVersion),
                FormatVersions(deferredVersions));
            Log.Information("Version Monitor list-only mode completed");
            return;
        }

        // Normal mode - download and trigger releases
        if (newVersions.Count == 0)
        {
            Log.Information("Latest Azure version is already present on GitHub. Historical gaps are ignored by this monitor.");
            return;
        }

        Log.Information("Latest Azure version requires release sync ({Count}): {Versions}",
            newVersions.Count,
            string.Join(", ", newVersions));
        Log.Information(
            "Automatic trigger boundary for this run: selected version {SelectedVersion}; deferred versions {DeferredVersions}",
            FormatVersionValue(selectedVersion),
            FormatVersions(deferredVersions));

        if (string.IsNullOrWhiteSpace(selectedVersion))
        {
            Log.Warning("No selected version resolved for automated processing. Skipping dispatch.");
            return;
        }

        if (EffectiveDryRun)
        {
            Log.Information(
                "Dry-run mode enabled - only selected version {SelectedVersion} would be dispatched; deferred versions remain untouched: {DeferredVersions}",
                selectedVersion,
                FormatVersions(deferredVersions));
        }

        TriggerReleaseForVersion(selectedVersion);
        TriggerDockerDispatch(selectedVersion);

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

            // Log command for debugging
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

    void TriggerReleaseForVersion(string version)
    {
        var repository = EffectiveGitHubRepository;
        var dryRun = EffectiveDryRun;

        Log.Information("Processing version: {Version}", version);

        if (dryRun)
        {
            Log.Warning("[DRY RUN] Would trigger release for version {Version}", version);
            return;
        }

        try
        {
            // Build complete request body as JSON
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

            // Write JSON body to stdin
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

            // Verify dispatch created a workflow run
            VerifyDispatchCreated(version, repository);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to trigger release for version {Version}", version);
            throw;
        }
    }

    /// <summary>
    /// Triggers Aliyun ACR Docker build workflow via repository_dispatch event.
    /// Uses event_type "version-monitor-docker-aliyun" to trigger Aliyun ACR build.
    ///
    /// <param name="version">The version to build</param>
    void TriggerDockerDispatchAliyun(string version)
    {
        var repository = EffectiveGitHubRepository;
        var dryRun = EffectiveDryRun;

        Log.Information("Triggering Aliyun ACR Docker dispatch for version: {Version}", version);

        if (dryRun)
        {
            Log.Warning("[DRY RUN] Would trigger Aliyun ACR Docker dispatch for version {Version}", version);
            return;
        }

        try
        {
            var requestBody = JsonSerializer.Serialize(new
            {
                event_type = "version-monitor-docker-aliyun",
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

            process.StandardInput.Write(requestBody);
            process.StandardInput.Close();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"gh api dispatch failed: {error}");
            }

            Log.Information("Successfully triggered Aliyun ACR Docker workflow for version {Version}", version);

            VerifyDispatchCreated(version, repository);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to trigger Aliyun ACR Docker dispatch for version {Version}", version);
            throw;
        }
    }

    /// <summary>
    /// Triggers Azure ACR Docker build workflow via repository_dispatch event.
    /// Uses event_type "version-monitor-docker-azure" to trigger Azure ACR build.
    ///
    /// <param name="version">The version to build</param>
    void TriggerDockerDispatchAzure(string version)
    {
        var repository = EffectiveGitHubRepository;
        var dryRun = EffectiveDryRun;

        Log.Information("Triggering Azure ACR Docker dispatch for version: {Version}", version);

        if (dryRun)
        {
            Log.Warning("[DRY RUN] Would trigger Azure ACR Docker dispatch for version {Version}", version);
            return;
        }

        try
        {
            var requestBody = JsonSerializer.Serialize(new
            {
                event_type = "version-monitor-docker-azure",
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

            process.StandardInput.Write(requestBody);
            process.StandardInput.Close();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"gh api dispatch failed: {error}");
            }

            Log.Information("Successfully triggered Azure ACR Docker workflow for version {Version}", version);

            VerifyDispatchCreated(version, repository);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to trigger Azure ACR Docker dispatch for version {Version}", version);
            throw;
        }
    }

    /// <summary>
    /// Triggers DockerHub Docker build workflow via repository_dispatch event.
    /// Uses event_type "version-monitor-docker-dockerhub" to trigger DockerHub build.
    ///
    /// <param name="version">The version to build</param>
    void TriggerDockerDispatchDockerHub(string version)
    {
        var repository = EffectiveGitHubRepository;
        var dryRun = EffectiveDryRun;

        Log.Information("Triggering DockerHub Docker dispatch for version: {Version}", version);

        if (dryRun)
        {
            Log.Warning("[DRY RUN] Would trigger DockerHub Docker dispatch for version {Version}", version);
            return;
        }

        try
        {
            var requestBody = JsonSerializer.Serialize(new
            {
                event_type = "version-monitor-docker-dockerhub",
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

            process.StandardInput.Write(requestBody);
            process.StandardInput.Close();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"gh api dispatch failed: {error}");
            }

            Log.Information("Successfully triggered DockerHub Docker workflow for version {Version}", version);

            VerifyDispatchCreated(version, repository);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to trigger DockerHub Docker dispatch for version {Version}", version);
            throw;
        }
    }

    /// <summary>
    /// Triggers Docker build workflows via repository_dispatch events.
    /// Triggers three independent events for all configured registries:
    /// - version-monitor-docker-aliyun: triggers Aliyun ACR build
    /// - version-monitor-docker-azure: triggers Azure ACR build
    /// - version-monitor-docker-dockerhub: triggers DockerHub build
    ///
    /// <param name="version">The version to build</param>
    void TriggerDockerDispatch(string version)
    {
        Log.Information("Triggering Docker dispatch for version: {Version}", version);

        // Trigger all three Docker registry dispatches in sequence
        // Errors are logged but don't block other dispatches
        var successCount = 0;
        var failCount = 0;

        // Aliyun ACR dispatch
        try
        {
            TriggerDockerDispatchAliyun(version);
            successCount++;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to trigger Aliyun ACR Docker dispatch for version {Version}", version);
            failCount++;
        }

        // Azure ACR dispatch
        try
        {
            TriggerDockerDispatchAzure(version);
            successCount++;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to trigger Azure ACR Docker dispatch for version {Version}", version);
            failCount++;
        }

        // DockerHub dispatch
        try
        {
            TriggerDockerDispatchDockerHub(version);
            successCount++;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to trigger DockerHub Docker dispatch for version {Version}", version);
            failCount++;
        }

        // If all dispatches failed, throw an exception
        if (failCount == 3)
        {
            Log.Error("All Docker dispatches failed for version {Version} ({FailCount}/3 failed)", version, failCount);
            throw new Exception($"All Docker dispatches failed for version {version}");
        }

        if (failCount > 0)
        {
            Log.Warning("Docker dispatch partially succeeded for version {Version}: {SuccessCount}/3 succeeded, {FailCount}/3 failed",
                version, successCount, failCount);
        }
        else
        {
            Log.Information("All Docker dispatches succeeded for version {Version} (3/3)", version);
        }
    }

    /// <summary>
    /// Verifies that a repository_dispatch event successfully created a workflow run.
    /// Queries GitHub Actions API to find a matching run within last 60 seconds.
    
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
                // Query for recent workflow runs without filtering by workflow name
                // This allows verification regardless of which workflow handles the dispatch
                var processInfo = new ProcessStartInfo
                {
                    FileName = "gh",
                    ArgumentList =
                    {
                        "run", "list",
                        "--json", "databaseId,createdAt,event,name",
                        "--limit", "10"
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
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Log.Warning("gh run list failed (exit code {ExitCode}): {Error}", process.ExitCode, error);
                    Log.Debug("Output: {Output}", output);
                    break;
                }

                Log.Debug("GitHub run list response: {Output}", output);

                // Parse JSON output using System.Text.Json
                if (!string.IsNullOrWhiteSpace(output))
                {
                    using var jsonDoc = JsonDocument.Parse(output);
                    var root = jsonDoc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var run in root.EnumerateArray())
                        {
                            if (run.TryGetProperty("createdAt", out var createdAtElement) &&
                                run.TryGetProperty("databaseId", out var idElement))
                            {
                                var createdAtStr = createdAtElement.GetString();
                                if (DateTimeOffset.TryParse(createdAtStr, out var createdAt))
                                {
                                    var timeSinceDispatch = DateTime.UtcNow - createdAt.DateTime;
                                    var eventName = run.TryGetProperty("event", out var eventElement) ? eventElement.GetString() : "unknown";
                                    var workflowName = run.TryGetProperty("name", out var workflowNameElement) ? workflowNameElement.GetString() : "unknown";
                                    Log.Debug("Found workflow run {RunId} ({Event} - {WorkflowName}) created {Seconds}s ago: {CreatedAt}",
                                        idElement.GetInt64(),
                                        eventName,
                                        workflowName,
                                        (int)timeSinceDispatch.TotalSeconds,
                                        createdAtStr);

                                    if (timeSinceDispatch.TotalSeconds <= 60)
                                    {
                                        var runId = idElement.GetInt64();
                                        Log.Information("✓ Dispatch confirmed: {WorkflowName} - https://github.com/{Repository}/actions/runs/{RunId}",
                                            workflowName, repository, runId);
                                        found = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                if (found)
                {
                    break;
                }

                // Wait 2 seconds before retrying
                Thread.Sleep(2000);
            }
            catch (JsonException ex)
            {
                Log.Warning("Failed to parse GitHub API response: {Error}", ex.Message);
                break;
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

    static void LogVersionSelectionSummary(ReleaseVersionMonitorPlan releasePlan)
    {
        Log.Information("Selected version for this run: {SelectedVersion}",
            FormatVersionValue(releasePlan.SelectedVersion));
        Log.Information("Deferred versions for later runs/manual handling: {DeferredVersions}",
            FormatVersions(releasePlan.DeferredVersions));
    }

    static string FormatVersions(IEnumerable<string> versions)
    {
        var items = versions.Where(static version => !string.IsNullOrWhiteSpace(version)).ToList();
        return items.Count == 0 ? "(none)" : string.Join(", ", items);
    }

    static string FormatVersionValue(string version)
    {
        return string.IsNullOrWhiteSpace(version) ? "(none)" : version;
    }
}
