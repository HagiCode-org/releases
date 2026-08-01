#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/docker-local-common.sh"

ENV_FILE="$DEFAULT_ENV_FILE"
SECRETS_FILE=""
NO_CACHE=false
CLI_RELEASE_VERSION=""
CLI_PLATFORM=""
CLI_IMAGE=""

while [ $# -gt 0 ]; do
    case "$1" in
        --env-file)
            ENV_FILE="$2"
            shift 2
            ;;
        --secrets-file)
            SECRETS_FILE="$2"
            shift 2
            ;;
        --version)
            CLI_RELEASE_VERSION="$2"
            shift 2
            ;;
        --platform)
            CLI_PLATFORM="$2"
            shift 2
            ;;
        --image)
            CLI_IMAGE="$2"
            shift 2
            ;;
        --no-cache)
            NO_CACHE=true
            shift
            ;;
        *)
            fail "Unknown argument: $1"
            ;;
    esac
done

load_local_env "$ENV_FILE" "$SECRETS_FILE"

if [ -n "$CLI_RELEASE_VERSION" ]; then
    HAGICODE_RELEASE_VERSION="$CLI_RELEASE_VERSION"
fi

if [ -n "$CLI_PLATFORM" ]; then
    HAGICODE_DOCKER_PLATFORM="$CLI_PLATFORM"
fi

if [ -n "$CLI_IMAGE" ]; then
    HAGICODE_LOCAL_IMAGE="$CLI_IMAGE"
fi

export HAGICODE_RELEASE_VERSION
export HAGICODE_DOCKER_PLATFORM
export HAGICODE_LOCAL_IMAGE
export NUGEX_ReleaseVersion="$HAGICODE_RELEASE_VERSION"

ensure_release_version
ensure_single_platform "$HAGICODE_DOCKER_PLATFORM"
prepare_local_dirs


log "Reusing any matching package already in output/download"
ensure_download_artifacts_present

log "Preparing Docker build context"
"$REPO_ROOT/build.sh" \
    DockerPrepareLocalContext \
    --ReleaseVersion "$HAGICODE_RELEASE_VERSION" \
    --DockerPlatform "$HAGICODE_DOCKER_PLATFORM"

log_local_build_network_requirements

# Use `docker buildx build --load` so the resulting image is imported into the
# local Docker daemon and can be consumed directly by `docker compose`.
build_args=(
    buildx
    build
    --load
    --platform "$HAGICODE_DOCKER_PLATFORM"
    --tag "$HAGICODE_LOCAL_IMAGE"
    "$REPO_ROOT/output/docker-build-context"
)

if [ "$NO_CACHE" = true ]; then
    build_args=(
        buildx
        build
        --load
        --no-cache
        --platform "$HAGICODE_DOCKER_PLATFORM"
        --tag "$HAGICODE_LOCAL_IMAGE"
        "$REPO_ROOT/output/docker-build-context"
    )
fi

log "Building local image ${HAGICODE_LOCAL_IMAGE}"
docker "${build_args[@]}"
log "Local image is ready: ${HAGICODE_LOCAL_IMAGE}"
