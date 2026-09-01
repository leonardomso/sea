#!/usr/bin/env sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
failed=0

check_pattern() {
  pattern=$1
  shift
  if rg -n "$pattern" "$@"; then
    failed=1
  fi
}

cd "$repo_root"

check_pattern '": "(latest|\^|~|[0-9]+\.(x|\*))' \
  package.json \
  apps/admin/package.json \
  packages/contracts/package.json
check_pattern 'Version="[^"]*[\*\^~]' \
  server/spacetimedb/spacetimedb/StdbModule.csproj \
  server/spacetimedb/tests/Sea.Server.Tests.csproj \
  tests/integration/Sea.Server.IntegrationTests/Sea.Server.IntegrationTests.csproj \
  tests/performance/Sea.Server.Benchmarks/Sea.Server.Benchmarks.csproj \
  tests/load/Sea.LoadTests/Sea.LoadTests.csproj \
  packages/spacetimedb-unity/SpacetimeDB.ClientSDK.csproj \
  packages/spacetimedb-unity/SpacetimeDB.ClientSDK.Godot.csproj
check_pattern '(FROM|image:).*:latest' apps/admin/Dockerfile infra/docker-compose.yml
check_pattern 'clockworklabs/spacetime:latest|mcr.microsoft.com/dotnet/sdk:latest' \
  scripts/spacetime.sh scripts/dotnet.sh scripts/dotnet10.sh .env.example

if ! grep -q 'spacetime-local' scripts/spacetime.sh \
  || ! grep -q 'spacetime_config=' scripts/spacetime.sh; then
  echo "The local SpacetimeDB CLI identity must persist across container runs." >&2
  failed=1
fi

if rg -n -- '--anonymous' package.json scripts/reset-spacetime.sh; then
  echo "The persistent local world must not be published with a throwaway identity." >&2
  failed=1
fi

if [ "$failed" -ne 0 ]; then
  echo "Dependency pin or local tool-state validation failed." >&2
  exit 1
fi

echo "Production dependency pins are exact."
