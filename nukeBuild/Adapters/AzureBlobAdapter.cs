using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Nuke.Common.IO;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NukeBuild.Adapters
{
    public interface IAzureBlobAdapter
    {
        bool ValidateSasUrl(string sasUrl);
        AzureBlobDownloadResult DownloadPackage(AzureBlobDownloadOptions options);
        AzureBlobDownloadAllResult DownloadAllPackagesForVersion(AzureBlobDownloadAllOptions options);
        PackageIndex? DownloadIndexJson(string sasUrl);
        string? FindVersion(PackageIndex? index, string version, string platform);
        List<string> GetAllPackagePaths(PackageIndex? index, string version);
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

                // Extract SAS query parameters from the original SAS URL
                var blobUri = new Uri(options.SasUrl);
                var sasQueryParameters = blobUri.Query;
                var containerBaseUri = $"{blobUri.Scheme}://{blobUri.Host}{blobUri.AbsolutePath}";

                // Build full URL for the package with SAS token
                var packageUrl = $"{containerBaseUri.TrimEnd('/')}/{packagePath}{sasQueryParameters}";

                // Extract filename from path
                var packageFileName = System.IO.Path.GetFileName(packagePath);
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

            // Find the version entry
            var versionEntry = index.Versions.FirstOrDefault(v => v.Version == normalizedVersion);

            if (versionEntry == null)
            {
                Log.Warning("Version {Version} not found in index. Available versions: {Versions}",
                    normalizedVersion,
                    string.Join(", ", index.Versions.Select(v => v.Version)));
                return null;
            }

            // Find the asset for the specified platform
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

        public List<string> GetAllPackagePaths(PackageIndex? index, string version)
        {
            var paths = new List<string>();

            if (index == null || index.Versions.Count == 0)
            {
                Log.Warning("Index is empty or null");
                return paths;
            }

            // Normalize version (remove 'v' prefix if present)
            var normalizedVersion = version.TrimStart('v');

            // Find the version entry
            var versionEntry = index.Versions.FirstOrDefault(v => v.Version == normalizedVersion);

            if (versionEntry == null)
            {
                Log.Warning("Version {Version} not found in index. Available versions: {Versions}",
                    normalizedVersion,
                    string.Join(", ", index.Versions.Select(v => v.Version)));
                return paths;
            }

            // Return all asset paths for this version
            foreach (var asset in versionEntry.Assets)
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
                var packagePaths = GetAllPackagePaths(index, options.Version);

                if (packagePaths.Count == 0)
                {
                    result.ErrorMessage = $"Version {options.Version} not found in index";
                    return result;
                }

                Log.Information("Downloading {Count} packages for version {Version}", packagePaths.Count, options.Version);

                // Extract SAS query parameters from the original SAS URL
                var blobUri = new Uri(options.SasUrl);
                var sasQueryParameters = blobUri.Query;
                var containerBaseUri = $"{blobUri.Scheme}://{blobUri.Host}{blobUri.AbsolutePath}";

                // Download all packages
                foreach (var packagePath in packagePaths)
                {
                    // Build full URL with SAS token
                    var packageUrl = $"{containerBaseUri.TrimEnd('/')}/{packagePath}{sasQueryParameters}";
                    var packageFileName = System.IO.Path.GetFileName(packagePath);
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

        private void DownloadFileWithRetry(string url, AbsolutePath localPath, out long downloadedBytes)
        {
            downloadedBytes = 0;

            // Check if file already exists and skip download
            if (System.IO.File.Exists(localPath))
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

                    // Get the file size after download
                    downloadedBytes = new FileInfo(localPath).Length;

                    Log.Information("Download completed: {Bytes:N0} bytes", downloadedBytes);
                    return;
                }
                catch (RequestFailedException ex) when (attempt < MaxRetries)
                {
                    lastException = ex;
                    Log.Warning("Download attempt {Attempt} failed: {Message}. Retrying in {Delay}ms...",
                        attempt, ex.Message, RetryDelayMilliseconds);

                    Task.Delay(RetryDelayMilliseconds).Wait();
                }
            }

            throw new InvalidOperationException($"Failed to download after {MaxRetries} attempts", lastException);
        }
    }
}
