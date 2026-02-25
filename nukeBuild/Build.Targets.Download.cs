using Nuke.Common;
using NukeBuild.Adapters;
using Serilog;
using System.Linq;

/// <summary>
/// Download target - downloads all platform packages from Azure Blob Storage
/// </summary>
partial class Build
{
    Target Download => _ => _
        .DependsOn(Clean)
        .Requires(() => AzureBlobSasUrl)
        .Requires(() => ReleaseVersion)
        .Produces(DownloadDirectory / "*.zip")
        .Executes(DownloadExecute);

    void DownloadExecute()
    {
        Log.Information("Downloading ALL packages version {Version} from Azure Blob Storage", ReleaseVersion);

        var adapter = new AzureBlobAdapter();

        // Step 1: Download index.json
        var index = adapter.DownloadIndexJson(AzureBlobSasUrl);
        if (index == null)
        {
            Log.Error("Failed to download index.json from Azure Blob Storage");
            throw new Exception("Failed to download index.json from Azure Blob Storage");
        }

        // Step 2: Determine channel from version
        var channel = ExtractChannelFromVersion(FullVersion);
        Log.Information("Detected channel: {Channel} from version {Version}", channel, FullVersion);

        // Step 3: Validate that the requested version is the latest for the channel
        if (!string.IsNullOrEmpty(channel))
        {
            var latestVersion = adapter.GetLatestVersionForChannel(index, channel);
            if (latestVersion != null)
            {
                if (FullVersion != latestVersion)
                {
                    // Requested version is not the channel latest
                    Log.Error("Requested version {RequestedVersion} is not the latest version in the '{Channel}' channel.",
                        FullVersion, channel);
                    Log.Error("Latest version in '{Channel}' channel: {LatestVersion}", channel, latestVersion);
                    Log.Error("Hint: The 'release main push' workflow only processes the latest version of each channel.");
                    Log.Error("Please use the latest version or update the channel configuration.");

                    var errorMessage = FormatNotLatestVersionError(FullVersion, channel, latestVersion);
                    throw new Exception(errorMessage);
                }

                Log.Information("Version {Version} is confirmed as the latest in the '{Channel}' channel",
                    FullVersion, channel);
            }
            else
            {
                Log.Warning("Could not determine latest version for channel '{Channel}', skipping channel validation", channel);
            }
        }

        // Step 4: Validate that the requested version exists in the index
        var allVersions = adapter.GetAllVersions(index);
        var normalizedVersion = FullVersion.TrimStart('v');

        if (!allVersions.Contains(normalizedVersion))
        {
            Log.Error("Version {Version} not found in Azure Blob Storage index", FullVersion);
            Log.Error("Available versions ({Count} total):", allVersions.Count);

            var formattedVersions = FormatAvailableVersionsList(allVersions);
            Log.Error("{Versions}", formattedVersions);

            Log.Error("Hint: The requested version may have been removed from storage.");
            Log.Error("Use the latest available version or verify the version number.");

            var errorMessage = FormatVersionNotFoundError(FullVersion, allVersions);
            throw new Exception(errorMessage);
        }

        Log.Information("Version {Version} found in index, proceeding with download", FullVersion);

        // Step 5: Proceed with download
        var options = new AzureBlobDownloadAllOptions
        {
            SasUrl = AzureBlobSasUrl,
            Version = FullVersion,
            OutputDirectory = DownloadDirectory
        };

        var result = adapter.DownloadAllPackagesForVersion(options);

        if (!result.Success)
        {
            Log.Error("Download failed: {ErrorMessage}", result.ErrorMessage);
            throw new Exception($"Package download failed: {result.ErrorMessage}");
        }

        Log.Information("Download completed: {Count} packages, {Bytes:N0} total bytes",
            result.PackagePaths.Count, result.TotalDownloadedBytes);
    }

    /// <summary>
    /// Extracts the channel name from a version string (e.g., "beta" from "0.1.0-beta.1")
    /// </summary>
    string ExtractChannelFromVersion(string version)
    {
        // Common channel patterns: beta, stable, rc, alpha
        var channels = new[] { "beta", "stable", "rc", "alpha" };

        foreach (var channel in channels)
        {
            if (version.Contains(channel, StringComparison.OrdinalIgnoreCase))
            {
                return channel;
            }
        }

        // Default: if version contains a dash, use the part after the first dash as channel
        if (version.Contains('-'))
        {
            var parts = version.Split('-');
            if (parts.Length > 1)
            {
                // Extract the channel part (e.g., "beta" from "beta.1")
                var prereleasePart = parts[1];
                var dotIndex = prereleasePart.IndexOf('.');
                if (dotIndex > 0)
                {
                    return prereleasePart.Substring(0, dotIndex);
                }
                return prereleasePart;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Formats an error message when the requested version is not the channel latest.
    /// </summary>
    string FormatNotLatestVersionError(string requestedVersion, string channel, string latestVersion)
    {
        return $"Error: Requested version {requestedVersion} is not the latest version in the '{channel}' channel.\n" +
               $"\n" +
               $"Latest version in '{channel}' channel: {latestVersion}\n" +
               $"\n" +
               $"Hint: The 'release main push' workflow only processes the latest version of each channel.\n" +
               $"Please use the latest version or update the channel configuration.";
    }

    /// <summary>
    /// Formats an error message when the requested version is not found.
    /// </summary>
    string FormatVersionNotFoundError(string requestedVersion, System.Collections.Generic.List<string> availableVersions)
    {
        var formattedVersions = FormatAvailableVersionsList(availableVersions);
        return $"Error: Version {requestedVersion} not found in Azure Blob Storage index.\n" +
               $"\n" +
               $"Available versions ({availableVersions.Count} total):\n" +
               $"{formattedVersions}\n" +
               $"\n" +
               $"Hint: The requested version may have been removed from storage.\n" +
               $"Use the latest available version or verify the version number.";
    }

    /// <summary>
    /// Formats the available versions list into a readable multi-line format.
    /// </summary>
    static string FormatAvailableVersionsList(System.Collections.Generic.List<string> versions)
    {
        if (versions.Count == 0)
        {
            return "  (none)";
        }

        // Format with 8 versions per line for readability
        var lines = new System.Collections.Generic.List<string>();
        for (int i = 0; i < versions.Count; i += 8)
        {
            var batch = versions.Skip(i).Take(8);
            lines.Add("  " + string.Join(", ", batch));
        }
        return string.Join(",\n", lines);
    }
}
