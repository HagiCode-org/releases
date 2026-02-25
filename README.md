# Hagicode Release

A **release-only repository** for the Hagicode project. This repository handles distribution of pre-built packages to multiple channels (Docker Hub, Azure Container Registry, Alibaba Cloud Container Registry, GitHub Releases).

## Overview

This repository:
- Downloads pre-built packages from Azure Blob Storage
- Builds and publishes Docker images to multiple registries:
  - Docker Hub (`docker.io/newbe36524/hagicode`)
  - Azure Container Registry (`hagicode.azurecr.io`)
  - Alibaba Cloud Container Registry (`registry.cn-hangzhou.aliyuncs.com`)
- Creates GitHub Releases with downloadable artifacts
- Automates the entire release workflow via GitHub Actions

## Quick Start

### Using Docker Images

```bash
# Pull from Docker Hub (default)
docker pull newbe36524/hagicode:latest

# Pull from Azure Container Registry
docker pull hagicode.azurecr.io/hagicode:latest

# Pull from Alibaba Cloud Container Registry
docker pull registry.cn-hangzhou.aliyuncs.com/hagicode/hagicode:latest

# Run with default configuration
docker run -p 5000:5000 newbe36524/hagicode:latest

# Run with docker-compose
docker-compose up -d
```

### Creating a Release

#### Automated Release (Recommended)

The repository includes an automated version monitor that:
- Runs every 4 hours to check for new versions on Azure Blob Storage
- Compares Azure versions with existing GitHub Releases
- Automatically triggers the release workflow for new versions

No manual intervention required - new versions are detected and released automatically.

#### Manual Release

You can still create releases manually:

1. **Push a version tag** (triggers workflow):
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

2. **Or use workflow dispatch**:
   - Go to Actions > hagicode-server-publish
   - Click "Run workflow"
   - Enter the version (e.g., `1.0.0` or `v1.0.0`)

3. GitHub Actions will automatically:
   - Download the package from Azure Blob Storage
   - Build Docker images
   - Push to all configured registries (Docker Hub, Azure ACR, Aliyun ACR) with version tags
   - Create a GitHub Release with artifacts

## Architecture

```
Azure Blob Storage -> Download Package -> Docker Build -> Docker Hub
                                              -> Azure ACR
                                              -> Aliyun ACR
                                              -> GitHub Release
```

### Automated Version Monitoring

The repository includes an automated version monitoring system:

```
┌─────────────────┐
│ Version Monitor │
│ (Every 4 hours) │
└────────┬────────┘
         │
         ▼
    ┌────────────────┐
    │ Check Azure    │
    │ index.json     │
    └────────┬───────┘
             │
             ▼
      ┌──────────────────┐
      │ Compare with     │
      │ GitHub Releases  │
      └────────┬─────────┘
               │
         ▼────────▼
   New version?  No new version
    │              │
    ▼              ▼
Trigger Release  Exit
    │
    ▼
┌─────────────────────┐
│ Publish Workflow    │
│ - Create Release    │
│ - Download Package  │
│ - Build Images      │
│ - Push to Registries│
└─────────────────────┘
```

**Features**:
- Automatic detection of new versions on Azure Blob Storage
- Comparison with existing GitHub Releases
- Automatic triggering of the release workflow
- Failure notifications via GitHub Issues
- Manual trigger support for testing

## Local Development

### Prerequisites

- .NET 10 SDK
- Docker
- Nuke Global Tool

### Setup

1. Install Nuke global tool:
   ```bash
   dotnet tool install Nuke.GlobalTool --global
   ```

2. Configure environment variables (copy `.env.example` to `.env`):
   ```bash
   cp .env.example .env
   # Edit .env with your values
   ```

### Running Build Targets

```bash
# List all available targets
nuke

# Download package from Azure
nuke Download --AzureBlobSasUrl "<your-sas-url>" --Version v1.0.0

# Download all channels' latest versions (builds latest for beta, stable, etc.)
nuke Download --AzureBlobSasUrl "<your-sas-url>" --BuildAllChannels

# Build Docker image
nuke DockerBuild --Version v1.0.0

# Push to Docker Hub
nuke DockerPush --Version v1.0.0 --DockerUsername <user> --DockerPassword <pass>

# Push to Azure Container Registry
nuke DockerPushAzure --Version v1.0.0 --AzureAcrUsername <user> --AzureAcrPassword <pass>

# Push to Aliyun Container Registry
nuke DockerPushAliyun --Version v1.0.0 --AliyunAcrUsername <user> --AliyunAcrPassword <pass>

# Push to all registries
nuke DockerPushAll --Version v1.0.0

# Create GitHub Release
nuke GitHubRelease --Version v1.0.0 --GitHubToken <token> --GitHubRepository owner/repo

# Full release pipeline (pushes to all registries + GitHub release)
nuke Release --Version v1.0.0
```

