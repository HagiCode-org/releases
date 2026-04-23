#!/bin/bash

set -euo pipefail

READY_FILE="${HAGICODE_PM2_READY_FILE:?HAGICODE_PM2_READY_FILE is required}"
TIMEOUT_SECONDS="${HAGICODE_PM2_READY_TIMEOUT_SECONDS:-180}"
SLEEP_SECONDS="${HAGICODE_PM2_READY_POLL_SECONDS:-1}"

if [ "$#" -eq 0 ]; then
    echo "wait-for-ready.sh requires an application command" >&2
    exit 1
fi

deadline=$(( $(date +%s) + TIMEOUT_SECONDS ))

while [ ! -f "$READY_FILE" ]; do
    if [ "$(date +%s)" -ge "$deadline" ]; then
        echo "Timed out waiting for ready file: $READY_FILE" >&2
        exit 1
    fi
    sleep "$SLEEP_SECONDS"
done

exec "$@"
