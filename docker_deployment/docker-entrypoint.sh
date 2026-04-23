#!/bin/bash
# Docker Entrypoint Script for HagiCode
# This script configures runtime prerequisites, bootstraps Omniroute, and starts pm2.

set -euo pipefail

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
HAGICODE_BOOTSTRAP_DIR="${HAGICODE_APP_DIR}/bootstrap"
HAGICODE_CODE_SERVER_CONFIG_DIR="${HAGICODE_HOME}/.config/code-server"
HAGICODE_CODE_SERVER_CACHE_DIR="${HAGICODE_HOME}/.cache/code-server"
HAGICODE_CODE_SERVER_SHARE_DIR="${HAGICODE_HOME}/.local/share/code-server"
HAGICODE_CODE_SERVER_DATA_DIR="${HAGICODE_APP_DATA_DIR}/code-server"

OMNIROUTE_DEFAULT_HOST="127.0.0.1"
OMNIROUTE_DEFAULT_PORT="4060"
OMNIROUTE_DEFAULT_STATE_DIR="${HAGICODE_APP_DATA_DIR}/omniroute"
OMNIROUTE_DEFAULT_PM2_HOME="${OMNIROUTE_DEFAULT_STATE_DIR}/pm2"
OMNIROUTE_DEFAULT_RUNTIME_DIR="${OMNIROUTE_DEFAULT_STATE_DIR}/runtime"
OMNIROUTE_DEFAULT_READY_FILE="${OMNIROUTE_DEFAULT_RUNTIME_DIR}/hagicode.ready"
OMNIROUTE_DEFAULT_BOOTSTRAP_STATE_FILE="${OMNIROUTE_DEFAULT_STATE_DIR}/bootstrap-state.json"
OMNIROUTE_DEFAULT_PM2_LOG_DIR="${OMNIROUTE_DEFAULT_STATE_DIR}/logs"
OMNIROUTE_DEFAULT_PASSWORD_FILE="${OMNIROUTE_DEFAULT_STATE_DIR}/management-password"
OMNIROUTE_DEFAULT_JWT_SECRET_FILE="${OMNIROUTE_DEFAULT_STATE_DIR}/jwt-secret"
OMNIROUTE_DEFAULT_API_KEY_SECRET_FILE="${OMNIROUTE_DEFAULT_STATE_DIR}/api-key-secret"
OMNIROUTE_DEFAULT_SHARED_KEY_FILE="${OMNIROUTE_DEFAULT_STATE_DIR}/shared-api-key"

PM2_RUNTIME_PID=""

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
        "$HAGICODE_SSH_DIR" \
        "$HAGICODE_CODE_SERVER_CONFIG_DIR" \
        "$HAGICODE_CODE_SERVER_CACHE_DIR" \
        "$HAGICODE_CODE_SERVER_SHARE_DIR" \
        "$HAGICODE_CODE_SERVER_DATA_DIR" \
        "$HAGICODE_APP_DATA_DIR" \
        "$HAGICODE_APP_SAVES_DIR" \
        "$HAGICODE_BOOTSTRAP_DIR" \
        "$HAGICODE_APP_DIR"

    chown "$HAGICODE_USER:$HAGICODE_GROUP" "$HAGICODE_HOME" "$HAGICODE_APP_DIR"
    chown -R "$HAGICODE_USER:$HAGICODE_GROUP" \
        "$HAGICODE_CLAUDE_DIR" \
        "$HAGICODE_SSH_DIR" \
        "$HAGICODE_CODE_SERVER_CONFIG_DIR" \
        "$HAGICODE_CODE_SERVER_CACHE_DIR" \
        "$HAGICODE_CODE_SERVER_SHARE_DIR" \
        "$HAGICODE_CODE_SERVER_DATA_DIR" \
        "$HAGICODE_APP_DATA_DIR" \
        "$HAGICODE_APP_SAVES_DIR" \
        "$HAGICODE_BOOTSTRAP_DIR"
}

fail_startup() {
    echo "Error: $*" >&2
    exit 1
}

cleanup_pm2_runtime() {
    if [ -n "${PM2_RUNTIME_PID:-}" ] && kill -0 "$PM2_RUNTIME_PID" >/dev/null 2>&1; then
        kill "$PM2_RUNTIME_PID" >/dev/null 2>&1 || true
        wait "$PM2_RUNTIME_PID" >/dev/null 2>&1 || true
    fi
}

