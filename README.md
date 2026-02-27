# HagiCode Release Repository

Automated release management for the HagiCode platform. This repository handles version monitoring, GitHub releases, and multi-architecture Docker image publishing to Edge ACR.

## Overview

The HagiCode release repository manages:

- **Version Monitoring**: Detects new versions in Azure Blob Storage
- **GitHub Releases**: Creates GitHub releases with application packages
- **Docker Multi-Arch Builds**: Builds and publishes Docker images for linux/amd64 and linux/arm64
- **Edge ACR Publishing**: Pushes Docker images to Azure Container Registry

## Quick Start

### Prerequisites

- .NET 10 SDK
- Docker 20.10 or later with buildx support
- Azure Blob Storage access (SAS URL)
- GitHub personal access token (PAT)
- Edge ACR credentials (username, password, registry)

### Building Locally

```bash
# Clone repository
git clone https://github.com/your-org/hagicode-release.git
cd hagicode-release

# Run Nuke build
./build.sh DockerRelease \
  --ReleaseVersion "1.2.3" \
  --AzureBlobSasUrl "https://..." \
  --AzureAcrRegistry "hagicode.azurecr.io" \
  --AzureAcrUsername "username" \
  --AzureAcrPassword "password" \
  --DockerPlatform "all"
```

## Build Targets

### Available Targets

| Target | Description | Dependencies |
|--------|-------------|--------------|
| `Clean` | Clean output directories | - |
| `Restore` | Restore build dependencies | - |
| `Download` | Download packages from Azure Blob Storage | - |
| `VersionMonitor` | Monitor Azure Blob for new versions | - |
| `GitHubRelease` | Create GitHub release with packages | Download |
| `DockerBuild` | Build Docker images (local only) | Download |
| `DockerPush` | Push Docker images to Edge ACR | DockerBuild |
| `DockerRelease` | Build and push Docker images to Edge ACR | DockerPush |

### Docker Build Targets

#### Build Single Architecture (AMD64)

```bash
./build.sh DockerBuild \
  --ReleaseVersion "1.2.3" \
  --DockerPlatform "linux-amd64"
```

#### Build Single Architecture (ARM64)

```bash
./build.sh DockerBuild \
  --ReleaseVersion "1.2.3" \
  --DockerPlatform "linux-arm64"
```

#### Build Multi-Architecture (Both)

```bash
./build.sh DockerRelease \
  --ReleaseVersion "1.2.3" \
  --DockerPlatform "all"
```

## Docker Images

### Image Structure

```
hagicode/hagicode:base          - Base image (AMD64)
hagicode/hagicode:base-arm64    - Base image (ARM64 variant)
hagicode/hagicode:1.2.3         - Application image with version tag
hagicode/hagicode:1.2            - Application image with minor version tag
hagicode/hagicode:1              - Application image with major version tag
hagicode/hagicode:latest          - Application image with latest tag
```

### Pulling from Edge ACR

```bash
# Login to Edge ACR
docker login hagicode.azurecr.io -u <username> -p <password>

# Pull image
docker pull hagicode.azurecr.io/hagicode:1.2.3

# Run container
docker run -d -p 5000:5000 hagicode.azurecr.io/hagicode:1.2.3
```

### Running with Claude Code Configuration

```bash
docker run -d \
  -p 5000:5000 \
  -e ANTHROPIC_AUTH_TOKEN="sk-ant-..." \
  -v ~/claude-config:/claude-mount \
  hagicode.azurecr.io/hagicode:1.2.3
```

## GitHub Actions Workflows

### Automated Triggers

The repository includes GitHub Actions workflows that are triggered automatically:

| Workflow | Trigger | Description |
|----------|----------|-------------|
| `version-monitor.yml` | Push to main | Monitors Azure Blob for new versions |
| `github-release-workflow.yml` | Repository dispatch | Creates GitHub releases |
| `docker-build.yml` | Tag push (v*.*.*) | Builds and publishes Docker images |

### Manual Workflow Trigger

You can trigger workflows manually from the GitHub Actions UI:

1. Go to **Actions** tab
2. Select **Docker Multi-Arch Build and Push to Edge ACR**
3. Click **Run workflow**
4. Configure:
   - **Version**: `1.2.3`
   - **Platform**: `all`, `linux-amd64`, or `linux-arm64`
   - **Dry Run**: Enable to test without publishing

## Configuration

### Required Environment Variables

See [ENVIRONMENT_VARIABLES.md](ENVIRONMENT_VARIABLES.md) for detailed configuration.

#### Minimum Required Variables

```bash
export AZURE_BLOB_SAS_URL="https://hagicode.blob.core.windows.net/packages?sp=..."
export GITHUB_TOKEN="ghp_xxxxxxxxxxxxxxxxxxxx"
export AZURE_ACR_USERNAME="hagicode"
export AZURE_ACR_PASSWORD="password"
export AZURE_ACR_REGISTRY="hagicode.azurecr.io"
```

