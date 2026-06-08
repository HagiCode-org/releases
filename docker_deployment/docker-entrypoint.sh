#!/bin/bash
# Docker Entrypoint Script for HagiCode
# This script configures runtime prerequisites and starts the application directly.

set -euo pipefail

HAGICODE_USER="hagicode"
HAGICODE_GROUP="hagicode"
HAGICODE_HOME="/home/hagicode"
HAGICODE_CLAUDE_DIR="${HAGICODE_HOME}/.claude"
HAGICODE_CLAUDE_STATE_FILE="${HAGICODE_HOME}/.claude.json"
HAGICODE_NPM_PREFIX="${HAGICODE_HOME}/.npm-global"
HAGISCRIPT_MANAGED_RUNTIME="${HAGISCRIPT_MANAGED_RUNTIME:-${HAGICODE_HOME}/.hagiscript/node-runtime}"
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

export HOME="$HAGICODE_HOME"
export NPM_CONFIG_PREFIX="${NPM_CONFIG_PREFIX:-$HAGICODE_NPM_PREFIX}"
export PATH="${HAGICODE_NPM_PREFIX}/bin:${HAGISCRIPT_MANAGED_RUNTIME}/bin:${PATH}"

run_as_hagicode() {
    gosu "$HAGICODE_USER" env \
        HOME="$HAGICODE_HOME" \
        USER="$HAGICODE_USER" \
        LOGNAME="$HAGICODE_USER" \
        PATH="$PATH" \
        NPM_CONFIG_PREFIX="$HAGICODE_NPM_PREFIX" \
        HAGISCRIPT_MANAGED_RUNTIME="$HAGISCRIPT_MANAGED_RUNTIME" \
        "$@"
}

exec_as_hagicode() {
    exec gosu "$HAGICODE_USER" env \
        HOME="$HAGICODE_HOME" \
        USER="$HAGICODE_USER" \
        LOGNAME="$HAGICODE_USER" \
        PATH="$PATH" \
        NPM_CONFIG_PREFIX="$HAGICODE_NPM_PREFIX" \
        HAGISCRIPT_MANAGED_RUNTIME="$HAGISCRIPT_MANAGED_RUNTIME" \
        "$@"
}

