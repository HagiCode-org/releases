using Nuke.Common.IO;

/// <summary>
/// Helper methods for Build
/// </summary>
partial class Build
{
    /// <summary>
    /// Recursively copies a directory and all its contents
    /// </summary>
    static void CopyDirectoryRecursive(AbsolutePath sourceDir, AbsolutePath targetDir)
    {
        // Copy all files in the current directory
        foreach (var file in sourceDir.GlobFiles("*"))
        {
            System.IO.File.Copy(file, targetDir / file.Name, true);
        }

        // Recursively copy subdirectories
        foreach (var dir in sourceDir.GetDirectories())
        {
            var targetSubDir = targetDir / dir.Name;
            targetSubDir.CreateDirectory();
            CopyDirectoryRecursive(dir, targetSubDir);
        }
    }
}
