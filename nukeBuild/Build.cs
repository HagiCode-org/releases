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
    "version-monitor",
    GitHubActionsImage.UbuntuLatest,
    OnPushTags = new[] { "v*.*.*" },
    ImportSecrets = new[]
    {
        nameof(AzureBlobSasUrl),
        nameof(FeishuWebhookUrl),
        nameof(GitHubToken)
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

    [Parameter("GitHub token for release creation")]
    [Secret]
    readonly string GitHubToken = string.Empty;

    [Parameter("GitHub repository (e.g., owner/repo)")]
    readonly string GitHubRepository = string.Empty;

    [Parameter("Dry run mode (do not trigger actual releases)")]
    [Secret]
    readonly string DryRun = string.Empty;

    [Parameter("List only mode (do not trigger releases, just output versions)")]
    [Secret]
    readonly string ListOnly = string.Empty;

    /// <summary>
    /// Gets the effective DryRun value (from parameter or environment variable)
    /// </summary>
    bool EffectiveDryRun => !string.IsNullOrEmpty(DryRun)
        ? (Environment.GetEnvironmentVariable("NUGEX_DryRun")?.ToLower() == "true")
        : DryRun.ToLower() == "true";

    /// <summary>
    /// Gets the effective ListOnly value (from parameter or environment variable)
    /// </summary>
    bool EffectiveListOnly => !string.IsNullOrEmpty(ListOnly)
        ? (Environment.GetEnvironmentVariable("NUGEX_ListOnly")?.ToLower() == "true")
        : ListOnly.ToLower() == "true";

    [Parameter("Feishu webhook URL for notifications")]
    [Secret]
    readonly string FeishuWebhookUrl = string.Empty;

    [Parameter("Output directory for downloaded/extracted files")]
    AbsolutePath OutputDirectory = RootDirectory / "output";

    /// <summary>
    /// Gets the effective ReleaseVersion value (from parameter or environment variable)
    /// </summary>
    string EffectiveReleaseVersion => !string.IsNullOrEmpty(ReleaseVersion)
        ? Environment.GetEnvironmentVariable("NUGEX_ReleaseVersion") ?? string.Empty
        : ReleaseVersion;

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
    // - Build.Targets.VersionMonitor.cs : Monitors Azure Blob Storage for new versions
    // - Build.Targets.GitHub.cs       : Creates GitHub releases
    //
    // Each partial class file contains both the Target declaration and its
    // execution logic in separate methods for better organization.
    //
    // ==========================================================================
}
