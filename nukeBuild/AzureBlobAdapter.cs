using Azure;
using Azure.Storage.Blobs;
using Nuke.Common.IO;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NukeBuild.Adapters
{
    public interface IAzureBlobAdapter
    {
        bool ValidateSasUrl(string sasUrl);
        AzureBlobDownloadResult DownloadPackage(AzureBlobDownloadOptions options);
        AzureBlobDownloadAllResult DownloadAllPackagesForVersion(AzureBlobDownloadAllOptions options);
        PackageIndex? DownloadIndexJson(string sasUrl);
        string? FindVersion(PackageIndex? index, string version, string platform);
        List<string> GetAllPackagePaths(PackageIndex? index, string version, List<string>? platforms = null);
        List<string> GetAllVersions(PackageIndex? index);
        string? GetLatestVersionForChannel(PackageIndex? index, string channel);
        Dictionary<string, string> GetAllChannelsWithLatestVersions(PackageIndex? index);
    }

    public class AzureBlobDownloadOptions
    {
        public string SasUrl { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Platform { get; set; } = "linux-x64"; // Default platform
        public AbsolutePath OutputDirectory { get; set; } = null!;
    }

    public class AzureBlobDownloadAllOptions
    {
        public string SasUrl { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public AbsolutePath OutputDirectory { get; set; } = null!;
        public List<string> Platforms { get; set; } = new();
    }

    public class AzureBlobDownloadResult
    {
        public bool Success { get; set; }
        public string? PackagePath { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> Warnings { get; set; } = new();
        public long DownloadedBytes { get; set; }
    }

    public class AzureBlobDownloadAllResult
    {
        public bool Success { get; set; }
        public List<string> PackagePaths { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public List<string> Warnings { get; set; } = new();
        public long TotalDownloadedBytes { get; set; }
    }

    #region Index Models

    public class PackageIndex
    {
        [JsonPropertyName("updatedAt")]
        public string UpdatedAt { get; set; } = string.Empty;

        [JsonPropertyName("versions")]
        public List<PackageVersion> Versions { get; set; } = new();
    }

    public class PackageVersion
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("files")]
        public List<string> Files { get; set; } = new();

        [JsonPropertyName("assets")]
        public List<PackageAsset> Assets { get; set; } = new();
    }

    public class PackageAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("lastModified")]
        public string LastModified { get; set; } = string.Empty;
    }

    #endregion

    public class AzureBlobAdapter : IAzureBlobAdapter
    {
        private const int MaxRetries = 3;
        private const int RetryDelayMilliseconds = 1000;

        public bool ValidateSasUrl(string sasUrl)
        {
            if (string.IsNullOrWhiteSpace(sasUrl))
            {
                Log.Error("SAS URL is empty");
                return false;
            }

            if (!sasUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !sasUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                Log.Error("SAS URL must start with https:// or http://");
                return false;
            }

            // Check for required SAS parameters
            var hasPermissions = sasUrl.Contains("sp=", StringComparison.OrdinalIgnoreCase);
            var hasSignature = sasUrl.Contains("sig=", StringComparison.OrdinalIgnoreCase);

            if (!hasPermissions || !hasSignature)
            {
                Log.Warning("SAS URL may be invalid: missing permissions or signature");
            }

            return true;
        }

        public AzureBlobDownloadResult DownloadPackage(AzureBlobDownloadOptions options)
        {
            var result = new AzureBlobDownloadResult();

            if (!ValidateSasUrl(options.SasUrl))
            {
                result.ErrorMessage = "Invalid SAS URL";
                return result;
            }

            try
            {
                // Create output directory
                options.OutputDirectory.CreateDirectory();

                // Download index.json first
                var index = DownloadIndexJson(options.SasUrl);
                var packagePath = FindVersion(index, options.Version, options.Platform);

                if (string.IsNullOrEmpty(packagePath))
                {
                    result.ErrorMessage = $"Version {options.Version} (platform: {options.Platform}) not found in index";
                    return result;
                }

                Log.Information("Downloading package from: {PackagePath}", packagePath);

                // Extract SAS query parameters from original SAS URL
                var blobUri = new Uri(options.SasUrl);
                var sasQueryParameters = blobUri.Query;
                var containerBaseUri = $"{blobUri.Scheme}://{blobUri.Host}{blobUri.AbsolutePath}";

                // Build full URL for package with SAS token
                var packageUrl = $"{containerBaseUri.TrimEnd('/')}/{packagePath}{sasQueryParameters}";

                // Extract filename from path
                var packageFileName = Path.GetFileName(packagePath);
                var localPath = options.OutputDirectory / packageFileName;

                DownloadFileWithRetry(packageUrl, localPath, out var downloadedBytes);

                result.Success = true;
                result.PackagePath = localPath;
                result.DownloadedBytes = downloadedBytes;

                Log.Information("Package downloaded successfully: {Path} ({Bytes:N0} bytes)", localPath, downloadedBytes);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Download failed: {ex.Message}";
                Log.Error(ex, "Package download failed");
            }

            return result;
        }

        public PackageIndex? DownloadIndexJson(string sasUrl)
        {
            try
            {
                var blobContainerClient = new BlobContainerClient(new Uri(sasUrl));

                // Try to get index.json from root
                var blobClient = blobContainerClient.GetBlobClient("index.json");

                if (!blobClient.Exists())
                {
                    Log.Warning("index.json not found in container root");
                    return null;
                }

                var content = blobClient.DownloadContent();
                var jsonContent = content.Value.Content.ToString();

                Log.Debug("Index JSON content: {JsonContent}", jsonContent);

                var index = JsonSerializer.Deserialize<PackageIndex>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (index != null)
                {
                    Log.Information("Loaded index with {Count} versions", index.Versions.Count);
                }

                return index;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to download or parse index.json");
                return null;
            }
        }

        public string? FindVersion(PackageIndex? index, string version, string platform)
        {
            if (index == null || index.Versions.Count == 0)
            {
                Log.Warning("Index is empty or null");
                return null;
            }

            // Normalize version (remove 'v' prefix if present)
            var normalizedVersion = version.TrimStart('v');

            Log.Information("Looking for version {Version} with platform {Platform}", normalizedVersion, platform);

            // Find version entry
            var versionEntry = index.Versions.FirstOrDefault(v => v.Version == normalizedVersion);

            if (versionEntry == null)
            {
                Log.Warning("Version {Version} not found in index. Available versions: {Versions}",
                    normalizedVersion,
                    string.Join(", ", index.Versions.Select(v => v.Version)));
                return null;
            }

            // Find asset for specified platform
            // Platform patterns: linux-x64, osx-x64, win-x64
            var asset = versionEntry.Assets.FirstOrDefault(a =>
                a.Name.Contains(platform, StringComparison.OrdinalIgnoreCase));

            if (asset == null)
            {
                Log.Warning("Platform {Platform} not found for version {Version}. Available assets: {Assets}",
                    platform,
                    normalizedVersion,
                    string.Join(", ", versionEntry.Assets.Select(a => a.Name)));
                return null;
            }

            Log.Information("Found package: {Name} at {Path}", asset.Name, asset.Path);
            return asset.Path;
        }

        public List<string> GetAllPackagePaths(PackageIndex? index, string version, List<string>? platforms = null)
        {
            var paths = new List<string>();

            if (index == null || index.Versions.Count == 0)
            {
                Log.Warning("Index is empty or null");
                return paths;
            }

            // Normalize version (remove 'v' prefix if present)
            var normalizedVersion = version.TrimStart('v');

            // Find version entry
            var versionEntry = index.Versions.FirstOrDefault(v => v.Version == normalizedVersion);

            if (versionEntry == null)
            {
                var availableVersions = index.Versions.Select(v => v.Version).ToList();
                var formattedVersions = FormatAvailableVersionsList(availableVersions);
                Log.Warning("Version {Version} not found in index. Available versions ({Count} total):\n{Versions}",
                    normalizedVersion,
                    availableVersions.Count,
                    formattedVersions);
                return paths;
            }

            // Filter assets by platform if specified
            var assetsToProcess = versionEntry.Assets;
            if (platforms != null && platforms.Count > 0)
            {
                assetsToProcess = versionEntry.Assets
                    .Where(a => platforms.Any(p => a.Name.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                Log.Information("Filtered to {Count} packages matching platforms: {Platforms}", assetsToProcess.Count, string.Join(", ", platforms));
            }

            // Return all asset paths for this version (filtered by platform if applicable)
            foreach (var asset in assetsToProcess)
            {
                paths.Add(asset.Path);
                Log.Debug("Found package: {Name} at {Path}", asset.Name, asset.Path);
            }

            Log.Information("Found {Count} packages for version {Version}", paths.Count, normalizedVersion);
            return paths;
        }

        public AzureBlobDownloadAllResult DownloadAllPackagesForVersion(AzureBlobDownloadAllOptions options)
        {
            var result = new AzureBlobDownloadAllResult();

            if (!ValidateSasUrl(options.SasUrl))
            {
                result.ErrorMessage = "Invalid SAS URL";
                return result;
            }

            try
            {
                // Create output directory
                options.OutputDirectory.CreateDirectory();

                // Download index.json first
                var index = DownloadIndexJson(options.SasUrl);
                var packagePaths = GetAllPackagePaths(index, options.Version, options.Platforms);

                if (packagePaths.Count == 0)
                {
                    result.ErrorMessage = $"Version {options.Version} not found in index";
                    return result;
                }

                Log.Information("Downloading {Count} packages for version {Version}", packagePaths.Count, options.Version);

                // Extract SAS query parameters from original SAS URL
                var blobUri = new Uri(options.SasUrl);
                var sasQueryParameters = blobUri.Query;
                var containerBaseUri = $"{blobUri.Scheme}://{blobUri.Host}{blobUri.AbsolutePath}";

                // Download all packages
                foreach (var packagePath in packagePaths)
                {
                    // Build full URL with SAS token
                    var packageUrl = $"{containerBaseUri.TrimEnd('/')}/{packagePath}{sasQueryParameters}";
                    var packageFileName = Path.GetFileName(packagePath);
                    var localPath = options.OutputDirectory / packageFileName;

                    DownloadFileWithRetry(packageUrl, localPath, out var downloadedBytes);
                    result.PackagePaths.Add(localPath);
                    result.TotalDownloadedBytes += downloadedBytes;

                    Log.Information("Downloaded: {FileName} ({Bytes:N0} bytes)", packageFileName, downloadedBytes);
                }

                result.Success = true;
                Log.Information("All packages downloaded successfully. Total: {Bytes:N0} bytes", result.TotalDownloadedBytes);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Download failed: {ex.Message}";
                Log.Error(ex, "Package download failed");
            }

            return result;
        }

        public List<string> GetAllVersions(PackageIndex? index)
        {
            var versions = new List<string>();

            if (index == null || index.Versions.Count == 0)
            {
                Log.Warning("Index is empty or null");
                return versions;
            }

            // Extract all version strings from index
            foreach (var versionEntry in index.Versions)
            {
                versions.Add(versionEntry.Version);
            }

            Log.Information("Found {Count} versions in index", versions.Count);
            return versions;
        }

        private void DownloadFileWithRetry(string url, AbsolutePath localPath, out long downloadedBytes)
        {
            downloadedBytes = 0;

            // Check if file already exists and skip download
            if (File.Exists(localPath))
            {
                downloadedBytes = new FileInfo(localPath).Length;
                Log.Information("File already exists, skipping download: {Path} ({Bytes:N0} bytes)", localPath, downloadedBytes);
                return;
            }

            Exception? lastException = null;

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    var blobClient = new BlobClient(new Uri(url));

                    Log.Information("Download attempt {Attempt}/{MaxRetries}: {Url}", attempt, MaxRetries, url);

                    using var stream = File.OpenWrite(localPath);
                    blobClient.DownloadTo(stream);

                    // Get file size after download
                    downloadedBytes = new FileInfo(localPath).Length;

                    Log.Information("Download completed: {Bytes:N0} bytes", downloadedBytes);
                    return;
                }
                catch (RequestFailedException ex) when (attempt < MaxRetries)
                {
                    lastException = ex;
                    Log.Warning("Download attempt {Attempt} failed: {Message}. Retrying in {Delay}ms...",
                        attempt, ex.Message, RetryDelayMilliseconds);

                    Thread.Sleep(RetryDelayMilliseconds);
                }
            }

            throw new InvalidOperationException($"Failed to download after {MaxRetries} attempts", lastException);
        }

        /// <summary>
        /// Gets latest version for a specific channel from package index.
        
        /// <param name="index">The package index containing all versions.</param>
        /// <param name="channel">The channel to filter by (e.g., "beta", "stable").</param>
        /// <returns>The latest version string for specified channel, or null if not found.</returns>
        public string? GetLatestVersionForChannel(PackageIndex? index, string channel)
        {
            if (index == null || index.Versions.Count == 0)
            {
                Log.Warning("Index is empty or null, cannot get latest version for channel {Channel}", channel);
                return null;
            }

            // Filter versions by channel (case-insensitive)
            var channelVersions = index.Versions
                .Where(v => v.Version.Contains(channel, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (channelVersions.Count == 0)
            {
                Log.Warning("No versions found for channel {Channel}", channel);
                return null;
            }

            // Sort by semantic version to get the latest
            var latestVersion = channelVersions
                .OrderByDescending(v => v.Version, new SemanticVersionComparer())
                .FirstOrDefault();

            if (latestVersion != null)
            {
                Log.Information("Latest version for channel {Channel} is {Version}", channel, latestVersion.Version);
                return latestVersion.Version;
            }

            return null;
        }

        /// <summary>
        /// Gets all channels and their latest versions from package index.
        
        /// <param name="index">The package index containing all versions.</param>
        /// <returns>A dictionary mapping channel names to their latest version strings.</returns>
        public Dictionary<string, string> GetAllChannelsWithLatestVersions(PackageIndex? index)
        {
            var result = new Dictionary<string, string>();

            if (index == null || index.Versions.Count == 0)
            {
                Log.Warning("Index is empty or null, cannot get channels and latest versions");
                return result;
            }

            // Identify all unique channels
            var channels = new HashSet<string>();
            foreach (var version in index.Versions)
            {
                var channel = ExtractChannelFromVersion(version.Version);
                if (!string.IsNullOrEmpty(channel))
                {
                    channels.Add(channel);
                }
            }

            // If no channels found, treat all versions as "stable" channel
            if (channels.Count == 0)
            {
                Log.Warning("No channels identified in versions, treating all versions as 'stable'");
                channels.Add("stable");
            }

            // Get latest version for each channel
            foreach (var channel in channels)
            {
                var latest = GetLatestVersionForChannel(index, channel);
                if (!string.IsNullOrEmpty(latest))
                {
                    result[channel] = latest;
                }
            }

            Log.Information("Found {Count} channels with latest versions: {Channels}",
                result.Count, string.Join(", ", result.Keys));
            foreach (var kvp in result)
            {
                Log.Information("  {Channel}: {Version}", kvp.Key, kvp.Value);
            }

            return result;
        }

        /// <summary>
        /// Extracts channel name from a version string (e.g., "beta" from "0.1.0-beta.1")
        
        private string ExtractChannelFromVersion(string version)
        {
            // Common channel patterns: beta, stable, rc, alpha
            var knownChannels = new[] { "beta", "stable", "rc", "alpha" };

            foreach (var channel in knownChannels)
            {
                if (version.Contains(channel, StringComparison.OrdinalIgnoreCase))
                {
                    return channel;
                }
            }

            // Default: if version contains a dash, use part after first dash as channel
            if (version.Contains('-'))
            {
                var parts = version.Split('-');
                if (parts.Length > 1)
                {
                    // Extract channel part (e.g., "beta" from "beta.1")
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
        /// Formats available versions list into a readable multi-line format.
        
        private static string FormatAvailableVersionsList(List<string> versions)
        {
            if (versions.Count == 0)
            {
                return "  (none)";
            }

            // Format with 8 versions per line for readability
            var lines = new List<string>();
            for (int i = 0; i < versions.Count; i += 8)
            {
                var batch = versions.Skip(i).Take(8);
                lines.Add("  " + string.Join(", ", batch));
            }
            return string.Join(",\n", lines);
        }

        /// <summary>
        /// Compares semantic version strings for sorting.
        
        private class SemanticVersionComparer : IComparer<string>
        {
            public int Compare(string? x, string? y)
            {
                if (string.IsNullOrEmpty(x)) return -1;
                if (string.IsNullOrEmpty(y)) return 1;

                // Normalize versions
                var xVersion = x.TrimStart('v').ToLowerInvariant();
                var yVersion = y.TrimStart('v').ToLowerInvariant();

                // Split version parts
                var xParts = xVersion.Split(['-', '.']);
                var yParts = yVersion.Split(['-', '.']);

                // Compare each part
                for (int i = 0; i < Math.Max(xParts.Length, yParts.Length); i++)
                {
                    if (i >= xParts.Length) return -1;
                    if (i >= yParts.Length) return 1;

                    var xPart = xParts[i];
                    var yPart = yParts[i];

                    // Try numeric comparison
                    if (int.TryParse(xPart, out var xNum) && int.TryParse(yPart, out var yNum))
                    {
                        var numCompare = xNum.CompareTo(yNum);
                        if (numCompare != 0) return numCompare;
                    }
                    else
                    {
                        // String comparison for pre-release tags (beta, rc, etc.)
                        var stringCompare = string.CompareOrdinal(xPart, yPart);
                        if (stringCompare != 0) return stringCompare;
                    }
                }

                return 0;
            }
        }
    }
}
