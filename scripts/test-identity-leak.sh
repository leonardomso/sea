#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
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
    "http://127.0.0.1:3000/v1/database/sea-local/sql" >"$sql_result"

  node -e '
    const fs = require("node:fs");
    const result = JSON.parse(fs.readFileSync(process.argv[1], "utf8"));
    process.stdout.write(String(result[0]?.rows?.length ?? 0));
  ' "$sql_result"
}

curl --fail --silent --max-time 3 http://127.0.0.1:3000/v1/ping >/dev/null
curl --fail --silent --max-time 3 http://127.0.0.1:3001/health >/dev/null

before="$(identity_count)"

for _ in {1..25}; do
  curl --fail --silent --max-time 3 http://127.0.0.1:3001/health >/dev/null
done

for _ in {1..5}; do
  curl --fail --silent --max-time 5 http://127.0.0.1:3001/ >/dev/null
done

after="$(identity_count)"

if [ "$after" != "$before" ]; then
  echo "Anonymous admin or health traffic leaked player identities: $before -> $after" >&2
  exit 1
fi

echo "Identity count remained constant at $after through health and admin refreshes."