### Nuke Parameters

Pass parameters directly to Nuke:

```bash
./build.sh DockerRelease \
  --ReleaseVersion "1.2.3" \
  --DockerPlatform "all" \
  --DockerImageName "hagicode/hagicode"
```

## AI Agents Integration

### Supported Agents

The Docker images include pre-installed AI agents:

- **Claude Code CLI**: Version 2.1.34
- **OpenSpec CLI**: Version >=1.0.0 <2.0.0
- **UIPro CLI**: Version 2.1.3

See [AGENTS.md](AGENTS.md) for detailed documentation.

### Claude Code Configuration

Claude Code can be configured using environment variables:

```bash
docker run -e ANTHROPIC_AUTH_TOKEN="sk-ant-..." hagicode.azurecr.io/hagicode:1.2.3
```

Alternatively, mount host configuration:

```bash
docker run -v ~/claude-config:/claude-mount hagicode.azurecr.io/hagicode:1.2.3
```

## Docker Build Infrastructure

### Dockerfiles

| File | Purpose | Platform |
|------|-----------|----------|
| `docker_deployment/Dockerfile.base` | Base image for AMD64 | linux/amd64 |
| `docker_deployment/Dockerfile.base.arm64` | Base image for ARM64 | linux/arm64 |
| `docker_deployment/Dockerfile.app.template` | Application image template | multi-arch |
| `docker_deployment/docker-entrypoint.sh` | Container entrypoint | all |
| `docker_deployment/.dockerignore` | Build exclusions | all |

### Build Process

1. **QEMU Setup**: Install binfmt for cross-architecture builds
2. **Buildx Builder**: Create Docker buildx builder for multi-arch
3. **Base Image Build**: Build base image for target platforms
4. **App Dockerfile Generation**: Generate Dockerfile from template with version injection
5. **Application Image Build**: Build application image with base image
6. **Edge ACR Push**: Push images to registry with version tags
7. **Verification**: Verify images are available in registry

### Version Tagging Strategy

Images are tagged with multiple versions:

- **Full Version**: `1.2.3` (exact version)
- **Minor Version**: `1.2` (major.minor)
- **Major Version**: `1` (major)
- **Latest**: `latest` (always points to newest)

Example tags for version 1.2.3:
```
hagicode.azurecr.io/hagicode:1.2.3
hagicode.azurecr.io/hagicode:1.2
hagicode.azurecr.io/hagicode:1
hagicode.azurecr.io/hagicode:latest
```

## Troubleshooting

### Docker Build Fails

**Issue**: `docker: Error response from daemon: unknown`

**Solution**:
1. Verify Docker is running: `docker ps`
2. Check Docker version: `docker --version` (requires >= 20.10)
3. Enable buildx: `docker buildx version`

### QEMU Setup Fails

**Issue**: `Failed to setup QEMU for cross-architecture builds`

**Solution**:
```bash
# Manually install binfmt
docker run --privileged --rm tonistiigi/binfmt --install all
```

### Edge ACR Push Fails

**Issue**: `unauthorized: authentication required`

**Solution**:
1. Verify credentials are correct
2. Login manually: `docker login hagicode.azurecr.io`
3. Check token has not expired

### Image Not Available After Push

**Issue**: Registry verification fails after push

**Solution**:
1. Wait for registry propagation (may take 1-2 minutes)
2. Check image exists: `docker manifest inspect hagicode.azurecr.io/hagicode:1.2.3`
3. Verify image digest matches push output

## Development

### Build System (Nuke)

This repository uses [Nuke](https://nuke.build/) for build automation.

- **Definition**: `nukeBuild/Build.cs`
- **Targets**: Split across multiple files (Targets.*.cs)
- **Partial Classes**: State and helpers in `Build.Partial.cs`

### Adding New Targets

To add a new build target:

1. Create `nukeBuild/Build.Targets.YourTarget.cs`
2. Define target:
```csharp
Target YourTarget => _ => _
    .DependsOn(Download)
    .Executes(() => {
        // Your implementation
    });
```
3. Run with: `./build.sh YourTarget`

## Contributing

When contributing to this repository:

1. Follow existing code style and patterns
2. Update documentation for new features
3. Test with both AMD64 and ARM64 platforms
4. Ensure environment variables are documented
5. Update AGENTS.md when changing AI agent versions

## Documentation

- [AGENTS.md](AGENTS.md) - AI agents and integration
- [ENVIRONMENT_VARIABLES.md](ENVIRONMENT_VARIABLES.md) - Complete environment variable reference

## License

See [LICENSE](LICENSE) file for details.

## Support

For issues or questions:

- Check [GitHub Issues](https://github.com/newbe36524/hagicode-release/issues)
- Review [AGENTS.md](AGENTS.md) for AI agent issues
- Consult [ENVIRONMENT_VARIABLES.md](ENVIRONMENT_VARIABLES.md) for configuration help
