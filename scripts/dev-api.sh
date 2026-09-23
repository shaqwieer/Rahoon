#!/usr/bin/env bash
# Rebuilds and (re)starts the API in the background on http://localhost:5080.
#   bash scripts/dev-api.sh            # restart
#   bash scripts/dev-api.sh --reset    # drop, migrate and reseed the demo database first
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
API_DIR="$ROOT/server/src/Rahoon.Api"
LOG="${TMPDIR:-${TEMP:-/tmp}}/rahoon-api.log"

powershell -NoProfile -Command "Get-Process Rahoon.Api -ErrorAction SilentlyContinue | Stop-Process -Force" >/dev/null 2>&1 || pkill -f Rahoon.Api || true
cd "$API_DIR"
dotnet build -nologo -v q 2>&1 | grep -E " error |Build succeeded" | sort -u
if [[ "${1:-}" == "--reset" ]]; then
  ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build -- reset-demo 2>&1 | grep -E "rror|Exception|done" | grep -v "Connection\[20004\]" | head -20
fi
(Database__SeedOnStartup=false dotnet run --no-build > "$LOG" 2>&1 &)
for _ in $(seq 1 40); do sleep 1; curl -s http://localhost:5080/api/health >/dev/null 2>&1 && break; done
echo "API: $(curl -s http://localhost:5080/api/health || echo 'not responding') — log: $LOG"
