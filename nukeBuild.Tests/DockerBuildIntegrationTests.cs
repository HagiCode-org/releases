using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text.Json;
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
        Assert.DoesNotContain("FROM node:24 AS base", dockerfile);
        Assert.Contains("NVM_DIR=/usr/local/nvm", dockerfile);
        Assert.Contains("NVM_SYMLINK_CURRENT=true", dockerfile);
        Assert.Contains("NODE_VERSION=22", dockerfile);
        Assert.DoesNotContain("NODE_VERSION=24", dockerfile);
        Assert.Contains("nvm install \"${NODE_VERSION}\"", dockerfile);
        Assert.Contains("ln -sf \"${NODE_BIN_DIR}/node\" /usr/local/bin/node", dockerfile);
        Assert.Contains("ENV PATH=\"/home/hagicode/.npm-global/bin:/home/hagicode/.hagiscript/node-runtime/bin:/usr/local/nvm/current/bin:${DOTNET_ROOT}:${PATH}\"", dockerfile);
        Assert.Contains("npm config set prefix '/home/hagicode/.npm-global'", dockerfile);
        Assert.Contains("HAGISCRIPT_NPM_SYNC_MANIFEST=/app/bootstrap/hagiscript-sync-manifest.json", dockerfile);
        Assert.Contains("HAGISCRIPT_MANAGED_RUNTIME=/home/hagicode/.hagiscript/node-runtime", dockerfile);
        Assert.Contains("PINNED_HAGISCRIPT_VERSION=0.1.0", dockerfile);
        Assert.DoesNotContain("PINNED_CLAUDE_CODE_CLI_VERSION", dockerfile);
        Assert.DoesNotContain("PINNED_OPENSPEC_CLI_VERSION", dockerfile);
        Assert.DoesNotContain("PINNED_SKILLS_CLI_VERSION", dockerfile);
        Assert.DoesNotContain("PINNED_OPENCODE_CLI_VERSION", dockerfile);
        Assert.DoesNotContain("PINNED_CODEX_CLI_VERSION", dockerfile);
        Assert.DoesNotContain("PINNED_OMNIROUTE_VERSION", dockerfile);
        Assert.DoesNotContain("PINNED_PM2_VERSION", dockerfile);
        Assert.DoesNotContain("PINNED_CODE_SERVER_VERSION", dockerfile);
        Assert.DoesNotContain("PINNED_UIPRO_CLI_VERSION", dockerfile);
        Assert.DoesNotContain("PINNED_COPILOT_CLI_VERSION", dockerfile);
        Assert.DoesNotContain("PINNED_CODEBUDDY_CLI_VERSION", dockerfile);
        Assert.DoesNotContain("PINNED_QODER_CLI_VERSION", dockerfile);
        Assert.Contains("openssh-client", dockerfile);
        Assert.Contains("Install runtime dependencies needed by the app, non-root startup, and SSH/Git access.", dockerfile);
        Assert.Contains("Copy the release-owned HagiScript sync input before running the build-time tool sync.", dockerfile);
        Assert.Contains("Install pinned HagiScript first, then delegate the retained baked toolchain to npm-sync.", dockerfile);

        Assert.Contains("COPY --chown=hagicode:hagicode hagiscript-sync-manifest.json /app/bootstrap/hagiscript-sync-manifest.json", dockerfile);
        Assert.Contains("npm install -g \"@hagicode/hagiscript@${PINNED_HAGISCRIPT_VERSION}\"", dockerfile);
        Assert.Contains("hagiscript npm-sync", dockerfile);
        Assert.Contains("--managed-runtime \"${HAGISCRIPT_MANAGED_RUNTIME}\"", dockerfile);
        Assert.Contains("--manifest \"${HAGISCRIPT_NPM_SYNC_MANIFEST}\"", dockerfile);
        Assert.Contains("hagiscript --version", dockerfile);
        Assert.Contains("claude --version", dockerfile);
        Assert.Contains("openspec --version", dockerfile);
        Assert.Contains("skills --version", dockerfile);
        Assert.Contains("opencode --version", dockerfile);
        Assert.Contains("codex --version", dockerfile);
        Assert.Contains("omniroute --help >/dev/null", dockerfile);
        Assert.Contains("pm2 --version", dockerfile);
        Assert.Contains("command -v pm2-runtime >/dev/null", dockerfile);
        Assert.Contains("COPY --chown=hagicode:hagicode ecosystem.config.cjs /app/bootstrap/ecosystem.config.cjs", dockerfile);
        Assert.Contains("COPY --chown=hagicode:hagicode omniroute-bootstrap.mjs /app/bootstrap/omniroute-bootstrap.mjs", dockerfile);
        Assert.Contains("COPY --chown=hagicode:hagicode wait-for-ready.sh /app/bootstrap/wait-for-ready.sh", dockerfile);
        Assert.Contains("chmod +x /app/bootstrap/wait-for-ready.sh", dockerfile);
        Assert.Contains("RUN mkdir -p /app/data /app/data/omniroute /app/data/omniroute/pm2 /app/data/omniroute/runtime /app/saves && \\", dockerfile);
        Assert.Contains("code-server --version", dockerfile);
        Assert.Contains("RUN mkdir -p /app/data /app/data/omniroute /app/data/omniroute/pm2 /app/data/omniroute/runtime /app/saves && \\", dockerfile);
        Assert.DoesNotContain("/app/saves/save0", dockerfile);
        Assert.DoesNotContain("npm install -g \"code-server@${PINNED_CODE_SERVER_VERSION}\"", dockerfile);
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
        Assert.Single(customAgentClis);
        Assert.Equal("pm2", customAgentClis[0].GetProperty("packageName").GetString());
        Assert.Equal("6.0.14", customAgentClis[0].GetProperty("version").GetString());
        Assert.Equal("6.0.14", customAgentClis[0].GetProperty("target").GetString());
        Assert.DoesNotContain("qoder", selectedIds);
    }

    [Fact]
    public void Dockerfile_ShouldSanitize_NvmBootstrapEnvironment()
    {
        var dockerfile = ReadRepoFile("docker_deployment/Dockerfile.template");

        var unsetPrefixIndex = dockerfile.IndexOf("unset NPM_CONFIG_PREFIX &&");
        var nvmInstallIndex = dockerfile.IndexOf("nvm install \"${NODE_VERSION}\"");

        Assert.True(unsetPrefixIndex >= 0, "Docker template should clear NPM_CONFIG_PREFIX before invoking NVM.");
        Assert.True(nvmInstallIndex >= 0, "Docker template should install Node.js through nvm.");
        Assert.True(unsetPrefixIndex < nvmInstallIndex, "Docker template should clear NPM_CONFIG_PREFIX before running nvm install.");
        Assert.Contains("npm config set prefix '/home/hagicode/.npm-global'", dockerfile);
        Assert.Contains("ENV PATH=\"/home/hagicode/.npm-global/bin:/home/hagicode/.hagiscript/node-runtime/bin:/usr/local/nvm/current/bin:${DOTNET_ROOT}:${PATH}\"", dockerfile);
    }

    [Fact]
    public void Entrypoint_ShouldBootstrap_OmnirouteBeforeReleasingHagiCodeRuntime()
    {
        var entrypoint = ReadRepoFile("docker_deployment/docker-entrypoint.sh");
        var mainStart = entrypoint.IndexOf("main() {", StringComparison.Ordinal);
        var mainSection = entrypoint.Substring(mainStart);
        var captureIndex = mainSection.IndexOf("capture_upstream_provider_inputs", StringComparison.Ordinal);
        var normalizeIndex = mainSection.IndexOf("normalize_omniroute_runtime_contract", StringComparison.Ordinal);
        var exportIndex = mainSection.IndexOf("export_local_omniroute_routing", StringComparison.Ordinal);
        var configureClaudeIndex = mainSection.IndexOf("configure_claude_runtime", StringComparison.Ordinal);
        var pm2Index = mainSection.IndexOf("start_pm2_runtime", StringComparison.Ordinal);
        var bootstrapIndex = mainSection.IndexOf("run_omniroute_bootstrap", StringComparison.Ordinal);

        Assert.Contains("run_as_hagicode()", entrypoint);
        Assert.Contains("exec_as_hagicode()", entrypoint);
        Assert.Contains("ensure_hagicode_runtime_paths()", entrypoint);
        Assert.Contains("groupmod -o -g \"$PGID\" \"$HAGICODE_GROUP\"", entrypoint);
        Assert.Contains("usermod -o -u \"$PUID\" -g \"$PGID\" -d \"$HAGICODE_HOME\" \"$HAGICODE_USER\"", entrypoint);
        Assert.Contains("verify_hagiscript_synced_toolchain()", entrypoint);
        Assert.Contains("HAGISCRIPT_MANAGED_RUNTIME", entrypoint);
        Assert.Contains("capture_upstream_provider_inputs()", entrypoint);
        Assert.Contains("normalize_omniroute_runtime_contract()", entrypoint);
        Assert.Contains("wait_for_omniroute_health()", entrypoint);
        Assert.Contains("export_local_omniroute_routing()", entrypoint);
        Assert.Contains("configure_claude_runtime()", entrypoint);
        Assert.Contains("run_omniroute_bootstrap()", entrypoint);
        Assert.Contains("start_pm2_runtime()", entrypoint);
        Assert.Contains("json_escape()", entrypoint);
        Assert.DoesNotContain("deluser hagicode", entrypoint);
        Assert.DoesNotContain("gosu node", entrypoint);
        Assert.DoesNotContain("/home/node", entrypoint);
        Assert.DoesNotContain("run_as_hagicode npm install -g", entrypoint);
        Assert.DoesNotContain("CLAUDE_CODE_CLI_VERSION", entrypoint);
        Assert.DoesNotContain("OPENSPEC_CLI_VERSION", entrypoint);
        Assert.DoesNotContain("PINNED_OPENCODE_CLI_VERSION", entrypoint);
        Assert.DoesNotContain("CODEX_CLI_VERSION", entrypoint);
        Assert.Contains("configure_code_server_runtime_if_needed()", entrypoint);
        Assert.Contains("HAGICODE_APP_DATA_DIR=\"${HAGICODE_APP_DIR}/data\"", entrypoint);
        Assert.Contains("HAGICODE_APP_SAVES_DIR=\"${HAGICODE_APP_DIR}/saves\"", entrypoint);
        Assert.Contains("CODE_SERVER_PASSWORD (masked)", entrypoint);
        Assert.Contains("CODE_SERVER_HASHED_PASSWORD (masked)", entrypoint);
        Assert.Contains("VsCodeServer__CodeServerAuthMode=password requires CODE_SERVER_PASSWORD", entrypoint);
        Assert.Contains("validate_accept_eula()", entrypoint);
        Assert.Contains("ACCEPT_EULA must be set to an accepted opt-in value", entrypoint);
        Assert.Contains("run_as_hagicode code-server --version >/dev/null", entrypoint);
        Assert.Contains("QODER_PERSONAL_ACCESS_TOKEN (masked)", entrypoint);
        Assert.Contains("HagiScript-synced image toolchain verified", entrypoint);
        Assert.Contains("OMNIROUTE_CLAUDE_UPSTREAM_AUTH_TOKEN", entrypoint);
        Assert.Contains("OMNIROUTE_CODEX_UPSTREAM_BASE_URL", entrypoint);
        Assert.Contains("OMNIROUTE_CODEX_UPSTREAM_API_KEY", entrypoint);
        Assert.Contains("OMNIROUTE_OPENCODE_UPSTREAM_BASE_URL", entrypoint);
        Assert.Contains("OMNIROUTE_OPENCODE_UPSTREAM_API_KEY", entrypoint);
        Assert.Contains("OMNIROUTE_PASSWORD_FILE", entrypoint);
        Assert.Contains("OMNIROUTE_SHARED_KEY_FILE", entrypoint);
        Assert.Contains("OMNIROUTE_SHARED_API_KEY", entrypoint);
        Assert.Contains("HAGICODE_PM2_READY_FILE", entrypoint);
        Assert.Contains("run_as_hagicode pm2-runtime start", entrypoint);
        Assert.Contains("run_as_hagicode node \"${HAGICODE_BOOTSTRAP_DIR}/omniroute-bootstrap.mjs\"", entrypoint);
        Assert.Contains("touch \"$OMNIROUTE_READY_FILE\"", entrypoint);
        Assert.Contains("HAGICODE_OMNIROUTE_BASE_URL", entrypoint);
        Assert.Contains("HAGICODE_OMNIROUTE_API_BASE_URL", entrypoint);
        Assert.Contains("OmniRoute__BaseUrl", entrypoint);
        Assert.Contains("OmniRoute__ApiBaseUrl", entrypoint);
        Assert.Contains("ANTHROPIC_URL=\"$OMNIROUTE_API_BASE_URL\"", entrypoint);
        Assert.Contains("CODEX_BASE_URL=\"$OMNIROUTE_API_BASE_URL\"", entrypoint);
        Assert.Contains("OPENAI_BASE_URL=\"$OMNIROUTE_API_BASE_URL\"", entrypoint);
        Assert.Contains("OPENCODE_BASE_URL=\"$OMNIROUTE_API_BASE_URL\"", entrypoint);
        Assert.DoesNotContain("${OPENCODE_CLI_VERSION", entrypoint);
        Assert.DoesNotContain("UIPRO_CLI_VERSION", entrypoint);
        Assert.DoesNotContain("COPILOT_CLI_VERSION", entrypoint);
        Assert.DoesNotContain("CODEBUDDY_CLI_VERSION", entrypoint);
        Assert.DoesNotContain("QODER_CLI_VERSION", entrypoint);
        Assert.DoesNotContain("\"uipro-cli\"", entrypoint);
        Assert.DoesNotContain("\"@github/copilot\"", entrypoint);
        Assert.DoesNotContain("\"@tencent-ai/codebuddy-code\"", entrypoint);
        Assert.DoesNotContain("\"@qoder-ai/qodercli\"", entrypoint);
        Assert.DoesNotContain("/app/saves/save0/config", entrypoint);
        Assert.DoesNotContain("/app/saves/save0/data", entrypoint);
        Assert.DoesNotContain("exec_as_hagicode dotnet PCode.Web.dll", entrypoint);

        Assert.True(mainStart >= 0, "Entrypoint should declare a main function.");
        Assert.True(captureIndex >= 0, "Entrypoint should capture upstream provider inputs.");
        Assert.True(normalizeIndex > captureIndex, "Entrypoint should normalize the Omniroute runtime contract after capturing upstream inputs.");
        Assert.True(exportIndex > normalizeIndex, "Entrypoint should export local Omniroute routing after runtime normalization.");
        Assert.True(configureClaudeIndex > exportIndex, "Entrypoint should configure Claude after local routing is available.");
        Assert.True(pm2Index > configureClaudeIndex, "Entrypoint should start pm2 after routing and Claude configuration are ready.");
        Assert.True(bootstrapIndex > pm2Index, "Entrypoint should run provider bootstrap only after pm2 has started Omniroute.");
    }

    [Fact]
    public void OmnirouteSupervisionArtifacts_ShouldStayAlignedWithDockerRuntimeContract()
    {
        var ecosystem = ReadRepoFile("docker_deployment/ecosystem.config.cjs");
        var waitForReady = ReadRepoFile("docker_deployment/wait-for-ready.sh");
        var bootstrap = ReadRepoFile("docker_deployment/omniroute-bootstrap.mjs");
        var appImageTarget = ReadRepoFile("nukeBuild/Build.Targets.Docker.AppImage.cs");

        Assert.Contains("name: \"omniroute\"", ecosystem);
        Assert.Contains("name: \"hagicode-app\"", ecosystem);
        Assert.Contains("script: \"omniroute\"", ecosystem);
        Assert.Contains("script: path.join(__dirname, \"wait-for-ready.sh\")", ecosystem);
        Assert.Contains("HAGICODE_APP_COMMAND is required for pm2 startup", ecosystem);

        Assert.Contains("File.Copy(DockerPm2EcosystemConfig, DockerBuildContext / \"ecosystem.config.cjs\", true);", appImageTarget);
        Assert.Contains("File.Copy(DockerHagiScriptSyncManifest, DockerBuildContext / \"hagiscript-sync-manifest.json\", true);", appImageTarget);
        Assert.Contains("File.Copy(DockerOmnirouteBootstrapScript, DockerBuildContext / \"omniroute-bootstrap.mjs\", true);", appImageTarget);
        Assert.Contains("File.Copy(DockerWaitForReadyScript, DockerBuildContext / \"wait-for-ready.sh\", true);", appImageTarget);
        Assert.Contains("platform-aware when a platform list is supplied", appImageTarget);

        Assert.Contains("READY_FILE=\"${HAGICODE_PM2_READY_FILE:?HAGICODE_PM2_READY_FILE is required}\"", waitForReady);
        Assert.Contains("HAGICODE_PM2_READY_TIMEOUT_SECONDS", waitForReady);
        Assert.Contains("HAGICODE_PM2_READY_POLL_SECONDS", waitForReady);
        Assert.Contains("Timed out waiting for ready file", waitForReady);

        Assert.Contains("/api/auth/login", bootstrap);
        Assert.Contains("/api/provider-nodes", bootstrap);
        Assert.Contains("/api/providers", bootstrap);
        Assert.Contains("process.env.OMNIROUTE_CLAUDE_UPSTREAM_AUTH_TOKEN || process.env.ANTHROPIC_AUTH_TOKEN", bootstrap);
        Assert.Contains("process.env.OMNIROUTE_CODEX_UPSTREAM_API_KEY || process.env.CODEX_API_KEY || process.env.OPENAI_API_KEY", bootstrap);
        Assert.Contains("process.env.OMNIROUTE_OPENCODE_UPSTREAM_API_KEY || process.env.OPENCODE_API_KEY", bootstrap);
        Assert.Contains("await ensureDirectory(path.dirname(bootstrapStateFile));", bootstrap);
        Assert.Contains("await ensureDirectory(path.dirname(readyFile));", bootstrap);
        Assert.Contains("await fs.writeFile(readyFile", bootstrap);
        Assert.Contains("provider: \"claude\"", bootstrap);
        Assert.Contains("provider: \"codex\"", bootstrap);
        Assert.Contains("provider: \"opencode\"", bootstrap);
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

    [Theory]
    [InlineData(null, false)]
    [InlineData("Y", true)]
    [InlineData("yes", true)]
    [InlineData("TRUE", true)]
    [InlineData("1", true)]
    [InlineData("no", false)]
    public void Entrypoint_ShouldGate_StartupOnAcceptedEulaValues(string? acceptEulaValue, bool shouldPass)
    {
        var entrypointPath = Path.Combine(RepoRoot, "docker_deployment", "docker-entrypoint.sh");
        var result = RunBashScript(
            $$"""
            #!/usr/bin/env bash
            set -euo pipefail
            source "{{entrypointPath}}"
            validate_accept_eula
            """,
            ("ACCEPT_EULA", acceptEulaValue)
        );

        if (shouldPass)
        {
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Container EULA acceptance detected", result.StdOut);
            return;
        }

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("ACCEPT_EULA must be set to an accepted opt-in value", result.StdErr);
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
            HAGICODE_BOOTSTRAP_DIR="${HAGICODE_APP_DIR}/bootstrap"
            HAGICODE_CODE_SERVER_CONFIG_DIR="${HAGICODE_HOME}/.config/code-server"
            HAGICODE_CODE_SERVER_CACHE_DIR="${HAGICODE_HOME}/.cache/code-server"
            HAGICODE_CODE_SERVER_SHARE_DIR="${HAGICODE_HOME}/.local/share/code-server"
            HAGICODE_CODE_SERVER_DATA_DIR="${HAGICODE_APP_DATA_DIR}/code-server"

            chown() { :; }

            ensure_hagicode_runtime_paths

            test -d "$HAGICODE_APP_DATA_DIR"
            test -d "$HAGICODE_APP_SAVES_DIR"
            test -d "$HAGICODE_CODE_SERVER_DATA_DIR"
            test ! -e "$HAGICODE_APP_SAVES_DIR/save0"

            touch "$HAGICODE_APP_DATA_DIR/.write-check"
            touch "$HAGICODE_APP_SAVES_DIR/.write-check"
            """);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("save0", result.StdOut + result.StdErr);
    }

    [Fact]
    public void ReleaseDocs_ShouldDescribe_OmnirouteUnifiedProviderContract()
    {
        var readme = ReadRepoFile("README.md");
        var readmeCn = ReadRepoFile("README_cn.md");
        var environmentVariables = ReadRepoFile("ENVIRONMENT_VARIABLES.md");
        var agentGuidance = ReadRepoFile("AGENTS.md");

        Assert.Contains("clean `debian:bookworm-slim` base", readme);
        Assert.Contains("Node.js 22 is installed through an image-managed NVM layout", readme);
        Assert.DoesNotContain("Node.js 24", readme);
        Assert.Contains("clears `NPM_CONFIG_PREFIX` before `nvm install`", readme);
        Assert.Contains("installs pinned `@hagicode/hagiscript` first", readme);
        Assert.Contains("`hagiscript npm-sync --managed-runtime /home/hagicode/.hagiscript/node-runtime --manifest /app/bootstrap/hagiscript-sync-manifest.json`", readme);
        Assert.Contains("HagiScript catalog-backed `code-server` runtime", readme);
        Assert.Contains("Only `hagicode` is supported as the non-root runtime user", readme);
        Assert.Contains("`claude`", readme);
        Assert.Contains("`opencode`", readme);
        Assert.Contains("`codex`", readme);
        Assert.Contains("`openspec` remains in the image as the retained workflow tool", readme);
        Assert.Contains("`skills` remains bundled as the retained skill-management CLI", readme);
        Assert.Contains("UI-managed install path", readme);
        Assert.Contains("`uipro` is no longer part of the image because the bundled `skills` command replaces", readme);
        Assert.Contains("`openssh-client`", readme);
        Assert.Contains("`SSH_PRIVATE_KEY_PATH`", readme);
        Assert.Contains("`SSH_KNOWN_HOSTS_PATH`", readme);
        Assert.Contains("`SSH_STRICT_HOST_KEY_CHECKING`", readme);
        Assert.Contains("default of `accept-new`", readme);
        Assert.Contains("skip SSH bootstrap entirely", readme);
        Assert.Contains("`GIT_SSH_COMMAND`", readme);
        Assert.Contains("Bundled Code Server runtime", readme);
        Assert.Contains("`VsCodeServer__*`", readme);
        Assert.Contains("`ACCEPT_EULA=Y`", readme);
        Assert.Contains("`CODE_SERVER_PASSWORD`", readme);
        Assert.Contains("`127.0.0.1`", readme);
        Assert.Contains("Both persistence roots are required in production deployments", readme);
        Assert.Contains("`hagicode_data:/app/data`", readme);
        Assert.Contains("`hagicode_saves:/app/saves`", readme);
        Assert.Contains("`/app/saves/save0/...`", readme);
        Assert.Contains("The image and entrypoint prepare only `/app/data` and `/app/saves`", readme);
        Assert.Contains("add a named volume or bind mount for `/app/saves`", readme);
        Assert.Contains("Omniroute as the unified local provider proxy", readme);
        Assert.Contains("`127.0.0.1:4060`", readme);
        Assert.Contains("`/app/data/omniroute`", readme);
        Assert.Contains("`pm2-runtime`", readme);
        Assert.Contains("`wait-for-ready.sh`", readme);
        Assert.Contains("`hagicode-app`", readme);
        Assert.Contains("`/app/data/omniroute/runtime/hagicode.ready`", readme);
        Assert.Contains("upserts provider nodes/connections through the Omniroute HTTP API", readme);
        Assert.Contains("`HAGICODE_OMNIROUTE_ENABLED=true`", readme);
        Assert.Contains("`OmniRoute__BaseUrl`", readme);

        Assert.Contains("`debian:bookworm-slim`", readmeCn);
        Assert.Contains("Node.js 22", readmeCn);
        Assert.DoesNotContain("Node.js 24", readmeCn);
        Assert.Contains("会先清理 `NPM_CONFIG_PREFIX` 再执行 `nvm install`", readmeCn);
        Assert.Contains("先安装固定版本的 `@hagicode/hagiscript`", readmeCn);
        Assert.Contains("`hagiscript npm-sync --managed-runtime /home/hagicode/.hagiscript/node-runtime --manifest /app/bootstrap/hagiscript-sync-manifest.json`", readmeCn);
        Assert.Contains("HagiScript catalog-backed 的 `code-server` 运行时", readmeCn);
        Assert.Contains("唯一受支持的非 root 运行用户是 `hagicode`", readmeCn);
        Assert.Contains("主要 agent CLI 基线", readmeCn);
        Assert.Contains("`openspec` 仍作为镜像保留的工作流工具存在", readmeCn);
        Assert.Contains("`skills` 也作为镜像保留的技能管理 CLI 默认内置", readmeCn);
        Assert.Contains("`SSH_PRIVATE_KEY_PATH`", readmeCn);
        Assert.Contains("`SSH_KNOWN_HOSTS_PATH`", readmeCn);
        Assert.Contains("`SSH_STRICT_HOST_KEY_CHECKING`", readmeCn);
        Assert.Contains("默认的 `accept-new`", readmeCn);
        Assert.Contains("`GIT_SSH_COMMAND`", readmeCn);
        Assert.Contains("内置 Code Server 运行时", readmeCn);
        Assert.Contains("`VsCodeServer__*`", readmeCn);
        Assert.Contains("`ACCEPT_EULA=Y`", readmeCn);
        Assert.Contains("`CODE_SERVER_PASSWORD`", readmeCn);
        Assert.Contains("`127.0.0.1`", readmeCn);
        Assert.Contains("生产部署必须同时持久化这两个根目录", readmeCn);
        Assert.Contains("`hagicode_data:/app/data`", readmeCn);
        Assert.Contains("`hagicode_saves:/app/saves`", readmeCn);
        Assert.Contains("`/app/saves/save0/...`", readmeCn);
        Assert.Contains("镜像与入口脚本只会准备 `/app/data` 和 `/app/saves`", readmeCn);
        Assert.Contains("补充 `/app/saves` 的 named volume 或 bind mount", readmeCn);
        Assert.Contains("Omniroute 作为容器内部 Claude、Codex/OpenAI 与 OpenCode 流量的统一本地 provider proxy", readmeCn);
        Assert.Contains("`127.0.0.1:4060`", readmeCn);
        Assert.Contains("`/app/data/omniroute`", readmeCn);
        Assert.Contains("`pm2-runtime`", readmeCn);
        Assert.Contains("`wait-for-ready.sh`", readmeCn);
        Assert.Contains("`hagicode-app`", readmeCn);
        Assert.Contains("`/app/data/omniroute/runtime/hagicode.ready`", readmeCn);
        Assert.Contains("幂等 upsert provider node 与 connection", readmeCn);
        Assert.Contains("`HAGICODE_OMNIROUTE_ENABLED=true`", readmeCn);
        Assert.Contains("`OmniRoute__BaseUrl`", readmeCn);

        Assert.Contains("CODEBUDDY_API_KEY", environmentVariables);
        Assert.Contains("CODEBUDDY_INTERNET_ENVIRONMENT", environmentVariables);
        Assert.Contains("QODER_PERSONAL_ACCESS_TOKEN", environmentVariables);
        Assert.Contains("qodercli --acp", environmentVariables);
        Assert.Contains("no longer owns startup-time per-tool reinstall variables", environmentVariables);
        Assert.Contains("update the HagiScript catalog or this release manifest and rebuild the image", environmentVariables);
        Assert.Contains("There is intentionally no `OPENCODE_CLI_VERSION`", environmentVariables);
        Assert.Contains("UI-managed installs: `copilot`, `codebuddy`, and `qodercli`", environmentVariables);
        Assert.Contains("`uipro` is no longer shipped because the bundled `skills` command replaces its runtime role", environmentVariables);
        Assert.Contains("Supported non-root runtime user: `hagicode` only", environmentVariables);
        Assert.Contains("the image does not rely on the upstream `node` user or `/home/node`", environmentVariables);
        Assert.Contains("clears `NPM_CONFIG_PREFIX` before `nvm install`", environmentVariables);
        Assert.Contains("Pinned `@hagicode/hagiscript` is installed first", environmentVariables);
        Assert.Contains("`hagiscript npm-sync` consumes `/app/bootstrap/hagiscript-sync-manifest.json`", environmentVariables);
        Assert.Contains("HagiScript catalog-backed `code-server`", environmentVariables);
        Assert.Contains("Shared PATH exposure comes from `/usr/local/nvm/current/bin`, `/home/hagicode/.npm-global/bin`, and `/home/hagicode/.hagiscript/node-runtime/bin`", environmentVariables);
        Assert.Contains("Primary baked agent CLI baseline: `claude`, `opencode`, and `codex`", environmentVariables);
        Assert.Contains("Retained bundled tools: `openspec` for workflow automation and `skills` for skill management", environmentVariables);
        Assert.Contains("### Skills CLI", agentGuidance);
        Assert.Contains("retained bundled skill-management tool", agentGuidance);
        Assert.Contains("Bundled tools: `openspec` for workflow automation and `skills` for skill management", agentGuidance);
        Assert.Contains("the bundled `skills` command replaces its previous runtime role", agentGuidance);
        Assert.Contains("Code Server Deployment Contract", environmentVariables);
        Assert.Contains("ACCEPT_EULA", environmentVariables);
        Assert.Contains("Builder EULA toggle", environmentVariables);
        Assert.Contains("accepted opt-in value", environmentVariables);
        Assert.Contains("VsCodeServer__CodeServerAuthMode", environmentVariables);
        Assert.Contains("CODE_SERVER_PASSWORD", environmentVariables);
        Assert.Contains("CODE_SERVER_HASHED_PASSWORD", environmentVariables);
        Assert.Contains("PASSWORD", environmentVariables);
        Assert.Contains("HASHED_PASSWORD", environmentVariables);
        Assert.Contains("code-server --version", environmentVariables);
        Assert.Contains("Both persistence roots are required in production deployments", environmentVariables);
        Assert.Contains("/app/data/code-server", environmentVariables);
        Assert.Contains("hagicode_saves:/app/saves", environmentVariables);
        Assert.Contains("/app/saves/save0/...", environmentVariables);
        Assert.Contains("entrypoint only prepare `/app/data` and `/app/saves`", environmentVariables);
        Assert.Contains("add a named volume or bind mount for `/app/saves`", environmentVariables);
        Assert.Contains("SSH_PRIVATE_KEY_PATH", environmentVariables);
        Assert.Contains("SSH_KNOWN_HOSTS_PATH", environmentVariables);
        Assert.Contains("SSH_STRICT_HOST_KEY_CHECKING", environmentVariables);
        Assert.Contains("skip SSH bootstrap", environmentVariables);
        Assert.Contains("`accept-new`", environmentVariables);
        Assert.Contains("GIT_SSH_COMMAND", environmentVariables);
        Assert.Contains("Omniroute Unified Provider Bootstrap", environmentVariables);
        Assert.Contains("OMNIROUTE_ENABLE_BOOTSTRAP", environmentVariables);
        Assert.Contains("OMNIROUTE_BASE_URL", environmentVariables);
        Assert.Contains("OMNIROUTE_API_BASE_URL", environmentVariables);
        Assert.Contains("OMNIROUTE_STATE_DIR", environmentVariables);
        Assert.Contains("OMNIROUTE_PM2_HOME", environmentVariables);
        Assert.Contains("OMNIROUTE_READY_FILE", environmentVariables);
        Assert.Contains("OMNIROUTE_BOOTSTRAP_STATE_FILE", environmentVariables);
        Assert.Contains("OMNIROUTE_SHARED_API_KEY", environmentVariables);
        Assert.Contains("HAGICODE_PM2_READY_TIMEOUT_SECONDS", environmentVariables);
        Assert.Contains("pm2-runtime", environmentVariables);
        Assert.Contains("wait-for-ready.sh", environmentVariables);
        Assert.Contains("API-first", environmentVariables);
        Assert.Contains("HAGICODE_OMNIROUTE_ENABLED=true", environmentVariables);
        Assert.Contains("OMNIROUTE_CLAUDE_UPSTREAM_AUTH_TOKEN", environmentVariables);
        Assert.Contains("OMNIROUTE_CODEX_UPSTREAM_API_KEY", environmentVariables);
        Assert.Contains("OMNIROUTE_OPENCODE_UPSTREAM_API_KEY", environmentVariables);
        Assert.DoesNotContain("UIPRO_CLI_VERSION", environmentVariables);
        Assert.DoesNotContain("COPILOT_CLI_VERSION", environmentVariables);
        Assert.DoesNotContain("CODEBUDDY_CLI_VERSION", environmentVariables);
        Assert.DoesNotContain("QODER_CLI_VERSION", environmentVariables);
    }

    [Fact]
    public void ReleaseDocs_ShouldKeep_SshBootstrapAndOmnirouteRoutingAligned()
    {
        var readme = ReadRepoFile("README.md");
        var readmeCn = ReadRepoFile("README_cn.md");
        var environmentVariables = ReadRepoFile("ENVIRONMENT_VARIABLES.md");

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
            "`skills`",
            "Omniroute",
            "127.0.0.1:4060",
            "/app/data/omniroute",
        };

        foreach (var token in sharedEnglishTokens)
        {
            Assert.Contains(token, readme);
            Assert.Contains(token, environmentVariables);
        }

        Assert.Contains("主要 agent CLI 基线", readmeCn);
        Assert.Contains("`SSH_PRIVATE_KEY_PATH`", readmeCn);
        Assert.Contains("`SSH_KNOWN_HOSTS_PATH`", readmeCn);
        Assert.Contains("`SSH_STRICT_HOST_KEY_CHECKING`", readmeCn);
        Assert.Contains("`openspec`", readmeCn);
        Assert.Contains("Omniroute", readmeCn);
        Assert.Contains("`127.0.0.1:4060`", readmeCn);
        Assert.Contains("`/app/data/omniroute`", readmeCn);
        Assert.Equal(0, new[] { readme, environmentVariables }.Count(doc => doc.Contains("`copilot`") && doc.Contains("default built-in agent CLI")));
    }

    [Fact]
    public void LocalDockerComposeWorkflow_ShouldShip_WithScriptsAndDocs()
    {
        var compose = ReadRepoFile("docker-compose.local.yml");
        var envTemplate = ReadRepoFile(".env.local.example");
        var envSecretsTemplate = ReadRepoFile(".env.secrets.local.example");
        var buildScript = ReadRepoFile("scripts/docker-local-build.sh");
        var upScript = ReadRepoFile("scripts/docker-local-up.sh");
        var testScript = ReadRepoFile("scripts/docker-local-test.sh");
        var commonScript = ReadRepoFile("scripts/docker-local-common.sh");
        var buildWrapper = ReadRepoFile("build.sh");
        var buildWrapperPs = ReadRepoFile("build.ps1");
        var readme = ReadRepoFile("README.md");
        var readmeCn = ReadRepoFile("README_cn.md");
        var environmentVariables = ReadRepoFile("ENVIRONMENT_VARIABLES.md");

        Assert.Contains("name: hagicode-local", compose);
        Assert.Contains("image: ${HAGICODE_LOCAL_IMAGE:-hagicode-local:dev}", compose);
        Assert.Contains(".local/hagicode/data:/app/data", compose);
        Assert.Contains(".local/hagicode/saves:/app/saves", compose);
        Assert.Contains("OMNIROUTE_ENABLE_BOOTSTRAP: ${OMNIROUTE_ENABLE_BOOTSTRAP:-true}", compose);
        Assert.Contains("ACCEPT_EULA: ${ACCEPT_EULA:-Y}", compose);

        Assert.Contains("HAGICODE_RELEASE_VERSION=", envTemplate);
        Assert.Contains("HAGICODE_DOCKER_PLATFORM=", envTemplate);
        Assert.Contains("AZURE_BLOB_SAS_URL=", envTemplate);
        Assert.Contains("ACCEPT_EULA=Y", envTemplate);
        Assert.Contains("OMNIROUTE_ENABLE_BOOTSTRAP=true", envTemplate);
        Assert.Contains("AZURE_BLOB_SAS_URL=", envSecretsTemplate);
        Assert.Contains("CODEX_API_KEY=", envSecretsTemplate);
        Assert.Contains("NUGEX_AzureAcrPassword=", envSecretsTemplate);

        Assert.Contains("DockerPrepareLocalContext", buildScript);
        Assert.Contains("--secrets-file", buildScript);
        Assert.Contains("docker buildx build", buildScript);
        Assert.Contains("--secrets-file", upScript);
        Assert.Contains("run_compose up -d", upScript);
        Assert.Contains("HTTP health check passed", testScript);
        Assert.Contains("hagiscript --version", testScript);
        Assert.Contains("openspec --version", testScript);
        Assert.Contains("skills --version", testScript);
        Assert.Contains("omniroute --help", testScript);
        Assert.Contains("pm2 --version", testScript);
        Assert.Contains("pm2-runtime", testScript);
        Assert.Contains("HagiScript-synced bundled CLI smoke test passed", testScript);
        Assert.Contains("DEFAULT_SECRETS_FILE", commonScript);
        Assert.Contains("Loaded local secrets override", commonScript);
        Assert.Contains("detect_host_platform()", commonScript);
        Assert.Contains("run_compose()", commonScript);
        Assert.Contains("output/download/*${HAGICODE_RELEASE_VERSION}*${download_platform}*.zip", commonScript);
        Assert.Contains(".env.secrets.local", buildWrapper);
        Assert.Contains("Loaded local secrets override", buildWrapper);
        Assert.Contains(".env.secrets.local", buildWrapperPs);
        Assert.Contains("Loaded local secrets override", buildWrapperPs);
        Assert.Contains("GetDownloadedZipFilesForVersion", ReadRepoFile("nukeBuild/Build.Partial.cs"));
        Assert.Contains("ExtractZipFiles(platformDir, version, platform)", ReadRepoFile("nukeBuild/Build.Targets.Docker.AppImage.cs"));
        Assert.Contains("No downloaded zip packages for version", ReadRepoFile("nukeBuild/Build.Targets.Docker.Local.cs"));

        Assert.Contains("docker-compose.local.yml", readme);
        Assert.Contains("docker-local-build.sh", readme);
        Assert.Contains("docker-local-test.sh", readme);
        Assert.Contains(".env.secrets.local.example", readme);
        Assert.Contains("Docker Hub", readme);
        Assert.Contains("docker-compose.local.yml", readmeCn);
        Assert.Contains("docker-local-build.sh", readmeCn);
        Assert.Contains("docker-local-test.sh", readmeCn);
        Assert.Contains(".env.secrets.local.example", readmeCn);
        Assert.Contains("Docker Hub", readmeCn);
        Assert.Contains("Local Docker Compose Workflow", environmentVariables);
        Assert.Contains(".env.local.example", environmentVariables);
        Assert.Contains(".env.secrets.local", environmentVariables);
        Assert.Contains("Docker Hub", environmentVariables);
        Assert.Contains("log_local_build_network_requirements", buildScript);
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
        Assert.Contains(requiredRootsEnglish, environmentVariables);
        Assert.Contains("`/app/data/code-server`", readme);
        Assert.Contains("`/app/saves/save0/...`", readme);

        Assert.Contains(requiredRootsChinese, readmeCn);
        Assert.Contains("`/app/data/code-server`", readmeCn);
        Assert.Contains("`/app/saves/save0/...`", readmeCn);
    }

    [Fact]
    public void OmnirouteBootstrap_ShouldPreserveUpstreamProviderPrecedenceBeforeLocalReroute()
    {
        var entrypoint = ReadRepoFile("docker_deployment/docker-entrypoint.sh");
        var bootstrap = ReadRepoFile("docker_deployment/omniroute-bootstrap.mjs");

        Assert.Contains("OMNIROUTE_CLAUDE_UPSTREAM_BASE_URL", entrypoint);
        Assert.Contains("OMNIROUTE_CLAUDE_UPSTREAM_AUTH_TOKEN", entrypoint);
        Assert.Contains("OMNIROUTE_CODEX_UPSTREAM_BASE_URL", entrypoint);
        Assert.Contains("OMNIROUTE_CODEX_UPSTREAM_API_KEY", entrypoint);
        Assert.Contains("OMNIROUTE_OPENCODE_UPSTREAM_BASE_URL", entrypoint);
        Assert.Contains("OMNIROUTE_OPENCODE_UPSTREAM_API_KEY", entrypoint);
        Assert.Contains("process.env.OMNIROUTE_CLAUDE_UPSTREAM_BASE_URL || process.env.ANTHROPIC_URL", bootstrap);
        Assert.Contains("process.env.OMNIROUTE_CLAUDE_UPSTREAM_AUTH_TOKEN || process.env.ANTHROPIC_AUTH_TOKEN", bootstrap);
        Assert.Contains("process.env.OMNIROUTE_CODEX_UPSTREAM_BASE_URL ||", bootstrap);
        Assert.Contains("process.env.OMNIROUTE_CODEX_UPSTREAM_API_KEY || process.env.CODEX_API_KEY || process.env.OPENAI_API_KEY", bootstrap);
        Assert.Contains("process.env.OMNIROUTE_OPENCODE_UPSTREAM_BASE_URL ||", bootstrap);
        Assert.Contains("process.env.OMNIROUTE_OPENCODE_UPSTREAM_API_KEY || process.env.OPENCODE_API_KEY", bootstrap);
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
