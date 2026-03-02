using Nuke.Common;
using NukeBuild.Adapters;
using Serilog;

/// <summary>
/// Download target - downloads packages from Azure Blob Storage

partial class Build
{
    Target Download => _ => _
        .Executes(DownloadExecute);

    void DownloadExecute()
    {
        Log.Information("Downloading packages from Azure Blob Storage");

        var sasUrl = EffectiveAzureBlobSasUrl;
        var version = EffectiveReleaseVersion;

        if (string.IsNullOrEmpty(sasUrl))
        {
            throw new Exception("Azure Blob SAS URL is not specified");
        }

        if (string.IsNullOrEmpty(version))
        {
            throw new Exception("Release version is not specified");
        }

        var adapter = new AzureBlobAdapter();
        var downloadOptions = new AzureBlobDownloadAllOptions
        {
            SasUrl = sasUrl,
            Version = version,
            OutputDirectory = DownloadDirectory,
            Platforms = new List<string> { "linux-x64", "linux-arm64" }
        };

        var result = adapter.DownloadAllPackagesForVersion(downloadOptions);

        if (!result.Success)
        {
            throw new Exception($"Failed to download packages: {result.ErrorMessage}");
        }

        // Filter to only include zip files (not JSON manifests)
        var zipFiles = result.PackagePaths.Where(p => p.ToString().EndsWith(".zip")).ToList();
        if (zipFiles.Count == 0)
        {
            throw new Exception($"No zip packages found for version {version}");
        }

        Log.Information("Successfully downloaded {Count} zip packages ({Bytes:N0} bytes)",
            zipFiles.Count, result.TotalDownloadedBytes);
    }

    /// <summary>
    /// Gets the effective Azure Blob SAS URL from environment variable or parameter
    
    string EffectiveAzureBlobSasUrl => Environment.GetEnvironmentVariable("NUGEX_AzureBlobSasUrl") ?? AzureBlobSasUrl;
}
