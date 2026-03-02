# HagiCode Release Repository

Automated release management for the HagiCode platform. This repository handles version monitoring, GitHub releases, and multi-architecture Docker image publishing to Edge ACR.

## Overview

The HagiCode release repository manages:

- **Version Monitoring**: Detects new versions in Azure Blob Storage
- **GitHub Releases**: Creates GitHub releases with application packages
- **Docker Multi-Arch Builds**: Builds and publishes Docker images for linux/amd64 and linux/arm64
- **Multi-Registry Publishing**: Pushes Docker images to Azure ACR, Aliyun ACR, and DockerHub
- **Publish Result Tracking**: Automatically generates and uploads publish result JSON files to GitHub Releases
- **Aliyun Image Sync**: Syncs Azure ACR images to Aliyun ACR with automatic version discovery
- **DockerHub Integration**: Syncs images to DockerHub with username-based path format

## Quick Start

### Prerequisites

- .NET 10 SDK
- Docker 20.10 or later with buildx support
- Azure Blob Storage access (SAS URL)
- GitHub personal access token (PAT)
- Edge ACR credentials (username, password, registry)
- jq (for JSON parsing in multi-arch verification)

### Building Locally

```bash
# Clone repository
git clone https://github.com/your-org/hagicode-release.git
cd hagicode-release

# Run Nuke build (use version without v prefix)
./build.sh DockerRelease \
  --ReleaseVersion "1.2.3" \
  --AzureBlobSasUrl "https://..." \
  --AzureAcrRegistry "hagicode.azurecr.io" \
  --AzureAcrUsername "username" \
  --AzureAcrPassword "password" \
  --DockerPlatform "all"
```

### Version Format Requirements

**Important**: Version numbers follow semantic versioning (semver) format.

- **Recommended**: `1.2.3`, `1.2.3-beta.1` (without "v" prefix)
- **Accepted**: `v1.2.3`, `v1.2.3-beta.1` (with "v" prefix, for backward compatibility)
- **Incorrect**: `1.2`, `1.2.3 beta`, `latest`

**Note**: Version numbers with and without "v" prefix are functionally equivalent (e.g., `1.2.3` = `v1.2.3`). The system treats them as the same version for comparison and download purposes.

Version format is validated automatically in:
1. Version Monitor (skips invalid versions with warning)
2. Docker Build workflow (fails with error message)

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
| `DockerPush` | Push Docker images to configured registries | DockerBuild |
| `DockerRelease` | Build and push Docker images to configured registries | DockerPush |

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
hagicode/hagicode:1.2.3         - Application image with version tag
hagicode/hagicode:1.2            - Application image with minor version tag
hagicode/hagicode:1              - Application image with major version tag
hagicode/hagicode:latest          - Application image with latest tag
```

**Note**: The Docker image uses a unified multi-stage build. Base tools (Node.js, .NET runtime, CLI tools) and application code are combined in a single image. No separate base image is pushed to registries.

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

### Pulling from DockerHub

```bash
# Login to DockerHub
docker login -u <username> -p <token>

# Pull image
docker pull newbe36524/hagicode:1.2.3

# Run container
docker run -d -p 5000:5000 newbe36524/hagicode:1.2.3
```

### Pulling from Aliyun ACR

```bash
# Login to Aliyun ACR
docker login registry.cn-hangzhou.aliyuncs.com -u <username> -p <password>

# Pull image
docker pull registry.cn-hangzhou.aliyuncs.com/hagicode/hagicode:1.2.3

