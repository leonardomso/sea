#!/usr/bin/env sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)

cd "$repo_root"
"$script_dir/dotnet.sh" format whitespace server/spacetimedb/spacetimedb/StdbModule.csproj --verify-no-changes
"$script_dir/dotnet.sh" format whitespace server/spacetimedb/tests/Sea.Server.Tests.csproj --verify-no-changes
"$script_dir/dotnet.sh" format whitespace tests/performance/Sea.Server.Benchmarks/Sea.Server.Benchmarks.csproj --verify-no-changes
"$script_dir/dotnet10.sh" format whitespace tests/load/Sea.LoadTests/Sea.LoadTests.csproj --verify-no-changes
