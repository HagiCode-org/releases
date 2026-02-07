using Nuke.Common;
using Nuke.Common.IO;
using Serilog;
using System.IO.Compression;
using System.Linq;

/// <summary>
/// Extract target - extracts Linux package for Docker build
/// </summary>
partial class Build
{
    Target Extract => _ => _
        .DependsOn(Download)
        .Executes(ExtractExecute);

    void ExtractExecute()
    {
        Log.Information("Extracting Linux package for Docker build");

        System.IO.Directory.CreateDirectory(ExtractedDirectory);
        foreach (var file in System.IO.Directory.GetFiles(ExtractedDirectory))
        {
            System.IO.File.Delete(file);
        }
        foreach (var dir in System.IO.Directory.GetDirectories(ExtractedDirectory))
        {
            System.IO.Directory.Delete(dir, true);
        }

        // Find the linux-x64 package for Docker build
        var zipFiles = DownloadDirectory.GlobFiles("*linux-x64*.zip");
        if (zipFiles.Count == 0)
        {
            throw new Exception("No Linux x64 .zip package found in download directory");
        }

        var zipFile = zipFiles.First();
        Log.Information("Extracting {ZipFile} to {ExtractDir}", zipFile, ExtractedDirectory);

        ZipFile.ExtractToDirectory(zipFile, ExtractedDirectory);

        Log.Information("Extraction completed");
    }
}
