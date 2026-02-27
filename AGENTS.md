# AGENTS.md - HagiCode Release Repository

This document describes the AI agents (Claude, OpenSpec) that work with this repository.

## Overview

The HagiCode release repository manages automated releases of the HagiCode platform. This repository integrates with multiple AI agents to streamline release workflows and spec-driven development.

## Supported AI Agents

### Claude Code CLI

**Purpose**: AI-assisted development and code generation
**Version**: 2.1.34
**Installation**: Included in Docker base images via npm

The Claude Code CLI is pre-installed in all Docker base images and provides:
- AI-powered code generation and refactoring
- Interactive development assistance
- File and project context understanding
- Multi-agent collaboration support

**Usage**:
```bash
# Run Claude Code within a HagiCode container
claude --help

# Open the HagiCode project in Claude Code
claude --project /path/to/hagicode-mono
```

### OpenSpec CLI

**Purpose**: Spec-driven development workflow management
**Version**: >=1.0.0 <2.0.0
**Installation**: Included in Docker base images via npm

The OpenSpec CLI manages proposals, changes, and specifications:
- Create and manage OpenSpec proposals
- Track implementation progress through task lists
- Generate specs from templates
- Integrate with AI assistants for spec creation

**Usage**:
```bash
# List available OpenSpec commands
openspec --help

# Check OpenSpec version
openspec --version

# View OpenSpec status
openspec status
```

## Docker Integration

All AI agents are pre-installed in the Docker base images:

- **Base Images**:
  - `hagicode/hagicode:base` - AMD64 base image
  - `hagicode/hagicode:base-arm64` - ARM64 base image

- **Application Images**: Built on top of base images with application code

### AI Agent Configuration

The Docker entrypoint script (`docker-entrypoint.sh`) automatically configures AI agents based on environment variables:

#### Claude Code Configuration

- `ANTHROPIC_AUTH_TOKEN`: Anthropic API token (highest priority)
- `ANTHROPIC_URL`: Custom Anthropic API endpoint
- `ANTHROPIC_SONNET_MODEL`: Default Sonnet model
- `ANTHROPIC_OPUS_MODEL`: Default Opus model
- `ANTHROPIC_HAIKU_MODEL`: Default Haiku model
- `CLAUDE_HOST_CONFIG_ENABLED`: Enable/disable host config mount (default: true)
- `CLAUDE_CONFIG_MOUNT_PATH`: Path for mounted Claude config (default: /claude-mount)
- `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS`: Enable Agent Teams feature (default: 1)

#### Host Configuration

Claude Code can be configured using host-mounted config files:

```bash
docker run -v ~/claude-config:/claude-mount hagicode/hagicode
```

The container will automatically copy settings from `/claude-mount/settings.json` to the hagicode user's `.claude` directory.

## Build System Integration

The Nuke build system integrates AI agents through:

1. **Automated CLI execution**: Build targets can invoke CLI commands
2. **Version injection**: AI agents receive version information from build parameters
3. **Workflow triggers**: AI agents can be invoked during release workflows

## Release Workflow

The release process involves the following AI agent-aware steps:

1. **Version Monitor**: Detects new versions in Azure Blob Storage
2. **Download**: Downloads application packages
3. **GitHub Release**: Creates GitHub releases with artifacts
4. **Docker Build**: Builds multi-arch Docker images with AI agents pre-installed
5. **Edge ACR Push**: Publishes images to Edge ACR registry

AI agents are available in all Docker images pushed to registries, enabling:
- AI-assisted development when pulling images
- Spec-driven workflows for changes
- Multi-architecture support for AI tools

## Troubleshooting

### Claude Code Not Working

**Symptom**: Claude Code CLI commands fail or show "not configured" errors

**Solutions**:
1. Ensure `ANTHROPIC_AUTH_TOKEN` environment variable is set
2. Check host config mount path is correct
3. Verify permissions on mounted config directory

### OpenSpec CLI Not Working

**Symptom**: OpenSpec commands fail or show version errors

**Solutions**:
1. Verify OpenSpec CLI version >=1.0.0 <2.0.0
2. Check that the Docker image includes the `@fission-ai/openspec@1` package
3. Ensure network connectivity for OpenSpec operations

### Docker Build Issues

**Symptom**: Docker build fails with CLI-related errors

**Solutions**:
1. Ensure npm packages are installed correctly in base image
2. Check that base image is pushed to registry before app build
3. Verify multi-arch builder is set up correctly

## Contributing

When contributing to this repository:

1. Maintain AI agent version compatibility
2. Test Docker builds with both AMD64 and ARM64 platforms
3. Update this AGENTS.md when changing AI agent versions
4. Ensure environment variable documentation is updated

## Version Compatibility Matrix

| Component | Version Required | Installed Version | Status |
|------------|------------------|-------------------|--------|
| .NET SDK | 10.0 | 10.0 | ✓ |
| Docker | >=20.10 with buildx | Latest | ✓ |
| QEMU | Any | Via binfmt image | ✓ |
| Claude Code CLI | - | 2.1.34 | ✓ |
| OpenSpec CLI | >=1.0.0 <2.0.0 | 1.x | ✓ |
| UIPro CLI | - | 2.1.3 | ✓ |

## Additional Resources

- [Claude Code Documentation](https://claude.ai/code)
- [OpenSpec Documentation](https://openspec.dev)
- [Docker Buildx Documentation](https://docs.docker.com/buildx/working-with-buildx/)
- [HagiCode Documentation](https://hagicode.dev/docs)
