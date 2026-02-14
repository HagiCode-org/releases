# Hagicode Container Environment Variables

This document provides comprehensive documentation for all environment variables supported by the Hagicode Docker container.

## Table of Contents

- [User and Permission Variables](#user-and-permission-variables)
- [Claude Code Configuration Variables](#claude-code-configuration-variables)
- [Built-in Environment Variables](#built-in-environment-variables)
- [Usage Examples](#usage-examples)

## User and Permission Variables

These variables control the user permissions inside the container, allowing proper file access for mounted volumes.

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `PUID` | User ID for the `hagicode` user inside the container | `1000` | `1000` |
| `PGID` | Group ID for the `hagicode` group inside the container | `1000` | `1000` |

**Notes:**
- The container runs as the `hagicode` user (not root) for security
- Set these to match your host user's UID/GID to avoid permission issues with mounted volumes
- To find your host UID/GID, run `id` on Linux/macOS or check your user account settings

## Claude Code Configuration Variables

These variables configure the Claude Code AI assistant features inside the container.

### Priority Order

The container tries configuration sources in this order:
1. `ANTHROPIC_AUTH_TOKEN` - Custom Anthropic API endpoint (highest priority)
2. Host configuration mount - Files from `$CLAUDE_CONFIG_MOUNT_PATH`

### Anthropic API Configuration

| Variable | Description | Required | Default | Example |
|----------|-------------|----------|---------|---------|
| `ANTHROPIC_AUTH_TOKEN` | Anthropic API key for Claude Code AI features | No | - | `sk-ant-xxxxxxxxxxxxx` |
| `ANTHROPIC_URL` | Custom Anthropic API endpoint URL | No | Anthropic official | `https://api.anthropic.com` |
| `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS` | Enable multi-agent collaboration (1=enabled, 0=disabled) | No | `1` | `1` |

**Usage Notes:**
- When `ANTHROPIC_AUTH_TOKEN` is set, the container uses Anthropic's official API
- Set `ANTHROPIC_URL` to use a custom proxy or compatible endpoint
- Get your API key from: https://console.anthropic.com/

### Model Configuration

| Variable | Description | Required | Default | Example |
|----------|-------------|----------|---------|---------|
| `ANTHROPIC_SONNET_MODEL` | Sonnet layer model version for Claude Code | No | - | `claude-sonnet-4-20250514` |
| `ANTHROPIC_OPUS_MODEL` | Opus layer model version for Claude Code | No | - | `claude-opus-4-20250514` |
| `ANTHROPIC_HAIKU_MODEL` | Haiku layer model version for Claude Code | No | - | `claude-haiku-4-20250514` |

**Usage Notes:**
- Model variables are only configured when set (no default values)
- When set, these variables override the default model selection
- Works with any Anthropic-compatible API endpoint

### Host Configuration Mount

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `CLAUDE_CONFIG_MOUNT_PATH` | Path to mount host Claude configuration files | `/claude-mount` | `/host-config/claude` |
| `CLAUDE_HOST_CONFIG_ENABLED` | Enable/disable host config mounting (true/false) | `true` | `false` |

**Usage Notes:**
- Used when `ANTHROPIC_AUTH_TOKEN` is not set
- Mount your host's `~/.claude` directory to share Claude Code settings
- The container copies `settings.json` from the mount path into the container
- Set `CLAUDE_HOST_CONFIG_ENABLED=false` to disable this feature

### Agent Teams Feature

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS` | Enable multi-agent collaboration feature | `1` | `0` |

**Usage Notes:**
- Set to `1` or leave unset to enable Agent Teams
- Set to `0` to disable Agent Teams
- This feature allows multiple AI agents to collaborate on tasks

## Built-in Environment Variables

These variables are pre-configured in the container and should not need modification.

| Variable | Description | Default |
|----------|-------------|---------|
| `DOTNET_ROOT` | Path to .NET runtime | `/usr/share/dotnet` |
| `PATH` | System executable search path | Includes `/usr/share/dotnet` and npm global bin |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core environment | `Production` |
| `ASPNETCORE_URLS` | ASP.NET Core binding URL | `http://+:5000` |

**Usage Notes:**
- The application listens on port 5000 by default
- To change the port, map it using `-p` flag: `docker run -p 8080:5000 ...`

## Usage Examples

### Using Anthropic API

```bash
docker run -d \
  -p 5000:5000 \
  -e ANTHROPIC_AUTH_TOKEN="sk-ant-your-key-here" \
  -e ANTHROPIC_SONNET_MODEL="claude-sonnet-4-20250514" \
  -e ANTHROPIC_OPUS_MODEL="claude-opus-4-20250514" \
  --name hagicode \
  newbe36524/hagicode:latest
```

### Using Host Configuration Mount

```bash
docker run -d \
  -p 5000:5000 \
  -v ~/.claude:/claude-mount \
  -e CLAUDE_CONFIG_MOUNT_PATH="/claude-mount" \
  --name hagicode \
  newbe36524/hagicode:latest
```

### Custom User Permissions

```bash
# Find your UID/GID on host
id -u
id -g

# Run with matching permissions
docker run -d \
  -p 5000:5000 \
  -e ANTHROPIC_AUTH_TOKEN="sk-ant-your-key-here" \
  -e PUID="$(id -u)" \
  -e PGID="$(id -g)" \
  -v $(pwd)/data:/app/data \
  --name hagicode \
  newbe36524/hagicode:latest
```

### Docker Compose Example

```yaml
version: '3.8'

services:
  hagicode:
    image: newbe36524/hagicode:latest
    ports:
      - "5000:5000"
    environment:
      # Anthropic API authentication
      - ANTHROPIC_AUTH_TOKEN=${ANTHROPIC_AUTH_TOKEN}

      # Optional model configuration (only configured when set)
      - ANTHROPIC_SONNET_MODEL=${ANTHROPIC_SONNET_MODEL}
      - ANTHROPIC_OPUS_MODEL=${ANTHROPIC_OPUS_MODEL}
      - ANTHROPIC_HAIKU_MODEL=${ANTHROPIC_HAIKU_MODEL}

      # Agent Teams feature
      - CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=${CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS:-1}

      # User permissions (optional, for volume mounts)
      - PUID=${PUID:-1000}
      - PGID=${PGID:-1000}

    volumes:
      - ./data:/app/data
      # Uncomment to use host Claude config instead of API key
      # - ~/.claude:/claude-mount
```

### Kubernetes Example

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: hagicode-config
data:
  claude-code-experimental-agent-teams: "1"
---
apiVersion: v1
kind: Secret
metadata:
  name: hagicode-secrets
type: Opaque
stringData:
  anthropic-auth-token: "sk-ant-your-key-here"
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: hagicode
spec:
  replicas: 1
  selector:
    matchLabels:
      app: hagicode
  template:
    metadata:
      labels:
        app: hagicode
    spec:
      containers:
      - name: hagicode
        image: newbe36524/hagicode:latest
        ports:
        - containerPort: 5000
        envFrom:
        - configMapRef:
            name: hagicode-config
        - secretRef:
            name: hagicode-secrets
```

## Configuration Source Priority

When multiple configuration sources are available, the container uses them in this order:

1. **ANTHROPIC_AUTH_TOKEN** (highest priority)
   - If set, uses Anthropic API with this token
   - `ANTHROPIC_URL` can customize the endpoint

2. **ZAI_API_KEY** (second priority)
   - If set (and ANTHROPIC_AUTH_TOKEN is not), uses Zhipu AI API
   - Automatically configures ZAI model endpoints

3. **Host Configuration Mount** (fallback)
   - If neither API key is set, tries to use mounted configuration
   - Copies files from `CLAUDE_CONFIG_MOUNT_PATH` to container

4. **No Configuration** (error)
   - If no configuration is found, Claude Code features will not work
   - Container will start but AI features will fail

## Environment Variable Reference

Complete list of all environment variables:

| Variable | Type | Required | Default |
|----------|------|----------|---------|
| **User Permissions** |
| `PUID` | integer | No | `1000` |
| `PGID` | integer | No | `1000` |
| **Claude Code - Anthropic** |
| `ANTHROPIC_AUTH_TOKEN` | string (sensitive) | No* | - |
| `ANTHROPIC_URL` | URL | No | Anthropic official |
| **Claude Code - Models** |
| `ANTHROPIC_SONNET_MODEL` | string | No | - |
| `ANTHROPIC_OPUS_MODEL` | string | No | - |
| `ANTHROPIC_HAIKU_MODEL` | string | No | - |
| **Claude Code - Host Config** |
| `CLAUDE_CONFIG_MOUNT_PATH` | path | No | `/claude-mount` |
| `CLAUDE_HOST_CONFIG_ENABLED` | boolean | No | `true` |
| **Claude Code - Features** |
| `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS` | boolean | No | `1` |
| **Built-in** |
| `DOTNET_ROOT` | path | Yes | `/usr/share/dotnet` |
| `PATH` | path | Yes | (auto-configured) |
| `ASPNETCORE_ENVIRONMENT` | string | Yes | `Production` |
| `ASPNETCORE_URLS` | URL | Yes | `http://+:5000` |

*`ANTHROPIC_AUTH_TOKEN` or host configuration is required for Claude Code features.

## Security Best Practices

1. **Never commit API keys** to version control
2. **Use Docker Secrets** or **Kubernetes Secrets** for sensitive values in production
3. **Rotate API keys** regularly
4. **Use least privilege** API tokens with minimal required scopes
5. **Run as non-root user** - Container already runs as `hagicode` user
6. **Scan images** for vulnerabilities before deployment

## Troubleshooting

### Claude Code Not Working

**Problem:** Claude Code features fail with authentication errors

**Solutions:**
1. Verify `ANTHROPIC_AUTH_TOKEN` is set correctly
2. Check API key has not expired or been revoked
3. Ensure network connectivity to the API endpoint
4. Check container logs: `docker logs hagicode`

### Permission Denied Errors

**Problem:** Cannot write to mounted volumes

**Solutions:**
1. Set `PUID` and `PGID` to match your host user: `id -u` and `id -g`
2. Ensure mounted volumes have correct ownership
3. Run `chown -R UID:GID ./data` on host before mounting

### Port Already in Use

**Problem:** Cannot start container, port 5000 already in use

**Solutions:**
1. Map to a different host port: `-p 8080:5000`
2. Stop the conflicting service
3. Use Docker Compose with port management

## Related Documentation

- [README.md](../README.md) - Project overview and quick start
- [Docker Hub](https://hub.docker.com/r/newbe36524/hagicode) - Container image registry
- [Anthropic API Documentation](https://docs.anthropic.com/) - Claude API reference