trap cleanup_pm2_runtime EXIT

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

normalize_bool() {
    local value="${1:-}"
    value="$(printf '%s' "$value" | tr '[:upper:]' '[:lower:]')"
    case "$value" in
        1|true|yes|on)
            echo "true"
            ;;
        *)
            echo "false"
            ;;
    esac
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

persist_secret_file() {
    local secret_file="$1"
    local env_name="$2"
    local default_prefix="$3"
    local length="${4:-48}"
    local resolved_value="${!env_name:-}"

    mkdir -p "$(dirname "$secret_file")"

    if [ -n "$resolved_value" ]; then
        printf '%s' "$resolved_value" > "$secret_file"
    elif [ -f "$secret_file" ]; then
        resolved_value="$(cat "$secret_file")"
    else
        local random_hex_length=$(( (length + 1) / 2 ))
        local random_suffix
        random_suffix="$(openssl rand -hex "$random_hex_length" | cut -c1-"$length")"
        resolved_value="${default_prefix}_${random_suffix}"
        printf '%s' "$resolved_value" > "$secret_file"
    fi

    chmod 600 "$secret_file"
    chown "$HAGICODE_USER:$HAGICODE_GROUP" "$secret_file"
    export "$env_name=$resolved_value"
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

capture_upstream_provider_inputs() {
    export OMNIROUTE_CLAUDE_UPSTREAM_BASE_URL="${OMNIROUTE_CLAUDE_UPSTREAM_BASE_URL:-${ANTHROPIC_URL:-}}"
    export OMNIROUTE_CLAUDE_UPSTREAM_AUTH_TOKEN="${OMNIROUTE_CLAUDE_UPSTREAM_AUTH_TOKEN:-${ANTHROPIC_AUTH_TOKEN:-}}"
    export OMNIROUTE_CODEX_UPSTREAM_BASE_URL="${OMNIROUTE_CODEX_UPSTREAM_BASE_URL:-${CODEX_BASE_URL:-${OPENAI_BASE_URL:-}}}"
    export OMNIROUTE_CODEX_UPSTREAM_API_KEY="${OMNIROUTE_CODEX_UPSTREAM_API_KEY:-${CODEX_API_KEY:-${OPENAI_API_KEY:-}}}"
    export OMNIROUTE_OPENCODE_UPSTREAM_BASE_URL="${OMNIROUTE_OPENCODE_UPSTREAM_BASE_URL:-${OPENCODE_BASE_URL:-${OPENCODE_API_BASE_URL:-${OPENCODE_BASE_URL_COMPAT:-}}}}"
    export OMNIROUTE_OPENCODE_UPSTREAM_API_KEY="${OMNIROUTE_OPENCODE_UPSTREAM_API_KEY:-${OPENCODE_API_KEY:-}}"
}

normalize_omniroute_runtime_contract() {
    export OMNIROUTE_ENABLE_BOOTSTRAP="${OMNIROUTE_ENABLE_BOOTSTRAP:-true}"
    export OMNIROUTE_ENABLE_BOOTSTRAP="$(normalize_bool "$OMNIROUTE_ENABLE_BOOTSTRAP")"

    export OMNIROUTE_HOST="${OMNIROUTE_HOST:-$OMNIROUTE_DEFAULT_HOST}"
    export OMNIROUTE_PORT="${OMNIROUTE_PORT:-$OMNIROUTE_DEFAULT_PORT}"
    export OMNIROUTE_STATE_DIR="${OMNIROUTE_STATE_DIR:-$OMNIROUTE_DEFAULT_STATE_DIR}"
    export OMNIROUTE_PM2_HOME="${OMNIROUTE_PM2_HOME:-$OMNIROUTE_DEFAULT_PM2_HOME}"
    export OMNIROUTE_RUNTIME_DIR="${OMNIROUTE_RUNTIME_DIR:-$OMNIROUTE_DEFAULT_RUNTIME_DIR}"
    export OMNIROUTE_READY_FILE="${OMNIROUTE_READY_FILE:-$OMNIROUTE_DEFAULT_READY_FILE}"
    export OMNIROUTE_BOOTSTRAP_STATE_FILE="${OMNIROUTE_BOOTSTRAP_STATE_FILE:-$OMNIROUTE_DEFAULT_BOOTSTRAP_STATE_FILE}"
    export OMNIROUTE_PM2_LOG_DIR="${OMNIROUTE_PM2_LOG_DIR:-$OMNIROUTE_DEFAULT_PM2_LOG_DIR}"
    export OMNIROUTE_PASSWORD_FILE="${OMNIROUTE_PASSWORD_FILE:-${OMNIROUTE_STATE_DIR}/management-password}"
    export OMNIROUTE_JWT_SECRET_FILE="${OMNIROUTE_JWT_SECRET_FILE:-${OMNIROUTE_STATE_DIR}/jwt-secret}"
    export OMNIROUTE_API_KEY_SECRET_FILE="${OMNIROUTE_API_KEY_SECRET_FILE:-${OMNIROUTE_STATE_DIR}/api-key-secret}"
    export OMNIROUTE_SHARED_KEY_FILE="${OMNIROUTE_SHARED_KEY_FILE:-${OMNIROUTE_STATE_DIR}/shared-api-key}"
    export OMNIROUTE_BASE_URL="${OMNIROUTE_BASE_URL:-http://${OMNIROUTE_HOST}:${OMNIROUTE_PORT}}"
    export OMNIROUTE_API_BASE_URL="${OMNIROUTE_API_BASE_URL:-${OMNIROUTE_BASE_URL}/v1}"
    export PORT="${PORT:-$OMNIROUTE_PORT}"
    export DATA_DIR="${DATA_DIR:-$OMNIROUTE_STATE_DIR}"
    export PM2_HOME="${PM2_HOME:-$OMNIROUTE_PM2_HOME}"
    export HAGICODE_PM2_READY_FILE="$OMNIROUTE_READY_FILE"

    mkdir -p "$OMNIROUTE_STATE_DIR" "$OMNIROUTE_PM2_HOME" "$OMNIROUTE_RUNTIME_DIR" "$OMNIROUTE_PM2_LOG_DIR"
    rm -f "$OMNIROUTE_READY_FILE"
    chown -R "$HAGICODE_USER:$HAGICODE_GROUP" "$OMNIROUTE_STATE_DIR"

    persist_secret_file "$OMNIROUTE_PASSWORD_FILE" "OMNIROUTE_INITIAL_PASSWORD" "omniroute-pw" 32
    persist_secret_file "$OMNIROUTE_JWT_SECRET_FILE" "JWT_SECRET" "omniroute-jwt" 48
    persist_secret_file "$OMNIROUTE_API_KEY_SECRET_FILE" "API_KEY_SECRET" "omniroute-apisecret" 48
    persist_secret_file "$OMNIROUTE_SHARED_KEY_FILE" "OMNIROUTE_SHARED_API_KEY" "omniroute-shared" 40

    export INITIAL_PASSWORD="$OMNIROUTE_INITIAL_PASSWORD"
    export OMNIROUTE_API_KEY="${OMNIROUTE_API_KEY:-$OMNIROUTE_SHARED_API_KEY}"
    export ROUTER_API_KEY="${ROUTER_API_KEY:-$OMNIROUTE_SHARED_API_KEY}"

    echo "✓ Omniroute runtime contract prepared"
    echo "  OMNIROUTE_ENABLE_BOOTSTRAP=${OMNIROUTE_ENABLE_BOOTSTRAP}"
    echo "  OMNIROUTE_BASE_URL=${OMNIROUTE_BASE_URL}"
    echo "  OMNIROUTE_API_BASE_URL=${OMNIROUTE_API_BASE_URL}"
    echo "  OMNIROUTE_STATE_DIR=${OMNIROUTE_STATE_DIR}"
}

wait_for_omniroute_health() {
    local health_url="${OMNIROUTE_BASE_URL}/api/monitoring/health"
    local timeout_seconds="${OMNIROUTE_STARTUP_TIMEOUT_SECONDS:-180}"
    local sleep_seconds="${OMNIROUTE_STARTUP_POLL_SECONDS:-2}"
    local deadline=$(( $(date +%s) + timeout_seconds ))

    while true; do
        if curl -fsS "$health_url" >/dev/null 2>&1; then
            echo "✓ Omniroute health check passed: ${health_url}"
            return 0
        fi

        if [ "$(date +%s)" -ge "$deadline" ]; then
            fail_startup "Timed out waiting for Omniroute health endpoint: ${health_url}"
        fi

        sleep "$sleep_seconds"
    done
}

export_local_omniroute_routing() {
    export HAGICODE_OMNIROUTE_ENABLED="true"
    export HAGICODE_OMNIROUTE_BASE_URL="$OMNIROUTE_BASE_URL"
    export HAGICODE_OMNIROUTE_API_BASE_URL="$OMNIROUTE_API_BASE_URL"
    export OmniRoute__Enabled="true"
    export OmniRoute__BaseUrl="$OMNIROUTE_BASE_URL"
    export OmniRoute__ApiBaseUrl="$OMNIROUTE_API_BASE_URL"

    export CODEX_BASE_URL="$OMNIROUTE_API_BASE_URL"
    export OPENAI_BASE_URL="$OMNIROUTE_API_BASE_URL"
    export CODEX_API_KEY="$OMNIROUTE_SHARED_API_KEY"
    export OPENAI_API_KEY="$OMNIROUTE_SHARED_API_KEY"
    export OPENCODE_BASE_URL="$OMNIROUTE_API_BASE_URL"
    export OPENCODE_API_BASE_URL="$OMNIROUTE_API_BASE_URL"
    export OPENCODE_BASE_URL_COMPAT="$OMNIROUTE_API_BASE_URL"
    export OPENCODE_API_KEY="$OMNIROUTE_SHARED_API_KEY"
    export ANTHROPIC_URL="$OMNIROUTE_API_BASE_URL"
    export ANTHROPIC_AUTH_TOKEN="$OMNIROUTE_SHARED_API_KEY"

    echo "✓ Exported local Omniroute routing for Claude, Codex/OpenAI, OpenCode, and HagiCode"
    echo "  Claude route: ${ANTHROPIC_URL}"
    echo "  Codex route: ${CODEX_BASE_URL}"
    echo "  OpenCode route: ${OPENCODE_BASE_URL}"
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
        echo "✓ Claude Code configured for routed Anthropic endpoint"
        return 0
    fi

    if [ "${CLAUDE_HOST_CONFIG_ENABLED:-true}" = "false" ]; then
        echo "⚠ Warning: Claude host configuration is disabled and no routed token was configured"
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

    echo "⚠ Warning: No Claude configuration available after runtime routing"
}

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
    run_as_hagicode npm install -g "${package_name}@${override_version}"
    run_as_hagicode "${command_name}" --version >/dev/null
    run_as_hagicode npm cache clean --force >/dev/null 2>&1 || true
}

