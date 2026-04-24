#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/docker-local-common.sh"

ENV_FILE="$DEFAULT_ENV_FILE"
SECRETS_FILE=""

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
        *)
            fail "Unknown argument: $1"
            ;;
    esac
done

load_local_env "$ENV_FILE" "$SECRETS_FILE"

log "Stopping local compose stack"
run_compose down --remove-orphans
