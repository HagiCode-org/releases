#!/usr/bin/env bash

set -eo pipefail
SCRIPT_DIR=$(cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd)

LOCAL_SECRETS_FILE="${HAGICODE_LOCAL_SECRETS_FILE:-$SCRIPT_DIR/.env.secrets.local}"
if [[ -z "${GITHUB_ACTIONS:-}" && -f "$LOCAL_SECRETS_FILE" ]]; then
    set -a
    # shellcheck disable=SC1090
    source "$LOCAL_SECRETS_FILE"
    set +a
    echo "Loaded local secrets override from $LOCAL_SECRETS_FILE"
fi

if [[ "${BUILD_ENGINE:-invoke}" == "nuke" ]]; then
    echo "Nuke build engine is no longer supported for repos/hagicode-release; use PyBuild/Invoke." >&2
    exit 1
fi

PYTHON_BIN="${PYTHON_EXE:-}"
if [[ -z "$PYTHON_BIN" ]]; then
    if command -v python3 >/dev/null 2>&1; then
        PYTHON_BIN=python3
    elif command -v python >/dev/null 2>&1; then
        PYTHON_BIN=python
    else
        echo "Python executable not found. Install python3 or set PYTHON_EXE." >&2
        exit 1
    fi
fi

echo "Using PyBuild/Invoke build engine"
cd "$SCRIPT_DIR"
exec "$PYTHON_BIN" -m pybuild.entry "$@"
