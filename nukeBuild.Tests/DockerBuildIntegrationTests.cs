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
    public void Dockerfile_ShouldInstall_SharedAndExtendedCliTools()
    {
        var dockerfile = ReadRepoFile("docker_deployment/Dockerfile.template");

        Assert.Contains("PINNED_OPENCODE_CLI_VERSION=1.2.25", dockerfile);
        Assert.Contains("PINNED_CODEBUDDY_CLI_VERSION=2.61.2", dockerfile);
        Assert.Contains("PINNED_IFLOW_CLI_VERSION=0.5.17", dockerfile);

        Assert.Contains("npm install -g opencode-ai@1.2.25", dockerfile);
        Assert.Contains("/home/hagicode/.npm-global/bin/opencode --version", dockerfile);

        Assert.Contains("npm install -g @tencent-ai/codebuddy-code@2.61.2", dockerfile);
        Assert.Contains("/home/hagicode/.npm-global/bin/codebuddy --version", dockerfile);

        Assert.Contains("npm install -g @iflow-ai/iflow-cli@0.5.17", dockerfile);
        Assert.Contains("/home/hagicode/.npm-global/bin/iflow --version", dockerfile);
    }

    [Fact]
    public void Entrypoint_ShouldExpose_NewCliOverrides_WithoutInventingOpenCodeOverride()
    {
        var entrypoint = ReadRepoFile("docker_deployment/docker-entrypoint.sh");

        Assert.Contains("PINNED_OPENCODE_CLI_VERSION", entrypoint);
        Assert.Contains("CODEBUDDY_CLI_VERSION", entrypoint);
        Assert.Contains("IFLOW_CLI_VERSION", entrypoint);
        Assert.Contains("\"@tencent-ai/codebuddy-code\"", entrypoint);
        Assert.Contains("\"@iflow-ai/iflow-cli\"", entrypoint);
        Assert.Contains("OpenCode CLI using pinned image version", entrypoint);
        Assert.DoesNotContain("${OPENCODE_CLI_VERSION", entrypoint);
    }

    [Fact]
    public void ReleaseDocs_ShouldDescribe_CodeBuddyIFlowAndOpenCodeContainerContract()
    {
        var readme = ReadRepoFile("README.md");
        var environmentVariables = ReadRepoFile("ENVIRONMENT_VARIABLES.md");

        Assert.Contains("codebuddy --version", readme);
        Assert.Contains("iflow --version", readme);
        Assert.Contains("opencode --version", readme);
        Assert.Contains("CODEBUDDY_CLI_VERSION", readme);
        Assert.Contains("IFLOW_CLI_VERSION", readme);
        Assert.Contains("managed OpenCode runtime contract", readme);

        Assert.Contains("CODEBUDDY_API_KEY", environmentVariables);
        Assert.Contains("CODEBUDDY_INTERNET_ENVIRONMENT", environmentVariables);
        Assert.Contains("CODEBUDDY_CLI_VERSION", environmentVariables);
        Assert.Contains("IFLOW_CLI_VERSION", environmentVariables);
        Assert.Contains("There is intentionally no `OPENCODE_CLI_VERSION`", environmentVariables);
        Assert.Contains("complete `iflow` login interactively", environmentVariables);
    }

    [Fact]
    public void CodexRuntimeVariables_ShouldUseDeterministicPrecedence()
    {
        var entrypoint = ReadRepoFile("docker_deployment/docker-entrypoint.sh");

        Assert.Contains("CODEX_BASE_URL > OPENAI_BASE_URL", entrypoint);
        Assert.Contains("CODEX_API_KEY > OPENAI_API_KEY", entrypoint);
        Assert.Contains("API key source: $CODEX_API_SOURCE (masked)", entrypoint);
    }
}
