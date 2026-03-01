#!/usr/bin/env bash
set -euo pipefail

AIWORKER_PORT="${AIWORKER_PORT:-8001}"
PORT="${PORT:-8080}"

export ASPNETCORE_URLS="http://0.0.0.0:${PORT}"
export External__AIWorkerBaseUrl="${External__AIWorkerBaseUrl:-http://127.0.0.1:${AIWORKER_PORT}}"

cd /app/aiworker
python3 -m uvicorn app.main:app --host 127.0.0.1 --port "${AIWORKER_PORT}" &
ai_pid=$!

cd /app/brain
dotnet Web.dll &
brain_pid=$!

shutdown() {
    kill -TERM "${brain_pid}" 2>/dev/null || true
    kill -TERM "${ai_pid}" 2>/dev/null || true
    wait "${brain_pid}" 2>/dev/null || true
    wait "${ai_pid}" 2>/dev/null || true
}

trap shutdown SIGINT SIGTERM

wait -n "${brain_pid}" "${ai_pid}"
shutdown