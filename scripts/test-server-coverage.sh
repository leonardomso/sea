#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet_image="${DOTNET_IMAGE:-mcr.microsoft.com/dotnet/sdk:8.0.424@sha256:bb32ba3ba3ea36e38572d9d8db76fa15f7cbf722f3f886e06bca6d528bd4fba8}"

rm -rf "$project_root/coverage/server"

docker run --rm \
  --user "$(id -u):$(id -g)" \
  -e DOTNET_CLI_HOME=/tmp/dotnet-cli \
  -v "$project_root:/workspace" \
  -w /workspace \
  --entrypoint /bin/sh \
  "$dotnet_image" \
  -c 'dotnet test server/spacetimedb/tests/Sea.Server.Tests.csproj --collect:"XPlat Code Coverage" --results-directory coverage/server/raw -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.IncludeTestAssembly=true DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByFile="**/Generated/**,**/*.g.cs,**/packages/spacetimedb-unity/**,**/server/spacetimedb/tests/**,**/Microsoft.NET.Test.Sdk.Program.cs" && dotnet tool restore && dotnet tool run reportgenerator -reports:"coverage/server/raw/**/coverage.cobertura.xml" -targetdir:coverage/server/report -reporttypes:"Html;Cobertura;TextSummary"'

test -f "$project_root/coverage/server/report/Summary.txt"
rg -q 'Assemblies: [1-9]' "$project_root/coverage/server/report/Summary.txt"

line_coverage="$(awk '/^  Line coverage:/ { gsub("%", "", $3); print $3; exit }' \
  "$project_root/coverage/server/report/Summary.txt")"
branch_coverage="$(awk '/^  Branch coverage:/ { gsub("%", "", $3); print $3; exit }' \
  "$project_root/coverage/server/report/Summary.txt")"
if ! awk -v line="$line_coverage" -v branch="$branch_coverage" \
  'BEGIN { exit !(line >= 95 && branch >= 90) }'; then
  cat "$project_root/coverage/server/report/Summary.txt" >&2
  echo "Pure domain coverage must remain at least 95% line and 90% branch." >&2
  exit 1
fi
