using Nuke.Common.IO;
using Serilog;
using System.Diagnostics;

/// Docker application image build target
///
/// This partial class provides application Docker image building functionality.
/// Builds the unified application image using a single multi-stage Dockerfile.
partial class Build
{
    // ==========================================================================
    // App Image State Properties
    // ==========================================================================

    /// Gets the Docker app template Dockerfile path
    AbsolutePath DockerAppTemplateDockerfile => DockerDeploymentDirectory / "Dockerfile.template";

    /// Gets the Docker entrypoint script path
    AbsolutePath DockerEntrypointScript => DockerDeploymentDirectory / "docker-entrypoint.sh";

    /// Gets the extracted package directory for Docker build
    AbsolutePath DockerBuildContext => OutputDirectory / "docker-build-context";

    /// Gets the generated app Dockerfile path
    AbsolutePath GeneratedAppDockerfile => DockerBuildContext / "Dockerfile";

    // ==========================================================================
    // App Image Implementation
    // ==========================================================================

    void BuildApplicationImage(DockerImageInfo image,
        string version,
        IEnumerable<string> platforms,
        bool pushToRegistry = true)
    {
        Log.Information("Building unified Docker image for version {Version}...", version);
        Log.Debug("PushToRegistry: {PushToRegistry}", pushToRegistry);

        // Prepare build context for multi-arch builds
        var isMultiPlatform = platforms.Count() > 1;
        PrepareBuildContext(version, isMultiPlatform: isMultiPlatform, platforms: platforms.ToList());

        var registryImageTag = image.WithTag(version);

        var platformArg = platforms.Count() > 1
            ? "--platform=linux/amd64,linux/arm64"
            : $"--platform={platforms.First()}";

        var buildArgs = new List<string>
        {
            "buildx",
            "build",
            platformArg,
            "--file", GeneratedAppDockerfile,
            DockerBuildContext.ToString()
        };

        buildArgs.Add("--tag");
        buildArgs.Add(registryImageTag.FullImageNameWithTag);

        // Add push flag if Edge ACR is configured and pushToRegistry is true
        // Note: Due to docker-container driver limitations with multi-arch images,
        // we use type=registry for now. The independent push optimization
        // will be implemented in a future iteration.
        buildArgs.Add("--output");
        buildArgs.Add("type=registry");

        ExecuteDockerCommand(buildArgs, "unified image build");

        Log.Information("Unified Docker image built successfully: {Tag}", registryImageTag);
    }

    void PrepareBuildContext(string version, bool isMultiPlatform,
        List<string>? platforms = null)
    {
        Log.Information("Preparing Docker build context...");
        Log.Debug("Multi-platform build: {IsMultiPlatform}", isMultiPlatform);
        if (platforms is { Count: > 0 })
        {
            Log.Debug("Target platforms: {Platforms}", string.Join(", ", platforms));
        }

        // Create build context directory
        DockerBuildContext.CreateOrCleanDirectory();
        GeneratedAppDockerfile.Parent.CreateDirectory();

        // Copy entrypoint script
        File.Copy(DockerEntrypointScript, DockerBuildContext / "docker-entrypoint.sh", true);

        // Extract and copy lib directory for each platform BEFORE generating Dockerfile
        // This ensures directories exist when Dockerfile references them
        // For multi-arch builds, extract to platform-specific subdirectories
        // For single platform, extract directly to lib/
        if (isMultiPlatform)
        {
            // Multi-arch build: extract each platform to its own subdirectory
            Log.Information("Multi-platform build: extracting to platform-specific lib directories");
            if (platforms == null || platforms.Count == 0)
            {
                Log.Warning("isMultiPlatform is true but platforms list is empty, defaulting to both platforms");
                platforms = new List<string> { "linux/amd64", "linux/arm64" };
            }

            foreach (var platform in platforms)
            {
                var platformName = GetPlatformName(platform);
                var platformDir = DockerBuildContext / $"lib-{platformName}";
                ExtractZipFiles(platformDir, platform);
                Log.Information("Extracted platform {Platform} to {Directory}", platform, platformDir);
            }

            // Log directory structure for debugging
            Log.Debug("Build context directories:");
            foreach (var dir in DockerBuildContext.GetDirectories())
            {
                Log.Debug("  - {Dir}", dir.Name);
            }
        }
        else
        {
            // Single platform build: extract directly to lib/
            Log.Information("Single-platform build: extracting to default lib directory");
            var extractedDir = DockerBuildContext / "lib";
            var platform = platforms?.FirstOrDefault() ?? "linux/amd64";
            ExtractZipFiles(extractedDir, platform);
        }

        // Generate unified Dockerfile from template AFTER directories are created
        // This ensures Dockerfile can reference the lib directories that were just created
        GenerateAppDockerfile(version, isMultiPlatform: isMultiPlatform);

        Log.Information("Docker build context prepared at {Path}", DockerBuildContext);
    }

