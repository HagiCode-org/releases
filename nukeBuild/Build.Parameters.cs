using Nuke.Common;
using Nuke.Common.IO;

internal partial class Build
{
    // ==========================================================================
    // Parameters
    // ==========================================================================

    [Parameter("Azure Blob Storage SAS URL for downloading packages")] [Secret]
    readonly string AzureBlobSasUrl = string.Empty;

    [Parameter("GitHub token for release creation")] [Secret]
    readonly string GitHubToken = string.Empty;

    [Parameter("GitHub repository (e.g., owner/repo)")]
    readonly string GitHubRepository = string.Empty;

    [Parameter("Dry run mode (do not trigger actual releases)")]
    readonly string DryRun = string.Empty;

    [Parameter("List only mode (do not trigger releases, just output versions)")]
    readonly string ListOnly = string.Empty;

    [Parameter("Feishu webhook URL for notifications")] [Secret]
    readonly string FeishuWebhookUrl = string.Empty;

    [Parameter("Azure ACR username")] [Secret]
    readonly string AzureAcrUsername = string.Empty;

    [Parameter("Azure ACR password")] [Secret]
    readonly string AzureAcrPassword = string.Empty;

    [Parameter("Azure ACR registry endpoint")] [Secret]
    readonly string AzureAcrRegistry = string.Empty;

    [Parameter("Azure ACR namespace")] [Secret]
    readonly string AzureAcrNamespace = string.Empty;

    [Parameter("Aliyun ACR username")] [Secret]
    readonly string AliyunAcrUsername = string.Empty;

    [Parameter("Aliyun ACR password")] [Secret]
    readonly string AliyunAcrPassword = string.Empty;

    [Parameter("Aliyun ACR registry endpoint")] [Secret]
    readonly string AliyunAcrRegistry = string.Empty;

    [Parameter("Aliyun ACR namespace")] [Secret]
    readonly string AliyunAcrNamespace = string.Empty;

    [Parameter("DockerHub username")] [Secret]
    readonly string DockerHubUsername = string.Empty;

    [Parameter("DockerHub access token")] [Secret]
    readonly string DockerHubToken = string.Empty;

    [Parameter("DockerHub namespace")] [Secret]
    readonly string DockerHubNamespace = string.Empty;

    [Parameter("Output directory for downloaded/extracted files")]
    AbsolutePath OutputDirectory = RootDirectory / "output";

    /// Gets the effective DryRun value (from environment variable or parameter)
    bool EffectiveDryRun => Environment.GetEnvironmentVariable("NUGEX_DryRun")?.ToLower() == "true" ||
                            DryRun.ToLower() == "true";

    /// Gets the effective ListOnly value (from environment variable or parameter)
    bool EffectiveListOnly => Environment.GetEnvironmentVariable("NUGEX_ListOnly")?.ToLower() == "true" ||
                              ListOnly.ToLower() == "true";

    /// Gets the effective ReleaseVersion value (from environment variable or parameter)
    string EffectiveReleaseVersion => Environment.GetEnvironmentVariable("NUGEX_ReleaseVersion") ?? ReleaseVersion;

}