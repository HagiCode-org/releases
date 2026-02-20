# HagiCode Release - Agent Configuration

## Root Configuration
Inherits all behavior from `/AGENTS.md` at monorepo root.

## Project Context

HagiCode Release is a **release-only repository** for distributing pre-built HagiCode packages to multiple channels. This repository does not contain source code - it handles:

- Downloading pre-built packages from Azure Blob Storage
- Building and publishing Docker images to multiple registries:
  - Docker Hub (`docker.io/newbe36524/hagicode`)
  - Azure Container Registry (`hagicode.azurecr.io`)
  - Alibaba Cloud Container Registry (`registry.cn-hangzhou.aliyuncs.com`)
- Creating GitHub Releases with downloadable artifacts
- Automated release workflow via GitHub Actions

## Tech Stack

### Build Framework
- **Nuke**: C# based build automation system
- **Docker**: Container image building and publishing
- **GitHub Actions**: CI/CD automation

### Development Requirements
- **.NET 10 SDK**: For running Nuke build
- **Docker**: For building and pushing images

## Project Structure

```
├── .github/
│   └── workflows/
│       ├── hagicode-server-publish.yml  # Main publish workflow
│       ├── version-monitor.yml          # Automated version monitoring
│       └── docker-build.yml             # Docker build workflow
├── nukeBuild/
│   ├── _build.csproj                    # Nuke build project
│   ├── Build.cs                         # Main build definition
│   ├── Build.Partial.cs                 # Shared properties and CI integration
│   ├── Build.Targets.Download.cs        # Download from Azure
│   ├── Build.Targets.Extract.cs         # Extract packages
│   ├── Build.Targets.Docker.cs          # Build/push Docker images
│   ├── Build.Targets.GitHub.cs          # Create GitHub releases
│   └── Adapters/
│       └── AzureBlobAdapter.cs          # Azure download adapter
├── docker_deployment/
│   ├── Dockerfile                       # Container image definition
│   └── .dockerignore                    # Build exclusions
├── ReleaseScripts/
│   └── release_config.yml               # Release configuration
├── docker-compose.yml                   # Development compose
└── .env.example                         # Environment template
```

## Agent Behavior

When working in the hagicode-release submodule:

1. **Release automation only**: No source code development here
2. **Nuke-based builds**: All operations use Nuke build targets
3. **Multi-registry publishing**: Docker images go to 3 registries
4. **Automated version monitoring**: Checks Azure every 4 hours
5. **GitHub Releases**: All releases create GitHub release artifacts

### Development Workflow
```bash
cd repos/hagicode-release

# Install Nuke global tool
dotnet tool install Nuke.GlobalTool --global

# List all available targets
nuke

# Download package from Azure
nuke Download --AzureBlobSasUrl "<your-sas-url>" --Version v1.0.0

# Build Docker image
nuke DockerBuild --Version v1.0.0

# Push to all registries
nuke DockerPushAll --Version v1.0.0

# Full release pipeline
nuke Release --Version v1.0.0
```

### Release Process

**Automated (Recommended)**:
- Version monitor runs every 4 hours
- Checks Azure Blob Storage for new versions
- Automatically triggers release workflow

**Manual**:
- Push version tag: `git tag v1.0.0 && git push origin v1.0.0`
- Or use workflow dispatch in GitHub Actions

## Specific Conventions

### Version Tagging
Docker images are tagged with:
- Full version: `v1.2.3`
- Minor version: `v1.2`
- Major version: `v1`
- `latest`: Most recent stable (not pre-releases)

### Environment Variables
Required for releases:
- `AZURE_BLOB_SAS_URL` - Azure Blob Storage SAS URL
- `DOCKER_USERNAME` / `DOCKER_PASSWORD` - Docker Hub credentials
- `ALIYUN_ACR_USERNAME` / `ALIYUN_ACR_PASSWORD` - Aliyun credentials
- `AZURE_ACR_USERNAME` / `AZURE_ACR_PASSWORD` - Azure ACR credentials
- `GITHUB_TOKEN` - GitHub API token (auto in CI)
- `FEISHU_WEBHOOK_URL` - Optional notifications

### Registry Configuration
- **Docker Hub**: `docker.io/newbe36524/hagicode`
- **Azure ACR**: `hagicode.azurecr.io/hagicode`
- **Aliyun ACR**: `registry.cn-hangzhou.aliyuncs.com/hagicode/hagicode`

## Disabled Capabilities

AI assistants should NOT suggest:
- **Source code development**: This repo only handles releases
- **Application logic**: No business logic code here
- **Frontend/UI changes**: No user interface
- **Database changes**: No database schema or migrations
- **API endpoints**: No REST API development (except release triggers)
- **Test writing**: No application tests (only build/release validation)

## References

- **Root AGENTS.md**: `/AGENTS.md` at monorepo root
- **Monorepo CLAUDE.md**: See root directory for monorepo-wide conventions
- **OpenSpec Workflow**: Proposal-driven development happens at monorepo root level (`/openspec/`)
- **README**: `repos/hagicode-release/README.md`
- **Container Environment Variables**: `repos/hagicode-release/docs/container-environment-variables.md`