ensure_hagicode_runtime_paths() {
    mkdir -p \
        "$HAGICODE_CLAUDE_DIR" \
        "$HAGICODE_NPM_PREFIX" \
        "$(dirname "$HAGISCRIPT_MANAGED_RUNTIME")" \
        "$HAGICODE_SSH_DIR" \
        "$HAGICODE_APP_DATA_DIR" \
        "$HAGICODE_APP_SAVES_DIR" \
        "$HAGICODE_APP_DIR"

    chown "$HAGICODE_USER:$HAGICODE_GROUP" "$HAGICODE_HOME" "$HAGICODE_APP_DIR"
    chown -R "$HAGICODE_USER:$HAGICODE_GROUP" \
        "$HAGICODE_CLAUDE_DIR" \
        "$(dirname "$HAGISCRIPT_MANAGED_RUNTIME")" \
        "$HAGICODE_SSH_DIR" \
        "$HAGICODE_APP_DATA_DIR" \
        "$HAGICODE_APP_SAVES_DIR"
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

json_escape() {
    local value="${1:-}"
    value="${value//\\/\\\\}"
    value="${value//\"/\\\"}"
    value="${value//$'\n'/\\n}"
    value="${value//$'\r'/\\r}"
    value="${value//$'\t'/\\t}"
    printf '%s' "$value"
}

resolve_application_command() {
    if [ -f "/app/PCode.Web.dll" ]; then
        export HAGICODE_APP_COMMAND="dotnet"
        export HAGICODE_APP_ARGUMENTS="PCode.Web.dll"
    elif [ -f "/app/Hagicode.dll" ]; then
        export HAGICODE_APP_COMMAND="dotnet"
        export HAGICODE_APP_ARGUMENTS="Hagicode.dll"
    elif [ -f "/app/lib/PCode.Web.dll" ]; then
        export HAGICODE_APP_COMMAND="dotnet"
        export HAGICODE_APP_ARGUMENTS="lib/PCode.Web.dll"
    elif [ -f "/app/lib/Hagicode.dll" ]; then
        export HAGICODE_APP_COMMAND="dotnet"
        export HAGICODE_APP_ARGUMENTS="lib/Hagicode.dll"
    else
        fail_startup "Could not find application entry point (PCode.Web.dll or Hagicode.dll)"
    fi
}

configure_claude_runtime() {
    local settings_file="${HAGICODE_CLAUDE_DIR}/settings.json"
    local mount_path="${CLAUDE_CONFIG_MOUNT_PATH:-/claude-mount}"
    local -a env_entries=()
    local index

    mkdir -p "$HAGICODE_CLAUDE_DIR"

    if [ -n "${ANTHROPIC_AUTH_TOKEN:-}" ]; then
        env_entries+=("    \"ANTHROPIC_AUTH_TOKEN\": \"$(json_escape "${ANTHROPIC_AUTH_TOKEN}")\"")
        env_entries+=("    \"ANTHROPIC_BASE_URL\": \"$(json_escape "${ANTHROPIC_URL:-}")\"")
        env_entries+=("    \"ANTHROPIC_URL\": \"$(json_escape "${ANTHROPIC_URL:-}")\"")
        env_entries+=("    \"API_TIMEOUT_MS\": \"3000000\"")
        env_entries+=("    \"CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC\": \"1\"")
        env_entries+=("    \"CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS\": \"$(json_escape "${CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS:-1}")\"")

        if [ -n "${ANTHROPIC_HAIKU_MODEL:-}" ]; then
            env_entries+=("    \"ANTHROPIC_DEFAULT_HAIKU_MODEL\": \"$(json_escape "${ANTHROPIC_HAIKU_MODEL}")\"")
        fi

        if [ -n "${ANTHROPIC_SONNET_MODEL:-}" ]; then
            env_entries+=("    \"ANTHROPIC_DEFAULT_SONNET_MODEL\": \"$(json_escape "${ANTHROPIC_SONNET_MODEL}")\"")
        fi

        if [ -n "${ANTHROPIC_OPUS_MODEL:-}" ]; then
            env_entries+=("    \"ANTHROPIC_DEFAULT_OPUS_MODEL\": \"$(json_escape "${ANTHROPIC_OPUS_MODEL}")\"")
        fi

        {
            printf '{\n'
            printf '  "env": {\n'
            for index in "${!env_entries[@]}"; do
                if [ "$index" -gt 0 ]; then
                    printf ',\n'
                fi
                printf '%s' "${env_entries[$index]}"
            done
            printf '\n  }\n'
            printf '}\n'
        } > "$settings_file"

        cat > "$HAGICODE_CLAUDE_STATE_FILE" <<EOF
{
  "hasCompletedOnboarding": true
}
EOF
        chown -R "$HAGICODE_USER:$HAGICODE_GROUP" "$HAGICODE_CLAUDE_DIR" "$HAGICODE_CLAUDE_STATE_FILE"
        chmod 600 "$settings_file"
        echo "✓ Claude Code configured from ANTHROPIC_AUTH_TOKEN"
        return 0
    fi

    if [ "${CLAUDE_HOST_CONFIG_ENABLED:-true}" = "false" ]; then
        echo "⚠ Warning: Claude host configuration is disabled and no token was configured"
        return 0
    fi

    if [ -f "${mount_path}/settings.json" ]; then
        cp "${mount_path}/settings.json" "$settings_file"
        cat > "$HAGICODE_CLAUDE_STATE_FILE" <<EOF
{
  "hasCompletedOnboarding": true
}
EOF
        chown -R "$HAGICODE_USER:$HAGICODE_GROUP" "$HAGICODE_CLAUDE_DIR" "$HAGICODE_CLAUDE_STATE_FILE"
        chmod 600 "$settings_file"
        echo "✓ Claude Code configured from mounted host settings"
        return 0
    fi

    echo "⚠ Warning: No Claude configuration available"
}

verify_hagiscript_synced_toolchain() {
    local required_commands=(
        hagiscript
        claude
        openspec
        skills
        opencode
        codex
    )

    for command_name in "${required_commands[@]}"; do
        if ! command -v "$command_name" >/dev/null 2>&1; then
            fail_startup "${command_name} is not available on PATH; rebuild the image with hagiscript npm-sync completed"
        fi
    done

    run_as_hagicode hagiscript --version >/dev/null
    run_as_hagicode claude --version >/dev/null
    run_as_hagicode openspec --version >/dev/null
    run_as_hagicode skills --version >/dev/null
    run_as_hagicode opencode --version >/dev/null
    run_as_hagicode codex --version >/dev/null

    echo "✓ HagiScript-synced image toolchain verified"

    if [ -n "${QODER_PERSONAL_ACCESS_TOKEN:-}" ]; then
        echo "✓ Qoder runtime token detected: QODER_PERSONAL_ACCESS_TOKEN (masked)"
    else
        echo "✓ No Qoder runtime token provided; UI-managed qodercli installs may rely on mounted runtime state."
    fi
}

main() {
    if [ "$#" -gt 0 ]; then
        exec_as_hagicode "$@"
    fi

    if [ -n "${PUID:-}" ] && [ -n "${PGID:-}" ]; then
        if ! id "$HAGICODE_USER" >/dev/null 2>&1; then
            groupadd -g "$PGID" "$HAGICODE_GROUP"
            useradd -u "$PUID" -g "$PGID" -s /bin/bash -m -d "$HAGICODE_HOME" "$HAGICODE_USER"
        else
            local current_uid current_gid
            current_uid="$(id -u "$HAGICODE_USER")"
            current_gid="$(id -g "$HAGICODE_USER")"
            if [ "$current_uid" != "$PUID" ] || [ "$current_gid" != "$PGID" ]; then
                groupmod -o -g "$PGID" "$HAGICODE_GROUP"
                usermod -o -u "$PUID" -g "$PGID" -d "$HAGICODE_HOME" "$HAGICODE_USER"
            fi
        fi
    fi

    ensure_hagicode_runtime_paths
    configure_ssh_private_key_if_needed
    verify_hagiscript_synced_toolchain
    resolve_application_command
    configure_claude_runtime

    echo "✓ Starting HagiCode application: ${HAGICODE_APP_COMMAND} ${HAGICODE_APP_ARGUMENTS}"
    exec_as_hagicode "$HAGICODE_APP_COMMAND" "$HAGICODE_APP_ARGUMENTS"
}

if [ "${BASH_SOURCE[0]}" = "$0" ]; then
    main "$@"
fi
