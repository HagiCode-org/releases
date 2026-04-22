#!/bin/bash
# Docker Entrypoint Script for HagiCode
# This script configures Claude Code settings before starting the application

set -e

HAGICODE_USER="hagicode"
HAGICODE_GROUP="hagicode"
HAGICODE_HOME="/home/hagicode"
HAGICODE_CLAUDE_DIR="${HAGICODE_HOME}/.claude"
HAGICODE_CLAUDE_STATE_FILE="${HAGICODE_HOME}/.claude.json"
HAGICODE_NPM_PREFIX="${HAGICODE_HOME}/.npm-global"
HAGICODE_SSH_DIR="${HAGICODE_HOME}/.ssh"
HAGICODE_IMPORTED_SSH_KEY="${HAGICODE_SSH_DIR}/imported_key"
HAGICODE_IMPORTED_KNOWN_HOSTS="${HAGICODE_SSH_DIR}/known_hosts"
HAGICODE_SSH_CONFIG_FILE="${HAGICODE_SSH_DIR}/config"
HAGICODE_SSH_MANAGED_BEGIN="# >>> HAGICODE SSH BOOTSTRAP >>>"
HAGICODE_SSH_MANAGED_END="# <<< HAGICODE SSH BOOTSTRAP <<<"
SSH_STRICT_HOST_KEY_CHECKING_DEFAULT="accept-new"
HAGICODE_APP_DIR="/app"
HAGICODE_APP_DATA_DIR="${HAGICODE_APP_DIR}/data"
HAGICODE_APP_SAVES_DIR="${HAGICODE_APP_DIR}/saves"
HAGICODE_CODE_SERVER_CONFIG_DIR="${HAGICODE_HOME}/.config/code-server"
HAGICODE_CODE_SERVER_CACHE_DIR="${HAGICODE_HOME}/.cache/code-server"
HAGICODE_CODE_SERVER_SHARE_DIR="${HAGICODE_HOME}/.local/share/code-server"
HAGICODE_CODE_SERVER_DATA_DIR="${HAGICODE_APP_DATA_DIR}/code-server"

export HOME="$HAGICODE_HOME"
export NPM_CONFIG_PREFIX="${NPM_CONFIG_PREFIX:-$HAGICODE_NPM_PREFIX}"
export PATH="${HAGICODE_NPM_PREFIX}/bin:${PATH}"

run_as_hagicode() {
    gosu "$HAGICODE_USER" env \
        HOME="$HAGICODE_HOME" \
        USER="$HAGICODE_USER" \
        LOGNAME="$HAGICODE_USER" \
        PATH="$PATH" \
        NPM_CONFIG_PREFIX="$HAGICODE_NPM_PREFIX" \
        "$@"
}

exec_as_hagicode() {
    exec gosu "$HAGICODE_USER" env \
        HOME="$HAGICODE_HOME" \
        USER="$HAGICODE_USER" \
        LOGNAME="$HAGICODE_USER" \
        PATH="$PATH" \
        NPM_CONFIG_PREFIX="$HAGICODE_NPM_PREFIX" \
        "$@"
}

ensure_hagicode_runtime_paths() {
    mkdir -p \
        "$HAGICODE_CLAUDE_DIR" \
        "$HAGICODE_NPM_PREFIX" \
        "$HAGICODE_CODE_SERVER_CONFIG_DIR" \
        "$HAGICODE_CODE_SERVER_CACHE_DIR" \
        "$HAGICODE_CODE_SERVER_SHARE_DIR" \
        "$HAGICODE_CODE_SERVER_DATA_DIR" \
        "$HAGICODE_APP_DATA_DIR" \
        "$HAGICODE_APP_SAVES_DIR" \
        "$HAGICODE_APP_DIR"
    chown -R "$HAGICODE_USER:$HAGICODE_GROUP" "$HAGICODE_HOME" "$HAGICODE_APP_DIR"
}

fail_startup() {
    echo "Error: $*" >&2
    exit 1
}

validate_readable_file_path() {
    local env_name="$1"
    local file_path="$2"

    if [ ! -e "$file_path" ]; then
        fail_startup "${env_name} points to a missing path: ${file_path}"
    fi

    if [ ! -f "$file_path" ]; then
        fail_startup "${env_name} must point to a readable file: ${file_path}"
    fi

    if [ ! -r "$file_path" ]; then
        fail_startup "${env_name} is not readable by container startup: ${file_path}"
    fi
}

validate_strict_host_key_checking() {
    local strict_value="$1"

    case "$strict_value" in
        yes|no|ask|accept-new|off)
            ;;
        *)
            fail_startup "SSH_STRICT_HOST_KEY_CHECKING must be one of: yes, no, ask, accept-new, off"
            ;;
    esac
}

