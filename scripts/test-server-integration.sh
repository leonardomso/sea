#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
database_name="sea-integration-$$"
state_relative=".cache/spacetime-integration-$$"
state_directory="$project_root/$state_relative"

cleanup() {
  SPACETIME_STATE_RELATIVE="$state_relative" \
    "$project_root/scripts/spacetime.sh" delete "$database_name" \
      --server http://host.docker.internal:3000 --yes >/dev/null 2>&1 || true
  rm -rf "$state_directory"
}
trap cleanup EXIT

curl --fail --silent --max-time 2 http://127.0.0.1:3000/v1/ping >/dev/null

SPACETIME_STATE_RELATIVE="$state_relative" \
  "$project_root/scripts/spacetime.sh" publish "$database_name" \
    --server http://host.docker.internal:3000 \
    --yes \
    --module-path server/spacetimedb/spacetimedb >/dev/null

test_arguments=(
  test
  tests/integration/Sea.Server.IntegrationTests/Sea.Server.IntegrationTests.csproj
)
if [ -n "${SEA_TEST_FILTER:-}" ]; then
  test_arguments+=( --filter "$SEA_TEST_FILTER" )
fi

SEA_TEST_DATABASE="$database_name" \
SEA_TEST_SERVER="http://host.docker.internal:3000" \
  "$project_root/scripts/dotnet.sh" "${test_arguments[@]}"
