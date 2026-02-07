# Project Context

## Purpose

**Hagicode Release** is a **release-only repository** for the Hagicode project. This repository does not contain source code. Its primary purpose is to distribute pre-built packages to multiple channels:

- Download pre-built packages from Azure Blob Storage
- Build and publish Docker images to multiple container registries:
  - Docker Hub (`newbe36524/hagicode`)
  - Azure Container Registry (`hagicode.azurecr.io`)
  - Alibaba Cloud Container Registry (`registry.cn-hangzhou.aliyuncs.com`)
- Upload artifacts to GitHub Releases
- Automate the entire release workflow via GitHub Actions

This separation allows the main Hagicode project to focus on development while this repository handles distribution.

## Tech Stack

### Core Technologies
- **.NET 10 SDK** (v10.0.100) - Runtime framework for build tools
- **Nuke 10.1.0** - Cross-platform build automation system
- **Docker** - Container image building and publishing
- **GitHub Actions** - CI/CD automation

### Key Dependencies
- **Azure.Storage.Blobs 12.27.0** - Azure Blob Storage SDK for downloading packages
- **Nuke.Common 10.1.0** - Nuke build framework core library

### External Services
- **Azure Blob Storage** - Source of pre-built packages (.zip files)
- **Docker Hub** - Container registry (`docker.io/newbe36524`)
- **Azure Container Registry** - Container registry (`hagicode.azurecr.io`)
- **Alibaba Cloud Container Registry** - Container registry (`registry.cn-hangzhou.aliyuncs.com`)
- **GitHub Releases** - Release distribution platform

## Architecture

```
Azure Blob Storage → Download Package → Extract → Build Docker Image → Push to Docker Hub
                                                            ↓
                                                      Push to Azure ACR
                                                            ↓
                                                   Push to Aliyun ACR
                                                            ↓
                                                      Upload to GitHub Release
```

### Release Flow
1. Version tag (`v*.*.*`) pushed to repository
2. GitHub Actions triggers Nuke build workflow
3. Package downloaded from Azure Blob Storage
4. Package extracted for Docker build
5. Docker image built and pushed with multiple tags
6. Packages uploaded to GitHub Release (pre-existing release)

## Project Conventions

### Code Style
- **C# conventions**: Use `ImplicitUsings` and `Nullable` reference types
- **Build targets**: Organized in separate partial class files (e.g., `Build.Targets.Docker.cs`, `Build.Targets.Download.cs`)
- **Nuke patterns**: Follow Nuke's target-based build structure with dependencies

### Architecture Patterns
- **Separation of concerns**: Release-only repository with no source code
- **Adapter pattern**: `AzureBlobAdapter.cs` handles Azure-specific download logic
- **Modular targets**: Each build target in a separate partial class file
- **Multi-stage Docker builds**: Base image + application image template pattern

### Build Framework (Nuke)

Nuke provides cross-platform build automation with C# based definitions:

| Target | Description |
|--------|-------------|
| `Clean` | Clean output directory |
| `Restore` | Restore dependencies |
| `Download` | Download package from Azure Blob Storage |
| `Extract` | Extract downloaded .zip package |
| `DockerBuild` | Build container images |
| `DockerLogin` | Log in to Docker Hub |
| `DockerPush` | Push images to Docker Hub |
| `DockerPushAzure` | Push images to Azure Container Registry |
| `DockerPushAliyun` | Push images to Aliyun Container Registry |
| `DockerPushAll` | Push images to all configured registries |
| `GitHubRelease` | Upload packages to GitHub Release |
| `Release` | Full pipeline orchestrator (pushes to all registries + GitHub release) |

**Usage:**
```bash
nuke                          # List all targets
nuke Download --Version v1.0.0
nuke Release --Version v1.0.0
```

### GitHub Actions Workflows

The repository uses two GitHub Actions workflows:

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| `hagicode-server-publish.yml` | Version tags (`v*.*.*`) | Full release pipeline |
| `docker-build.yml` | Push to `main` branch | Docker build validation |

### Version Tagging Strategy

Docker images use semantic versioning with multiple tags:

| Tag Pattern | Example | Description |
|-------------|---------|-------------|
| Full version | `v1.2.3` | Exact version |
| Minor version | `v1.2` | Latest patch in minor |
| Major version | `v1` | Latest minor/patch in major |
| `latest` | `latest` | Most recent stable (not pre-release) |

**Pre-release handling**: Tags with `-alpha`, `-beta`, or `-rc` do NOT update `latest`.

### GitHub Release Behavior

