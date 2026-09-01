#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity test "$project_root/apps/game-unity" \
  --mode PlayMode \
  --filter Sea.Tests.PlayMode \
  --output "$project_root/apps/game-unity/Build/playmode-results.xml"

rg -q '<test-run .*total="[1-9]' \
  "$project_root/apps/game-unity/Build/playmode-results.xml"
