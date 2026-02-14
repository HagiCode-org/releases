# Change: Migrate ZAI to Anthropic Model Configuration

## Why

The current container configuration uses ZAI-specific naming (`ZAI_SONNET_MODEL`, `ZAI_OPUS_MODEL`) for model version configuration. According to project maintainers:

1. **ZAI API key is deprecated** - The `ZAI_API_KEY` configuration is no longer needed and should be removed from the system
2. **Configuration naming is unclear** - Using ZAI-specific names for Anthropic model settings confuses users about what these variables actually configure
3. **Better alignment with Anthropic** - The models are Anthropic models (Sonnet/Opus), so the configuration should reflect this

## What Changes

- **REMOVED** `ZAI_API_KEY` environment variable documentation and references
- **RENAMED** `ZAI_SONNET_MODEL` → `ANTHROPIC_SONNET_MODEL`
- **RENAMED** `ZAI_OPUS_MODEL` → `ANTHROPIC_OPUS_MODEL`
- **UPDATED** Default values remain `glm-4.7` for both models
- **UPDATED** `openspec/project.md` environment variables section (lines 198-200)
- **UPDATED** `.env.example` to reflect new variable names
- **UPDATED** `docs/container-environment-variables.md` documentation
- **UPDATED** `docker_deployment/docker-entrypoint.sh` to use new variable names

## Impact

### Affected Specs
- `documentation` - Container environment variables reference

### Affected Code
- `openspec/project.md` (lines 198-201)
- `.env.example` (lines 51-54)
- `docs/container-environment-variables.md` (multiple sections)
- `docker_deployment/docker-entrypoint.sh` (lines 101-102, 117-118)

### User Impact

**Breaking Change**: Users currently using `ZAI_SONNET_MODEL` or `ZAI_OPUS_MODEL` will need to update their configurations:

| Old Variable | New Variable |
|--------------|--------------|
| `ZAI_SONNET_MODEL` | `ANTHROPIC_SONNET_MODEL` |
| `ZAI_OPUS_MODEL` | `ANTHROPIC_OPUS_MODEL` |

**Benefits**:
- Clearer naming aligned with Anthropic product terminology
- Easier for users to understand what they're configuring
- Consistent with `ANTHROPIC_AUTH_TOKEN` and other Anthropic-related variables

### Build and Deployment Impact

- **No changes** required to Nuke build targets
- **No changes** required to Docker build process
- **No changes** required to GitHub Actions workflows
- Docker images will need to be rebuilt after this change

## Migration Path

For users with existing deployments:

1. Update environment variable names in Docker run commands
2. Update Docker Compose files with new variable names
3. Update Kubernetes ConfigMaps with new keys
4. Redeploy containers with updated configuration

## Success Criteria

1. All `ZAI_SONNET_MODEL` references are replaced with `ANTHROPIC_SONNET_MODEL`
2. All `ZAI_OPUS_MODEL` references are replaced with `ANTHROPIC_OPUS_MODEL`
3. Documentation is updated consistently across all files
4. Default values remain unchanged (`glm-4.7`)
5. `openspec validate migrate-zai-to-anthropic-config --strict` passes
