#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/docker-local-common.sh"

ENV_FILE="$DEFAULT_ENV_FILE"
SECRETS_FILE=""
TIMEOUT_SECONDS=180

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
        --timeout)
            TIMEOUT_SECONDS="$2"
            shift 2
            ;;
        *)
            fail "Unknown argument: $1"
            ;;
    esac
done

load_local_env "$ENV_FILE" "$SECRETS_FILE"

health_url="http://${HAGICODE_HTTP_BIND:-127.0.0.1}:${HAGICODE_HTTP_PORT:-5000}/"
deadline=$(( $(date +%s) + TIMEOUT_SECONDS ))

log "Waiting for ${health_url}"
while true; do
    if curl -fsS "$health_url" >/dev/null 2>&1; then
        break
    fi

    if [ "$(date +%s)" -ge "$deadline" ]; then
        fail "Timed out waiting for ${health_url}"
    fi

    sleep 2
done

log "HTTP health check passed"
run_compose exec -T hagicode bash -lc 'claude --version >/dev/null && openspec --version >/dev/null && skills --version >/dev/null && opencode --version >/dev/null && codex --version >/dev/null && code-server --version >/dev/null'
log "Bundled CLI smoke test passed"
