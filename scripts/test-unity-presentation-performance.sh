#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
game_binary="$project_root/apps/game-unity/Build/Sea.app/Contents/MacOS/game-unity"
test_directory="$(mktemp -d)"
performance_log="$test_directory/presentation-performance.log"
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

if [ "$completed" != true ]; then
  echo "The built macOS presentation performance probe failed." >&2
  tail -n 120 "$performance_log" >&2 || true
  exit 1
fi

if rg -q 'Unhandled Exception|Fatal error|MissingReferenceException' "$performance_log"; then
  echo "The built macOS presentation performance probe reported a runtime error." >&2
  tail -n 120 "$performance_log" >&2
  exit 1
fi

rg 'Sea presentation performance:' "$performance_log"