The `GitHubRelease` target uploads packages to **pre-existing** releases:
- Uses `gh release upload` command (not `gh release create`)
- Supports releases created by other tools or processes
- Uses `--clobber` flag to overwrite existing assets
- Requires release to exist before upload

### Testing Strategy
- Local testing with `.env` file configuration
- Nuke targets can be run individually for debugging
- GitHub Actions workflow provides CI validation

### Git Workflow
- **Main branch**: `main` - protected for releases
- **Current branch**: `synctoazureandliyun` - active development
- **Version tags**: Format `v*.*.*` (e.g., `v1.0.0`) trigger releases
- **Commit conventions**: Follow conventional commit format for release notes

## Domain Context

### Package Distribution Model
This repository implements a **staging and distribution** pattern:

1. **Main project** (hagicode) builds and uploads packages to Azure Blob Storage
2. **This repository** downloads packages and distributes to end users
3. **Separation of concerns**: Development vs. distribution

### Release Artifacts
- **Docker images**: Multi-arch container images with semantic version tags
- **GitHub Releases**: Original .zip packages uploaded to pre-existing releases
- **Release notes**: Created separately during release creation

## Important Constraints

### Technical Constraints
- **No source code**: This repository cannot build applications from source
- **Azure dependency**: Requires valid Azure Blob Storage SAS URL
- **Docker Hub authentication**: Requires valid credentials for image publishing
- **.NET 10 requirement**: Build tools require .NET 10 SDK

### Release Constraints
- **Version format**: Must follow semantic versioning (`vX.Y.Z`)
- **Tag triggers**: Only version tags trigger release workflow
- **Pre-release detection**: Automatic detection via version suffix
- **Idempotency**: Releases must be reproducible

## External Dependencies

### Required Services

| Service | Purpose | Authentication |
|---------|---------|----------------|
| Azure Blob Storage | Package source | SAS URL with Read permissions |
| Docker Hub | Container registry | Username + password/token |
| Azure Container Registry | Container registry | Username + password/token |
| Aliyun Container Registry | Container registry | Username + password/token |
| GitHub API | Release upload | Personal Access Token |

### Environment Variables

Required (see `.env.example`):
- `AZURE_BLOB_SAS_URL` - Azure Storage SAS URL
- `DOCKER_USERNAME` - Docker Hub username
- `DOCKER_PASSWORD` - Docker Hub access token
- `ALIYUN_ACR_USERNAME` - Aliyun ACR username
- `ALIYUN_ACR_PASSWORD` - Aliyun ACR password/token
- `AZURE_ACR_USERNAME` - Azure ACR username
- `AZURE_ACR_PASSWORD` - Azure ACR password/token
- `GITHUB_TOKEN` - GitHub API token (auto in CI)

Optional:
- `DOCKER_REGISTRY` - Registry URL (default: `docker.io/newbe36524`)
- `DOCKER_IMAGE_NAME` - Image name (default: `hagicode`)
- `VERSION` - Version tag (auto-detected from git)
- `OUTPUT_DIRECTORY` - Output path (default: `./output`)
- `ALIYUN_ACR_REGISTRY` - Aliyun ACR URL (default: `registry.cn-hangzhou.aliyuncs.com`)
- `AZURE_ACR_REGISTRY` - Azure ACR URL (default: `hagicode.azurecr.io`)

## Project Structure

```
.
├── .github/workflows/          # GitHub Actions workflows
│   ├── docker-build.yml        # Docker image build workflow
│   └── hagicode-server-publish.yml  # Full release workflow
├── nukeBuild/                  # Nuke build configuration
│   ├── _build.csproj           # Build project file
│   ├── Build.cs                # Main build definition
│   ├── Build.Helpers.cs        # Helper functions
│   ├── Build.Partial.cs        # Partial build definitions
│   ├── Build.Targets.*.cs      # Modular build targets
│   │   ├── Build.Targets.Download.cs   # Package download
│   │   ├── Build.Targets.Extract.cs    # Package extraction
│   │   ├── Build.Targets.Docker.cs     # Docker operations
│   │   └── Build.Targets.GitHub.cs     # GitHub release upload
│   └── Adapters/               # External service adapters
│       └── AzureBlobAdapter.cs # Azure download logic
├── docker_deployment/          # Docker configuration
│   ├── Dockerfile.app.template # App-specific template
│   ├── Dockerfile.base         # Base runtime image
│   ├── docker-entrypoint.sh    # Container entry point
│   └── .dockerignore           # Build exclusions
├── output/                     # Build output directory
│   ├── download/               # Downloaded packages
│   ├── extracted/              # Extracted package contents
│   └── docker-context/         # Docker build context
├── openspec/                   # OpenSpec documentation
│   ├── project.md              # This file
│   ├── AGENTS.md               # AI agent instructions
│   ├── PROPOSAL_DESIGN_GUIDELINES.md  # Design doc standards
│   ├── specs/                  # Current specifications
│   └── changes/                # Change proposals
│       └── archive/            # Completed changes
├── .env.example                # Environment template
├── global.json                 # .NET SDK version
├── build.sh / build.ps1 / build.cmd  # Build bootstrap scripts
└── README.md                   # User documentation
```

