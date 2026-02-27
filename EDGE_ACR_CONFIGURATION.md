# Edge ACR Configuration

This document provides detailed configuration for publishing Docker images to Edge Azure Container Registry (Edge ACR).

## Overview

Edge ACR is the target Azure Container Registry for HagiCode Docker images. All multi-architecture Docker images are built and pushed to Edge ACR as part of the release process.

## Prerequisites

### Azure Container Registry Requirements

- **Account**: Azure account with Container Registry access
- **Registry**: Edge ACR endpoint (e.g., `hagicode.azurecr.io`)
- **Permissions**: `AcrPush`, `AcrPull`, `AcrImageSign`, `AcrDelete`
- **Authentication**: Username/password or service principal/token

### Docker Requirements

- **Docker Version**: 20.10 or later with buildx support
- **QEMU**: Cross-architecture emulation (via binfmt image)
- **Storage**: Sufficient space for Docker images (~2-5GB)

## Authentication Methods

### Username/Password (Basic Auth)

Most common authentication method for interactive use:

```bash
docker login hagicode.azurecr.io -u <username> -p <password>
```

**Environment Variables**:
```bash
export AZURE_ACR_USERNAME="hagicode"
export AZURE_ACR_PASSWORD="password_or_token"
export AZURE_ACR_REGISTRY="hagicode.azurecr.io"
```

### Azure Service Principal

Recommended for CI/CD and automated builds:

```bash
# Create service principal
az ad sp create-for-rbac \
  --name hagicode-docker-push \
  --role acrpull \
  --role acrpush \
  --scope /subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.ContainerRegistry/registries/<registry>

# Get credentials
export AZURE_ACR_USERNAME="<service-principal-app-id>"
export AZURE_ACR_PASSWORD="<service-principal-password>"
```

### Azure Managed Identity

Most secure option for production workloads:

```bash
# User-assigned managed identity
az identity create -g <resource-group> -n hagicode-docker-identity

# Assign ACR role
az role assignment create \
  --assignee <identity-id> \
  --role acrpull \
  --role acrpush \
  --scope /subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.ContainerRegistry/registries/<registry>
```

### Azure CLI Authentication

Use Azure CLI for authentication (requires `az acr login`):

```bash
# Install Azure CLI
# macOS: brew install azure-cli
# Linux: curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Login to Azure
az login

# Login to ACR
az acr login --name hagicode
```

## Registry Configuration

### Required Variables

| Variable | Description | Example | Required |
|----------|-------------|----------|-----------|
| `AZURE_ACR_REGISTRY` | Edge ACR registry endpoint | `hagicode.azurecr.io` | Yes |
| `AZURE_ACR_USERNAME` | Registry username or service principal | `hagicode` or `sp-app-id` | Yes |
| `AZURE_ACR_PASSWORD` | Registry password or service principal secret | `password` or `sp-secret` | Yes |

### Nuke Prefix Alternative

All variables can be prefixed with `NUGEX_` for Nuke builds:

```bash
export NUGEX_AzureAcrRegistry="hagicode.azurecr.io"
export NUGEX_AzureAcrUsername="hagicode"
export NUGEX_AzureAcrPassword="password"
```

## Image Naming Convention

### Full Image Reference

```
<registry>/<image-name>:<tag>
```

### Examples

| Tag Type | Example | Use Case |
|-----------|----------|-----------|
| Base (AMD64) | `hagicode.azurecr.io/hagicode:base` | Base image for linux/amd64 |
| Base (ARM64) | `hagicode.azurecr.io/hagicode:base-arm64` | Base image for linux/arm64 |
| Version | `hagicode.azurecr.io/hagicode:1.2.3` | Specific version |
| Minor | `hagicode.azurecr.io/hagicode:1.2` | Latest in 1.2.x series |
| Major | `hagicode.azurecr.io/hagicode:1` | Latest in 1.x series |
| Latest | `hagicode.azurecr.io/hagicode:latest` | Most recent release |

## Multi-Architecture Support

### Platform Support

Edge ACR supports multi-architecture manifests that reference both AMD64 and ARM64 images:

```yaml
# Example multi-arch manifest
{
  "schemaVersion": 2,
  "mediaType": "application/vnd.docker.distribution.manifest.v2+json",
  "manifests": [
    {
      "platform": {
        "architecture": "amd64",
        "os": "linux"
      },
      "digest": "sha256:..."
    },
    {
      "platform": {
        "architecture": "arm64",
        "os": "linux"
      },
      "digest": "sha256:..."
    }
  ]
}
```

### Building Multi-Arch Images

The build system automatically creates multi-arch manifests using Docker buildx:

```bash
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --tag hagicode.azurecr.io/hagicode:1.2.3 \
  --output type=registry \
  .
```

### Verifying Multi-Arch Images

Check if a multi-arch manifest exists:

