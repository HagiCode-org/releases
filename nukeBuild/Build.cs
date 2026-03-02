using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Serilog;

[GitHubActions(
    "version-monitor",
    GitHubActionsImage.UbuntuLatest,
    OnPushTags = new[] { "*.*.*" },
    ImportSecrets = new[]
    {
        nameof(AzureBlobSasUrl),
        nameof(FeishuWebhookUrl),
        nameof(GitHubToken),
        nameof(AzureAcrUsername),
        nameof(AzureAcrPassword),
        nameof(AzureAcrRegistry)
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
    // All parameter definitions have been moved to Build.Parameters.cs
    // to improve code organization and maintainability.
    // ==========================================================================

    #region Dependencies

    Target DetermineBuildConfig => _ => _
        .Description("Determines build version and platform from Git context or environment")
        .Executes(DetermineBuildConfigExecute);

    #endregion

    /// Push to Aliyun ACR target - builds and pushes Docker images to Aliyun Container Registry only

    Target PushToAliyunAcr => _ => _
        .Description("Builds and pushes Docker images to Aliyun Container Registry only")
        .DependsOn(DetermineBuildConfig)
        .DependsOn(Download)
        .Executes(() =>
        {
            var version = EffectiveBuildVersion;
            var dockerImageInfo = new DockerImageInfo(AliyunAcrRegistry, "hagicode", "hagicode");
            var platforms = new List<string> { "linux/amd64", "linux/arm64" };
            LoginToAliyunAcr();
            BuildApplicationImage(dockerImageInfo,
                version,
                platforms);
            RetagImages(dockerImageInfo, version);
            Log.Information("Starting Aliyun ACR push for version: {Version}", version);
            Log.Information("Aliyun ACR push completed successfully");
        });

    /// Push to Azure ACR target - builds and pushes Docker images to Azure Container Registry only
    Target PushToAzureAcr => _ => _
        .Description("Builds and pushes Docker images to Azure Container Registry only")
        .DependsOn(DetermineBuildConfig)
        .DependsOn(Download)
        .Executes(() =>
        {
            var version = EffectiveBuildVersion;
            var dockerImageInfo =
                new DockerImageInfo(EffectiveAzureAcrRegistry, "", "hagicode");

            var platforms = new List<string> { "linux/amd64", "linux/arm64" };
            LoginToAzureAcr();
            BuildApplicationImage(dockerImageInfo,
                version,
                platforms,
                pushToRegistry: true);
            RetagImages(dockerImageInfo, version);
            Log.Information("Starting Azure ACR push for version: {Version}", version);
            Log.Information("Azure ACR push completed successfully");
        });


    /// Push to DockerHub target - builds and pushes Docker images to DockerHub only

    Target PushToDockerHub => _ => _
        .Description("Builds and pushes Docker images to DockerHub only")
        .DependsOn(DetermineBuildConfig)
        .DependsOn(Download)
        .Executes(() =>
        {
            var version = EffectiveBuildVersion;
            var dockerImageInfo = new DockerImageInfo("docker.io", "newbe36524", "hagicode");

            var platforms = new List<string> { "linux/amd64", "linux/arm64" };
            LoginToDockerHub();
            BuildApplicationImage(dockerImageInfo,
                version,
                platforms,
                pushToRegistry: false);
            RetagImages(dockerImageInfo, version);
            Log.Information("Starting DockerHub push for version: {Version}", version);
            Log.Information("DockerHub push completed successfully");
        });


    Target PushToAllRegistries => _ => _
        .Description("Builds and pushes Docker images to all configured registries (Azure ACR, Aliyun ACR, DockerHub)")
        .DependsOn(PushToAliyunAcr)
        .DependsOn(PushToAzureAcr)
        .DependsOn(PushToDockerHub);
}