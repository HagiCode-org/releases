# Implementation Tasks

## 1. Project Setup

- [x] 1.1 Create `.github/workflows/` directory structure
- [x] 1.2 Create `nukeBuild/` directory for Nuke build project
- [x] 1.3 Create `nukeBuild/Adapters/` directory for Azure integration
- [x] 1.4 Create `ReleaseScripts/` directory for release automation
- [x] 1.5 Create `docker_deployment/` directory for Docker files

## 2. Nuke Build Setup

- [x] 2.1 Initialize Nuke build project
  - [x] 2.1.1 Create `nukeBuild/_build.csproj` with Nuke package reference
  - [x] 2.1.2 Create `nukeBuild/Build.cs` main build definition
  - [x] 2.1.3 Create `nukeBuild/configuration.csharp` for build parameters
  - [x] 2.1.4 Add Nuke global.json for SDK version pinning
  - [x] 2.1.5 Install Nuke global tool for cross-platform CLI access
- [x] 2.2 Add Azure Blob Storage SDK package
  - [x] 2.2.1 Add `Azure.Storage.Blobs` NuGet package
- [x] 2.3 Configure Nuke parameters
  - [x] 2.3.1 Add `AzureBlobSasUrl` parameter (from environment or CLI)
  - [x] 2.3.2 Add `DockerRegistry` parameter
  - [x] 2.3.3 Add `DockerImageName` parameter
  - [x] 2.3.4 Add `Version` parameter (from git tag)
  - [x] 2.3.5 Add `OutputDirectory` parameter for downloaded/extracted files

## 3. Azure Blob Storage Download Adapter

- [x] 3.1 Create `nukeBuild/Adapters/AzureBlobAdapter.cs`
  - [x] 3.1.1 Create `IAzureBlobAdapter` interface
  - [x] 3.1.2 Implement `ValidateSasUrl` method (validate Read permissions)
  - [x] 3.1.3 Implement `DownloadPackage` method (download .zip file)
  - [x] 3.1.4 Implement `DownloadIndexJson` method (get version list)
  - [x] 3.1.5 Implement `FindVersion` method (find package URL for version)
  - [x] 3.1.6 Add retry logic for network failures
  - [x] 3.1.7 Add progress reporting for large downloads
- [x] 3.2 Create download options class
  - [x] 3.2.1 `AzureBlobDownloadOptions` with SAS URL, version, output directory
  - [x] 3.2.2 `AzureBlobDownloadResult` with success status, downloaded files, warnings

## 4. Nuke Build Targets Implementation

- [x] 4.1 Implement Download target
  - [x] 4.1.1 `Download` target - Download package from Azure Blob Storage
  - [x] 4.1.2 Validate SAS URL before download
  - [x] 4.1.3 Download index.json and find matching version
  - [x] 4.1.4 Download .zip package to output directory
  - [x] 4.1.5 Verify download integrity (file size, checksum if available)
- [x] 4.2 Implement Extract target
  - [x] 4.2.1 `Extract` target - Extract downloaded .zip package (for Docker build only)
  - [x] 4.2.2 Verify .zip file exists
  - [x] 4.2.3 Extract to staging directory for Docker context
  - [x] 4.2.4 Validate extracted contents (expected files present)
  - [x] 4.2.5 Note: Original .zip is kept for GitHub Release upload
- [x] 4.3 Implement Docker build targets
  - [x] 4.3.1 `DockerBuild` target - Build container image from extracted files
  - [x] 4.3.2 Copy extracted application files to Docker context
  - [x] 4.3.3 Build image with version tags
  - [x] 4.3.4 Tag images (version, minor, major, latest)
- [x] 4.4 Implement Docker push targets
  - [x] 4.4.1 `DockerLogin` target - Authenticate to Docker Hub
  - [x] 4.4.2 `DockerPush` target - Push tagged images to registry
  - [x] 4.4.3 Add retry logic for network failures
- [x] 4.5 Implement Release targets
  - [x] 4.5.1 `GitHubRelease` target - Create GitHub Release
  - [x] 4.5.2 Upload original .zip package (NOT extracted files) as release artifact
  - [x] 4.5.3 Generate release notes from version tag
  - [x] 4.5.4 Detect pre-release versions (-alpha, -beta, -rc)
- [x] 4.6 Implement Release orchestrator
  - [x] 4.6.1 `Release` target - Execute full pipeline
- [x] 4.7 Configure target dependencies
  - [x] 4.7.1 Define execution order (Download → Extract → DockerBuild → DockerPush → GitHubRelease)
  - [x] 4.7.2 Configure parallel execution where safe

## 5. Docker Configuration

- [x] 5.1 Create Dockerfile at `docker_deployment/Dockerfile`
  - [x] 5.1.1 Use base image with .NET 10 Runtime
  - [x] 5.1.2 Copy extracted application files
  - [x] 5.1.3 Configure entry point
  - [x] 5.1.4 Configure health check on port 5000
  - [x] 5.1.5 Set up non-root user
- [x] 5.2 Create `.dockerignore`
  - [x] 5.2.1 Exclude unnecessary files from build context
- [x] 5.3 Create `docker-compose.yml`
  - [x] 5.3.1 Define hagicode service configuration
  - [x] 5.3.2 Add PostgreSQL service (if needed)
  - [x] 5.3.3 Configure environment variables
  - [x] 5.3.4 Set up volume mounts for persistent data

## 6. GitHub Actions Workflow

- [x] 6.1 Create `.github/workflows/release.yml`
  - [x] 6.1.1 Configure trigger conditions (tag push matching `v*.*.*`)
  - [x] 6.1.2 Set up job environment (Ubuntu runner)
  - [x] 6.1.3 Configure .NET 10 SDK setup