# Run container
docker run -d -p 5000:5000 registry.cn-hangzhou.aliyuncs.com/hagicode/hagicode:1.2.3
```

## GitHub Actions Workflows

### Automated Triggers

The repository includes GitHub Actions workflows that are triggered automatically:

| Workflow | Trigger | Description |
|----------|----------|-------------|
| `version-monitor.yml` | Push to main, Schedule (every 4 hours) | Monitors Azure Blob for new versions |
| `github-release-workflow.yml` | Repository dispatch (`version-monitor-release`) | Creates GitHub releases |
| `docker-build.yml` | Tag push (v*.*.*), Repository dispatch (`version-monitor-docker`) | Builds and publishes Docker images to Azure ACR, Aliyun ACR, and DockerHub |

### Publish Result Tracking

The `docker-build.yml` workflow automatically generates a publish result JSON file after successfully publishing images to both Azure ACR and Aliyun ACR. This file is uploaded to the GitHub Release and contains:

- **Version**: The semantic version of the release
- **Published At**: ISO 8601 timestamp of publish time
- **Azure Registry**: The Azure Container Registry URL
- **Azure Images**: Array of published image URLs (base, version, major/minor, major, latest tags)
- **Aliyun Registry**: The Aliyun Container Registry URL
- **Aliyun Images**: Array of published image URLs to Aliyun ACR
- **GitHub Run Info**: Workflow run ID and URL for traceability

The publish result file is named `publish-results-{version}.json` and can be found in the Release's download section.

### Additional Registry Integration

The build system supports pushing images to multiple registries after successfully building and pushing to Azure ACR. The workflow automatically pushes to:

1. **Aliyun ACR**: Pushes images from Azure ACR to Aliyun ACR using `docker buildx imagetools create`
2. **DockerHub**: Pushes images from Azure ACR to DockerHub using username-based path format

#### Aliyun ACR Push Process

1. **Logs in to Aliyun ACR** using credentials from GitHub Secrets
2. **Retags and pushes images** from Azure ACR to Aliyun ACR using `docker buildx imagetools create`
3. **Generates version tags** (major.minor, major) for both registries
4. **Handles pre-release versions** - skips `latest` tag for pre-release versions (rc, beta, alpha, preview, dev)

#### DockerHub Push Process

1. **Logs in to DockerHub** using credentials from GitHub Secrets
2. **Retags and pushes images** from Azure ACR to DockerHub using `docker buildx imagetools create`
3. **Generates version tags** (major.minor, major) for both registries
4. **Handles pre-release versions** - skips `latest` tag for pre-release versions

#### Image Tags

For **stable releases** (e.g., `1.2.3`):
- `1.2.3` - Full version
- `1.2` - Major.minor
- `1` - Major
- `latest` - Latest stable

For **pre-release versions** (e.g., `1.2.3-rc.1`):
- `1.2.3-rc.1` - Full version
- `1.2` - Major.minor
- `1` - Major
- (no `latest` tag)

#### Requirements

**For Aliyun ACR**, configure the following GitHub Secrets:
- `ALIYUN_ACR_USERNAME` - Aliyun ACR username
- `ALIYUN_ACR_PASSWORD` - Aliyun ACR password

**For DockerHub**, configure the following GitHub Secrets:
- `DOCKERHUB_USERNAME` - DockerHub username
- `DOCKERHUB_TOKEN` - DockerHub access token (created at https://hub.docker.com/settings/security)

#### Publish Result JSON Schema

```json
{
  "version": "1.2.3",
  "publishedAt": "2024-01-01T00:00:00Z",
  "github": {
    "runId": "1234567890",
    "runUrl": "https://github.com/.../actions/runs/...",
    "repository": "owner/repo"
  },
  "azure": {
    "registry": "hagicode.azurecr.io",
    "images": [
      {
        "name": "hagicode",
        "tag": "base",
        "fullUrl": "hagicode.azurecr.io/hagicode:base"
      },
      {
        "name": "hagicode",
        "tag": "1.2.3",
        "fullUrl": "hagicode.azurecr.io/hagicode:1.2.3"
      },
      {
        "name": "hagicode",
        "tag": "1.2",
        "fullUrl": "hagicode.azurecr.io/hagicode:1.2"
      },
      {
        "name": "hagicode",
        "tag": "1",
        "fullUrl": "hagicode.azurecr.io/hagicode:1"
      },
      {
        "name": "hagicode",
        "tag": "latest",
        "fullUrl": "hagicode.azurecr.io/hagicode:latest"
      }
    ]
  },
  "aliyun": {
    "registry": "registry.cn-hangzhou.aliyuncs.com",
    "images": [
      {
        "name": "hagicode",
        "tag": "base",
        "fullUrl": "registry.cn-hangzhou.aliyuncs.com/hagicode:base"
      },
      {
        "name": "hagicode",
        "tag": "1.2.3",
        "fullUrl": "registry.cn-hangzhou.aliyuncs.com/hagicode:1.2.3"
      },
      {
        "name": "hagicode",
        "tag": "1.2",
        "fullUrl": "registry.cn-hangzhou.aliyuncs.com/hagicode:1.2"
      },
      {
        "name": "hagicode",
        "tag": "1",
        "fullUrl": "registry.cn-hangzhou.aliyuncs.com/hagicode:1"
      },
      {
        "name": "hagicode",
        "tag": "latest",
        "fullUrl": "registry.cn-hangzhou.aliyuncs.com/hagicode:latest"
      }
    ]
  },
  "dockerhub": {
    "registry": "docker.io",
    "images": [
      {
        "name": "hagicode",
        "tag": "base",
        "fullUrl": "newbe36524/hagicode:base"
      },
      {
        "name": "hagicode",
        "tag": "1.2.3",
        "fullUrl": "newbe36524/hagicode:1.2.3"
      },
      {
        "name": "hagicode",
        "tag": "1.2",
        "fullUrl": "newbe36524/hagicode:1.2"
      },
      {
        "name": "hagicode",
        "tag": "1",
        "fullUrl": "newbe36524/hagicode:1"
      },
      {
        "name": "hagicode",
        "tag": "latest",
        "fullUrl": "newbe36524/hagicode:latest"
      }
    ]
  }
}
```

### Repository Dispatch Events

The workflows can be triggered programmatically via repository_dispatch events:

| Event Type | Triggered By | Payload Required |
|------------|---------------|------------------|
| `version-monitor-release` | Version Monitor | `{"version": "1.2.3"}` |
| `version-monitor-docker` | Version Monitor | `{"version": "1.2.3"}` |

Example trigger via GitHub CLI:
```bash
# Trigger GitHub Release
gh api --method POST repos/{owner}/{repo}/dispatches \
  -F event_type='version-monitor-release' \
  -F client_payload='{"version": "1.2.3"}'

