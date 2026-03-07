# Environment Variables - HagiCode Release

This document describes all environment variables required or used by the HagiCode release system.

## Required Environment Variables

### Azure Blob Storage

| Variable | Description | Required | Example |
|----------|-------------|-----------|----------|
| `AZURE_BLOB_SAS_URL` | Azure Blob Storage SAS URL for downloading application packages | Yes | `https://hagicode.blob.core.windows.net/packages?sp=...` |
| `NUGEX_AzureBlobSasUrl` | Alternative way to pass Azure Blob SAS URL (Nuke prefix) | No | Same as above |

### GitHub Integration

| Variable | Description | Required | Example |
|----------|-------------|-----------|----------|
| `GITHUB_TOKEN` | GitHub personal access token for creating releases | Yes | `ghp_xxxxxxxxxxxxxxxxxxxx` |
| `NUGEX_GitHubToken` | Alternative way to pass GitHub token (Nuke prefix) | No | Same as above |
| `GITHUB_REPOSITORY` | GitHub repository in format `owner/repo` | Yes (CI) | `newbe36524/hagicode` |
| `NUGEX_GitHubRepository` | Alternative way to pass GitHub repo (Nuke prefix) | No | Same as above |

### Edge ACR (Azure Container Registry)

| Variable | Description | Required | Example |
|----------|-------------|-----------|----------|
| `AZURE_ACR_USERNAME` | Edge ACR username for authentication | Yes (Docker push) | `hagicode` |
| `NUGEX_AzureAcrUsername` | Alternative way to pass ACR username (Nuke prefix) | No | Same as above |
| `AZURE_ACR_PASSWORD` | Edge ACR password or access token | Yes (Docker push) | `password_or_token` |
| `NUGEX_AzureAcrPassword` | Alternative way to pass ACR password (Nuke prefix) | No | Same as above |
| `AZURE_ACR_REGISTRY` | Edge ACR registry endpoint | Yes (Docker push) | `hagicode.azurecr.io` |
| `NUGEX_AzureAcrRegistry` | Alternative way to pass ACR registry (Nuke prefix) | No | Same as above |

### Aliyun ACR (Aliyun Container Registry)

| Variable | Description | Required | Example |
|----------|-------------|-----------|----------|
| `ALIYUN_ACR_USERNAME` | Aliyun ACR username for authentication | No (optional push) | `your-username` |
| `NUGEX_AliyunAcrUsername` | Alternative way to pass Aliyun ACR username (Nuke prefix) | No | Same as above |
| `ALIYUN_ACR_PASSWORD` | Aliyun ACR password or access token | No (optional push) | `your-password` |
| `NUGEX_AliyunAcrPassword` | Alternative way to pass Aliyun ACR password (Nuke prefix) | No | Same as above |
| `ALIYUN_ACR_REGISTRY` | Aliyun ACR registry endpoint | No (optional push) | `registry.cn-hangzhou.aliyuncs.com` |
| `NUGEX_AliyunAcrRegistry` | Alternative way to pass Aliyun ACR registry (Nuke prefix) | No | Same as above |
| `ALIYUN_ACR_NAMESPACE` | Aliyun ACR namespace for image path | No | `hagicode` |

### DockerHub

| Variable | Description | Required | Example |
|----------|-------------|-----------|----------|
| `DOCKERHUB_USERNAME` | DockerHub username for authentication | No (optional push) | `your-username` |
| `NUGEX_DockerHubUsername` | Alternative way to pass DockerHub username (Nuke prefix) | No | Same as above |
| `DOCKERHUB_TOKEN` | DockerHub access token (not password) | No (optional push) | `dckr_pat_...` |
| `NUGEX_DockerHubToken` | Alternative way to pass DockerHub token (Nuke prefix) | No | Same as above |

**Note**: DockerHub access tokens can be created at https://hub.docker.com/settings/security

### Docker Build

| Variable | Description | Required | Default | Example |
|----------|-------------|-----------|----------|
| `DOCKER_PLATFORM` | Target platform(s) for Docker build | No | `all` |
| `NUGEX_DockerPlatform` | Alternative way to pass Docker platform (Nuke prefix) | No | Same as above |

**Accepted values**:
- `all` - Build for both linux/amd64 and linux/arm64 (default)
- `linux-amd64` or `amd64` - Build only for AMD64
- `linux-arm64` or `arm64` - Build only for ARM64

