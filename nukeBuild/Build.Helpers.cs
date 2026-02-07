using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.Docker;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>
    /// Tags a local Docker image for a target registry
    /// </summary>
    /// <param name="sourceImage">Source image (e.g., docker.io/newbe36524/hagicode:v1.0.0)</param>
    /// <param name="targetRegistry">Target registry (e.g., hagicode.azurecr.io)</param>
    /// <returns>The tagged image name</returns>
    string TagImageForRegistry(string sourceImage, string targetRegistry)
    {
        // Extract image name and tag from source
        // e.g., "docker.io/newbe36524/hagicode:v1.0.0" -> "hagicode:v1.0.0"
        var lastSlashIndex = sourceImage.LastIndexOf('/');
        var imageWithTag = lastSlashIndex >= 0 ? sourceImage.Substring(lastSlashIndex + 1) : sourceImage;

        // Create new image with target registry
        // e.g., "hagicode.azurecr.io/hagicode:v1.0.0"
        var targetImage = $"{targetRegistry}/{imageWithTag}";

        Log.Information("Tagging {Source} as {Target}", sourceImage, targetImage);

        DockerTasks.DockerTag(s => s
            .SetSourceImage(sourceImage)
            .SetTargetImage(targetImage));

        return targetImage;
    }

    /// <summary>
    /// Pushes a Docker image to a specific registry
    /// </summary>
    /// <param name="localImage">Local image to push</param>
    /// <param name="targetRegistry">Target registry address</param>
    /// <param name="username">Registry username</param>
    /// <param name="password">Registry password</param>
    /// <param name="tags">List of tags to push</param>
    void PushToRegistry(string localImage, string targetRegistry, string username, string password, List<string> tags)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Log.Warning("Skipping push to {Registry} - credentials not provided", targetRegistry);
            return;
        }

        try
        {
            Log.Information("Logging in to {Registry}", targetRegistry);

            DockerTasks.DockerLogin(s => s
                .SetServer(targetRegistry)
                .SetUsername(username)
                .SetPassword(password));

            Log.Information("Successfully logged in to {Registry}", targetRegistry);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to log in to {Registry}", targetRegistry);
            throw;
        }

        Log.Information("Pushing {Image} with {Count} tags to {Registry}", localImage, tags.Count, targetRegistry);

        foreach (var tag in tags)
        {
            var taggedImage = TagImageForRegistry(tag, targetRegistry);
            Log.Information("Pushing {Tag}", taggedImage);

            try
            {
                DockerTasks.DockerPush(s => s.SetName(taggedImage));
                Log.Information("Successfully pushed {Tag}", taggedImage);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to push {Tag}", taggedImage);
                throw;
            }
        }

        Log.Information("Successfully pushed all tags to {Registry}", targetRegistry);
    }
}
