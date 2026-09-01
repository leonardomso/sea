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

if [ "$failed" -ne 0 ]; then
  echo "Floating production dependency found. Pin an exact version or immutable image digest." >&2
  exit 1
fi

echo "Production dependency pins are exact."