| Variable | Description | Required | Default | Example |
|----------|-------------|-----------|----------|
| `DOCKER_IMAGE_NAME` | Docker image name (without registry) | No | `hagicode/hagicode` |
| `DOCKER_BUILD_TIMEOUT` | Docker build timeout in seconds | No | `3600` |
| `DOCKER_FORCE_REBUILD` | Force rebuild of Docker images | No | `false` |
| `NUGEX_DockerIndependentBuild` | Enable independent multi-arch build and push | No | `false` |
| `NUGEX_EnableIndependentBuild` | Alternative way to enable independent build | No | `false` |

### Docker Independent Build

When `NUGEX_DockerIndependentBuild` or `NUGEX_EnableIndependentBuild` is set to `true`:

- Uses `docker buildx` for direct multi-architecture builds and pushes
- Enables parallel push to multiple registries (Azure ACR, Aliyun ACR, DockerHub)
- Provides failure isolation - one registry failure doesn't affect others
- Falls back to single architecture (amd64) if registry doesn't support multi-arch

### Release Management

| Variable | Description | Required | Example |
|----------|-------------|-----------|----------|
| `RELEASE_VERSION` | Version to release (e.g., 1.2.3) | Yes (manual) | `1.2.3` |
| `NUGEX_ReleaseVersion` | Alternative way to pass release version (Nuke prefix) | No | Same as above |

### Notification

| Variable | Description | Required | Example |
|----------|-------------|-----------|----------|
| `FEISHU_WEBHOOK_URL` | Feishu webhook URL for release notifications | No | `https://open.feishu.cn/open-apis/bot/v2/hook/...` |

### Build Modes

| Variable | Description | Required | Default | Example |
|----------|-------------|-----------|----------|
| `NUGEX_DryRun` | Dry run mode (no actual releases) | No | `false` |
| `NUGEX_ListOnly` | List only mode (show versions, no releases) | No | `false` |

## Docker Runtime Environment Variables

These variables are used inside Docker containers to configure AI agents:

### Claude Code Configuration

| Variable | Description | Required | Example |
|----------|-------------|-----------|----------|
| `ANTHROPIC_AUTH_TOKEN` | Anthropic API authentication token | Yes (Claude Code) | `sk-ant-...` |
| `ANTHROPIC_URL` | Custom Anthropic API endpoint | No | `https://api.anthropic.com` |
| `ANTHROPIC_SONNET_MODEL` | Default Sonnet model | No | `claude-sonnet-4-20250514` |
| `ANTHROPIC_OPUS_MODEL` | Default Opus model | No | `claude-opus-4-20250514` |
| `ANTHROPIC_HAIKU_MODEL` | Default Haiku model | No | `claude-haiku-3-5-20250218` |
| `CLAUDE_HOST_CONFIG_ENABLED` | Enable host config mount | No | `true` |
| `CLAUDE_CONFIG_MOUNT_PATH` | Path for mounted Claude config | No | `/claude-mount` |
| `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS` | Enable Agent Teams feature | No | `1` |

### Codex Global Configuration

These variables configure Codex global runtime settings in container startup.
No additional app-side API key or UI configuration is required.

#### Precedence Rules

- Base URL: `CODEX_BASE_URL` > `OPENAI_BASE_URL`
- API key: `CODEX_API_KEY` > `OPENAI_API_KEY`

#### Variables

| Variable | Description | Required | Example |
|----------|-------------|-----------|----------|
| `CODEX_BASE_URL` | Primary Codex endpoint variable | No | `https://api.openai.com/v1` |
| `CODEX_API_KEY` | Primary Codex API key variable | No | `sk-...` |
| `OPENAI_BASE_URL` | Compatibility alias for base URL | No | `https://api.openai.com/v1` |
| `OPENAI_API_KEY` | Compatibility alias for API key | No | `sk-...` |

**Behavior notes**:
- If no Codex variables are set, container startup behavior is unchanged.
- Startup logs never print raw API key values.
- Runtime bootstrap exports resolved values for Codex CLI global consumption.

### User/Permissions

| Variable | Description | Required | Default | Example |
|----------|-------------|-----------|----------|
| `PUID` | User ID to run container as | No | `1000` |
| `PGID` | Group ID to run container as | No | `1000` |

## GitHub Actions Secrets

When using GitHub Actions, configure these secrets in your repository settings:

### Required Secrets

1. `AZURE_BLOB_SAS_URL` - Azure Blob Storage SAS URL
2. `GITHUB_TOKEN` - GitHub PAT with repo and workflow permissions
3. `AZURE_ACR_USERNAME` - Edge ACR username
4. `AZURE_ACR_PASSWORD` - Edge ACR password/token
5. `AZURE_ACR_REGISTRY` - Edge ACR registry endpoint

