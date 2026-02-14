#!/bin/bash
# Docker Entrypoint Script for HagiCode
# This script configures Claude Code settings before starting the application

set -e

# Configure user UID/GID to match host user if specified
# This allows proper file permissions for mounted volumes
if [ -n "$PUID" ] && [ -n "$PGID" ]; then
    echo "Configuring user permissions..."

    # Check if user needs to be created/modified
    if ! id hagicode >/dev/null 2>&1; then
        # User doesn't exist, create it
        groupadd -g "$PGID" hagicode
        useradd -u "$PUID" -g "$PGID" -s /bin/bash -m hagicode
        echo "✓ Created hagicode user with UID=$PUID, GID=$PGID"
    else
        # User exists, check if UID/GID need to be updated
        CURRENT_UID=$(id -u hagicode)
        CURRENT_GID=$(id -g hagicode)

        if [ "$CURRENT_UID" != "$PUID" ] || [ "$CURRENT_GID" != "$PGID" ]; then
            # Modify existing user
            deluser hagicode
            groupadd -g "$PGID" hagicode 2>/dev/null || true
            useradd -u "$PUID" -g "$PGID" -s /bin/bash -m hagicode
            chown -R hagicode:hagicode /home/hagicode /app
            echo "✓ Updated hagicode user to UID=$PUID, GID=$PGID"
        fi
    fi
fi

# ==================================================
# Claude Code Configuration
# ==================================================
# Configuration Priority: ANTHROPIC_AUTH_TOKEN > Host Config > None
# Host config uses a copy mechanism to solve permission issues

CLAUDE_CONFIGURED=false

# Enable Agent Teams feature by default (set to 0 or empty to disable)
CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS="${CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS:-1}"

# 1. Check if ANTHROPIC_AUTH_TOKEN environment variable is set (highest priority)
if [ -n "$ANTHROPIC_AUTH_TOKEN" ]; then
    echo "✓ Configuring Claude Code with custom Anthropic API endpoint..."
    echo "  Source: ANTHROPIC_AUTH_TOKEN environment variable"

    # Create .claude directory for hagicode user
    mkdir -p /home/hagicode/.claude

    # Build settings.json dynamically based on available configuration
    SETTINGS_BASE=$(cat <<EOF
{
  "env": {
    "ANTHROPIC_AUTH_TOKEN": "${ANTHROPIC_AUTH_TOKEN}",
EOF
)

    # Add ANTHROPIC_BASE_URL if custom URL is provided
    if [ -n "$ANTHROPIC_URL" ]; then
        echo "  Custom API URL: $ANTHROPIC_URL"
        SETTINGS_BASE="${SETTINGS_BASE}
    \"ANTHROPIC_BASE_URL\": \"${ANTHROPIC_URL}\","
    fi

    # Add common settings
    SETTINGS_BASE="${SETTINGS_BASE}
    \"API_TIMEOUT_MS\": \"3000000\",
    \"CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC\": 1,
    \"CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS\": \"${CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS}\""

    # Add model configurations only if variables are set
    if [ -n "$ANTHROPIC_SONNET_MODEL" ] || [ -n "$ANTHROPIC_OPUS_MODEL" ] || [ -n "$ANTHROPIC_HAIKU_MODEL" ]; then
        SETTINGS_BASE="${SETTINGS_BASE},"

        if [ -n "$ANTHROPIC_HAIKU_MODEL" ]; then
            SETTINGS_BASE="${SETTINGS_BASE}
    \"ANTHROPIC_DEFAULT_HAIKU_MODEL\": \"${ANTHROPIC_HAIKU_MODEL}\""
        fi

        if [ -n "$ANTHROPIC_SONNET_MODEL" ]; then
            # Add comma if Haiku model was also set
            if [ -n "$ANTHROPIC_HAIKU_MODEL" ]; then
                SETTINGS_BASE="${SETTINGS_BASE},"
            fi
            SETTINGS_BASE="${SETTINGS_BASE}
    \"ANTHROPIC_DEFAULT_SONNET_MODEL\": \"${ANTHROPIC_SONNET_MODEL}\""
        fi

        if [ -n "$ANTHROPIC_OPUS_MODEL" ]; then
            # Add comma if Haiku or Sonnet model was also set
            if [ -n "$ANTHROPIC_HAIKU_MODEL" ] || [ -n "$ANTHROPIC_SONNET_MODEL" ]; then
                SETTINGS_BASE="${SETTINGS_BASE},"
            fi
            SETTINGS_BASE="${SETTINGS_BASE}
    \"ANTHROPIC_DEFAULT_OPUS_MODEL\": \"${ANTHROPIC_OPUS_MODEL}\""
        fi
    fi

    # Close the JSON
    echo "${SETTINGS_BASE}
  }
}" > /home/hagicode/.claude/settings.json

    # Write .claude.json to skip onboarding
    cat > /home/hagicode/.claude.json << EOF
{
  "hasCompletedOnboarding": true
}
EOF

    # Ensure proper ownership
    chown -R hagicode:hagicode /home/hagicode/.claude /home/hagicode/.claude.json
    chmod 600 /home/hagicode/.claude/settings.json

    CLAUDE_CONFIGURED=true
    echo "✓ Claude Code configured with custom Anthropic API endpoint"

