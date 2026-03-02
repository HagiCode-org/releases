using Nuke.Common;
using Nuke.Common.IO;
using Serilog;
using System.Diagnostics;

/// <summary>
/// GitHub Release target - creates GitHub releases for version monitor dispatches
///
/// This target is triggered by repository_dispatch events from the version monitor workflow.
/// It creates GitHub releases without requiring any Docker-related dependencies.

partial class Build
{
    /// <summary>
    /// Gets or sets the version to create a release for (from dispatch payload)
    
    [Parameter("Version to create GitHub release for")]
    readonly string ReleaseVersion = string.Empty;

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

        if (string.IsNullOrEmpty(EffectiveReleaseVersion))
        {
            throw new Exception("Release version is not specified");
        }

        // Normalize version tag (ensure 'v' prefix)
        var releaseTag = EffectiveReleaseVersion.StartsWith("v") ? EffectiveReleaseVersion : $"v{EffectiveReleaseVersion}";

        // Check if release already exists and upload files, or create new release
        if (ReleaseExists(token, repository, releaseTag))
        {
            Log.Information("Release {Version} already exists, uploading files...", EffectiveReleaseVersion);
            UploadExistingRelease(token, repository, releaseTag);
        }
        else
        {
            // Create the release
            CreateGitHubRelease(token, repository, EffectiveReleaseVersion);
        }
    }

    /// <summary>
    /// Uploads packages to an existing GitHub release
    
    void UploadExistingRelease(string token, string repository, string releaseTag)
    {
        // Get the downloaded zip files
        var zipFiles = DownloadedZipFiles;
        Log.Information("Found {Count} zip files to upload: {Files}",
            zipFiles.Count,
            string.Join(", ", zipFiles.Select(f => Path.GetFileName(f))));

        if (zipFiles.Count == 0)
        {
            Log.Warning("No zip packages found to upload to release");
            return;
        }

        UploadPackagesToRelease(token, repository, releaseTag, zipFiles);
        Log.Information("Successfully updated release {ReleaseTag} with files", releaseTag);
    }

    /// <summary>
    /// Checks if a release for the specified version already exists
    
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
    
    void CreateGitHubRelease(string token, string repository, string version)
    {
        // Normalize version tag (ensure 'v' prefix)
        var releaseTag = version.StartsWith("v") ? version : $"v{version}";
        var releaseTitle = $"Release {version.TrimStart('v')}";

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

            // Download packages from Azure Blob Storage
            var zipFiles = DownloadedZipFiles;
            Log.Information("Found {Count} zip files to upload: {Files}",
                zipFiles.Count,
                string.Join(", ", zipFiles.Select(f => Path.GetFileName(f))));
            if (zipFiles.Count == 0)
            {
                Log.Warning("No zip packages found to upload to release");
            }
            else
            {
                UploadPackagesToRelease(token, repository, releaseTag, zipFiles);
            }

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
            Log.Error(ex, "Failed to create GitHub release for version {Version}", version);
            SendFeishuNotification(version, null, false, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Uploads packages to an existing GitHub release
    
    void UploadPackagesToRelease(string token, string repository, string releaseTag, IReadOnlyCollection<AbsolutePath> zipFiles)
    {
        Log.Information("Uploading {Count} packages to release {ReleaseTag}", zipFiles.Count, releaseTag);

        foreach (var zipFile in zipFiles)
        {
            var fileName = Path.GetFileName(zipFile);
            Log.Information("Uploading {FileName}...", fileName);

            var processInfo = new ProcessStartInfo
            {
                FileName = "gh",
                ArgumentList =
                {
                    "release",
                    "upload",
                    releaseTag,
                    zipFile,
                    "--repo", repository,
                    "--clobber"
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
                throw new Exception($"Failed to start gh upload process for {fileName}");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Failed to upload {fileName} to release: {error}");
            }

            Log.Information("Uploaded {FileName} successfully", fileName);
        }

        Log.Information("All packages uploaded successfully");
    }

    /// <summary>
    /// Extracts the release URL from gh release create output
    
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
    
    async Task SendFeishuNotificationAsync(string version, string? releaseUrl, bool success, string? errorMessage = null)
    {
        if (string.IsNullOrEmpty(FeishuWebhookUrl))
        {
            Log.Debug("Feishu webhook URL not configured, skipping notification");
            return;
        }

        try
        {
            using var httpClient = new HttpClient();

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
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            Log.Debug("Sending Feishu notification: {Message}", message);

            var response = await httpClient.PostAsync(FeishuWebhookUrl, content);

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

    /// <summary>
    /// Sends a Feishu notification about the release status (sync wrapper)
    
    void SendFeishuNotification(string version, string? releaseUrl, bool success, string? errorMessage = null)
    {
        SendFeishuNotificationAsync(version, releaseUrl, success, errorMessage).GetAwaiter().GetResult();
    }
}
