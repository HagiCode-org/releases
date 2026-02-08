using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities.Collections;
using Nuke.Common.CI.GitHubActions;
using Serilog;
using System;

[GitHubActions(
    "hagicode-server-publish",
    GitHubActionsImage.UbuntuLatest,
    OnPushTags = new[] { "v*.*.*" },
    InvokedTargets = new[] { nameof(Release) },
    ImportSecrets = new[]
    {
        nameof(AzureBlobSasUrl),
        nameof(DockerUsername),
        nameof(DockerPassword),
        nameof(AliyunAcrUsername),
        nameof(AliyunAcrPassword),
        nameof(AzureAcrUsername),
        nameof(AzureAcrPassword)
    },
    EnableGitHubToken = true,
    AutoGenerate = false)]
[GitHubActions(
    "docker-build",
    GitHubActionsImage.UbuntuLatest,
    OnPushBranches = new[] { "main" },
    InvokedTargets = new[] { nameof(DockerBuild) },
    ImportSecrets = new[]
    {
        nameof(AzureBlobSasUrl),
    },
    EnableGitHubToken = false,
    AutoGenerate = false)]
partial class Build : Nuke.Common.NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(static x => (Target?)null);

    // ==========================================================================
    // Parameters
    // ==========================================================================

    [Parameter("Azure Blob Storage SAS URL for downloading packages")]
    [Secret]
    readonly string AzureBlobSasUrl = string.Empty;

    [Parameter("Docker registry (e.g., docker.io/newbe36524)")]
    readonly string DockerRegistry = "docker.io/newbe36524";

    [Parameter("Docker image name")]
    readonly string DockerImageName = "hagicode";

    [Parameter("Version from git tag (e.g., v1.0.0)")]
    readonly string Version = "0.1.0-beta.1";

    [Parameter("Docker Hub username")]
    [Secret]
    readonly string DockerUsername = string.Empty;

    [Parameter("Docker Hub password/token")]
    [Secret]
    readonly string DockerPassword = string.Empty;

    [Parameter("Output directory for downloaded/extracted files")]
    AbsolutePath OutputDirectory = RootDirectory / "output";

    [Parameter("GitHub token for release creation")]
    [Secret]
    readonly string GitHubToken = string.Empty;

    [Parameter("GitHub repository (e.g., owner/repo)")]
    readonly string GitHubRepository = string.Empty;

    [Parameter("Aliyun ACR username")]
    [Secret]
    readonly string AliyunAcrUsername = string.Empty;

    [Parameter("Aliyun ACR password/token")]
    [Secret]
    readonly string AliyunAcrPassword = string.Empty;

    [Parameter("Aliyun ACR registry (default: registry.cn-hangzhou.aliyuncs.com)")]
    readonly string AliyunAcrRegistry = "registry.cn-hangzhou.aliyuncs.com/hagicode";

    [Parameter("Azure ACR username")]
    [Secret]
    readonly string AzureAcrUsername = string.Empty;

    [Parameter("Azure ACR password/token")]
    [Secret]
    readonly string AzureAcrPassword = string.Empty;

    [Parameter("Azure ACR registry (default: hagicode.azurecr.io)")]
    readonly string AzureAcrRegistry = "hagicode.azurecr.io";

    [Parameter("Dry run mode (do not trigger actual releases)")]
    readonly bool DryRun = false;

    // ==========================================================================
    // Dependencies
    // ==========================================================================

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            OutputDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            Log.Information("Restore completed");
        });

    // ==========================================================================
    // Build Targets (declarations only - implementations in partial classes)
    // ==========================================================================
    //
    // Target declarations are split across multiple partial class files:
    // - Build.Targets.Download.cs   : Downloads packages from Azure Blob Storage
    // - Build.Targets.Extract.cs    : Extracts Linux package for Docker build
    // - Build.Targets.Docker.cs     : Builds, logs in, and pushes Docker images
    // - Build.Targets.GitHub.cs     : Creates GitHub releases
    //
    // Each partial class file contains both the Target declaration and its
    // execution logic in separate methods for better organization.
    //
    // ==========================================================================
    // Release Target (main entry point)
    // ==========================================================================

    Target Release => _ => _
        .DependsOn(DockerPushAll, GitHubRelease)
        .Executes(() =>
        {
            Log.Information("Release workflow completed successfully");
        });
}