strip_managed_ssh_block() {
    local config_path="$1"

    if [ ! -f "$config_path" ]; then
        return 0
    fi

    awk -v begin="$HAGICODE_SSH_MANAGED_BEGIN" -v end="$HAGICODE_SSH_MANAGED_END" '
        $0 == begin { skip = 1; next }
        $0 == end { skip = 0; next }
        skip == 0 { print }
    ' "$config_path"
}

configure_ssh_private_key_if_needed() {
    local private_key_path="${SSH_PRIVATE_KEY_PATH:-}"
    local known_hosts_path="${SSH_KNOWN_HOSTS_PATH:-}"
    local strict_host_key_checking="${SSH_STRICT_HOST_KEY_CHECKING:-$SSH_STRICT_HOST_KEY_CHECKING_DEFAULT}"
    local temp_config=""
    local temp_existing_config=""

    if [ -z "$private_key_path" ]; then
        echo "✓ SSH bootstrap skipped: SSH_PRIVATE_KEY_PATH is not set."
        return 0
    fi

    validate_readable_file_path "SSH_PRIVATE_KEY_PATH" "$private_key_path"

    if [ -n "$known_hosts_path" ]; then
        validate_readable_file_path "SSH_KNOWN_HOSTS_PATH" "$known_hosts_path"
    fi

    validate_strict_host_key_checking "$strict_host_key_checking"

    mkdir -p "$HAGICODE_SSH_DIR"
    cp "$private_key_path" "$HAGICODE_IMPORTED_SSH_KEY"

    if [ -n "$known_hosts_path" ]; then
        cp "$known_hosts_path" "$HAGICODE_IMPORTED_KNOWN_HOSTS"
    else
        : > "$HAGICODE_IMPORTED_KNOWN_HOSTS"
    fi

    temp_config="$(mktemp)"
    cat > "$temp_config" <<EOF
$HAGICODE_SSH_MANAGED_BEGIN
Host *
  IdentityFile $HAGICODE_IMPORTED_SSH_KEY
  IdentitiesOnly yes
  UserKnownHostsFile $HAGICODE_IMPORTED_KNOWN_HOSTS
  StrictHostKeyChecking $strict_host_key_checking
$HAGICODE_SSH_MANAGED_END
EOF

    if [ -f "$HAGICODE_SSH_CONFIG_FILE" ]; then
        temp_existing_config="$(mktemp)"
        strip_managed_ssh_block "$HAGICODE_SSH_CONFIG_FILE" > "$temp_existing_config"

        if [ -s "$temp_existing_config" ]; then
            printf "\n" >> "$temp_config"
            cat "$temp_existing_config" >> "$temp_config"
        fi
    fi

    mv "$temp_config" "$HAGICODE_SSH_CONFIG_FILE"
    rm -f "$temp_existing_config"

    chown -R "$HAGICODE_USER:$HAGICODE_GROUP" "$HAGICODE_SSH_DIR"
    chmod 700 "$HAGICODE_SSH_DIR"
    chmod 600 "$HAGICODE_IMPORTED_SSH_KEY"
    chmod 644 "$HAGICODE_IMPORTED_KNOWN_HOSTS" "$HAGICODE_SSH_CONFIG_FILE"

    export GIT_SSH_COMMAND="ssh -F ${HAGICODE_SSH_CONFIG_FILE}"
    export HAGICODE_IMPORTED_SSH_KEY_PATH="$HAGICODE_IMPORTED_SSH_KEY"
    export HAGICODE_IMPORTED_KNOWN_HOSTS_PATH="$HAGICODE_IMPORTED_KNOWN_HOSTS"

    echo "✓ SSH bootstrap configured from SSH_PRIVATE_KEY_PATH: $private_key_path"
    if [ -n "$known_hosts_path" ]; then
        echo "  Known hosts source: SSH_KNOWN_HOSTS_PATH=$known_hosts_path"
    else
        echo "  Known hosts source: none provided; using managed runtime file"
    fi
    echo "  StrictHostKeyChecking: $strict_host_key_checking"
    echo "  Git/SSH wiring: GIT_SSH_COMMAND uses ${HAGICODE_SSH_CONFIG_FILE}"
}