    /// Gets the platform directory name from Docker platform identifier
    /// <param name="platform">Docker platform (e.g., "linux/amd64")</param>
    /// <returns>Platform name for directory (e.g., "amd64")</returns>
    string GetPlatformName(string platform)
    {
        return platform switch
        {
            "linux/amd64" => "amd64",
            "linux/arm64" => "arm64",
            _ => platform.Replace("linux/", "").Replace("/", "-")
        };
    }

    void GenerateAppDockerfile(string version, AbsolutePath? dockerfilePath = null,
        bool isMultiPlatform = false)
    {
        var outputPath = dockerfilePath ?? GeneratedAppDockerfile;
        Log.Information("Generating unified Dockerfile for version {Version}...", version);
        Log.Debug("Output path: {Path}", outputPath);
        Log.Debug("Multi-platform build: {IsMultiPlatform}", isMultiPlatform);

        var template = File.ReadAllText(DockerAppTemplateDockerfile);
        var dockerfile = template
            .Replace("{version}", version)
            .Replace("{build_date}", BuildDate);

        File.WriteAllText(outputPath, dockerfile);
        Log.Information("Dockerfile generated at: {Path}", outputPath);
    }

    /// Maps Docker platform identifier to ZIP file naming convention.
    /// <param name="platform">Docker platform (e.g., "linux/amd64", "linux/arm64")</param>
    /// <returns>Platform suffix for ZIP file matching (e.g., "linux-x64", "linux-arm64")</returns>
    string GetPlatformZipSuffix(string platform)
    {
        return platform switch
        {
            "linux/amd64" => "linux-x64",
            "linux/arm64" => "linux-arm64",
            _ => platform.Replace("linux/", "")
        };
    }

