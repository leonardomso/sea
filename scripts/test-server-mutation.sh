#!/usr/bin/env bash
# Runs Stryker on ONE domain source file. Whole-domain runs exhaust memory, so the file is the
# unit of work: `pnpm server:test:mutation Domain/ShipStatRules.cs` (paths are relative to
# server/spacetimedb/spacetimedb; CombatRules.cs and TacticalRules.cs live at its root).
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet_image="${DOTNET_IMAGE:-mcr.microsoft.com/dotnet/sdk:8.0.424@sha256:bb32ba3ba3ea36e38572d9d8db76fa15f7cbf722f3f886e06bca6d528bd4fba8}"
concurrency="${SEA_STRYKER_CONCURRENCY:-2}"

source_file="${1:-}"
if [ -z "$source_file" ] || [ ! -f "$project_root/server/spacetimedb/spacetimedb/$source_file" ]; then
  echo "usage: $0 <file relative to server/spacetimedb/spacetimedb, e.g. Domain/ShipStatRules.cs>" >&2
  exit 2
fi

label="$(basename "$source_file" .cs)"
output_relative="Build/performance/stryker/$label"
report="$project_root/$output_relative/reports/mutation-report.json"

rm -rf "$project_root/$output_relative"
docker run --rm \
  --user "$(id -u):$(id -g)" \
  -e DOTNET_CLI_HOME=/tmp/dotnet-cli \
  -v "$project_root:/workspace" \
  -w /workspace/server/spacetimedb/domain \
  --entrypoint /bin/sh \
  "$dotnet_image" \
  -c "dotnet tool restore >/dev/null && dotnet tool run dotnet-stryker -- --test-project ../tests/Sea.Server.Tests.csproj --mutate '../spacetimedb/$source_file' --reporter Json --output '/workspace/$output_relative' --concurrency $concurrency --skip-version-check"

test -s "$report"
node "$project_root/scripts/check-stryker-report.mjs" "$report" "$label" "$project_root/Build/performance/mutation/$label.json"