configure_code_server_runtime_if_needed() {
    local auth_mode="${VsCodeServer__CodeServerAuthMode:-none}"
    local resolved_password="${PASSWORD:-}"
    local resolved_hashed_password="${HASHED_PASSWORD:-}"

    if ! command -v code-server >/dev/null 2>&1; then
        fail_startup "code-server is not available on PATH; rebuild the image with the pinned binary installed"
    fi

    run_as_hagicode code-server --version >/dev/null

    if [ -n "${CODE_SERVER_PASSWORD:-}" ]; then
        export PASSWORD="${CODE_SERVER_PASSWORD}"
        resolved_password="${CODE_SERVER_PASSWORD}"
        echo "✓ Code Server password source: CODE_SERVER_PASSWORD (masked)"
    elif [ -n "$resolved_password" ]; then
        echo "✓ Code Server password source: PASSWORD (masked)"
    fi

    if [ -n "${CODE_SERVER_HASHED_PASSWORD:-}" ]; then
        export HASHED_PASSWORD="${CODE_SERVER_HASHED_PASSWORD}"
        resolved_hashed_password="${CODE_SERVER_HASHED_PASSWORD}"
        echo "✓ Code Server hashed password source: CODE_SERVER_HASHED_PASSWORD (masked)"
    elif [ -n "$resolved_hashed_password" ]; then
        echo "✓ Code Server hashed password source: HASHED_PASSWORD (masked)"
    fi

    if [ "$auth_mode" = "password" ] && [ -z "$resolved_password" ] && [ -z "$resolved_hashed_password" ]; then
        fail_startup "VsCodeServer__CodeServerAuthMode=password requires CODE_SERVER_PASSWORD, CODE_SERVER_HASHED_PASSWORD, PASSWORD, or HASHED_PASSWORD"
    fi

    echo "✓ Code Server runtime bootstrap prepared at ${HAGICODE_CODE_SERVER_DATA_DIR}"
    echo "  Auth mode: ${auth_mode}"
}

validate_accept_eula() {
    local raw_value="${ACCEPT_EULA:-}"
    local normalized_value

    normalized_value="$(printf '%s' "$raw_value" | tr '[:upper:]' '[:lower:]')"

    case "$normalized_value" in
        y|yes|true|1)
            echo "✓ Container EULA acceptance detected: ACCEPT_EULA=${raw_value}"
            ;;
        *)
            fail_startup "ACCEPT_EULA must be set to an accepted opt-in value (Y, YES, TRUE, or 1) before startup"
            ;;
    esac
}