# Trigger Docker Build
gh api --method POST repos/{owner}/{repo}/dispatches \
  -F event_type='version-monitor-docker' \
  -F client_payload='{"version": "1.2.3"}'
```

### Manual Workflow Trigger

You can trigger workflows manually from the GitHub Actions UI:

#### Docker Build

1. Go to **Actions** tab
2. Select **Docker Multi-Arch Build and Push to Edge ACR**
3. Click **Run workflow**
4. Configure:
   - **Version**: `1.2.3`
   - **Platform**: `all`, `linux-amd64`, or `linux-arm64`
   - **Dry Run**: Enable to test without publishing

#### Aliyun Image Sync

1. Go to **Actions** tab
2. Select **Azure to Aliyun Image Sync**
3. Click **Run workflow**
4. Configure:
   - **VERSION**: Optional version to sync (e.g., `v1.2.3` or `1.2.3`)
   - Leave empty to sync the latest release automatically

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

#### Optional: DockerHub Configuration

To push images to DockerHub, configure the following:

```bash
export DOCKERHUB_USERNAME="your-dockerhub-username"
export DOCKERHUB_TOKEN="your-dockerhub-access-token"
```

**Note**: DockerHub access tokens can be created at https://hub.docker.com/settings/security

#### Optional: Aliyun ACR Configuration

To push images to Aliyun ACR, configure the following:

```bash
export ALIYUN_ACR_USERNAME="your-aliyun-username"
export ALIYUN_ACR_PASSWORD="your-aliyun-password"
export ALIYUN_ACR_REGISTRY="registry.cn-hangzhou.aliyuncs.com"
export ALIYUN_ACR_NAMESPACE="hagicode"
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
| `docker_deployment/Dockerfile.template` | Unified multi-stage build template | multi-arch |
| `docker_deployment/docker-entrypoint.sh` | Container entrypoint | all |
| `docker_deployment/.dockerignore` | Build exclusions | all |

### Build Process

1. **QEMU Setup**: Install binfmt for cross-architecture builds
2. **Buildx Builder**: Create Docker buildx builder for multi-arch
3. **Unified Image Build**: Build unified Docker image using multi-stage build (base stage + final stage in single Dockerfile)
4. **Dockerfile Generation**: Generate Dockerfile from template with version injection
5. **Azure ACR Push**: Push unified images to Azure ACR with version tags
6. **Multi-Architecture Verification**: Verify images contain both amd64 and arm64 architectures
7. **Additional Registry Pushes**: Replicate images to Aliyun ACR and DockerHub with verification

### Multi-Registry Image Publishing

The build system uses an adapter pattern to support pushing images to multiple container registries:

| Registry | Path Format | Example |
|----------|-------------|---------|
| Azure ACR | `{registry}/{image}` | `hagicode.azurecr.io/hagicode` |
| Aliyun ACR | `{registry}/{namespace}/{image}` | `registry.cn-hangzhou.aliyuncs.com/hagicode/hagicode` |
| DockerHub | `{username}/{image}` | `newbe36524/hagicode` |

Registry pushes are configured independently - only configured registries will be used.

### Multi-Architecture Verification

All published Docker images are verified to contain both linux/amd64 and linux/arm64 architectures:

- **Azure ACR**: After push, `Verify Images in Registry` step checks manifest for both architectures
- **Aliyun ACR**: After sync, `Verify Images in Aliyun ACR` step verifies all pushed tags

If either architecture is missing, the workflow fails with a clear error message:
```
Error: Tag 1.2.3 does not contain amd64 architecture
```

This ensures users on both AMD64 and ARM64 platforms can pull compatible images.

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

- [MIGRATION.md](MIGRATION.md) - Release process migration guide
- [AGENTS.md](AGENTS.md) - AI agents and integration
- [ENVIRONMENT_VARIABLES.md](ENVIRONMENT_VARIABLES.md) - Complete environment variable reference

## License

See [LICENSE](LICENSE) file for details.

## Support

For issues or questions:

- Check [GitHub Issues](https://github.com/newbe36524/hagicode-release/issues)
- Review [AGENTS.md](AGENTS.md) for AI agent issues
- Consult [ENVIRONMENT_VARIABLES.md](ENVIRONMENT_VARIABLES.md) for configuration help
