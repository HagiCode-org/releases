## 1. Documentation Updates

- [ ] 1.1 Update `openspec/project.md` environment variables section
  - [ ] 1.1.1 Rename `ZAI_SONNET_MODEL` to `ANTHROPIC_SONNET_MODEL` (line 199)
  - [ ] 1.1.2 Rename `ZAI_OPUS_MODEL` to `ANTHROPIC_OPUS_MODEL` (line 200)
  - [ ] 1.1.3 Update variable descriptions to reflect Anthropic naming

- [ ] 1.2 Update `.env.example` file
  - [ ] 1.2.1 Rename `ZAI_SONNET_MODEL` to `ANTHROPIC_SONNET_MODEL` (line 52)
  - [ ] 1.2.2 Rename `ZAI_OPUS_MODEL` to `ANTHROPIC_OPUS_MODEL` (line 54)
  - [ ] 1.2.3 Update inline comments to reference Anthropic models

- [ ] 1.3 Update `docs/container-environment-variables.md`
  - [ ] 1.3.1 Update "ZAI API Configuration (Zhipu AI)" section header and description
  - [ ] 1.3.2 Update variable reference table entries for `ZAI_SONNET_MODEL` and `ZAI_OPUS_MODEL`
  - [ ] 1.3.3 Update "Using ZAI (Zhipu AI) API" example section
  - [ ] 1.3.4 Update Docker Compose example configuration
  - [ ] 1.3.5 Update Kubernetes ConfigMap example
  - [ ] 1.3.6 Update environment variable reference table
  - [ ] 1.3.7 Update all section descriptions to use Anthropic terminology

## 2. Implementation Changes

- [ ] 2.1 Update `docker_deployment/docker-entrypoint.sh`
  - [ ] 2.1.1 Rename `ZAI_SONNET_MODEL` variable to `ANTHROPIC_SONNET_MODEL` (line 101)
  - [ ] 2.1.2 Rename `ZAI_OPUS_MODEL` variable to `ANTHROPIC_OPUS_MODEL` (line 102)
  - [ ] 2.1.3 Update settings.json references to use new variable names (lines 117-118)

## 3. Validation

- [ ] 3.1 Run `openspec validate migrate-zai-to-anthropic-config --strict`
- [ ] 3.2 Verify all references are updated by running: `rg -n "ZAI_SONNET_MODEL|ZAI_OPUS_MODEL" --exclude-dir=openspec/changes/archive .`
- [ ] 3.3 Confirm no old variable names remain (except in archive)
- [ ] 3.4 Test Docker build with new configuration
