using System.IO;
using System.Linq;
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

        Assert.Contains("FROM debian:bookworm-slim AS base", dockerfile);
        Assert.DoesNotContain("FROM node:24 AS base", dockerfile);
        Assert.Contains("NVM_DIR=/usr/local/nvm", dockerfile);
        Assert.Contains("NVM_SYMLINK_CURRENT=true", dockerfile);
        Assert.Contains("NODE_VERSION=24", dockerfile);
        Assert.Contains("nvm install \"${NODE_VERSION}\"", dockerfile);
        Assert.Contains("ln -sf \"${NODE_BIN_DIR}/node\" /usr/local/bin/node", dockerfile);
        Assert.Contains("ENV PATH=\"/home/hagicode/.npm-global/bin:/usr/local/nvm/current/bin:${DOTNET_ROOT}:${PATH}\"", dockerfile);
        Assert.Contains("npm config set prefix '/home/hagicode/.npm-global'", dockerfile);
        Assert.Contains("PINNED_CLAUDE_CODE_CLI_VERSION=2.1.71", dockerfile);
        Assert.Contains("PINNED_OPENSPEC_CLI_VERSION=1.2.0", dockerfile);
        Assert.Contains("PINNED_OPENCODE_CLI_VERSION=1.2.25", dockerfile);
        Assert.Contains("PINNED_CODEX_CLI_VERSION=0.112.0", dockerfile);
        Assert.DoesNotContain("PINNED_UIPRO_CLI_VERSION", dockerfile);
        Assert.DoesNotContain("PINNED_COPILOT_CLI_VERSION", dockerfile);
        Assert.DoesNotContain("PINNED_CODEBUDDY_CLI_VERSION", dockerfile);
        Assert.DoesNotContain("PINNED_QODER_CLI_VERSION", dockerfile);
        Assert.Contains("openssh-client", dockerfile);
        Assert.Contains("Install runtime dependencies needed by the app, non-root startup, and SSH/Git access.", dockerfile);
        Assert.Contains("Install OpenSpec CLI as the retained workflow tool in the image baseline", dockerfile);
        Assert.Contains("Install the retained primary agent CLI baseline", dockerfile);
        Assert.Contains("Provider CLIs outside claude/opencode/codex stay UI-managed at runtime", dockerfile);

        Assert.Contains("npm install -g \"@anthropic-ai/claude-code@${PINNED_CLAUDE_CODE_CLI_VERSION}\"", dockerfile);
        Assert.Contains("claude --version", dockerfile);
        Assert.Contains("npm install -g \"@fission-ai/openspec@${PINNED_OPENSPEC_CLI_VERSION}\"", dockerfile);
        Assert.Contains("openspec --version", dockerfile);
        Assert.Contains("npm install -g \"opencode-ai@${PINNED_OPENCODE_CLI_VERSION}\"", dockerfile);
        Assert.Contains("opencode --version", dockerfile);
        Assert.Contains("npm install -g \"@openai/codex@${PINNED_CODEX_CLI_VERSION}\"", dockerfile);
        Assert.Contains("codex --version", dockerfile);
        Assert.DoesNotContain("uipro-cli@", dockerfile);
        Assert.DoesNotContain("@github/copilot@", dockerfile);
        Assert.DoesNotContain("@tencent-ai/codebuddy-code@", dockerfile);
        Assert.DoesNotContain("@qoder-ai/qodercli@", dockerfile);
        Assert.DoesNotContain("uipro --version", dockerfile);
        Assert.DoesNotContain("copilot --version", dockerfile);
        Assert.DoesNotContain("codebuddy --version", dockerfile);
        Assert.DoesNotContain("qodercli --version", dockerfile);
    }

    [Fact]
    public void Entrypoint_ShouldExpose_OnlyRetainedCliOverrides()
    {
        var entrypoint = ReadRepoFile("docker_deployment/docker-entrypoint.sh");

        Assert.Contains("run_as_hagicode()", entrypoint);
        Assert.Contains("exec_as_hagicode()", entrypoint);
        Assert.Contains("ensure_hagicode_runtime_paths()", entrypoint);
        Assert.Contains("groupmod -o -g \"$PGID\" \"$HAGICODE_GROUP\"", entrypoint);
        Assert.Contains("usermod -o -u \"$PUID\" -g \"$PGID\" -d \"$HAGICODE_HOME\" \"$HAGICODE_USER\"", entrypoint);
        Assert.Contains("run_as_hagicode npm install -g", entrypoint);
        Assert.Contains("run_as_hagicode \"${command_name}\" --version >/dev/null", entrypoint);
        Assert.Contains("exec_as_hagicode dotnet PCode.Web.dll", entrypoint);
        Assert.DoesNotContain("deluser hagicode", entrypoint);
        Assert.DoesNotContain("gosu node", entrypoint);
        Assert.DoesNotContain("/home/node", entrypoint);
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
    public void Entrypoint_ShouldBootstrap_SshFromMountedFilesDeterministically()
    {
        var entrypoint = ReadRepoFile("docker_deployment/docker-entrypoint.sh");

        Assert.Contains("configure_ssh_private_key_if_needed()", entrypoint);
        Assert.Contains("validate_readable_file_path", entrypoint);
        Assert.Contains("validate_strict_host_key_checking", entrypoint);
        Assert.Contains("SSH bootstrap skipped: SSH_PRIVATE_KEY_PATH is not set.", entrypoint);
        Assert.Contains("points to a missing path:", entrypoint);
        Assert.Contains("validate_readable_file_path \"SSH_PRIVATE_KEY_PATH\" \"$private_key_path\"", entrypoint);
        Assert.Contains("SSH_KNOWN_HOSTS_PATH", entrypoint);
        Assert.Contains("SSH_STRICT_HOST_KEY_CHECKING_DEFAULT=\"accept-new\"", entrypoint);
        Assert.Contains("mkdir -p \"$HAGICODE_SSH_DIR\"", entrypoint);
        Assert.Contains("cp \"$private_key_path\" \"$HAGICODE_IMPORTED_SSH_KEY\"", entrypoint);
        Assert.Contains("cp \"$known_hosts_path\" \"$HAGICODE_IMPORTED_KNOWN_HOSTS\"", entrypoint);
        Assert.Contains(": > \"$HAGICODE_IMPORTED_KNOWN_HOSTS\"", entrypoint);
        Assert.Contains("IdentityFile $HAGICODE_IMPORTED_SSH_KEY", entrypoint);
        Assert.Contains("IdentitiesOnly yes", entrypoint);
        Assert.Contains("UserKnownHostsFile $HAGICODE_IMPORTED_KNOWN_HOSTS", entrypoint);
        Assert.Contains("StrictHostKeyChecking $strict_host_key_checking", entrypoint);
        Assert.Contains("chmod 700 \"$HAGICODE_SSH_DIR\"", entrypoint);
        Assert.Contains("chmod 600 \"$HAGICODE_IMPORTED_SSH_KEY\"", entrypoint);
        Assert.Contains("chmod 644 \"$HAGICODE_IMPORTED_KNOWN_HOSTS\" \"$HAGICODE_SSH_CONFIG_FILE\"", entrypoint);
        Assert.Contains("export GIT_SSH_COMMAND=\"ssh -F ${HAGICODE_SSH_CONFIG_FILE}\"", entrypoint);
        Assert.Contains("configure_ssh_private_key_if_needed", entrypoint);
    }

    [Fact]
    public void ReleaseDocs_ShouldDescribe_StreamlinedContainerContract()
    {
        var readme = ReadRepoFile("README.md");
        var readmeCn = ReadRepoFile("README_cn.md");
        var environmentVariables = ReadRepoFile("ENVIRONMENT_VARIABLES.md");
        var agents = ReadRepoFile("AGENTS.md");

        Assert.Contains("clean `debian:bookworm-slim` base", readme);
        Assert.Contains("Node.js 24 is installed through an image-managed NVM layout", readme);
        Assert.Contains("Only `hagicode` is supported as the non-root runtime user", readme);
        Assert.Contains("`claude`", readme);
        Assert.Contains("`opencode`", readme);
        Assert.Contains("`codex`", readme);
        Assert.Contains("`openspec` remains in the image as the retained workflow tool", readme);
        Assert.Contains("UI-managed install path", readme);
        Assert.Contains("`uipro` is no longer part of the image", readme);
        Assert.Contains("`openssh-client`", readme);
        Assert.Contains("`SSH_PRIVATE_KEY_PATH`", readme);
        Assert.Contains("`SSH_KNOWN_HOSTS_PATH`", readme);
        Assert.Contains("`SSH_STRICT_HOST_KEY_CHECKING`", readme);
        Assert.Contains("default of `accept-new`", readme);
        Assert.Contains("skip SSH bootstrap entirely", readme);
        Assert.Contains("`GIT_SSH_COMMAND`", readme);

        Assert.Contains("`debian:bookworm-slim`", readmeCn);
        Assert.Contains("Node.js 24", readmeCn);
        Assert.Contains("唯一受支持的非 root 运行用户是 `hagicode`", readmeCn);
        Assert.Contains("主要 agent CLI 基线", readmeCn);
        Assert.Contains("`openspec` 仍作为镜像保留的工作流工具存在", readmeCn);
        Assert.Contains("`SSH_PRIVATE_KEY_PATH`", readmeCn);
        Assert.Contains("`SSH_KNOWN_HOSTS_PATH`", readmeCn);
        Assert.Contains("`SSH_STRICT_HOST_KEY_CHECKING`", readmeCn);
        Assert.Contains("默认的 `accept-new`", readmeCn);
        Assert.Contains("`GIT_SSH_COMMAND`", readmeCn);

        Assert.Contains("CODEBUDDY_API_KEY", environmentVariables);
        Assert.Contains("CODEBUDDY_INTERNET_ENVIRONMENT", environmentVariables);
        Assert.Contains("QODER_PERSONAL_ACCESS_TOKEN", environmentVariables);
        Assert.Contains("qodercli --acp", environmentVariables);
        Assert.Contains("There is intentionally no `OPENCODE_CLI_VERSION`", environmentVariables);
        Assert.Contains("UI-managed installs: `copilot`, `codebuddy`, and `qodercli`", environmentVariables);
        Assert.Contains("`uipro` is no longer shipped because skill management replaces its runtime role", environmentVariables);
        Assert.Contains("Supported non-root runtime user: `hagicode` only", environmentVariables);
        Assert.Contains("the image does not rely on the upstream `node` user or `/home/node`", environmentVariables);
        Assert.Contains("Shared PATH exposure comes from `/usr/local/nvm/current/bin` and `/home/hagicode/.npm-global/bin`", environmentVariables);
        Assert.Contains("Primary baked agent CLI baseline: `claude`, `opencode`, and `codex`", environmentVariables);
        Assert.Contains("Retained workflow tool: `openspec`", environmentVariables);
        Assert.Contains("SSH_PRIVATE_KEY_PATH", environmentVariables);
        Assert.Contains("SSH_KNOWN_HOSTS_PATH", environmentVariables);
        Assert.Contains("SSH_STRICT_HOST_KEY_CHECKING", environmentVariables);
        Assert.Contains("skip SSH bootstrap", environmentVariables);
        Assert.Contains("`accept-new`", environmentVariables);
        Assert.Contains("GIT_SSH_COMMAND", environmentVariables);
        Assert.DoesNotContain("UIPRO_CLI_VERSION", environmentVariables);
        Assert.DoesNotContain("COPILOT_CLI_VERSION", environmentVariables);
        Assert.DoesNotContain("CODEBUDDY_CLI_VERSION", environmentVariables);
        Assert.DoesNotContain("QODER_CLI_VERSION", environmentVariables);

        Assert.Contains("The unified release image keeps only the primary agent CLI baseline baked into the container", agents);
        Assert.Contains("Workflow tool: `openspec`", agents);
        Assert.Contains("Runtime SSH client: `openssh-client`", agents);
        Assert.Contains("Base stages start from `debian:bookworm-slim`", agents);
        Assert.Contains("Node.js 24 is installed through a shared NVM layout", agents);
        Assert.Contains("`hagicode` is the only supported non-root runtime user", agents);
        Assert.Contains("`qodercli` now follows the UI-managed install path", agents);
        Assert.Contains("QODER_PERSONAL_ACCESS_TOKEN", agents);
        Assert.Contains("skill management replaces its previous runtime role", agents);
        Assert.Contains("SSH_PRIVATE_KEY_PATH", agents);
        Assert.Contains("SSH_KNOWN_HOSTS_PATH", agents);
        Assert.Contains("SSH_STRICT_HOST_KEY_CHECKING", agents);
        Assert.Contains("defaults to `accept-new`", agents);
        Assert.Contains("exports `GIT_SSH_COMMAND`", agents);
    }

    [Fact]
    public void ReleaseDocs_ShouldKeep_SshBootstrapAndPrimaryCliBaselineAligned()
    {
        var readme = ReadRepoFile("README.md");
        var readmeCn = ReadRepoFile("README_cn.md");
        var environmentVariables = ReadRepoFile("ENVIRONMENT_VARIABLES.md");
        var agents = ReadRepoFile("AGENTS.md");

        var sharedEnglishTokens = new[]
        {
            "SSH_PRIVATE_KEY_PATH",
            "SSH_KNOWN_HOSTS_PATH",
            "SSH_STRICT_HOST_KEY_CHECKING",
            "accept-new",
            "GIT_SSH_COMMAND",
            "`claude`",
            "`opencode`",
            "`codex`",
            "`openspec`",
        };

        foreach (var token in sharedEnglishTokens)
        {
            Assert.Contains(token, readme);
            Assert.Contains(token, environmentVariables);
            Assert.Contains(token, agents);
        }

        Assert.Contains("主要 agent CLI 基线", readmeCn);
        Assert.Contains("`SSH_PRIVATE_KEY_PATH`", readmeCn);
        Assert.Contains("`SSH_KNOWN_HOSTS_PATH`", readmeCn);
        Assert.Contains("`SSH_STRICT_HOST_KEY_CHECKING`", readmeCn);
        Assert.Contains("`openspec`", readmeCn);
        Assert.Equal(0, new[] { readme, environmentVariables, agents }.Count(doc => doc.Contains("`copilot`") && doc.Contains("default built-in agent CLI")));
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
