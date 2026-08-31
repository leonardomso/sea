#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
game_binary="$project_root/apps/game-unity/Build/Sea.app/Contents/MacOS/game-unity"
preference_domain="com.DefaultCompany.game-unity"
token_key="spacetimedb.identity_token"
runtime_directory="$(mktemp -d)"
runtime_log="$runtime_directory/player.log"
game_pid=""
had_original_token=false
original_token=""

cleanup() {
  if [ -n "$game_pid" ] && kill -0 "$game_pid" 2>/dev/null; then
    kill -TERM "$game_pid" 2>/dev/null || true
    wait "$game_pid" 2>/dev/null || true
  fi

  if [ "$had_original_token" = true ]; then
    defaults write "$preference_domain" "$token_key" -string "$original_token"
  else
    defaults delete "$preference_domain" "$token_key" 2>/dev/null || true
  fi

  rm -rf "$runtime_directory"
}
trap cleanup EXIT

test -x "$game_binary"
curl --fail --silent --max-time 2 http://127.0.0.1:3000/v1/ping >/dev/null

if original_token=$(defaults read "$preference_domain" "$token_key" 2>/dev/null); then
  had_original_token=true
fi

defaults write "$preference_domain" "$token_key" -string "invalid-local-runtime-test-token"

"$game_binary" -batchmode -nographics -logFile "$runtime_log" >/dev/null 2>&1 &
game_pid=$!

ready=false
for _ in {1..30}; do
  if rg -q "Sea client ready\." "$runtime_log" 2>/dev/null; then
    ready=true
    break
  fi

  if ! kill -0 "$game_pid" 2>/dev/null; then
    break
  fi

  sleep 1
done

if [ "$ready" != true ]; then
  echo "Unity runtime did not become ready." >&2
  tail -n 120 "$runtime_log" >&2 || true
  exit 1
fi

rg -q "Cached identity rejected; retrying anonymously\." "$runtime_log"
rg -q "Sea client ready\." "$runtime_log"
if rg -q "No runtime-compatible shader|ArgumentNullException: Value cannot be null.*shader|Unhandled Exception|Fatal error" "$runtime_log"; then
  echo "Unity runtime reported a fatal or shader error." >&2
  tail -n 120 "$runtime_log" >&2
  exit 1
fi
echo "Unity runtime recovered a stale identity and reached Ready."
