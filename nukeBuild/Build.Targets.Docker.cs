using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.Docker;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Docker targets - builds, logs in, and pushes Docker images
/// </summary>
partial class Build
{
    Target DockerBuild => _ => _
        .DependsOn(Extract)
        .Requires(() => DockerImageName)
        .Produces(OutputDirectory / "docker-image-built.txt")
        .Executes(DockerBuildExecute);

    Target DockerLogin => _ => _
        .Requires(() => DockerUsername)
        .Requires(() => DockerPassword)
        .Executes(DockerLoginExecute);

    Target DockerPush => _ => _
        .DependsOn(DockerBuild)
        .Requires(() => DockerUsername)
        .Requires(() => DockerPassword)
        .Executes(DockerPushExecute);

    void DockerBuildExecute()
    {
        Log.Information("Building Docker images (base + application)");

        var appTags = GetAppDockerTags();
        Log.Information("Application tags: {Tags}", string.Join(", ", appTags));

        // Step 1: Build base image
        BuildDockerBaseImage();

        // Step 2: Generate application Dockerfile from template
        var dockerfileContent = GenerateAppDockerfile();
        PrepareDockerBuildContext(dockerfileContent);

        // Step 3: Build application image
        BuildDockerApplicationImage(appTags);

        // Mark that docker image was built
        System.IO.File.WriteAllText(
            OutputDirectory / "docker-image-built.txt",
            $"Built at {DateTime.UtcNow:O}\nBase: {BaseDockerTag}\nApp: {string.Join(", ", appTags)}");
    }

    void BuildDockerBaseImage()
    {
        Log.Information("Building base image: {BaseTag}", BaseDockerTag);
        Log.Information("  Context: {Context}", DockerDeploymentDirectory);
        Log.Information("  Dockerfile: {DockerfileBase}", DockerDeploymentDirectory / "Dockerfile.base");

        DockerTasks.DockerBuild(s => s
            .SetPath(DockerDeploymentDirectory)
            .SetFile(DockerDeploymentDirectory / "Dockerfile.base")
            .SetTag(BaseDockerTag)
            .EnableRm());

        Log.Information("✓ Base image built successfully: {BaseTag}", BaseDockerTag);
    }

    string GenerateAppDockerfile()
    {
        var templateContent = System.IO.File.ReadAllText(DockerDeploymentDirectory / "Dockerfile.app.template");
        return templateContent
            .Replace("{version}", FullVersion)
            .Replace("{build_date}", BuildDate)
            .Replace("{base_image_name}", BaseImageName);
    }

    void PrepareDockerBuildContext(string dockerfileContent)
    {
        DockerBuildContext.CreateDirectory();

        // Write Dockerfile
        var appDockerfilePath = DockerBuildContext / "Dockerfile";
        System.IO.File.WriteAllText(appDockerfilePath, dockerfileContent);

        // Copy entrypoint script
        System.IO.File.Copy(
            DockerDeploymentDirectory / "docker-entrypoint.sh",
            DockerBuildContext / "docker-entrypoint.sh",
            true);

        // Copy lib/ directory (framework-dependent assemblies)
        var libDir = ExtractedDirectory / "lib";
        if (!System.IO.Directory.Exists(libDir))
        {
            throw new Exception($"Required lib/ directory not found in extracted package at {libDir}");
        }
        CopyDirectoryRecursive(libDir, DockerBuildContext);

        // Copy any .sh scripts from package
        foreach (var scriptFile in ExtractedDirectory.GlobFiles("*.sh"))
        {
            System.IO.File.Copy(scriptFile, DockerBuildContext / scriptFile.Name, true);
        }

        Log.Information("Docker build context prepared at: {Context}", DockerBuildContext);
    }

    void BuildDockerApplicationImage(List<string> tags)
    {
        Log.Information("Building application image");
        Log.Information("  Context: {Context}", DockerBuildContext);
        Log.Information("  Dockerfile: {Dockerfile}", DockerBuildContext / "Dockerfile");

        DockerTasks.DockerBuild(s => s
            .SetPath(DockerBuildContext)
            .SetFile(DockerBuildContext / "Dockerfile")
            .SetTag(tags)
            .SetBuildArg($"VERSION={FullVersion}")
            .EnableRm());

        Log.Information("✓ Application image built successfully");
    }

    void DockerLoginExecute()
    {
        Log.Information("Logging in to Docker Hub");

        DockerTasks.DockerLogin(s => s
            .SetServer("docker.io")
            .SetUsername(DockerUsername)
            .SetPassword(DockerPassword));

        Log.Information("Docker login successful");
    }

    void DockerPushExecute()
    {
        Log.Information("Pushing Docker images");

        var tags = GetAppDockerTags();

        foreach (var tag in tags)
        {
            Log.Information("Pushing {Tag}", tag);
            DockerTasks.DockerPush(s => s.SetName(tag));
        }

        Log.Information("All Docker images pushed successfully");
    }

    List<string> GetAppDockerTags()
    {
        var tags = new List<string>
        {
            $"{BaseImageName}:{FullVersion}",
            $"{BaseImageName}:{MinorVersion}",
            $"{BaseImageName}:{MajorVersion}"
        };

        if (!IsPreRelease)
        {
            tags.Add($"{BaseImageName}:latest");
        }

        return tags;
    }
}
