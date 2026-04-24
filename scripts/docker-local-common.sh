#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(cd "${SCRIPT_DIR}/.." && pwd)
DEFAULT_ENV_FILE="${REPO_ROOT}/.env.local"
DEFAULT_ENV_TEMPLATE="${REPO_ROOT}/.env.local.example"
DEFAULT_SECRETS_FILE="${REPO_ROOT}/.env.secrets.local"
COMPOSE_FILE="${REPO_ROOT}/docker-compose.local.yml"

log() {
    printf '[hagicode-local] %s\n' "$*"
}

fail() {
    printf '[hagicode-local] %s\n' "$*" >&2
    exit 1
}

detect_host_platform() {
    case "$(uname -m)" in
        x86_64|amd64)
            printf 'linux/amd64\n'
            ;;
        aarch64|arm64)
            printf 'linux/arm64\n'
            ;;
        *)
            fail "Unsupported host architecture: $(uname -m). Set HAGICODE_DOCKER_PLATFORM explicitly in .env.local."
            ;;
    esac
}

platform_to_download_name() {
    case "$1" in
        linux/amd64)
            printf 'linux-x64\n'
            ;;
        linux/arm64)
            printf 'linux-arm64\n'
            ;;
        *)
            fail "Unsupported local Docker platform: $1"
            ;;
    esac
}

ensure_env_file() {
    local env_file="$1"

    if [ -f "$env_file" ]; then
        return 0
    fi

    [ -f "$DEFAULT_ENV_TEMPLATE" ] || fail "Missing env template: ${DEFAULT_ENV_TEMPLATE}"

    cp "$DEFAULT_ENV_TEMPLATE" "$env_file"
    log "Created ${env_file} from .env.local.example"
}

load_local_env() {
    local env_file="${1:-$DEFAULT_ENV_FILE}"
    local secrets_file="${2:-}"
    local resolved_secrets_file=""

    ensure_env_file "$env_file"

    set -a
    # shellcheck disable=SC1090
    source "$env_file"
    set +a

    if [ -n "$secrets_file" ]; then
        resolved_secrets_file="$secrets_file"
    elif [ -n "${HAGICODE_LOCAL_SECRETS_FILE:-}" ]; then
        resolved_secrets_file="$HAGICODE_LOCAL_SECRETS_FILE"
    else
        resolved_secrets_file="$DEFAULT_SECRETS_FILE"
    fi

    if [ -f "$resolved_secrets_file" ]; then
        set -a
        # shellcheck disable=SC1090
        source "$resolved_secrets_file"
        set +a
        log "Loaded local secrets override from ${resolved_secrets_file}"
    elif [ -n "$secrets_file" ] || [ -n "${HAGICODE_LOCAL_SECRETS_FILE:-}" ]; then
        fail "Configured local secrets file was not found: ${resolved_secrets_file}"
    fi

    export HAGICODE_DOCKER_PLATFORM="${HAGICODE_DOCKER_PLATFORM:-$(detect_host_platform)}"
    export HAGICODE_RELEASE_VERSION="${HAGICODE_RELEASE_VERSION:-}"
    export HAGICODE_LOCAL_IMAGE="${HAGICODE_LOCAL_IMAGE:-hagicode-local:${HAGICODE_RELEASE_VERSION:-dev}}"
    export HAGICODE_LOCAL_CONTAINER_NAME="${HAGICODE_LOCAL_CONTAINER_NAME:-hagicode-local}"
    export NUGEX_ReleaseVersion="${NUGEX_ReleaseVersion:-${HAGICODE_RELEASE_VERSION:-}}"
    export NUGEX_AzureBlobSasUrl="${NUGEX_AzureBlobSasUrl:-${AZURE_BLOB_SAS_URL:-}}"
    export ACTIVE_ENV_FILE="$env_file"
    export ACTIVE_SECRETS_FILE="${resolved_secrets_file}"
}

ensure_release_version() {
    [ -n "${HAGICODE_RELEASE_VERSION:-}" ] || fail "HAGICODE_RELEASE_VERSION is required. Set it in .env.local or pass --version."
}

ensure_single_platform() {
    case "$1" in
        linux/amd64|linux/arm64)
            ;;
        *)
            fail "Local image builds require a single platform. Use linux/amd64 or linux/arm64."
            ;;
    esac
}

prepare_local_dirs() {
    mkdir -p \
        "${REPO_ROOT}/.local/hagicode/data" \
        "${REPO_ROOT}/.local/hagicode/saves" \
        "${REPO_ROOT}/.local/hagicode/claude" \
        "${REPO_ROOT}/.local/hagicode/runtime-secrets"
}

run_compose() {
    docker compose \
        --env-file "${ACTIVE_ENV_FILE:-$DEFAULT_ENV_FILE}" \
        -f "$COMPOSE_FILE" \
        "$@"
}

local_image_exists() {
    docker image inspect "$HAGICODE_LOCAL_IMAGE" >/dev/null 2>&1
}

log_local_build_network_requirements() {
    if docker image inspect debian:bookworm-slim >/dev/null 2>&1; then
        log "Base image debian:bookworm-slim is already cached locally"
    else
        log "Base image debian:bookworm-slim is not cached locally; docker buildx will pull it from Docker Hub"
    fi

    log "Dockerfile.template also downloads dependencies from dot.net, raw.githubusercontent.com, github.com, and registry.npmjs.org unless your host already provides a mirror or cache"
}

ensure_download_artifacts_present() {
    local download_platform
    local expected_pattern

    download_platform="$(platform_to_download_name "$HAGICODE_DOCKER_PLATFORM")"
    expected_pattern="${REPO_ROOT}/output/download/*${HAGICODE_RELEASE_VERSION}*${download_platform}*.zip"

    compgen -G "$expected_pattern" >/dev/null || fail \
        "No downloaded ${download_platform} package for version ${HAGICODE_RELEASE_VERSION} was found under output/download. Run scripts/docker-local-build.sh without --skip-download or place the matching package there first."
}
