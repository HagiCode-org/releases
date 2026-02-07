# Hagicode Release

A **release-only repository** for the Hagicode project. This repository handles distribution of pre-built packages to multiple channels (Docker Hub, GitHub Releases).

## Overview

This repository:
- Downloads pre-built packages from Azure Blob Storage
- Builds and publishes Docker images to Docker Hub
- Creates GitHub Releases with downloadable artifacts
- Automates the entire release workflow via GitHub Actions

## Quick Start

### Using Docker Images

```bash
# Pull the latest image
docker pull newbe36524/hagicode:latest

# Run with default configuration
docker run -p 5000:5000 newbe36524/hagicode:latest

# Run with docker-compose
docker-compose up -d
```

### Creating a Release

1. Push a version tag to trigger the release workflow:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

2. GitHub Actions will automatically:
   - Download the package from Azure Blob Storage
   - Build Docker images
   - Push to Docker Hub with version tags
   - Create a GitHub Release with artifacts

## Architecture

```
Azure Blob Storage -> Download Package -> Docker Build -> Docker Hub
                                              -> GitHub Release
```

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

# Build Docker image
nuke DockerBuild --Version v1.0.0

# Push to Docker Hub
nuke DockerPush --Version v1.0.0 --DockerUsername <user> --DockerPassword <pass>

# Create GitHub Release
nuke GitHubRelease --Version v1.0.0 --GitHubToken <token> --GitHubRepository owner/repo

# Full release pipeline
nuke Release --Version v1.0.0
```

## Configuration

### Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `AZURE_BLOB_SAS_URL` | Azure Blob Storage SAS URL with Read permissions | Yes |
| `DOCKER_USERNAME` | Docker Hub username | Yes |
| `DOCKER_PASSWORD` | Docker Hub password/token | Yes |
| `GITHUB_TOKEN` | GitHub API token | Auto in CI |
| `DOCKER_REGISTRY` | Docker registry (default: docker.io/newbe36524) | Optional |
| `DOCKER_IMAGE_NAME` | Docker image name (default: hagicode) | Optional |

### Azure Blob Storage Setup

1. Navigate to your Storage Account in Azure Portal
2. Generate a SAS URL for the container:
   - Allowed services: Blob
   - Allowed permissions: Read
   - Start/End dates: Set appropriate expiry
3. Copy the generated SAS URL to `.env` or GitHub Secrets

### GitHub Secrets

Configure these in your repository settings (Settings > Secrets and variables > Actions):

- `AZURE_BLOB_SAS_URL` - Azure Blob Storage SAS URL
- `DOCKER_USERNAME` - Docker Hub username
- `DOCKER_PASSWORD` - Docker Hub access token

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
│       └── release.yml          # GitHub Actions workflow
├── nukeBuild/
│   ├── _build.csproj            # Nuke build project
│   ├── Build.cs                 # Main build definition
│   ├── configuration.csharp     # Build parameters
│   └── Adapters/
│       └── AzureBlobAdapter.cs  # Azure download adapter
├── docker_deployment/
│   ├── Dockerfile               # Container image definition
│   └── .dockerignore            # Build context exclusions
├── ReleaseScripts/
│   └── release_config.yml       # Release configuration
├── docker-compose.yml           # Development compose file
└── .env.example                 # Environment template
```

## Build Framework

This project uses **Nuke** for build automation. Nuke provides:
- Cross-platform support (Windows, Linux, macOS)
- C# based build definitions
- Modular, composable targets
- CI/CD integration

## Troubleshooting

### Download Fails

- Verify `AZURE_BLOB_SAS_URL` is valid and not expired
- Check the version exists in Azure Blob Storage
- Ensure SAS URL has Read permissions

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

## License

See [LICENSE](LICENSE) for details.