# 2. If ANTHROPIC_AUTH_TOKEN is not set, check for mounted Claude config (host config)
# Auto-detect host configuration unless explicitly disabled
else
    # Default mount path is /claude-mount, can be customized via CLAUDE_CONFIG_MOUNT_PATH
    MOUNT_PATH="${CLAUDE_CONFIG_MOUNT_PATH:-/claude-mount}"

    # Check if host config is explicitly disabled
    if [ "$CLAUDE_HOST_CONFIG_ENABLED" = "false" ]; then
        echo "⚠ Warning: No Claude configuration found"
        echo "  - ANTHROPIC_AUTH_TOKEN is not set"
        echo "  - Host configuration is explicitly disabled (CLAUDE_HOST_CONFIG_ENABLED=false)"
        echo "  → Claude Code features may not work properly"
    else
        echo "✓ Checking for host Claude configuration..."
        echo "  Mount path: $MOUNT_PATH"

        # Check if mount path exists with valid config files
        if [ -e "$MOUNT_PATH" ] || [ -e "$MOUNT_PATH/settings.json" ]; then
            echo "  Host config detected, attempting to use mounted configuration..."

            # Create target directory
            mkdir -p /home/hagicode/.claude

            CONFIG_FOUND=false
            CONFIG_SOURCE=""

            # Copy settings.json from mount
            if [ -f "$MOUNT_PATH/settings.json" ]; then
                echo "  Found: settings.json file at $MOUNT_PATH/settings.json"
                cp "$MOUNT_PATH/settings.json" /home/hagicode/.claude/settings.json
                chown hagicode:hagicode /home/hagicode/.claude/settings.json
                chmod 600 /home/hagicode/.claude/settings.json
                CONFIG_FOUND=true
                CONFIG_SOURCE="$MOUNT_PATH/settings.json"
                echo "    ✓ Copied settings.json"
            fi

            # Always create .claude.json with fixed content
            cat > /home/hagicode/.claude.json << EOF
{
  "hasCompletedOnboarding": true
}
EOF
            chown hagicode:hagicode /home/hagicode/.claude.json
            echo "    ✓ Created .claude.json (onboarding skip)"

            if [ "$CONFIG_FOUND" = true ]; then
                # Ensure entire .claude directory has correct ownership
                chown -R hagicode:hagicode /home/hagicode/.claude
                CLAUDE_CONFIGURED=true

                echo "✓ Claude Code configured with host configuration"
                echo "  Source: $CONFIG_SOURCE"
                echo "  Action: Copied to /home/hagicode/.claude/"
                echo "  Permissions: Configured for hagicode user (600)"
            else
                echo "⚠ Warning: Mount path exists but no valid config files found"
                echo "  Expected: $MOUNT_PATH/settings.json"
                echo "  → Claude Code features may not work properly"
            fi

        else
            echo "⚠ Warning: No Claude configuration found"
            echo "  - ANTHROPIC_AUTH_TOKEN is not set"
            echo "  - No host configuration found at $MOUNT_PATH"
            echo "  → Claude Code features may not work properly"
            echo "  → Set ANTHROPIC_AUTH_TOKEN or mount host config to use Claude Code"
        fi
    fi
fi

# ==================================================
# Start Application
# ==================================================

# Start the HagiCode Web application as hagicode user
# HagiCode requires the .NET runtime to run
# Change to app directory first
cd /app

# Try different possible entry points
if [ -f "/app/PCode.Web.dll" ]; then
    exec gosu hagicode dotnet PCode.Web.dll
elif [ -f "/app/Hagicode.dll" ]; then
    exec gosu hagicode dotnet Hagicode.dll
elif [ -f "/app/lib/PCode.Web.dll" ]; then
    exec gosu hagicode dotnet lib/PCode.Web.dll
elif [ -f "/app/lib/Hagicode.dll" ]; then
    exec gosu hagicode dotnet lib/Hagicode.dll
else
    echo "Error: Could not find application entry point"
    echo "Looked for: PCode.Web.dll, Hagicode.dll, lib/PCode.Web.dll, lib/Hagicode.dll"
    exit 1
fi