```bash
docker manifest inspect hagicode.azurecr.io/hagicode:1.2.3

# Output shows all supported platforms:
# Platform: linux/amd64
# Platform: linux/arm64
```

## Build System Integration

### Nuke Build Targets

The Nuke build system includes Edge ACR integration in `Build.Targets.Docker.cs`:

**Targets**:
- `DockerBuild` - Build images locally
- `DockerPush` - Push images to Edge ACR
- `DockerRelease` - Build and push (combined target)

**Parameters**:
- `--DockerPlatform` - Platform(s) to build (all, linux-amd64, linux-arm64)
- `--AzureAcrRegistry` - Edge ACR endpoint
- `--AzureAcrUsername` - ACR username
- `--AzureAcrPassword` - ACR password
- `--DockerImageName` - Image name (default: hagicode/hagicode)

### GitHub Actions Workflow

The `.github/workflows/docker-build.yml` workflow automatically:

1. Authenticates to Edge ACR using secrets
2. Builds multi-arch Docker images with buildx
3. Pushes images directly to Edge ACR registry
4. Creates version tags (full, minor, major, latest)
5. Verifies images are available in registry

## Troubleshooting

### Authentication Failures

**Error**: `unauthorized: authentication required`

**Causes**:
- Incorrect username/password
- Expired token
- Insufficient permissions

**Solutions**:
1. Verify credentials: `az acr show-credentials --name hagicode`
2. Check permissions: Ensure role includes `AcrPush`
3. Re-generate service principal secret if expired

### Push Failures

**Error**: `failed to upload layer to registry`

**Causes**:
- Network connectivity issues
- Image size exceeds ACR limits
- Registry throttling

**Solutions**:
1. Check network connectivity
2. Verify ACR SKU supports required operations
3. Retry with exponential backoff
4. Check ACR quota limits: `az acr show-usage`

### Manifest Issues

**Error**: `no such manifest` or `manifest invalid`

**Causes**:
- Incorrect image tag
- Multi-arch manifest not fully pushed
- Cache issues

**Solutions**:
1. List images in registry: `az acr repository show-tags --name hagicode --registry hagicode.azurecr.io`
2. Verify platform-specific images exist
3. Clear local Docker cache: `docker builder prune`

### Verification Failures

**Error**: `image not found in registry after push`

**Causes**:
- Registry propagation delay (1-2 minutes)
- Indexing not complete
- Multi-region replication delay

**Solutions**:
1. Wait and retry: Sleep 60 seconds, retry verification
2. Check across regions: Verify image exists in target region
3. Verify manifest digest matches build output

## Best Practices

### Security

1. **Use service principals**: Don't use personal accounts for CI/CD
2. **Rotate credentials**: Update service principal secrets regularly
3. **Least privilege**: Grant only `AcrPush` and `AcrPull` as needed
4. **Separate registries**: Consider separate registries for dev/prod
5. **Audit access**: Monitor who has ACR access via Azure AD

### Performance

1. **Enable caching**: Use Docker build cache to reduce rebuild time
2. **Parallel builds**: Build AMD64 and ARM64 in parallel with buildx
3. **Layer optimization**: Minimize layers by combining related changes
4. **Registry proximity**: Use region-specific ACR for faster pulls

### Operations

1. **Clean old images**: Remove untagged images periodically
2. **Monitor usage**: Track storage and request metrics
3. **Set retention policies**: Automate image cleanup with ACR retention rules
4. **Use manifests**: Prefer multi-arch manifests over separate images

## Azure CLI Commands

### Registry Management

```bash
# Login to ACR
az acr login --name hagicode

# List repositories
az acr repository list --name hagicode --registry hagicode.azurecr.io

# Show image tags
az acr repository show-tags --name hagicode --registry hagicode.azurecr.io

# Show image details
az acr manifest show-metadata \
  --name hagicode \
  --registry hagicode.azurecr.io \
  --image 1.2.3

# Delete image
az acr repository delete \
  --name hagicode \
  --registry hagicode.azurecr.io \
  --image 1.2.3
```

### Usage Monitoring

```bash
# Show ACR usage
az acr show-usage --resource-group <resource-group> --name hagicode

# List credentials
az acr credential-list --name hagicode
```

## Additional Resources

- [Azure Container Registry Documentation](https://docs.microsoft.com/azure/container-registry/)
- [Docker Buildx Documentation](https://docs.docker.com/buildx/working-with-buildx/)
- [Azure CLI Documentation](https://docs.microsoft.com/cli/azure/)
- [ACR Best Practices](https://docs.microsoft.com/azure/container-registry/container-registry-best-practices)

## Support

For Edge ACR configuration issues:

- Check [ENVIRONMENT_VARIABLES.md](ENVIRONMENT_VARIABLES.md) for variable setup
- Review [README.md](README.md) for build instructions
- Consult Azure Support for registry issues
- Review Nuke build logs: `./build.sh DockerRelease --help`