### Optional Secrets (Aliyun ACR Push)

1. `ALIYUN_ACR_USERNAME` - Aliyun ACR username
2. `ALIYUN_ACR_PASSWORD` - Aliyun ACR password
3. `ALIYUN_ACR_REGISTRY` - Aliyun ACR registry endpoint (default: `registry.cn-hangzhou.aliyuncs.com`)
4. `ALIYUN_ACR_NAMESPACE` - Aliyun ACR namespace (default: `hagicode`)

### Optional Secrets (DockerHub Push)

1. `DOCKERHUB_USERNAME` - DockerHub username
2. `DOCKERHUB_TOKEN` - DockerHub access token (created at https://hub.docker.com/settings/security)

### Other Optional Secrets

1. `FEISHU_WEBHOOK_URL` - Feishu notification webhook

## Setting Environment Variables

### Local Development

Set environment variables before running Nuke:

```bash
# macOS/Linux
export AZURE_BLOB_SAS_URL="https://..."
export GITHUB_TOKEN="ghp_xxxxxx"
export AZURE_ACR_USERNAME="hagicode"
export AZURE_ACR_PASSWORD="password"
export AZURE_ACR_REGISTRY="hagicode.azurecr.io"

# Windows PowerShell
$env:AZURE_BLOB_SAS_URL="https://..."
$env:GITHUB_TOKEN="ghp_xxxxxx"

# Run build
./build.sh DockerRelease --ReleaseVersion "1.2.3"
```

### Nuke Parameters

Pass environment variables using Nuke's parameter syntax:

```bash
./build.sh DockerRelease \
  --AzureBlobSasUrl "https://..." \
  --ReleaseVersion "1.2.3" \
  --DockerPlatform "all"
```

### Docker Run

Pass environment variables when running Docker containers:

```bash
docker run -e ANTHROPIC_AUTH_TOKEN="sk-ant-..." \
           -e CODEX_BASE_URL="https://api.openai.com/v1" \
           -e CODEX_API_KEY="sk-..." \
           -e CLAUDE_HOST_CONFIG_ENABLED="true" \
           -v ~/claude-config:/claude-mount \
           hagicode/hagicode:1.2.3
```

### Docker Compose

Use environment variables in docker-compose.yml:

```yaml
version: '3.8'
services:
  hagicode:
    image: hagicode.azurecr.io/hagicode:1.2.3
    environment:
      - ANTHROPIC_AUTH_TOKEN=sk-ant-...
      - CODEX_BASE_URL=https://api.openai.com/v1
      - CODEX_API_KEY=sk-...
      - CLAUDE_HOST_CONFIG_ENABLED=true
    volumes:
      - ./claude-config:/claude-mount
```

## Security Best Practices

1. **Never commit secrets to git**: Always use GitHub Secrets or environment variables
2. **Use least privilege**: Grant only necessary permissions to tokens
3. **Rotate credentials regularly**: Update secrets periodically
4. **Use service principals**: For Azure, use managed identities where possible
5. **Audit secret access**: Monitor who has access to repository secrets

## Troubleshooting

### Missing Environment Variables

**Error**: `Azure Blob SAS URL is not specified`

**Solution**: Set `AZURE_BLOB_SAS_URL` or `NUGEX_AzureBlobSasUrl`

### Authentication Errors

**Error**: `Failed to login to Edge ACR`

**Solution**: Verify `AZURE_ACR_USERNAME`, `AZURE_ACR_PASSWORD`, and `AZURE_ACR_REGISTRY` are correct

### Claude Code Not Working

**Error**: Claude Code not configured

**Solution**: Set `ANTHROPIC_AUTH_TOKEN` or mount host config to `/claude-mount`

### Codex Not Using Expected Endpoint/Key

**Error**: Codex calls use unexpected endpoint or authentication

**Solution**:
1. Check precedence order: `CODEX_*` overrides `OPENAI_*`
2. Ensure `CODEX_BASE_URL` and `CODEX_API_KEY` are both set when using Codex-specific variables
3. Verify no conflicting values are injected by compose files or CI environment

## Additional Resources

- [Azure Blob Storage Documentation](https://docs.microsoft.com/azure/storage/blobs/)
- [Azure Container Registry Documentation](https://docs.microsoft.com/azure/container-registry/)
- [GitHub Actions Secrets](https://docs.github.com/en/actions/security-guides/encrypted-secrets)
- [Claude Code Documentation](https://claude.ai/code/docs)
- [OpenSpec Documentation](https://openspec.dev/docs)
