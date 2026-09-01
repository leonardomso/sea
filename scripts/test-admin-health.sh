#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
runtime_directory="$(mktemp -d)"
runtime_log="$runtime_directory/admin.log"
health_body="$runtime_directory/health.json"
admin_pid=""
admin_port="${SEA_TEST_ADMIN_PORT:-3101}"

cleanup() {
  if [ -n "$admin_pid" ] && kill -0 "$admin_pid" 2>/dev/null; then
    kill -TERM "$admin_pid" 2>/dev/null || true
    wait "$admin_pid" 2>/dev/null || true
  fi

  rm -rf "$runtime_directory"
}
trap cleanup EXIT

test -f "$project_root/apps/admin/.output/server/index.mjs"

(
  cd "$project_root/apps/admin"
  HOST=127.0.0.1 PORT="$admin_port" NODE_ENV=production \
    node .output/server/index.mjs >"$runtime_log" 2>&1
) &
admin_pid=$!

healthy=false
for _ in {1..30}; do
  if curl --fail --silent --max-time 1 \
    "http://127.0.0.1:$admin_port/health" >"$health_body"; then
    healthy=true
    break
  fi

  if ! kill -0 "$admin_pid" 2>/dev/null; then
    break
  fi

  sleep 0.2
done

if [ "$healthy" != true ]; then
  echo "Admin production health endpoint did not become ready." >&2
  tail -n 80 "$runtime_log" >&2 || true
  exit 1
fi

node -e '
  const fs = require("node:fs");
  const health = JSON.parse(fs.readFileSync(process.argv[1], "utf8"));
  if (health.status !== "ok" || health.service !== "sea-admin") process.exit(1);
' "$health_body"

echo "Admin production health endpoint is lightweight and ready."
