# AGENTS.md - HagiCode Release Repository

This document describes the AI agents (Claude, OpenSpec, Codex, Copilot) that work with this repository.

## Overview

The HagiCode release repository manages automated releases of the HagiCode platform. This repository integrates with multiple AI agents to streamline release workflows and spec-driven development.

## Supported AI Agents

### Claude Code CLI

**Purpose**: AI-assisted development and code generation
**Version**: 2.1.34
**Installation**: Included in Docker base images via npm

The Claude Code CLI is pre-installed in all Docker base images and provides:
- AI-powered code generation and refactoring
- Interactive development assistance
- File and project context understanding
- Multi-agent collaboration support

**Usage**:
```bash
# Run Claude Code within a HagiCode container
claude --help

# Open the HagiCode project in Claude Code
claude --project /path/to/hagicode-mono
```

### OpenSpec CLI

**Purpose**: Spec-driven development workflow management
**Version**: >=1.0.0 <2.0.0
**Installation**: Included in Docker base images via npm

The OpenSpec CLI manages proposals, changes, and specifications:
- Create and manage OpenSpec proposals
- Track implementation progress through task lists
- Generate specs from templates
- Integrate with AI assistants for spec creation

**Usage**:
```bash
# List available OpenSpec commands
openspec --help

# Check OpenSpec version
openspec --version

# View OpenSpec status
openspec status
```

### Codex CLI

**Purpose**: AI coding task execution and automation
**Version**: Latest major stream via `@openai/codex`
**Installation**: Included in Docker base images via npm

The Codex CLI is pre-installed in unified release images and provides:
- AI coding workflow execution
- Command-driven coding automation
- Endpoint and key override via container runtime environment variables

**Usage**:
```bash
# Check Codex CLI version
codex --version

# View Codex CLI help
codex --help
```

### Copilot CLI

**Purpose**: GitHub Copilot coding agent workflow in terminal
**Version**: Latest major stream via `@github/copilot`
**Installation**: Included in Docker base images via npm

The Copilot CLI is pre-installed in unified release images and provides:
- Terminal-native Copilot coding workflow
- Prompt-driven code assistance in containerized runtime
- Endpoint and key override via container runtime environment variables

**Usage**:
```bash
# Check Copilot CLI version
copilot --version

# View Copilot CLI help
copilot --help
```

## Docker Integration

All AI agents are pre-installed in the Docker base images:

- **Base Images**:
  - `hagicode/hagicode:base` - AMD64 base image
  - `hagicode/hagicode:base-arm64` - ARM64 base image

- **Application Images**: Built on top of base images with application code

### AI Agent Configuration

The Docker entrypoint script (`docker-entrypoint.sh`) automatically configures AI agents based on environment variables:

#### Claude Code Configuration

- `ANTHROPIC_AUTH_TOKEN`: Anthropic API token (highest priority)
- `ANTHROPIC_URL`: Custom Anthropic API endpoint
- `ANTHROPIC_SONNET_MODEL`: Default Sonnet model
- `ANTHROPIC_OPUS_MODEL`: Default Opus model
- `ANTHROPIC_HAIKU_MODEL`: Default Haiku model
- `CLAUDE_HOST_CONFIG_ENABLED`: Enable/disable host config mount (default: true)
- `CLAUDE_CONFIG_MOUNT_PATH`: Path for mounted Claude config (default: /claude-mount)
- `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS`: Enable Agent Teams feature (default: 1)

#### Codex Global Settings Configuration

- `CODEX_BASE_URL`: Primary Codex endpoint variable
- `CODEX_API_KEY`: Primary Codex API key variable
- `OPENAI_BASE_URL`: Compatibility alias for endpoint
- `OPENAI_API_KEY`: Compatibility alias for API key

Precedence:
- Base URL: `CODEX_BASE_URL` > `OPENAI_BASE_URL`
- API key: `CODEX_API_KEY` > `OPENAI_API_KEY`

#### Copilot Global Settings Configuration

- `COPILOT_BASE_URL`: Copilot endpoint variable
- `COPILOT_API_KEY`: Copilot API key variable

Copilot variables are isolated and do not override Codex/OpenAI variables.

#### Host Configuration

Claude Code can be configured using host-mounted config files:

```bash
docker run -v ~/claude-config:/claude-mount hagicode/hagicode
```

The container will automatically copy settings from `/claude-mount/settings.json` to the hagicode user's `.claude` directory.

## Build System Integration

The Nuke build system integrates AI agents through:

1. **Automated CLI execution**: Build targets can invoke CLI commands
2. **Version injection**: AI agents receive version information from build parameters
3. **Workflow triggers**: AI agents can be invoked during release workflows

## Nuke Build System Best Practices

This repository uses [Nuke](https://nuke.build/) as the build automation framework. Nuke provides a C#-based build system with type-safe parameters and cross-platform support.

### Nuke Fundamentals

#### Repository Structure

Nuke organizes build logic across multiple partial classes for better maintainability:

- `Build.cs` - Main build entry point with parameters and dependencies
- `Build.Targets.*.cs` - Feature-specific build targets (e.g., Docker, VersionMonitor)
- `build.schema.json` - Auto-generated build schema for IDE integration

#### CLI Tools Integration

Nuke provides utilities for integrating external CLI tools:

```csharp
// Example: Using Docker CLI through Nuke
DockerTasks.DockerBuild(_ => _
    .SetImage(imageName)
    .SetPath(".")
    .SetTarget("linux-x64")
);
```

Best practices for CLI tool integration:
- Use Nuke's task abstractions (`DockerTasks`, `DotNetTasks`, etc.) when available
- For custom tools, use `ToolTasks.Execute()` with proper path handling
- Configure tool paths using `ToolPathResolver` for cross-platform compatibility
- Set process timeout and output capture for debugging

#### Parameters

Build parameters provide type-safe configuration with auto-completion:

```csharp
// Example parameter definition
[Parameter("Docker registry username")]
string DockerUsername => TryGetValue(() => DockerUsername);

[Parameter("Docker image version")]
string Version => TryGetValue(() => Version);
```

Best practices for parameters:
- Use `[Parameter]` attribute with descriptive help text
- Provide sensible defaults using `TryGetValue(() => defaultValue)`
- Group related parameters logically (e.g., Docker parameters together)
- Document required vs optional parameters clearly
- Use `bool` flags with `[Secret]` attribute for sensitive values

#### Logging

Nuke provides structured logging with different severity levels:

```csharp
// Example logging usage
Log.Information("Building image: {ImageName}", imageName);
Log.Warning("Cache miss for: {CacheKey}", cacheKey);
Log.Error(exception, "Failed to push image");
```

Best practices for logging:
- Use structured logging with message templates `{PropertyName}`
- Log at appropriate levels: `Information` for normal operations, `Warning` for recoverable issues, `Error` for failures
- Include context (image names, versions, platforms) in log messages
- Use `Serilog.Sinks.Console` for color-coded terminal output
- Configure minimum log level via command line: `--verbosity minimal|normal|detailed|diagnostic`

### Nuke in CI/CD

#### GitHub Actions Integration

Nuke provides official GitHub Actions integration:

```yaml
# Example workflow step
- name: Run Nuke Build
  run: ./build.cmd DockerPush --registry ghcr.io --version 1.2.3
```

Best practices for CI/CD:
- Use `build.sh` for Linux/macOS, `build.cmd` for Windows
- Pass CI-specific parameters via environment variables or command line
- Enable colored output: `build.sh --color-interaction auto`
- Set `NUKE_TELEMETRY_OPTOUT` to disable telemetry if required
- Cache NuGet packages: use `actions/cache` for `~/.nuget/packages`

### GitHub Actions Workflow for Multi-Platform Docker Builds

When running multi-platform Docker builds in GitHub Actions, the following setup steps are required before executing Nuke:

**Required Setup Steps (in order)**:
1. **Checkout code**: `actions/checkout@v4`
2. **Setup .NET SDK**: `actions/setup-dotnet@v4`
3. **Setup Docker Buildx**: `docker/setup-buildx-action@v3`
   - Required for multi-architecture Docker builds
   - Configures Buildx with docker-container driver
4. **Setup QEMU**: `docker/setup-qemu-action@v3`
   - Required for cross-platform builds (amd64, arm64)
   - Registers binfmt_misc handlers for emulation
5. **Run Nuke build**: Execute Docker build targets

**Example Workflow Configuration**:
```yaml
steps:
  - name: Checkout code
    uses: actions/checkout@v4

  - name: Setup .NET SDK
    uses: actions/setup-dotnet@v4
    with:
      dotnet-version: '10.0'

  - name: Set up Docker Buildx
    uses: docker/setup-buildx-action@v3

  - name: Set up QEMU
    uses: docker/setup-qemu-action@v3

  - name: Build Docker Images (Nuke)
    env:
      NUGEX_ReleaseVersion: ${{ inputs.version }}
      NUGEX_DockerPlatform: ${{ inputs.platform }}
    run: |
      cd nukeBuild
      dotnet run -- DockerRelease
```

**Why These Steps Are Required**:
- **Buildx**: Provides multi-architecture build capabilities that standard Docker doesn't have
- **QEMU**: Enables building ARM64 images on AMD64 runners via emulation
- Without these steps, Nuke Docker builds will fail with "no builder found" or "exec format error"

### GitHub Actions Docker Cache Configuration

GitHub Actions provides built-in caching for Docker Buildx to accelerate multi-platform builds:

**Cache Configuration**:
- Cache type: `gha` (GitHub Actions cache)
- Cache mode: `max` (maximum cache compression and sharing)
- Automatically enabled when `GITHUB_ACTIONS` environment variable is set

**How It Works**:
1. On first build: Docker layers are cached to GitHub Actions storage
2. On subsequent builds: Cached layers are retrieved, reducing build time
3. Cache is shared across workflows using the same cache keys

**Implementation in Nuke**:
The `Build.Targets.Docker.Cache.cs` file automatically detects GitHub Actions environment and applies appropriate cache settings:

```csharp
var isGitHubActions = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));
if (isGitHubActions)
{
    args.Add("--cache-from");
    args.Add("type=gha");
    args.Add("--cache-to");
    args.Add("type=gha,mode=max");
}
```

**Cache Strategy**:
- Cache is layered (local + registry + GitHub Actions)
- When GitHub Actions is detected, GHA cache is added first priority
- Registry cache serves as fallback for cross-workflow sharing
- Local cache used for development builds

**Best Practices**:
- Enable cache by default (avoid `DockerForceRebuild=true`)
- Monitor cache hit rate in GitHub Actions logs
- Consider cache key strategy for multi-platform builds
- GitHub Actions cache has a 10GB limit per repository

#### Running Nuke Locally

```bash
# List all available targets
./build.sh --help

# Run specific target with parameters
./build.sh DockerPush --registry registry.cn-hangzhou.aliyuncs.com --version 1.2.3

# Dry run to see what would execute
./build.sh DockerPush --skip

# Verbose logging
./build.sh DockerPush --verbosity diagnostic
```

### Multi-Platform Docker Builds

This repository uses Nuke for multi-architecture Docker builds:

**Supported Platforms**: linux-x64, linux-arm64

**Image Naming Convention**:
- **Docker Hub**: `hagicode/hagicode:1.2.3` (manifest with both arches)
- **Azure Container Registry**: `edgeacr.azurecr.io/hagicode/hagicode:1.2.3`
- **Aliyun Container Registry**: `registry.cn-hangzhou.aliyuncs.com/hagicode/hagicode:1.2.3`

**Registry Formats**:
| Registry | Format Example |
|----------|----------------|
| Docker Hub | `hagicode/hagicode:version` |
| ACR | `edgeacr.azurecr.io/hagicode/hagicode:version` |
| Aliyun | `registry.cn-hangzhou.aliyuncs.com/hagicode/hagicode:version` |

**Aliyun Registry Notes**:
- Aliyun registries follow format: `registry.{region}.aliyuncs.com/{namespace}/{image}:{tag}`
- Common regions: `cn-hangzhou`, `cn-shanghai`, `cn-beijing`
- Namespace is the user/organization name (e.g., `hagicode`)
- Image name is the container image identifier (e.g., `hagicode`)
- Tag is the version (e.g., `1.2.3`)

### Nuke File Organization

This project follows the target organization pattern:

| File | Purpose |
|------|---------|
| `Build.Targets.Docker.cs` | Docker-related targets (build, push, prune) |
| `Build.Targets.Docker.BaseImage.cs` | Base image building (arm64, x64) |
| `Build.Targets.Docker.Buildx.cs` | Buildx multi-arch builder setup |
| `Build.Targets.Docker.Cache.cs` | Docker cache management |
| `Build.Targets.Docker.Push.cs` | Registry push operations |
| `Build.Targets.Docker.Qemu.cs` | QEMU emulation for cross-arch builds |
| `Build.Targets.Docker.AppImage.cs` | AppImage packaging |
| `Build.Targets.Configuration.cs` | Build configuration management |
| `Build.Targets.VersionMonitor.cs` | Version monitoring from Azure Blob Storage |

### Common Nuke Commands

```bash
# Show all available targets and parameters
./build.sh --help

# Run default target
./build.sh

# Run specific target
./build.sh DockerBaseImage

# Run with parameters
./build.sh DockerPush --version 1.2.3 --registry registry.cn-hangzhou.aliyuncs.com

# Skip execution (dry run)
./build.sh DockerPush --skip

# Target specific platform
./build.sh DockerBaseImage --target linux-arm64

# Verbose output for debugging
./build.sh DockerBuild --verbosity diagnostic
```

### Adding New Nuke Targets

When adding new build targets:

1. Create a new `Build.Targets.{Feature}.cs` file
2. Add the target as a partial method in the `Build` class
3. Use `[Target]` attribute with dependencies: `[Target(Dependencies = new[] { nameof(BaseImage) })]`
4. Follow naming convention: PascalCase for targets, snake_case for parameters
5. Add unit tests in `nukeBuild.Tests/` directory
6. **IMPORTANT**: Run `./build.sh --help` to force Nuke build regeneration after any structural changes
7. Update `AGENTS.md` with new target documentation

#### Important: Force Build Regeneration

Nuke generates several files automatically that must be kept in sync with the build definition:

- `build.schema.json` - IDE integration schema
- `_build.csproj` - Project references and dependencies
- Generated parameter classes

**When to run `./build.sh --help`:**

- After adding new targets
- After adding/removing parameters
- After modifying target dependencies
- After adding new CLI tool packages

**Why this is required:**
Nuke uses the `--help` command as a trigger to regenerate its schema and ensure all targets are properly discovered. Failing to run this command can result in:
- New targets not appearing in `--help` output
- Parameter changes not being recognized
- Build failures due to schema mismatches

**Workflow example:**
```bash
# 1. Add new target/parameter in code
vim Build.Targets.MyFeature.cs

# 2. Regenerate Nuke schema (MANDATORY)
./build.sh --help

# 3. Run the new target
./build.sh MyNewTarget --param value
```

## Release Workflow

The release process involves the following AI agent-aware steps:

1. **Version Monitor**: Detects new versions in Azure Blob Storage
2. **Download**: Downloads application packages
3. **GitHub Release**: Creates GitHub releases with artifacts
4. **Docker Build**: Builds multi-arch Docker images with AI agents pre-installed
5. **Edge ACR Push**: Publishes images to Edge ACR registry

AI agents are available in all Docker images pushed to registries, enabling:
- AI-assisted development when pulling images
- Spec-driven workflows for changes
- Multi-architecture support for AI tools

## Version Format Requirements

**Version Format Validation**: This repository enforces strict version number formatting to ensure consistency across the release pipeline.

### Required Version Format

All version numbers must:
- **Start with a digit** (no "v" prefix allowed)
- **Contain only** alphanumeric characters, dots (.), hyphens (-), or underscores (_)
- **Follow semantic versioning** (semver) convention (e.g., `1.2.3`)

### Valid Examples
- `1.2.3` - Standard semver
- `0.1.0` - Leading zero allowed
- `1.2.3-beta.1` - Pre-release identifier
- `1.2.3-rc.1` - Release candidate
- `1.0.0-alpha` - Alpha release

### Invalid Examples (will be rejected)
- `v1.2.3` - v prefix is NOT allowed
- `1.2.3 beta` - Contains space
- `1.2.3@feature` - Contains special character @
- `1/2/3` - Contains slash
- `` - Empty string

### Validation Points

1. **Version Monitor** (`Build.Targets.VersionMonitor.cs`)
   - Validates versions from Azure Blob Storage
   - Skips invalid versions with warning logs

2. **Docker Build Workflow** (`.github/workflows/docker-build.yml`)
   - Validates version format in "Determine Version and Platform" step
   - Fails workflow with clear error message if format is invalid

### Error Message Example

```
Error: Invalid version format 'v1.2.3'
Version must start with a digit and only contain letters, numbers, dots, hyphens, or underscores.
Example: 1.2.3, 1.2.3-beta.1
```

## Troubleshooting

### Claude Code Not Working

**Symptom**: Claude Code CLI commands fail or show "not configured" errors

**Solutions**:
1. Ensure `ANTHROPIC_AUTH_TOKEN` environment variable is set
2. Check host config mount path is correct
3. Verify permissions on mounted config directory

### OpenSpec CLI Not Working

**Symptom**: OpenSpec commands fail or show version errors

**Solutions**:
1. Verify OpenSpec CLI version >=1.0.0 <2.0.0
2. Check that the Docker image includes the `@fission-ai/openspec@1` package
3. Ensure network connectivity for OpenSpec operations

### Codex CLI Not Working

**Symptom**: Codex commands fail or use unexpected endpoint/key

**Solutions**:
1. Verify the Docker image includes the `@openai/codex` package
2. Check variable precedence and conflicts: `CODEX_*` overrides `OPENAI_*`
3. Ensure both endpoint and API key are present when overriding Codex connectivity

### Copilot CLI Not Working

**Symptom**: Copilot commands fail or use unexpected endpoint/key

**Solutions**:
1. Verify the Docker image includes the `@github/copilot` package
2. Check Copilot variables independently: use `COPILOT_*` only for Copilot connectivity
3. Ensure both endpoint and API key are present when overriding Copilot connectivity

### Docker Build Issues

**Symptom**: Docker build fails with CLI-related errors

**Solutions**:
1. Ensure npm packages are installed correctly in base image
2. Check that base image is pushed to registry before app build
3. Verify multi-arch builder is set up correctly

## Contributing

When contributing to this repository:

1. Maintain AI agent version compatibility
2. Test Docker builds with both AMD64 and ARM64 platforms
3. Update this AGENTS.md when changing AI agent versions
4. Ensure environment variable documentation is updated

## Version Compatibility Matrix

| Component | Version Required | Installed Version | Status |
|------------|------------------|-------------------|--------|
| .NET SDK | 10.0 | 10.0 | ✓ |
| Docker | >=20.10 with buildx | Latest | ✓ |
| QEMU | Any | Via binfmt image | ✓ |
| Claude Code CLI | - | 2.1.34 | ✓ |
| OpenSpec CLI | >=1.0.0 <2.0.0 | 1.x | ✓ |
| UIPro CLI | - | 2.1.3 | ✓ |
| Codex CLI | - | latest major stream | ✓ |
| Copilot CLI | - | latest major stream | ✓ |

## Additional Resources

### AI Agents
- [Claude Code Documentation](https://claude.ai/code)
- [OpenSpec Documentation](https://openspec.dev)
- [Codex Documentation](https://developers.openai.com/codex)
- [GitHub Copilot CLI](https://github.com/github/copilot-cli)

### Build System
- [Nuke Documentation](https://nuke.build/docs/)
- [Nuke GitHub Actions Integration](https://nuke.build/docs/cicd/github-actions/)
- [Nuke Repository Structure](https://nuke.build/docs/common/repository/)
- [Nuke CLI Tools Integration](https://nuke.build/docs/common/cli-tools/)
- [Nuke Logging](https://nuke.build/docs/fundamentals/logging/)
- [Nuke Parameters](https://nuke.build/docs/fundamentals/parameters/)

### Docker
- [Docker Buildx Documentation](https://docs.docker.com/buildx/working-with-buildx/)
- [Docker Multi-Arch Builds](https://docs.docker.com/build/building/multi-platform/)

### Registries
- [Azure Container Registry](https://azure.microsoft.com/services/container-registry/)
- [Aliyun Container Registry](https://www.aliyun.com/product/acr)
- [Docker Hub](https://hub.docker.com/)

### HagiCode
- [HagiCode Documentation](https://hagicode.dev/docs)