## Configuration

> **Environment Variables Reference**: For comprehensive documentation of all supported environment variables, see [Container Environment Variables Documentation](docs/container-environment-variables.md).

### Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `AZURE_BLOB_SAS_URL` | Azure Blob Storage SAS URL with Read permissions | Yes |
| `DOCKER_USERNAME` | Docker Hub username | Yes |
| `DOCKER_PASSWORD` | Docker Hub password/token | Yes |
| `ALIYUN_ACR_USERNAME` | Aliyun ACR username | Yes |
| `ALIYUN_ACR_PASSWORD` | Aliyun ACR password/token | Yes |
| `ALIYUN_ACR_REGISTRY` | Aliyun ACR registry (default: registry.cn-hangzhou.aliyuncs.com) | Optional |
| `AZURE_ACR_USERNAME` | Azure ACR username | Yes |
| `AZURE_ACR_PASSWORD` | Azure ACR password/token | Yes |
| `AZURE_ACR_REGISTRY` | Azure ACR registry (default: hagicode.azurecr.io) | Optional |
| `GITHUB_TOKEN` | GitHub API token | Auto in CI |
| `DOCKER_REGISTRY` | Docker registry (default: docker.io/newbe36524) | Optional |
| `DOCKER_IMAGE_NAME` | Docker image name (default: hagicode) | Optional |
| `DOCKER_VERIFY_MAX_RETRIES` | Max retry attempts for base image verification (default: 5, range: 1-20) | Optional |

### Docker Image Availability Verification

For multi-architecture builds, the system automatically:

1. **Builds and pushes the base image** to the registry immediately after completion
2. **Verifies base image availability** before starting the application build
3. **Implements retry logic** with exponential backoff to handle registry propagation delays

**Default retry behavior:**
- Initial delay: 2 seconds
- Max retries: 5 attempts
- Backoff: Exponential (2s → 4s → 8s → 16s → 32s)
- Total max wait: ~62 seconds

**Configuration:**
Set the `DOCKER_VERIFY_MAX_RETRIES` environment variable to customize retry behavior (valid range: 1-20).

**Example:**
```bash
export DOCKER_VERIFY_MAX_RETRIES=10
nuke DockerBuild --Platform=all
```

### Azure Blob Storage Setup

1. Navigate to your Storage Account in Azure Portal
2. Generate a SAS URL for the container:
   - Allowed services: Blob
   - Allowed permissions: Read
   - Start/End dates: Set appropriate expiry
3. Copy the generated SAS URL to `.env` or GitHub Secrets

### GitHub Secrets

Configure these in your repository settings (Settings > Secrets and variables > Actions):

**Required for all releases:**
- `AZURE_BLOB_SAS_URL` - Azure Blob Storage SAS URL
- `DOCKER_USERNAME` - Docker Hub username
- `DOCKER_PASSWORD` - Docker Hub access token

**Required for multi-registry publishing:**
- `ALIYUN_ACR_USERNAME` - Aliyun Container Registry username
- `ALIYUN_ACR_PASSWORD` - Aliyun Container Registry password/token
- `AZURE_ACR_USERNAME` - Azure Container Registry username
- `AZURE_ACR_PASSWORD` - Azure Container Registry password/token

**Required for Feishu notifications (optional):**
- `FEISHU_WEBHOOK_URL` - Feishu webhook URL for release notifications

#### Feishu Notification Setup

This project uses the unified **haginotifier** workflow from the `HagiCode-org/haginotifier` repository for sending Feishu notifications. This provides a consistent notification mechanism across all HagiCode organization repositories.

To enable Feishu notifications for release workflows:

1. **Use the Organization-level Secret (Recommended)**:
   - The `FEISHU_WEBHOOK_URL` is configured at the organization level
   - All repositories within the organization can reuse it without managing their own webhook URL
   - Contact your organization administrator to configure access

2. **Or Configure Per-Repository**:
   - Go to your repository Settings > Secrets and variables > Actions
   - Create a new secret named `FEISHU_WEBHOOK_URL`
   - Paste your Feishu Webhook URL as the value

