#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/docker-local-common.sh"

ENV_FILE="$DEFAULT_ENV_FILE"
SECRETS_FILE=""
BUILD_IF_NEEDED=true
FORCE_BUILD=false

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
        --build)
            FORCE_BUILD=true
            shift
            ;;
        --skip-build)
            BUILD_IF_NEEDED=false
            shift
            ;;
        *)
            fail "Unknown argument: $1"
            ;;
    esac
done

load_local_env "$ENV_FILE" "$SECRETS_FILE"
prepare_local_dirs

build_args=(--env-file "$ENV_FILE")
if [ -n "$SECRETS_FILE" ]; then
    build_args+=(--secrets-file "$SECRETS_FILE")
fi

if [ "$FORCE_BUILD" = true ]; then
    "${SCRIPT_DIR}/docker-local-build.sh" "${build_args[@]}"
elif [ "$BUILD_IF_NEEDED" = true ] && ! local_image_exists; then
    log "Local image ${HAGICODE_LOCAL_IMAGE} is missing, building it first"
    "${SCRIPT_DIR}/docker-local-build.sh" "${build_args[@]}"
fi

log "Starting local compose stack"
run_compose up -d
log "Compose stack is up"
