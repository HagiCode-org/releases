using System.IO;
using Xunit;

namespace NukeBuild.Tests;

/// <summary>
/// Integration-oriented coverage for the unified Docker release contract.
/// These tests assert that the Docker template, entrypoint, and release docs
/// stay aligned on shipped CLI availability and runtime override behavior.
/// </summary>
public class DockerBuildIntegrationTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    private static string ResolveRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Hagicode.ReleaseTasks.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repos/hagicode-release root from test output directory.");
    }

    private static string ReadRepoFile(string relativePath)
    {
        var fullPath = Path.Combine(RepoRoot, relativePath);
        return File.ReadAllText(fullPath);
    }

    [Fact]
    public void Dockerfile_ShouldInstall_OnlyRetainedBakedCliTools()
    {
        var dockerfile = ReadRepoFile("docker_deployment/Dockerfile.template");

        Assert.Contains("PINNED_CLAUDE_CODE_CLI_VERSION=2.1.71", dockerfile);
        Assert.Contains("PINNED_OPENSPEC_CLI_VERSION=1.2.0", dockerfile);
        Assert.Contains("PINNED_OPENCODE_CLI_VERSION=1.2.25", dockerfile);
        Assert.Contains("PINNED_CODEX_CLI_VERSION=0.112.0", dockerfile);
        Assert.DoesNotContain("PINNED_UIPRO_CLI_VERSION", dockerfile);
        Assert.DoesNotContain("PINNED_COPILOT_CLI_VERSION", dockerfile);
        Assert.DoesNotContain("PINNED_CODEBUDDY_CLI_VERSION", dockerfile);
        Assert.DoesNotContain("PINNED_QODER_CLI_VERSION", dockerfile);

        Assert.Contains("npm install -g @anthropic-ai/claude-code@2.1.71", dockerfile);
        Assert.Contains("/home/hagicode/.npm-global/bin/claude --version", dockerfile);
        Assert.Contains("npm install -g @fission-ai/openspec@1.2.0", dockerfile);
        Assert.Contains("/home/hagicode/.npm-global/bin/openspec --version", dockerfile);
        Assert.Contains("npm install -g opencode-ai@1.2.25", dockerfile);
        Assert.Contains("/home/hagicode/.npm-global/bin/opencode --version", dockerfile);
        Assert.Contains("npm install -g @openai/codex@0.112.0", dockerfile);
        Assert.Contains("/home/hagicode/.npm-global/bin/codex --version", dockerfile);
        Assert.DoesNotContain("uipro-cli@", dockerfile);
        Assert.DoesNotContain("@github/copilot@", dockerfile);
        Assert.DoesNotContain("@tencent-ai/codebuddy-code@", dockerfile);
        Assert.DoesNotContain("@qoder-ai/qodercli@", dockerfile);
        Assert.DoesNotContain("/home/hagicode/.npm-global/bin/uipro --version", dockerfile);
        Assert.DoesNotContain("/home/hagicode/.npm-global/bin/copilot --version", dockerfile);
        Assert.DoesNotContain("/home/hagicode/.npm-global/bin/codebuddy --version", dockerfile);
        Assert.DoesNotContain("/home/hagicode/.npm-global/bin/qodercli --version", dockerfile);
    }

    [Fact]
    public void Entrypoint_ShouldExpose_OnlyRetainedCliOverrides()
    {
        var entrypoint = ReadRepoFile("docker_deployment/docker-entrypoint.sh");

        Assert.Contains("CLAUDE_CODE_CLI_VERSION", entrypoint);
        Assert.Contains("OPENSPEC_CLI_VERSION", entrypoint);
        Assert.Contains("PINNED_OPENCODE_CLI_VERSION", entrypoint);
        Assert.Contains("CODEX_CLI_VERSION", entrypoint);
        Assert.Contains("QODER_PERSONAL_ACCESS_TOKEN (masked)", entrypoint);
        Assert.Contains("OpenCode CLI using pinned image version", entrypoint);
        Assert.DoesNotContain("${OPENCODE_CLI_VERSION", entrypoint);
        Assert.DoesNotContain("UIPRO_CLI_VERSION", entrypoint);
        Assert.DoesNotContain("COPILOT_CLI_VERSION", entrypoint);
        Assert.DoesNotContain("CODEBUDDY_CLI_VERSION", entrypoint);
        Assert.DoesNotContain("QODER_CLI_VERSION", entrypoint);
        Assert.DoesNotContain("\"uipro-cli\"", entrypoint);
        Assert.DoesNotContain("\"@github/copilot\"", entrypoint);
        Assert.DoesNotContain("\"@tencent-ai/codebuddy-code\"", entrypoint);
        Assert.DoesNotContain("\"@qoder-ai/qodercli\"", entrypoint);
    }

    [Fact]
    public void ReleaseDocs_ShouldDescribe_StreamlinedContainerContract()
    {
        var readme = ReadRepoFile("README.md");
        var environmentVariables = ReadRepoFile("ENVIRONMENT_VARIABLES.md");
        var agents = ReadRepoFile("AGENTS.md");

        Assert.Contains("`claude`", readme);
        Assert.Contains("`openspec`", readme);
        Assert.Contains("`opencode`", readme);
        Assert.Contains("`codex`", readme);
        Assert.Contains("UI-managed install path", readme);
        Assert.Contains("`uipro` is no longer part of the image", readme);

        Assert.Contains("CODEBUDDY_API_KEY", environmentVariables);
        Assert.Contains("CODEBUDDY_INTERNET_ENVIRONMENT", environmentVariables);
        Assert.Contains("QODER_PERSONAL_ACCESS_TOKEN", environmentVariables);
        Assert.Contains("qodercli --acp", environmentVariables);
        Assert.Contains("There is intentionally no `OPENCODE_CLI_VERSION`", environmentVariables);
        Assert.Contains("UI-managed installs: `copilot`, `codebuddy`, and `qodercli`", environmentVariables);
        Assert.Contains("`uipro` is no longer shipped because skill management replaces its runtime role", environmentVariables);
        Assert.DoesNotContain("UIPRO_CLI_VERSION", environmentVariables);
        Assert.DoesNotContain("COPILOT_CLI_VERSION", environmentVariables);
        Assert.DoesNotContain("CODEBUDDY_CLI_VERSION", environmentVariables);
        Assert.DoesNotContain("QODER_CLI_VERSION", environmentVariables);

        Assert.Contains("The Docker base image pre-installs only the retained container baseline", agents);
        Assert.Contains("`qodercli` now follows the UI-managed install path", agents);
        Assert.Contains("QODER_PERSONAL_ACCESS_TOKEN", agents);
        Assert.Contains("skill management replaces its previous runtime role", agents);
    }

    [Fact]
    public void CodexRuntimeVariables_ShouldUseDeterministicPrecedence()
    {
        var entrypoint = ReadRepoFile("docker_deployment/docker-entrypoint.sh");

        Assert.Contains("CODEX_BASE_URL > OPENAI_BASE_URL", entrypoint);
        Assert.Contains("CODEX_API_KEY > OPENAI_API_KEY", entrypoint);
        Assert.Contains("API key source: $CODEX_API_SOURCE (masked)", entrypoint);
    }

    [Fact]
    public void ReleaseWorkflow_ShouldUseDispatchAndExplicitManualVersionOnly()
    {
        var workflow = ReadRepoFile(".github/workflows/github-release-workflow.yml");

        Assert.Contains("repository_dispatch:", workflow);
        Assert.Contains("types: [version-monitor-release]", workflow);
        Assert.Contains("workflow_dispatch:", workflow);
        Assert.Contains("description: 'Version to release (e.g., 1.2.3)'", workflow);
        Assert.Contains("required: true", workflow);
        Assert.DoesNotContain("\npush:\n", workflow);
        Assert.DoesNotContain("refs/tags/", workflow);
        Assert.Contains("VERSION=\"${{ github.event.client_payload.version }}\"", workflow);
        Assert.Contains("VERSION=\"${{ inputs.version }}\"", workflow);
    }

    [Theory]
    [InlineData(".github/workflows/docker-build-aliyun-acr.yml", "version-monitor-docker-aliyun")]
    [InlineData(".github/workflows/docker-build-azure-acr.yml", "version-monitor-docker-azure")]
    [InlineData(".github/workflows/docker-build-dockerhub.yml", "version-monitor-docker-dockerhub")]
    public void DockerWorkflows_ShouldRemoveTagPushAndResolveVersionConsistently(
        string workflowPath,
        string dispatchType)
    {
        var workflow = ReadRepoFile(workflowPath);

        Assert.Contains("workflow_dispatch:", workflow);
        Assert.Contains("repository_dispatch:", workflow);
        Assert.Contains($"types: [{dispatchType}]", workflow);
        Assert.Contains("description: 'Version to build (e.g., 1.2.3)'", workflow);
        Assert.Contains("required: true", workflow);
        Assert.Contains("VERSION=\"${{ github.event.client_payload.version }}\"", workflow);
        Assert.Contains("VERSION=\"${{ inputs.version }}\"", workflow);
        Assert.Contains("echo \"version=$VERSION\" >> \"$GITHUB_OUTPUT\"", workflow);
        Assert.Contains("echo \"- **Version**: ${VERSION}\" >> $GITHUB_STEP_SUMMARY", workflow);
        Assert.DoesNotContain("\npush:\n", workflow);
        Assert.DoesNotContain("refs/tags/", workflow);
    }

    [Fact]
    public void VersionMonitorTarget_ShouldExposeSelectedAndDeferredVersionsAndDispatchOnlyOneVersion()
    {
        var versionMonitorTarget = ReadRepoFile("nukeBuild/Build.Targets.VersionMonitor.cs");

        Assert.Contains("SetGitHubOutput(\"selected_version\", selectedVersion);", versionMonitorTarget);
        Assert.Contains("SetGitHubOutput(\"deferred_versions\", string.Join(\", \", deferredVersions));", versionMonitorTarget);
        Assert.Contains("LogVersionSelectionSummary(releasePlan);", versionMonitorTarget);
        Assert.Contains("Dry-run mode enabled - only selected version {SelectedVersion} would be dispatched; deferred versions remain untouched", versionMonitorTarget);
        Assert.Contains("TriggerReleaseForVersion(selectedVersion);", versionMonitorTarget);
        Assert.Contains("TriggerDockerDispatch(selectedVersion);", versionMonitorTarget);
        Assert.DoesNotContain("foreach (var version in newVersions)", versionMonitorTarget);
    }
}