install_cli_overrides() {
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

    if [ -n "${QODER_PERSONAL_ACCESS_TOKEN:-}" ]; then
        echo "✓ Qoder runtime token detected: QODER_PERSONAL_ACCESS_TOKEN (masked)"
    else
        echo "✓ No Qoder runtime token provided; UI-managed qodercli installs may rely on mounted runtime state."
    fi
}

run_omniroute_bootstrap() {
    if [ "$OMNIROUTE_ENABLE_BOOTSTRAP" != "true" ]; then
        echo "✓ Omniroute bootstrap disabled; releasing app startup without provider sync"
        touch "$OMNIROUTE_READY_FILE"
        chown "$HAGICODE_USER:$HAGICODE_GROUP" "$OMNIROUTE_READY_FILE"
        return 0
    fi

    wait_for_omniroute_health
    run_as_hagicode node "${HAGICODE_BOOTSTRAP_DIR}/omniroute-bootstrap.mjs"
    echo "✓ Omniroute provider bootstrap completed"
}

start_pm2_runtime() {
    export PM2_HOME="$OMNIROUTE_PM2_HOME"
    run_as_hagicode pm2-runtime start "${HAGICODE_BOOTSTRAP_DIR}/ecosystem.config.cjs" &
    PM2_RUNTIME_PID="$!"
    echo "✓ Started pm2-runtime with Omniroute and HagiCode application processes"
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
    validate_accept_eula
    configure_ssh_private_key_if_needed
    configure_code_server_runtime_if_needed
    install_cli_overrides
    resolve_application_command
    capture_upstream_provider_inputs
    normalize_omniroute_runtime_contract
    export_local_omniroute_routing
    configure_claude_runtime
    start_pm2_runtime
    run_omniroute_bootstrap

    if [ -z "$PM2_RUNTIME_PID" ]; then
        fail_startup "pm2-runtime did not start"
    fi

    wait "$PM2_RUNTIME_PID"
}

if [ "${BASH_SOURCE[0]}" = "$0" ]; then
    main "$@"
fi
