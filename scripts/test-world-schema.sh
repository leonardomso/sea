#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=scripts/lib/local-ports.sh
. "$project_root/scripts/lib/local-ports.sh"

schema_file="$(mktemp)"
trap 'rm -f "$schema_file"' EXIT

"$project_root/scripts/spacetime.sh" describe sea-local --server "$SEA_SPACETIME_DOCKER_URL" --json >"$schema_file"
node "$project_root/scripts/test-world-schema.mjs" "$SEA_SPACETIME_LOCAL_URL/v1/database/sea-local/sql" "$schema_file"