    /// Extracts downloaded zip files to the target directory
    /// <param name="targetDirectory">Target directory for extraction</param>
    /// <param name="platform">Docker platform to filter by (e.g., "linux/amd64")</param>
    void ExtractZipFiles(AbsolutePath targetDirectory, string? platform = null)
    {
        targetDirectory.CreateOrCleanDirectory();

        IEnumerable<AbsolutePath> zipFilesToExtract;

        if (!string.IsNullOrEmpty(platform))
        {
            // Filter ZIP files by platform
            var platformSuffix = GetPlatformZipSuffix(platform);
            zipFilesToExtract = DownloadedZipFiles
                .Where(zip => zip.ToString().Contains(platformSuffix));

            if (!zipFilesToExtract.Any())
            {
                Log.Warning("No ZIP files found for platform: {Platform}", platform);
                return;
            }
        }
        else
        {
            // Extract all ZIP files (backward compatibility)
            zipFilesToExtract = DownloadedZipFiles;
        }

        foreach (var zipFile in zipFilesToExtract)
        {
            Log.Information("Extracting {FileName} to {Directory}...",
                Path.GetFileName(zipFile), targetDirectory);

            // Use a temporary directory for extraction to avoid nested directory structure
            // The zip package structure is: lib/appsettings.yml, lib/PCode.Web.dll, etc.
            // We want these files to be extracted directly to targetDirectory
            var tempDir = targetDirectory.Parent / $"{targetDirectory.Name}_temp";

            try
            {
                // Clean temp directory if it exists
                if (tempDir.DirectoryExists())
                {
                    tempDir.DeleteDirectory();
                }

                tempDir.CreateDirectory();

                // Extract to temp directory with full structure
                var processInfo = new ProcessStartInfo
                {
                    FileName = "unzip",
                    ArgumentList =
                    {
                        "-q",
                        "-o",
                        zipFile.ToString(),
                        "-d",
                        tempDir.ToString()
                    },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Log.Debug("Executing unzip command: unzip -q -o {ZipFile} -d {TempDir}",
                    zipFile.ToString(), tempDir.ToString());

                using var process = Process.Start(processInfo);
                if (process == null)
                {
                    throw new Exception($"Failed to start unzip process for {zipFile}");
                }

                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Log.Error("Failed to extract {FileName}. Exit code: {ExitCode}, Error: {Error}",
                        Path.GetFileName(zipFile), process.ExitCode,
                        string.IsNullOrEmpty(stderr) ? "Unknown error" : stderr);
                    throw new Exception($"Failed to extract {zipFile}: {stderr}");
                }

                // Move contents of lib/ directory to target directory
                var extractedLibDir = tempDir / "lib";
                if (extractedLibDir.DirectoryExists())
                {
                    foreach (var file in extractedLibDir.GetFiles())
                    {
                        var targetFile = targetDirectory / file.Name;
                        // Overwrite existing files (platform-specific config files may have same names)
                        if (targetFile.FileExists())
                        {
                            File.Delete(targetFile);
                        }

                        file.Move(targetFile);
                    }

                    foreach (var dir in extractedLibDir.GetDirectories())
                    {
                        var targetDir = targetDirectory / dir.Name;
                        // Merge directories: copy contents if directory exists
                        if (targetDir.DirectoryExists())
                        {
                            // Move all files from subdirectory
                            foreach (var subFile in dir.GetFiles())
                            {
                                var targetSubFile = targetDir / subFile.Name;
                                if (targetSubFile.FileExists())
                                {
                                    File.Delete(targetSubFile);
                                }

                                subFile.Move(targetSubFile);
                            }

                            // Recursively merge subdirectories
                            foreach (var subDir in dir.GetDirectories())
                            {
                                var targetSubDir = targetDir / subDir.Name;
                                if (targetSubDir.DirectoryExists())
                                {
                                    foreach (var subSubFile in subDir.GetFiles())
                                    {
                                        var targetSubSubFile = targetSubDir / subSubFile.Name;
                                        if (targetSubSubFile.FileExists())
                                        {
                                            File.Delete(targetSubSubFile);
                                        }

                                        subSubFile.Move(targetSubSubFile);
                                    }

                                    foreach (var subSubDir in subDir.GetDirectories())
                                    {
                                        subSubDir.MoveToDirectory(targetSubDir);
                                    }

                                    subDir.DeleteDirectory();
                                }
                                else
                                {
                                    subDir.MoveToDirectory(targetDir);
                                }
                            }

                            dir.DeleteDirectory();
                        }
                        else
                        {
                            dir.MoveToDirectory(targetDirectory);
                        }
                    }

                    Log.Information("Successfully extracted {FileName} to {Directory}",
                        Path.GetFileName(zipFile), targetDirectory);
                }
                else
                {
                    Log.Warning("Expected lib/ directory not found in zip package {FileName}", zipFile);
                }
            }
            finally
            {
                // Clean up temp directory
                if (tempDir.DirectoryExists())
                {
                    try
                    {
                        tempDir.DeleteDirectory();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to clean up temp directory {TempDir}", tempDir);
                    }
                }
            }
        }
    }
}
