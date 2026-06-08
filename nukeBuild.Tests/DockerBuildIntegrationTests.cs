using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace NukeBuild.Tests;

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

    private static JsonElement ReadJsonFile(string relativePath)
    {
        return JsonDocument.Parse(ReadRepoFile(relativePath)).RootElement.Clone();
    }

    private static (int ExitCode, string StdOut, string StdErr) RunBashScript(
        string scriptContent,
        params (string Key, string? Value)[] environment)
    {
        var scriptPath = Path.GetTempFileName();
        File.WriteAllText(scriptPath, scriptContent);

        try
        {
            var startInfo = new ProcessStartInfo("bash", scriptPath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            foreach (var (key, value) in environment)
            {
                if (value is null)
                {
                    startInfo.Environment.Remove(key);
                }
                else
                {
                    startInfo.Environment[key] = value;
                }
            }

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start bash process.");
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, stdout, stderr);
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    [Fact]
    public void Dockerfile_ShouldInstall_OnlyRetainedBakedCliTools()
    {
        var dockerfile = ReadRepoFile("docker_deployment/Dockerfile.template");

        Assert.Contains("FROM debian:bookworm-slim AS base", dockerfile);
        Assert.Contains("NVM_DIR=/usr/local/nvm", dockerfile);
        Assert.Contains("NODE_VERSION=22", dockerfile);
        Assert.Contains("HAGISCRIPT_NPM_SYNC_MANIFEST=/app/bootstrap/hagiscript-sync-manifest.json", dockerfile);
        Assert.Contains("HAGISCRIPT_MANAGED_RUNTIME=/home/hagicode/.hagiscript/node-runtime", dockerfile);
        Assert.Contains("PINNED_HAGISCRIPT_VERSION=0.3.4", dockerfile);
        Assert.Contains("npm config set prefix '/home/hagicode/.npm-global'", dockerfile);
        Assert.Contains("npm install -g \"@hagicode/hagiscript@${PINNED_HAGISCRIPT_VERSION}\"", dockerfile);
        Assert.Contains("hagiscript npm-sync", dockerfile);
        Assert.Contains("claude --version", dockerfile);
        Assert.Contains("openspec --version", dockerfile);
        Assert.Contains("skills --version", dockerfile);
        Assert.Contains("opencode --version", dockerfile);
        Assert.Contains("codex --version", dockerfile);
        Assert.Contains("RUN mkdir -p /app/data /app/saves && \\", dockerfile);

        Assert.DoesNotContain("omniroute --help", dockerfile);
        Assert.DoesNotContain("code-server --version", dockerfile);
        Assert.DoesNotContain("pm2 --version", dockerfile);
        Assert.DoesNotContain("pm2-runtime", dockerfile);
        Assert.DoesNotContain("ecosystem.config.cjs", dockerfile);
        Assert.DoesNotContain("omniroute-bootstrap.mjs", dockerfile);
        Assert.DoesNotContain("wait-for-ready.sh", dockerfile);
        Assert.DoesNotContain("/app/data/omniroute", dockerfile);
        Assert.DoesNotContain("PINNED_CODE_SERVER_VERSION", dockerfile);
        Assert.DoesNotContain("PINNED_PM2_VERSION", dockerfile);
    }

    [Fact]
    public void HagiscriptSyncManifest_ShouldRepresent_RetainedBakedToolBoundary()
    {
        var manifest = ReadJsonFile("docker_deployment/hagiscript-sync-manifest.json");
        var tools = manifest.GetProperty("tools");
        var selectedIds = tools.GetProperty("selectedOptionalAgentCliIds")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        var customAgentClis = tools.GetProperty("customAgentClis").EnumerateArray().ToArray();

        Assert.True(tools.GetProperty("optionalAgentCliSyncEnabled").GetBoolean());
        Assert.Equal(new[] { "claude-code", "fission-openspec", "opencode", "codex" }, selectedIds);
        Assert.Empty(customAgentClis);
        Assert.DoesNotContain("omniroute", selectedIds);
        Assert.DoesNotContain("code-server", selectedIds);
        Assert.DoesNotContain("pm2", selectedIds);
    }

    [Fact]
    public void Dockerfile_ShouldSanitize_NvmBootstrapEnvironment()
    {
        var dockerfile = ReadRepoFile("docker_deployment/Dockerfile.template");

        var unsetPrefixIndex = dockerfile.IndexOf("unset NPM_CONFIG_PREFIX &&", StringComparison.Ordinal);
        var nvmInstallIndex = dockerfile.IndexOf("nvm install \"${NODE_VERSION}\"", StringComparison.Ordinal);

        Assert.True(unsetPrefixIndex >= 0, "Docker template should clear NPM_CONFIG_PREFIX before invoking NVM.");
        Assert.True(nvmInstallIndex >= 0, "Docker template should install Node.js through nvm.");
        Assert.True(unsetPrefixIndex < nvmInstallIndex, "Docker template should clear NPM_CONFIG_PREFIX before running nvm install.");
    }

    [Fact]
    public void Entrypoint_ShouldStart_AppDirectlyWithoutOmnirouteOrCodeServer()
    {
        var entrypoint = ReadRepoFile("docker_deployment/docker-entrypoint.sh");
        var mainStart = entrypoint.IndexOf("main() {", StringComparison.Ordinal);
        var mainSection = entrypoint.Substring(mainStart);
        var verifyIndex = mainSection.IndexOf("verify_hagiscript_synced_toolchain", StringComparison.Ordinal);
        var resolveIndex = mainSection.IndexOf("resolve_application_command", StringComparison.Ordinal);
        var configureClaudeIndex = mainSection.IndexOf("configure_claude_runtime", StringComparison.Ordinal);
        var execIndex = mainSection.IndexOf("exec_as_hagicode \"$HAGICODE_APP_COMMAND\" \"$HAGICODE_APP_ARGUMENTS\"", StringComparison.Ordinal);

        Assert.Contains("run_as_hagicode()", entrypoint);
        Assert.Contains("exec_as_hagicode()", entrypoint);
        Assert.Contains("ensure_hagicode_runtime_paths()", entrypoint);
        Assert.Contains("resolve_application_command()", entrypoint);
        Assert.Contains("configure_claude_runtime()", entrypoint);
        Assert.Contains("verify_hagiscript_synced_toolchain()", entrypoint);
        Assert.Contains("HAGISCRIPT_MANAGED_RUNTIME", entrypoint);
        Assert.Contains("QODER_PERSONAL_ACCESS_TOKEN (masked)", entrypoint);
        Assert.Contains("✓ Starting HagiCode application:", entrypoint);

        Assert.DoesNotContain("configure_code_server_runtime_if_needed", entrypoint);
        Assert.DoesNotContain("validate_accept_eula", entrypoint);
        Assert.DoesNotContain("normalize_omniroute_runtime_contract", entrypoint);
        Assert.DoesNotContain("capture_upstream_provider_inputs", entrypoint);
        Assert.DoesNotContain("export_local_omniroute_routing", entrypoint);
        Assert.DoesNotContain("wait_for_omniroute_health", entrypoint);
        Assert.DoesNotContain("run_omniroute_bootstrap", entrypoint);
        Assert.DoesNotContain("start_pm2_runtime", entrypoint);
        Assert.DoesNotContain("code-server", entrypoint);
        Assert.DoesNotContain("omniroute", entrypoint);
        Assert.DoesNotContain("pm2-runtime", entrypoint);

        Assert.True(mainStart >= 0, "Entrypoint should declare a main function.");
        Assert.True(verifyIndex >= 0, "Entrypoint should verify the retained HagiScript-synced toolchain.");
        Assert.True(resolveIndex > verifyIndex, "Entrypoint should resolve the app command after verifying the toolchain.");
        Assert.True(configureClaudeIndex > resolveIndex, "Entrypoint should configure Claude after the app command is known.");
        Assert.True(execIndex > configureClaudeIndex, "Entrypoint should start the app directly after runtime setup.");
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
    }

    [Fact]
    public void Entrypoint_ShouldPrepare_OnlyDualWritableRoots_ForMountedVolumes()
    {
        var entrypointPath = Path.Combine(RepoRoot, "docker_deployment", "docker-entrypoint.sh");
        var result = RunBashScript(
            $$"""
            #!/usr/bin/env bash
            set -euo pipefail
            source "{{entrypointPath}}"

            temp_root="$(mktemp -d)"
            trap 'rm -rf "$temp_root"' EXIT

            HAGICODE_USER="$(id -un)"
            HAGICODE_GROUP="$(id -gn)"
            HAGICODE_HOME="$temp_root/home"
            HAGICODE_CLAUDE_DIR="${HAGICODE_HOME}/.claude"
            HAGICODE_CLAUDE_STATE_FILE="${HAGICODE_HOME}/.claude.json"
            HAGICODE_NPM_PREFIX="${HAGICODE_HOME}/.npm-global"
            HAGISCRIPT_MANAGED_RUNTIME="${HAGICODE_HOME}/.hagiscript/node-runtime"
            HAGICODE_SSH_DIR="${HAGICODE_HOME}/.ssh"
            HAGICODE_IMPORTED_SSH_KEY="${HAGICODE_SSH_DIR}/imported_key"
            HAGICODE_IMPORTED_KNOWN_HOSTS="${HAGICODE_SSH_DIR}/known_hosts"
            HAGICODE_SSH_CONFIG_FILE="${HAGICODE_SSH_DIR}/config"
            HAGICODE_APP_DIR="$temp_root/app"
            HAGICODE_APP_DATA_DIR="${HAGICODE_APP_DIR}/data"
            HAGICODE_APP_SAVES_DIR="${HAGICODE_APP_DIR}/saves"

            chown() { :; }

            ensure_hagicode_runtime_paths

            test -d "$HAGICODE_APP_DATA_DIR"
            test -d "$HAGICODE_APP_SAVES_DIR"
            test ! -e "$HAGICODE_APP_DATA_DIR/omniroute"

            touch "$HAGICODE_APP_DATA_DIR/.write-check"
            touch "$HAGICODE_APP_SAVES_DIR/.write-check"
            """);

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void ReleaseDocs_ShouldDescribe_DirectRuntimeContract()
    {
        var readme = ReadRepoFile("README.md");
        var readmeCn = ReadRepoFile("README_cn.md");
        var environmentVariables = ReadRepoFile("ENVIRONMENT_VARIABLES.md");
        var agentGuidance = ReadRepoFile("AGENTS.md");

        Assert.Contains("clean `debian:bookworm-slim` base", readme);
        Assert.Contains("Node.js 22 is installed through an image-managed NVM layout", readme);
        Assert.Contains("`hagiscript npm-sync --managed-runtime /home/hagicode/.hagiscript/node-runtime --manifest /app/bootstrap/hagiscript-sync-manifest.json`", readme);
        Assert.Contains("`claude`", readme);
        Assert.Contains("`opencode`", readme);
        Assert.Contains("`codex`", readme);
        Assert.Contains("`openspec` remains in the image", readme);
        Assert.Contains("`skills` remains bundled", readme);
        Assert.Contains("no longer treated as bundled release-image support", readme);
        Assert.Contains("starts the app directly", readme);
        Assert.DoesNotContain("wait-for-ready.sh", readme);
        Assert.DoesNotContain("ACCEPT_EULA", readme);
        Assert.DoesNotContain("CODE_SERVER_PASSWORD", readme);

        Assert.Contains("`debian:bookworm-slim`", readmeCn);
        Assert.Contains("Node.js 22", readmeCn);
        Assert.Contains("`claude`", readmeCn);
        Assert.Contains("`opencode`", readmeCn);
        Assert.Contains("`codex`", readmeCn);
        Assert.Contains("`openspec` 仍作为镜像保留的工作流工具存在", readmeCn);
        Assert.Contains("`skills` 也作为镜像保留的技能管理 CLI 默认内置", readmeCn);
        Assert.Contains("不再视为 release 镜像的内置支持", readmeCn);
        Assert.Contains("直接启动应用", readmeCn);
        Assert.DoesNotContain("wait-for-ready.sh", readmeCn);
        Assert.DoesNotContain("ACCEPT_EULA", readmeCn);
        Assert.DoesNotContain("CODE_SERVER_PASSWORD", readmeCn);

        Assert.Contains("Removed from the baked release-image support contract", environmentVariables);
        Assert.Contains("### Direct Runtime Startup Contract", environmentVariables);
        Assert.Contains("hagiscript`, `claude`, `openspec`, `skills`, `opencode`, and `codex`", environmentVariables);
        Assert.Contains("Both persistence roots are still required", environmentVariables);
        Assert.DoesNotContain("### Omniroute Unified Provider Bootstrap", environmentVariables);
        Assert.DoesNotContain("### Code Server Deployment Contract", environmentVariables);
        Assert.DoesNotContain("ACCEPT_EULA", environmentVariables);
        Assert.DoesNotContain("CODE_SERVER_PASSWORD", environmentVariables);
        Assert.DoesNotContain("OMNIROUTE_ENABLE_BOOTSTRAP", environmentVariables);

        Assert.Contains("retained bundled skill-management tool", agentGuidance);
        Assert.Contains("retained baked baseline: `claude`, `openspec`, `skills`, `opencode`, and `codex`", agentGuidance);
        Assert.DoesNotContain("omniroute`, `pm2`, and `code-server`", agentGuidance);
    }

    [Fact]
    public void LocalDockerComposeWorkflow_ShouldShip_WithoutOmnirouteOrCodeServerRuntimeAssumptions()
    {
        var compose = ReadRepoFile("docker-compose.local.yml");
        var envTemplate = ReadRepoFile(".env.local.example");
        var envSecretsTemplate = ReadRepoFile(".env.secrets.local.example");
        var buildScript = ReadRepoFile("scripts/docker-local-build.sh");
        var upScript = ReadRepoFile("scripts/docker-local-up.sh");
        var testScript = ReadRepoFile("scripts/docker-local-test.sh");
        var commonScript = ReadRepoFile("scripts/docker-local-common.sh");
        var readme = ReadRepoFile("README.md");
        var readmeCn = ReadRepoFile("README_cn.md");
        var environmentVariables = ReadRepoFile("ENVIRONMENT_VARIABLES.md");

        Assert.Contains("name: hagicode-local", compose);
        Assert.Contains("image: ${HAGICODE_LOCAL_IMAGE:-hagicode-local:dev}", compose);
        Assert.Contains(".local/hagicode/data:/app/data", compose);
        Assert.Contains(".local/hagicode/saves:/app/saves", compose);
        Assert.DoesNotContain("ACCEPT_EULA", compose);
        Assert.DoesNotContain("OMNIROUTE_ENABLE_BOOTSTRAP", compose);
        Assert.DoesNotContain("VsCodeServer__", compose);
        Assert.DoesNotContain("CODE_SERVER_", compose);
        Assert.DoesNotContain("HAGICODE_CODE_SERVER_BIND", compose);

        Assert.Contains("HAGICODE_RELEASE_VERSION=", envTemplate);
        Assert.Contains("HAGICODE_DOCKER_PLATFORM=", envTemplate);
        Assert.Contains("AZURE_BLOB_SAS_URL=", envTemplate);
        Assert.DoesNotContain("ACCEPT_EULA=", envTemplate);
        Assert.DoesNotContain("OMNIROUTE_ENABLE_BOOTSTRAP=", envTemplate);
        Assert.DoesNotContain("CODE_SERVER_PASSWORD=", envTemplate);
        Assert.DoesNotContain("VSCODE_SERVER_", envTemplate);
        Assert.Contains("AZURE_BLOB_SAS_URL=", envSecretsTemplate);

        Assert.Contains("DockerPrepareLocalContext", buildScript);
        Assert.Contains("--secrets-file", buildScript);
        Assert.Contains("docker buildx build", buildScript);
        Assert.Contains("--secrets-file", upScript);
        Assert.Contains("run_compose up -d", upScript);
        Assert.Contains("HTTP health check passed", testScript);
        Assert.Contains("hagiscript --version", testScript);
        Assert.Contains("openspec --version", testScript);
        Assert.Contains("skills --version", testScript);
        Assert.Contains("opencode --version", testScript);
        Assert.Contains("codex --version", testScript);
        Assert.DoesNotContain("omniroute --help", testScript);
        Assert.DoesNotContain("pm2-runtime", testScript);
        Assert.DoesNotContain("code-server --version", testScript);
        Assert.Contains("DEFAULT_SECRETS_FILE", commonScript);
        Assert.Contains("run_compose()", commonScript);

        Assert.Contains("docker-compose.local.yml", readme);
        Assert.Contains("docker-local-build.sh", readme);
        Assert.Contains("docker-local-test.sh", readme);
        Assert.Contains("docker-compose.local.yml", readmeCn);
        Assert.Contains("docker-local-build.sh", readmeCn);
        Assert.Contains("docker-local-test.sh", readmeCn);
        Assert.Contains("Local Docker Compose Workflow", environmentVariables);
    }

    [Fact]
    public void DockerfileTemplateVersionPlaceholders_ShouldNotCollide_WithDockerVariableSyntax()
    {
        var dockerfileTemplate = ReadRepoFile("docker_deployment/Dockerfile.template");
        var appImageTarget = ReadRepoFile("nukeBuild/Build.Targets.Docker.AppImage.cs");

        Assert.Contains("LABEL version=\"__HAGICODE_VERSION__\"", dockerfileTemplate);
        Assert.Contains("LABEL build.date=\"__HAGICODE_BUILD_DATE__\"", dockerfileTemplate);
        Assert.DoesNotContain("LABEL version=\"${version}\"", dockerfileTemplate);
        Assert.DoesNotContain("LABEL build.date=\"${build_date}\"", dockerfileTemplate);
        Assert.Contains(".Replace(\"__HAGICODE_VERSION__\", version)", appImageTarget);
        Assert.Contains(".Replace(\"__HAGICODE_BUILD_DATE__\", BuildDate)", appImageTarget);
    }

    [Fact]
    public void ReleaseDocs_ShouldDescribe_BothPersistenceRootsAsRequired()
    {
        var readme = ReadRepoFile("README.md");
        var readmeCn = ReadRepoFile("README_cn.md");
        var environmentVariables = ReadRepoFile("ENVIRONMENT_VARIABLES.md");

        const string requiredRootsEnglish = "Both persistence roots are required in production deployments: `hagicode_data:/app/data` keeps system-scoped assets writable, and `hagicode_saves:/app/saves` keeps save-scoped runtime state writable";
        const string requiredRootsChinese = "生产部署必须同时持久化这两个根目录：`hagicode_data:/app/data` 负责保持 system-scoped 资源可写，`hagicode_saves:/app/saves` 负责保持 save-scoped 运行时状态可写";

        Assert.Contains(requiredRootsEnglish, readme);
        Assert.Contains(requiredRootsChinese, readmeCn);
        Assert.Contains("Both persistence roots are still required", environmentVariables);
        Assert.Contains("`/app/saves/save0/...`", readme);
        Assert.Contains("`/app/saves/save0/...`", readmeCn);
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
