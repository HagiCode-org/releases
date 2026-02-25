using Nuke.Common;
using Nuke.Common.IO;
using Serilog;
using System.IO.Compression;
using System.Linq;

/// <summary>
/// Extract target - extracts Linux package for Docker build
/// Supports platform-specific package extraction based on Platform parameter
/// </summary>
partial class Build
{
    Target Extract => _ => _
        .DependsOn(Download)
        .Executes(ExtractExecute);

    void ExtractExecute()
    {
        // Determine target platform from Platform parameter
        var targetPlatform = Platform.ToLowerInvariant() switch
        {
            "all" => "linux-x64", // Default to linux-x64 for "all"
            "linux-arm64" => "linux-arm64",
            "linux-x64" => "linux-x64",
            _ => "linux-x64" // Default fallback
        };

        Log.Information("Extracting Linux package for Docker build (platform: {Platform})", targetPlatform);

        System.IO.Directory.CreateDirectory(ExtractedDirectory);
        foreach (var file in System.IO.Directory.GetFiles(ExtractedDirectory))
        {
            System.IO.File.Delete(file);
        }
        foreach (var dir in System.IO.Directory.GetDirectories(ExtractedDirectory))
        {
            System.IO.Directory.Delete(dir, true);
        }

        // Find the platform-specific package for Docker build
        // Pattern: *linux-x64*.zip or *linux-arm64*.zip
        var zipFiles = DownloadDirectory.GlobFiles($"*{targetPlatform}*.zip");
        if (zipFiles.Count == 0)
        {
            throw new Exception($"No Linux {targetPlatform} .zip package found in download directory");
        }

        var zipFile = zipFiles.First();
        Log.Information("Extracting {ZipFile} to {ExtractDir}", zipFile, ExtractedDirectory);

        ZipFile.ExtractToDirectory(zipFile, ExtractedDirectory);

        Log.Information("Extraction completed for platform: {Platform}", targetPlatform);
    }
}
