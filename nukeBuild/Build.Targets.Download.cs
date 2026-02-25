using Nuke.Common;
using NukeBuild.Adapters;
using Serilog;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Download target - downloads all platform packages from Azure Blob Storage
/// </summary>
partial class Build
{
    Target Download => _ => _
        .DependsOn(Clean)
        .Requires(() => AzureBlobSasUrl)
        .Executes(DownloadExecute);

    void DownloadExecute()
    {
        // Determine which versions to download
        var versionsToDownload = DetermineVersionsToDownload();

        if (versionsToDownload.Count == 0)
        {
            Log.Error("No versions to download");
            throw new Exception("No versions to download");
        }

        // Download each version
        var adapter = new AzureBlobAdapter();
        var allResults = new List<string>();
        DownloadedVersions.Clear();

        foreach (var (channel, version) in versionsToDownload)
        {
            Log.Information("========================================");
            Log.Information("Downloading {Channel} channel: {Version}", channel, version);
            Log.Information("========================================");

            try
            {
                var result = DownloadVersion(adapter, version);
                DownloadedVersions.Add((channel, version));
                allResults.Add($"✓ {channel}: {version}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to download {Channel} channel version {Version}", channel, version);
                allResults.Add($"✗ {channel}: {version} - {ex.Message}");
                throw;
            }
        }

        // Summary
        Log.Information("========================================");
        Log.Information("Download Summary:");
        Log.Information("========================================");
        foreach (var result in allResults)
        {
            Log.Information("{Result}", result);
        }

        Log.Information("Using version for Docker builds: {Version}", EffectiveVersion);
    }

    /// <summary>
    /// Determines which versions to download based on command-line parameters
    /// </summary>
    /// <returns>A list of (channel, version) tuples to download</returns>
    List<(string channel, string version)> DetermineVersionsToDownload()
    {
        var adapter = new AzureBlobAdapter();
        var index = adapter.DownloadIndexJson(AzureBlobSasUrl);
        if (index == null)
        {
            Log.Error("Failed to download index.json from Azure Blob Storage");
            throw new Exception("Failed to download index.json from Azure Blob Storage");
        }

        // If a specific version is provided, download only that version
        if (!string.IsNullOrEmpty(ReleaseVersion))
        {
            Log.Information("Downloading specific version: {Version}", ReleaseVersion);
            var normalizedVersion = ReleaseVersion.TrimStart('v');

            // Validate version exists
            var allVersions = adapter.GetAllVersions(index);
            if (!allVersions.Contains(normalizedVersion))
            {
                Log.Warning("Version {Version} not found in Azure Blob Storage index", ReleaseVersion);
                Log.Warning("Available versions ({Count} total):", allVersions.Count);
                var formattedVersions = FormatAvailableVersionsList(allVersions);
                Log.Warning("{Versions}", formattedVersions);
                Log.Warning("Falling back to BuildAllChannels mode...");
                return GetAllChannelsVersions(adapter, index);
            }

            // Determine channel
            var channel = ExtractChannelFromVersion(FullVersion);
            if (string.IsNullOrEmpty(channel))
            {
                channel = "stable";
            }

            // Validate it's the latest for the channel
            var latestVersion = adapter.GetLatestVersionForChannel(index, channel);
            if (latestVersion != null && FullVersion != latestVersion)
            {
                Log.Warning("Requested version {RequestedVersion} is not the latest version in the '{Channel}' channel.",
                    FullVersion, channel);
                Log.Warning("Latest version in '{Channel}' channel: {LatestVersion}", channel, latestVersion);
                Log.Warning("Falling back to BuildAllChannels mode...");
                return GetAllChannelsVersions(adapter, index);
            }

            return new List<(string, string)> { (channel, FullVersion) };
        }

        // If BuildAllChannels is set, download all channels' latest versions
        if (BuildAllChannels)
        {
            Log.Information("BuildAllChannels enabled: downloading latest version for all channels");
            return GetAllChannelsVersions(adapter, index);
        }

        // Default: fall back to BuildAllChannels
        Log.Warning("No version specified and BuildAllChannels not set.");
        Log.Warning("Falling back to BuildAllChannels mode...");
        return GetAllChannelsVersions(adapter, index);
    }

    /// <summary>
    /// Gets all channels' latest versions as a list of (channel, version) tuples
    /// </summary>
    List<(string channel, string version)> GetAllChannelsVersions(IAzureBlobAdapter adapter, NukeBuild.Adapters.PackageIndex index)
    {
        var channelsDict = adapter.GetAllChannelsWithLatestVersions(index);
        return channelsDict.Select(kvp => (kvp.Key, kvp.Value)).ToList();
    }

    /// <summary>
    /// Downloads a specific version from Azure Blob Storage
    /// </summary>
    AzureBlobDownloadAllResult DownloadVersion(IAzureBlobAdapter adapter, string version)
    {
        Log.Information("Downloading ALL packages version {Version} from Azure Blob Storage", version);

        var options = new AzureBlobDownloadAllOptions
        {
            SasUrl = AzureBlobSasUrl,
            Version = version,
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

        return result;
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
