#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
game_binary="$project_root/apps/game-unity/Build/Sea.app/Contents/MacOS/game-unity"
test_directory="$(mktemp -d)"
performance_log="$test_directory/presentation-performance.log"
performance_evidence="$test_directory/presentation-performance.json"
game_pid=""

cleanup() {
  if [ -n "$game_pid" ] && kill -0 "$game_pid" 2>/dev/null; then
    kill -TERM "$game_pid" 2>/dev/null || true
    wait "$game_pid" 2>/dev/null || true
  fi

  rm -rf "$test_directory"
}
trap cleanup EXIT

test -x "$game_binary"

"$game_binary" -batchmode \
  -screen-width 1920 -screen-height 1080 \
  -seaPresentationPerformanceTest \
  -seaPerformanceEvidencePath "$performance_evidence" \
  -logFile "$performance_log" >/dev/null 2>&1 &
game_pid=$!

completed=false
for _ in {1..90}; do
  if rg -q 'Sea presentation performance: .*passed=True' "$performance_log" 2>/dev/null; then
    completed=true
    break
  fi

  if ! kill -0 "$game_pid" 2>/dev/null; then
    break
  fi

  sleep 1
done

if [ "$completed" != true ] || [ ! -s "$performance_evidence" ]; then
  echo "The built macOS presentation performance probe failed." >&2
  tail -n 120 "$performance_log" >&2 || true
  exit 1
fi

if rg -q 'Unhandled Exception|Fatal error|MissingReferenceException' "$performance_log"; then
  echo "The built macOS presentation performance probe reported a runtime error." >&2
  tail -n 120 "$performance_log" >&2
  exit 1
fi

node - "$performance_evidence" <<'NODE'
const fs = require("node:fs");
const evidence = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
const passed = evidence.schemaVersion === 1 &&
  evidence.visibleShips >= 250 &&
  evidence.frameP95Milliseconds <= 16.7 &&
  evidence.frameP99Milliseconds <= 25 &&
  evidence.idleBytesPerFrame === 0 &&
  evidence.poolsStable === true &&
  evidence.runtimeErrors === 0 &&
  evidence.missingAssets === 0;
if (!passed) {
  console.error(JSON.stringify(evidence, null, 2));
  process.exit(1);
}
NODE
rg 'Sea presentation performance:' "$performance_log"
