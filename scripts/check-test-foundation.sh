#!/usr/bin/env sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)

for file in \
  tests/integration/Sea.Server.IntegrationTests/Sea.Server.IntegrationTests.csproj \
  tests/integration/Sea.Server.IntegrationTests/ReducerIntegrationTests.cs \
  server/spacetimedb/tests/GameArbitraries.cs \
  server/spacetimedb/tests/ReplayRulesTests.cs \
  tests/performance/Sea.Server.Benchmarks/ServerBenchmarks.cs \
  tests/load/Sea.LoadTests/SpacetimeLoadClient.cs \
  apps/game-unity/Assets/Tests/PlayMode/SeaScenePlayModeTests.cs \
  apps/game-unity/Assets/Tests/Performance/SeaClientPerformanceTests.cs \
  apps/game-unity/Assets/Tests/EditMode/SeaSubscriptionTests.cs \
  apps/game-unity/Assets/Tests/EditMode/SeaPresentationInfrastructureTests.cs \
  scripts/test-server-integration.sh \
  scripts/test-shared-world.sh \
  scripts/launch-local-clients.sh \
  scripts/stop-local-clients.sh \
  scripts/test-server-coverage.sh \
  scripts/test-unity-playmode.sh \
  scripts/test-unity-performance.sh; do
  if [ ! -f "$repo_root/$file" ]; then
    echo "Missing test foundation file: $file" >&2
    exit 1
  fi
done

echo "Reducer, property, replay, Unity, benchmark, load, and coverage foundations are present."
