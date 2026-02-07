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
        .Requires(() => Version)
        .Produces(DownloadDirectory / "*.zip")
        .Executes(DownloadExecute);

    void DownloadExecute()
    {
        Log.Information("Downloading ALL packages version {Version} from Azure Blob Storage", Version);

        var adapter = new AzureBlobAdapter();
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
}
