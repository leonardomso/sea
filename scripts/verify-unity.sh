#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
"$project_root/scripts/check-unity-source.sh"

if ! command -v unity >/dev/null 2>&1; then
  echo "Unity CLI is required for Unity import, test, and build verification." >&2
  exit 2
fi

unity test apps/game-unity --mode EditMode --output apps/game-unity/Build/test-results.xml
unity build apps/game-unity --target WebGL --execute-method Sea.Editor.SeaBuild.PerformWebGLBuild --output-path apps/game-unity/Build/WebGL --no-tail
unity build apps/game-unity --target StandaloneOSX --execute-method Sea.Editor.SeaBuild.PerformMacOSBuild --output-path apps/game-unity/Build/Sea.app --no-tail
"$project_root/scripts/test-unity-runtime.sh"