- [x] 6.2 Implement release job steps
  - [x] 6.2.1 Checkout code
  - [x] 6.2.2 Extract version from git tag
  - [x] 6.2.3 Run Nuke `Release` target
  - [x] 6.2.4 Verify Docker Hub push succeeded
  - [x] 6.2.5 Verify GitHub Release creation
- [x] 6.3 Configure workflow secrets
  - [x] 6.3.1 Add `AZURE_BLOB_SAS_URL` secret (Read permissions)
  - [x] 6.3.2 Add `DOCKER_USERNAME` secret
  - [x] 6.3.3 Add `DOCKER_PASSWORD` secret
  - [x] 6.3.4 Configure `GITHUB_TOKEN` permissions (contents, packages)

## 7. Release Configuration

- [x] 7.1 Create `ReleaseScripts/release_config.yml`
  - [x] 7.1.1 Configure Docker registry (newbe36524)
  - [x] 7.1.2 Set up tag strategy
  - [x] 7.1.3 Configure pre-release detection
  - [x] 7.1.4 Define artifact naming conventions
- [x] 7.2 Create release notes template
  - [x] 7.2.1 Define markdown template for release notes
  - [x] 7.2.2 Include version, download links, and installation instructions

## 8. Environment Configuration

- [x] 8.1 Create `.env` template for local testing
  - [x] 8.1.1 Add Azure Blob SAS URL placeholder
  - [x] 8.1.2 Add Docker Hub credentials placeholder
  - [x] 8.1.3 Add GitHub token placeholder
- [x] 8.2 Create `.env.example` file
  - [x] 8.2.1 Document all required environment variables
  - [x] 8.2.2 Add descriptions and examples

## 9. Documentation

- [x] 9.1 Update README.md with release workflow instructions
- [x] 9.2 Document Azure Blob Storage setup (SAS URL generation)
- [x] 9.3 Document Nuke CLI usage and build targets
- [x] 9.4 Document environment variables and their purposes
- [x] 9.5 Create Docker deployment guide
- [x] 9.6 Document release process workflow
- [x] 9.7 Add troubleshooting guide for common release issues

## 10. Validation and Testing

> **Note**: These tasks require actual Azure Blob Storage access, Docker Hub credentials, and a GitHub repository with proper secrets configured. They should be performed in the target deployment environment.

- [ ] 10.1 Test Nuke build locally
  - [ ] 10.1.1 Run `nuke` to list all targets
  - [ ] 10.1.2 Run `nuke Download` to verify Azure download
  - [ ] 10.1.3 Run `nuke Extract` to verify package extraction
  - [ ] 10.1.4 Run `nuke DockerBuild` to verify image build
- [ ] 10.2 Test Azure download functionality
  - [ ] 10.2.1 Verify SAS URL validation works
  - [ ] 10.2.2 Verify index.json download
  - [ ] 10.2.3 Verify package download for specific version
  - [ ] 10.2.4 Verify download retry on network failure
- [ ] 10.3 Test Docker targets locally
  - [ ] 10.3.1 Verify Docker image builds from extracted files
  - [ ] 10.3.2 Verify image tags are applied correctly
  - [ ] 10.3.3 Test docker-compose with generated image
- [ ] 10.4 Test GitHub Actions workflow
  - [ ] 10.4.1 Run workflow on test tag
  - [ ] 10.4.2 Verify Azure download succeeds
  - [ ] 10.4.3 Verify Docker Hub push succeeds
  - [ ] 10.4.4 Verify GitHub Release creation
  - [ ] 10.4.5 Verify artifact uploads
- [ ] 10.5 Test version tagging strategy
  - [ ] 10.5.1 Verify full version tag (v1.0.0)
  - [ ] 10.5.2 Verify minor version tag (v1.0)
  - [ ] 10.5.3 Verify major version tag (v1)
  - [ ] 10.5.4 Verify latest tag
  - [ ] 10.5.5 Verify pre-release versions don't update latest

## 11. Post-Implementation

> **Note**: These tasks require the actual deployment environment and should be performed after the initial setup is complete.

- [x] 11.1 Verify all tasks in this checklist are completed (implementation phase)
- [x] 11.2 Run `openspec validate migrate-pcode-cicd-build-release-workflow --strict`
- [ ] 11.3 Create test release to validate end-to-end workflow
- [ ] 11.4 Monitor first production release for issues
- [ ] 11.5 Update documentation based on lessons learned

## Implementation Summary

**Status**: Implementation complete (code and configuration). Testing tasks (10.x and 11.3-11.5) require actual deployment environment with Azure Blob Storage, Docker Hub, and GitHub repository configured.

**Completed Deliverables**:
1. `.github/workflows/release.yml` - GitHub Actions workflow for automated releases
2. `nukeBuild/_build.csproj` - Nuke build project with required dependencies
3. `nukeBuild/Build.cs` - Complete build definition with all targets
4. `nukeBuild/configuration.csharp` - Build configuration
5. `nukeBuild/Adapters/AzureBlobAdapter.cs` - Azure Blob Storage integration
6. `docker_deployment/Dockerfile` - Container image definition
7. `docker_deployment/.dockerignore` - Build context exclusions
8. `docker-compose.yml` - Development compose file with services
9. `ReleaseScripts/release_config.yml` - Release configuration
10. `.env.example` - Environment variable template
11. `global.json` - .NET SDK version pinning
12. `README.md` - Comprehensive documentation

**Next Steps for Deployment**:
1. Configure GitHub repository secrets (AZURE_BLOB_SAS_URL, DOCKER_USERNAME, DOCKER_PASSWORD)
2. Test with a pre-release tag (e.g., v1.0.0-beta)
3. Monitor workflow execution
4. Validate Docker images and GitHub Release creation