3. **Notification Behavior**:
   - **Release Workflow**: Sends notification on every release (success or failure)
   - **Version Monitor**: Sends notification only when new versions are found or on failure
   - Notifications are sent via the reusable [haginotifier](https://github.com/HagiCode-org/haginotifier) workflow
   - If webhook is not configured, workflows will continue to work without notifications

For more information about haginotifier, visit: https://github.com/HagiCode-org/haginotifier

## Version Tagging

Docker images are tagged with multiple versions:

| Tag | Description | Example |
|-----|-------------|---------|
| Full version | Exact version | `v1.2.3` |
| Minor version | Latest patch in minor | `v1.2` |
| Major version | Latest minor/patch in major | `v1` |
| `latest` | Most recent stable release | `latest` |

**Note**: Pre-release versions (alpha, beta, rc) do not update the `latest` tag.

## Project Structure

```
.
├── .github/
│   └── workflows/
│       ├── hagicode-server-publish.yml  # GitHub Actions publish workflow
│       ├── version-monitor.yml          # Automated version monitoring workflow
│       └── docker-build.yml             # Docker build workflow
├── nukeBuild/
│   ├── _build.csproj                    # Nuke build project
│   ├── Build.cs                         # Main build definition
│   ├── Build.Partial.cs                 # Shared properties and CI integration
│   ├── Build.Targets.Download.cs        # Download packages from Azure
│   ├── Build.Targets.Extract.cs         # Extract packages for Docker
│   ├── Build.Targets.Docker.cs          # Build and push Docker images
│   ├── Build.Targets.GitHub.cs          # Create GitHub releases
│   └── Adapters/
│       └── AzureBlobAdapter.cs          # Azure download adapter
├── docker_deployment/
│   ├── Dockerfile                       # Container image definition
│   └── .dockerignore                    # Build context exclusions
├── ReleaseScripts/
│   └── release_config.yml               # Release configuration
├── docker-compose.yml                   # Development compose file
└── .env.example                         # Environment template
```

## Build Framework

This project uses **Nuke** for build automation. Nuke provides:
- Cross-platform support (Windows, Linux, macOS)
- C# based build definitions
- Modular, composable targets
- CI/CD integration

## Troubleshooting

### Version Monitor Issues

**Monitor workflow fails to download index.json**:
- Verify `AZURE_BLOB_SAS_URL` is valid and not expired
- Check that index.json exists in the Azure Blob Storage container
- Ensure SAS URL has Read permissions for the container

**Monitor doesn't detect new versions**:
- Check the workflow logs to see what versions were found
- Verify the version format in Azure index matches expectations (e.g., "1.0.0")
- Ensure the GitHub Release for that version doesn't already exist

**Release workflow not triggered**:
- Check that the repository_dispatch event was created successfully
- Verify the event type "version-monitor-release" is correctly configured
- Check GitHub Actions logs for the monitor workflow

### Download Fails

- Verify `AZURE_BLOB_SAS_URL` is valid and not expired
- Check the version exists in Azure Blob Storage
- Ensure SAS URL has Read permissions
- When using `--BuildAllChannels`, verify multiple channels exist in the index

### Building Multiple Channels

The build system supports building Docker images for multiple channels simultaneously:

```bash
# Build and push latest versions for all channels (beta, stable, etc.)
nuke Download --AzureBlobSasUrl "<your-sas-url>" --BuildAllChannels
nuke DockerBuild --BuildAllChannels
nuke DockerPushAll --BuildAllChannels
```

**Notes:**
- Each channel will build with its latest version
- Channels are identified from version patterns (e.g., "beta" in "0.1.0-beta.15")
- The system automatically detects available channels from the Azure index
- Build summary shows which channels succeeded/failed

### Docker Build Fails

- Verify extracted package contains expected files
- Check Docker daemon is running
- Ensure base images can be pulled

### Docker Push Fails

- Verify `DOCKER_USERNAME` and `DOCKER_PASSWORD` are correct
- Ensure you have push permissions to the registry
- Check network connectivity

### GitHub Release Fails

- Verify `GITHUB_TOKEN` has `contents: write` permission
- Check the repository name is correct
- Ensure tag exists in the repository

## Manual Testing

To manually trigger the version monitor for testing:

1. Go to Actions > Version Monitor in the GitHub repository
2. Click "Run workflow"
3. Set `dry_run` to `true` to test without triggering releases
4. Click "Run workflow" to execute

This will help you verify:
- Azure index.json can be downloaded
- Versions are correctly parsed
- GitHub Releases API works correctly
- Version comparison logic functions properly

## License

See [LICENSE](LICENSE) for details.
