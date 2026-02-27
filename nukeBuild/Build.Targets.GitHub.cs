using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

/// <summary>
/// GitHub Release target - creates GitHub releases for version monitor dispatches
///
/// This target is triggered by repository_dispatch events from the version monitor workflow.
/// It creates GitHub releases without requiring any Docker-related dependencies.
/// </summary>
partial class Build
{
    /// <summary>
    /// Gets or sets the version to create a release for (from dispatch payload)
    /// </summary>
    [Parameter("Version to create GitHub release for")]
    readonly string ReleaseVersion = string.Empty;

    Target GitHubRelease => _ => _
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

        if (string.IsNullOrEmpty(EffectiveReleaseVersion))
        {
            throw new Exception("Release version is not specified");
        }

        // Check if release already exists
        if (ReleaseExists(token, repository, EffectiveReleaseVersion))
        {
            Log.Information("Release {Version} already exists, skipping creation", EffectiveReleaseVersion);
            return;
        }

        // Create the release
        CreateGitHubRelease(token, repository, EffectiveReleaseVersion);
    }

    /// <summary>
    /// Checks if a release for the specified version already exists
    /// </summary>
    bool ReleaseExists(string token, string repository, string version)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "gh",
                ArgumentList =
                {
                    "release",
                    "view",
                    version,
                    "--repo", repository
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

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            // Exit code 0 means release exists
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error checking if release exists");
            return false;
        }
    }

    /// <summary>
    /// Creates a GitHub release for the specified version
    /// </summary>
    void CreateGitHubRelease(string token, string repository, string version)
    {
        // Normalize version tag (ensure 'v' prefix)
        var releaseTag = EffectiveReleaseVersion.StartsWith("v") ? EffectiveReleaseVersion : $"v{EffectiveReleaseVersion}";
        var releaseTitle = $"Release {EffectiveReleaseVersion.TrimStart('v')}";

        Log.Information("Creating GitHub release {ReleaseTag} for repository {Repository}",
            releaseTag, repository);

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "gh",
                ArgumentList =
                {
                    "release",
                    "create",
                    releaseTag,
                    "--title", releaseTitle,
                    "--notes", BuildReleaseNotes(version),
                    "--repo", repository
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
                throw new Exception($"gh release create failed: {error}");
            }

            Log.Information("GitHub Release {ReleaseTag} created successfully", releaseTag);

            // Extract release URL from output
            var releaseUrl = ExtractReleaseUrl(output);
            if (!string.IsNullOrEmpty(releaseUrl))
            {
                Log.Information("Release URL: {Url}", releaseUrl);
            }

            // Send notification if webhook is configured
            SendFeishuNotification(version, releaseUrl, true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create GitHub release for version {Version}", EffectiveReleaseVersion);
            SendFeishuNotification(EffectiveReleaseVersion, null, false, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Extracts the release URL from gh release create output
    /// </summary>
    string? ExtractReleaseUrl(string output)
    {
        // Output typically contains URL like: https://github.com/owner/repo/releases/tag/v1.2.3
        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return line.Trim();
            }
        }
        return null;
    }

    /// <summary>
    /// Builds release notes for the version
    /// </summary>
    string BuildReleaseNotes(string version)
    {
        var releaseTag = version.StartsWith("v") ? version : $"v{version}";
        return $"Release {version.TrimStart('v')}\n\n" +
               $"Automated release from the version monitor workflow.\n\n" +
               $"**Workflow:** github-release-workflow\n" +
               $"**Version:** {releaseTag}\n" +
               $"**Date:** {BuildDate}";
    }

    /// <summary>
    /// Sends a Feishu notification about the release status
    /// </summary>
    void SendFeishuNotification(string version, string? releaseUrl, bool success, string? errorMessage = null)
    {
        if (string.IsNullOrEmpty(FeishuWebhookUrl))
        {
            Log.Debug("Feishu webhook URL not configured, skipping notification");
            return;
        }

        try
        {
            using var httpClient = new System.Net.Http.HttpClient();

            var message = success
                ? $"✅ GitHub Release created successfully!\n\n" +
                  $"Version: {version}\n" +
                  $"Release URL: {releaseUrl ?? "N/A"}"
                : $"❌ GitHub Release failed!\n\n" +
                  $"Version: {version}\n" +
                  $"Error: {errorMessage ?? "Unknown error"}";

            var payload = new
            {
                msg_type = "text",
                content = new
                {
                    text = message
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

            Log.Debug("Sending Feishu notification: {Message}", message);

            var response = httpClient.PostAsync(FeishuWebhookUrl, content).GetAwaiter().GetResult();

            if (response.IsSuccessStatusCode)
            {
                Log.Information("Feishu notification sent successfully");
            }
            else
            {
                Log.Warning("Failed to send Feishu notification: {StatusCode}",
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to send Feishu notification");
        }
    }
}