main() {
# Configure user UID/GID to match host user if specified
# This allows proper file permissions for mounted volumes
if [ -n "$PUID" ] && [ -n "$PGID" ]; then
    echo "Configuring user permissions..."

    # Check if user needs to be created/modified
    if ! id "$HAGICODE_USER" >/dev/null 2>&1; then
        # User doesn't exist, create it
        groupadd -g "$PGID" "$HAGICODE_GROUP"
        useradd -u "$PUID" -g "$PGID" -s /bin/bash -m -d "$HAGICODE_HOME" "$HAGICODE_USER"
        echo "✓ Created hagicode user with UID=$PUID, GID=$PGID"
    else
        # User exists, check if UID/GID need to be updated
        CURRENT_UID=$(id -u "$HAGICODE_USER")
        CURRENT_GID=$(id -g "$HAGICODE_USER")

        if [ "$CURRENT_UID" != "$PUID" ] || [ "$CURRENT_GID" != "$PGID" ]; then
            # Modify existing user
            groupmod -o -g "$PGID" "$HAGICODE_GROUP"
            usermod -o -u "$PUID" -g "$PGID" -d "$HAGICODE_HOME" "$HAGICODE_USER"
            echo "✓ Updated hagicode user to UID=$PUID, GID=$PGID"
        fi
    fi
fi

ensure_hagicode_runtime_paths
validate_accept_eula
configure_ssh_private_key_if_needed
configure_code_server_runtime_if_needed

# ==================================================
# CLI Version Overrides
# ==================================================
# Use pinned versions baked into image by default.
# Users can override per tool with:
# - CLAUDE_CODE_CLI_VERSION
# - OPENSPEC_CLI_VERSION
# - CODEX_CLI_VERSION

install_cli_override_if_needed() {
    local display_name="$1"
    local package_name="$2"
    local command_name="$3"
    local pinned_version="$4"
    local override_version="$5"
    local override_env_name="$6"

    if [ -z "$override_version" ]; then
        echo "✓ ${display_name} using pinned version: ${pinned_version}"
        return 0
    fi

    if [ "$override_version" = "$pinned_version" ]; then
        echo "✓ ${display_name} override matches pinned version (${pinned_version}); skipping reinstall."
        return 0
    fi

    echo "✓ ${display_name} version override detected: ${override_env_name}=${override_version}"
    echo "  Installing ${package_name}@${override_version} ..."

    run_as_hagicode npm install -g "${package_name}@${override_version}"
    run_as_hagicode "${command_name}" --version >/dev/null
    run_as_hagicode npm cache clean --force >/dev/null 2>&1 || true

    echo "  Installed ${display_name} ${override_version}"
}

PINNED_CLAUDE_CODE_CLI_VERSION="${PINNED_CLAUDE_CODE_CLI_VERSION:-2.1.71}"
PINNED_OPENSPEC_CLI_VERSION="${PINNED_OPENSPEC_CLI_VERSION:-1.2.0}"
PINNED_OPENCODE_CLI_VERSION="${PINNED_OPENCODE_CLI_VERSION:-1.2.25}"
PINNED_CODEX_CLI_VERSION="${PINNED_CODEX_CLI_VERSION:-0.112.0}"

install_cli_override_if_needed \
    "Claude Code CLI" \
    "@anthropic-ai/claude-code" \
    "claude" \
    "$PINNED_CLAUDE_CODE_CLI_VERSION" \
    "${CLAUDE_CODE_CLI_VERSION:-}" \
    "CLAUDE_CODE_CLI_VERSION"

install_cli_override_if_needed \
    "OpenSpec CLI" \
    "@fission-ai/openspec" \
    "openspec" \
    "$PINNED_OPENSPEC_CLI_VERSION" \
    "${OPENSPEC_CLI_VERSION:-}" \
    "OPENSPEC_CLI_VERSION"

echo "✓ OpenCode CLI using pinned image version: ${PINNED_OPENCODE_CLI_VERSION} (command: opencode)"

install_cli_override_if_needed \
    "Codex CLI" \
    "@openai/codex" \
    "codex" \
    "$PINNED_CODEX_CLI_VERSION" \
    "${CODEX_CLI_VERSION:-}" \
    "CODEX_CLI_VERSION"

if [ -n "$QODER_PERSONAL_ACCESS_TOKEN" ]; then
    export QODER_PERSONAL_ACCESS_TOKEN="$QODER_PERSONAL_ACCESS_TOKEN"
    echo "✓ Qoder runtime token detected: QODER_PERSONAL_ACCESS_TOKEN (masked)"
else
    echo "✓ No Qoder runtime token provided; UI-managed qodercli installs may rely on mounted runtime state."
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
    mkdir -p "$HAGICODE_CLAUDE_DIR"

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

    # Close JSON
    echo "${SETTINGS_BASE}
  }
}" > "$HAGICODE_CLAUDE_DIR/settings.json"

    # Write .claude.json to skip onboarding
    cat > "$HAGICODE_CLAUDE_STATE_FILE" << EOF
{
  "hasCompletedOnboarding": true
}
EOF

    # Ensure proper ownership
    chown -R "$HAGICODE_USER:$HAGICODE_GROUP" "$HAGICODE_CLAUDE_DIR" "$HAGICODE_CLAUDE_STATE_FILE"
    chmod 600 "$HAGICODE_CLAUDE_DIR/settings.json"

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
            mkdir -p "$HAGICODE_CLAUDE_DIR"

            CONFIG_FOUND=false
            CONFIG_SOURCE=""

            # Copy settings.json from mount
            if [ -f "$MOUNT_PATH/settings.json" ]; then
                echo "  Found: settings.json file at $MOUNT_PATH/settings.json"
                cp "$MOUNT_PATH/settings.json" "$HAGICODE_CLAUDE_DIR/settings.json"
                chown "$HAGICODE_USER:$HAGICODE_GROUP" "$HAGICODE_CLAUDE_DIR/settings.json"
                chmod 600 "$HAGICODE_CLAUDE_DIR/settings.json"
                CONFIG_FOUND=true
                CONFIG_SOURCE="$MOUNT_PATH/settings.json"
                echo "    ✓ Copied settings.json"
            fi

            # Always create .claude.json with fixed content
            cat > "$HAGICODE_CLAUDE_STATE_FILE" << EOF
{
  "hasCompletedOnboarding": true
}
EOF
            chown "$HAGICODE_USER:$HAGICODE_GROUP" "$HAGICODE_CLAUDE_STATE_FILE"
            echo "    ✓ Created .claude.json (onboarding skip)"

            if [ "$CONFIG_FOUND" = true ]; then
                # Ensure entire .claude directory has correct ownership
                chown -R "$HAGICODE_USER:$HAGICODE_GROUP" "$HAGICODE_CLAUDE_DIR"
                CLAUDE_CONFIGURED=true

                echo "✓ Claude Code configured with host configuration"
                echo "  Source: $CONFIG_SOURCE"
                echo "  Action: Copied to ${HAGICODE_CLAUDE_DIR}/"
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
# Copilot Global Settings Bootstrap
# ==================================================
# Copilot runtime variables are isolated from Codex/OpenAI variables.