## OpenSpec Development Guidelines

This project uses **OpenSpec** for spec-driven development. When creating change proposals:

1. **Read agent instructions**: See `@/openspec/AGENTS.md` for the complete workflow
2. **Design standards**: Follow UI and code flow visualization guidelines in `@/openspec/PROPOSAL_DESIGN_GUIDELINES.md`
3. **Proposal structure**: Include ASCII mockups for UI changes, Mermaid diagrams for code flows
4. **Validation**: Always run `openspec validate <change-id> --strict` before requesting approval

### Key OpenSpec Concepts

| Directory | Purpose | State |
|-----------|---------|-------|
| `openspec/specs/` | Current truth - what IS built | Deployed |
| `openspec/changes/` | Proposals - what SHOULD change | Proposed |
| `openspec/changes/archive/` | Completed changes | Completed |

### Spec Format

All specs use:
- `## ADDED/MODIFIED/REMOVED/RENAMED Requirements` for delta operations
- `#### Scenario:` headers (4 hashtags) for scenario descriptions
- `**WHEN/THEN/AND` Gherkin-style scenario steps

For detailed guidelines on creating proposals with visualizations, refer to **[PROPOSAL_DESIGN_GUIDELINES.md](PROPOSAL_DESIGN_GUIDELINES.md)**.

## Recent Changes

### 2026-02-08: Docker Multi-Registry Publish Unification
- Added `DockerPushAzure` target for Azure Container Registry
- Added `DockerPushAliyun` target for Aliyun Container Registry
- Added `DockerPushAll` target to push to all registries
- Updated Release target to use `DockerPushAll` instead of `DockerPush`
- Added new environment variables for ACR credentials
- Unified multi-registry publishing in single build pipeline

### 2026-02-08: GitHub Release Upload Logic Refactor
- Changed from `gh release create` to `gh release upload`
- Added support for uploading to pre-existing releases
- Added `--clobber` flag for asset overwriting

### 2026-02-07: Docker Build Lib Directory Copy Fix
- Fixed lib directory copying in Docker build context
- Ensured framework-dependent assemblies are included

### 2026-02-07: Migrate PCode CI/CD Build Release Workflow
- Initial repository setup
- Nuke build configuration
- GitHub Actions workflows

## Troubleshooting

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| Download fails | Invalid/expired SAS URL | Regenerate Azure SAS URL with Read permissions |
| Docker build fails | Missing lib/ directory | Ensure package contains lib/ folder |
| Docker build fails | Missing base images | Ensure Docker daemon can pull images |
| Push fails (Docker Hub) | Invalid Docker Hub credentials | Verify `DOCKER_USERNAME` and `DOCKER_PASSWORD` |
| Push fails (Azure ACR) | Invalid Azure ACR credentials | Verify `AZURE_ACR_USERNAME` and `AZURE_ACR_PASSWORD` |
| Push fails (Aliyun ACR) | Invalid Aliyun ACR credentials | Verify `ALIYUN_ACR_USERNAME` and `ALIYUN_ACR_PASSWORD` |
| GitHub release fails | Release doesn't exist | Create release before uploading assets |
| GitHub release fails | Missing GitHub token permissions | Ensure `contents: write` scope |

### Debug Commands

```bash
# List all Nuke targets
nuke

# Download package only
nuke Download --AzureBlobSasUrl "<url>" --Version v1.0.0

# Build Docker image only
nuke DockerBuild --Version v1.0.0

# Full release pipeline (local)
nuke Release --Version v1.0.0
```

## Related Documentation

- [Nuke Documentation](https://nuke.build)
- [Azure Blob Storage SDK](https://docs.microsoft.com/azure/storage/blobs)
- [OpenSpec AGENTS.md](AGENTS.md) - AI agent workflow instructions
- [OpenSpec PROPOSAL_DESIGN_GUIDELINES.md](PROPOSAL_DESIGN_GUIDELINES.md) - Design visualization standards
