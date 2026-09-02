#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
. "$project_root/scripts/lib/local-ports.sh"
runtime_directory="$(mktemp -d)"
sql_result="$runtime_directory/identities.json"

cleanup() {
  rm -rf "$runtime_directory"
}
trap cleanup EXIT

identity_count() {
  curl --fail --silent --max-time 3 \
    --request POST \
    --header "content-type: text/plain" \
    --data "SELECT * FROM player_ownership" \
    "$SEA_SPACETIME_LOCAL_URL/v1/database/sea-local/sql" >"$sql_result"

  node -e '
    const fs = require("node:fs");
    const result = JSON.parse(fs.readFileSync(process.argv[1], "utf8"));
    process.stdout.write(String(result[0]?.rows?.length ?? 0));
  ' "$sql_result"
}

curl --fail --silent --max-time 3 "$SEA_SPACETIME_LOCAL_URL/v1/ping" >/dev/null
curl --fail --silent --max-time 3 "$SEA_ADMIN_LOCAL_URL/health" >/dev/null

before="$(identity_count)"

for _ in {1..25}; do
  curl --fail --silent --max-time 3 "$SEA_ADMIN_LOCAL_URL/health" >/dev/null
done

for _ in {1..5}; do
  curl --fail --silent --max-time 5 "$SEA_ADMIN_LOCAL_URL/" >/dev/null
done

after="$(identity_count)"

if [ "$after" != "$before" ]; then
  echo "Anonymous admin or health traffic leaked player identities: $before -> $after" >&2
  exit 1
fi

echo "Identity count remained constant at $after through health and admin refreshes."