if [ -n "$COPILOT_BASE_URL" ] || [ -n "$COPILOT_API_KEY" ]; then
    echo "✓ Configuring Copilot global settings from environment variables..."

    if [ -n "$COPILOT_BASE_URL" ]; then
        export COPILOT_BASE_URL="$COPILOT_BASE_URL"
        echo "  Base URL source: COPILOT_BASE_URL"
    fi

    if [ -n "$COPILOT_API_KEY" ]; then
        export COPILOT_API_KEY="$COPILOT_API_KEY"
        echo "  API key source: COPILOT_API_KEY (masked)"
    fi

    if [ -z "$COPILOT_BASE_URL" ] || [ -z "$COPILOT_API_KEY" ]; then
        echo "  ⚠ Warning: Copilot endpoint or API key is missing; CLI connectivity may be limited."
    fi
else
    echo "✓ No Copilot global overrides provided; using existing Copilot defaults."
fi

# ==================================================
# Codex Global Settings Bootstrap
# ==================================================
# Runtime precedence:
# - Base URL: CODEX_BASE_URL > OPENAI_BASE_URL
# - API key:  CODEX_API_KEY > OPENAI_API_KEY

CODEX_RESOLVED_BASE_URL=""
CODEX_BASE_SOURCE=""
if [ -n "$CODEX_BASE_URL" ]; then
    CODEX_RESOLVED_BASE_URL="$CODEX_BASE_URL"
    CODEX_BASE_SOURCE="CODEX_BASE_URL"
elif [ -n "$OPENAI_BASE_URL" ]; then
    CODEX_RESOLVED_BASE_URL="$OPENAI_BASE_URL"
    CODEX_BASE_SOURCE="OPENAI_BASE_URL"
fi

CODEX_RESOLVED_API_KEY=""
CODEX_API_SOURCE=""
if [ -n "$CODEX_API_KEY" ]; then
    CODEX_RESOLVED_API_KEY="$CODEX_API_KEY"
    CODEX_API_SOURCE="CODEX_API_KEY"
elif [ -n "$OPENAI_API_KEY" ]; then
    CODEX_RESOLVED_API_KEY="$OPENAI_API_KEY"
    CODEX_API_SOURCE="OPENAI_API_KEY"
fi

if [ -n "$CODEX_RESOLVED_BASE_URL" ] || [ -n "$CODEX_RESOLVED_API_KEY" ]; then
    echo "✓ Configuring Codex global settings from environment variables..."

    if [ -n "$CODEX_RESOLVED_BASE_URL" ]; then
        export CODEX_BASE_URL="$CODEX_RESOLVED_BASE_URL"
        export OPENAI_BASE_URL="$CODEX_RESOLVED_BASE_URL"
        echo "  Base URL source: $CODEX_BASE_SOURCE"
    fi

    if [ -n "$CODEX_RESOLVED_API_KEY" ]; then
        export CODEX_API_KEY="$CODEX_RESOLVED_API_KEY"
        export OPENAI_API_KEY="$CODEX_RESOLVED_API_KEY"
        echo "  API key source: $CODEX_API_SOURCE (masked)"
    fi

    if [ -z "$CODEX_RESOLVED_BASE_URL" ] || [ -z "$CODEX_RESOLVED_API_KEY" ]; then
        echo "  ⚠ Warning: Codex endpoint or API key is missing; CLI connectivity may be limited."
    fi
else
    echo "✓ No Codex global overrides provided; using existing Codex defaults."
fi

# ==================================================
# Start Application
# ==================================================

# Start the HagiCode Web application as hagicode user
# HagiCode requires .NET runtime to run
# Change to app directory first
cd /app

# Try different possible entry points
if [ -f "/app/PCode.Web.dll" ]; then
    exec_as_hagicode dotnet PCode.Web.dll
elif [ -f "/app/Hagicode.dll" ]; then
    exec_as_hagicode dotnet Hagicode.dll
elif [ -f "/app/lib/PCode.Web.dll" ]; then
    exec_as_hagicode dotnet lib/PCode.Web.dll
elif [ -f "/app/lib/Hagicode.dll" ]; then
    exec_as_hagicode dotnet lib/Hagicode.dll
else
    echo "Error: Could not find application entry point"
    echo "Looked for: PCode.Web.dll, Hagicode.dll, lib/PCode.Web.dll, lib/Hagicode.dll"
    exit 1
fi
}

if [ "${BASH_SOURCE[0]}" = "$0" ]; then
    main "$@"
fi
