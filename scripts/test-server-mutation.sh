#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet_image="${DOTNET_IMAGE:-mcr.microsoft.com/dotnet/sdk:8.0.424@sha256:bb32ba3ba3ea36e38572d9d8db76fa15f7cbf722f3f886e06bca6d528bd4fba8}"
output_relative="Build/performance/stryker-command-policy"
report="$project_root/$output_relative/reports/mutation-report.json"

rm -rf "$project_root/$output_relative"
docker run --rm \
  --user "$(id -u):$(id -g)" \
  -e DOTNET_CLI_HOME=/tmp/dotnet-cli \
  -v "$project_root:/workspace" \
  -w /workspace/server/spacetimedb/domain \
  --entrypoint /bin/sh \
  "$dotnet_image" \
  -c 'dotnet tool restore >/dev/null && dotnet tool run dotnet-stryker -- --test-project ../tests/Sea.Server.Tests.csproj --mutate ../spacetimedb/Domain/CommandPolicy.cs --reporter Json --output /workspace/Build/performance/stryker-command-policy --concurrency 4 --break-at 90 --threshold-low 90 --threshold-high 95 --skip-version-check'

test -s "$report"
node "$project_root/scripts/check-stryker-report.mjs" \
  "$report" \
  "$project_root/coverage/server/report/Summary.txt" \
  "$project_root/Build/performance/quality.json"
