#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
. "$project_root/scripts/lib/local-ports.sh"
game_binary="$project_root/apps/game-unity/Build/Sea.app/Contents/MacOS/game-unity"
preference_domain="com.DefaultCompany.game-unity"
runtime_profile="captain-runtime"
token_key="spacetimedb.identity_token.$runtime_profile"
runtime_directory="$(mktemp -d)"
runtime_log="$runtime_directory/player.log"
runtime_evidence="$runtime_directory/runtime-evidence.json"
runtime_database="sea-runtime-$$"
spacetime_state_relative=".cache/spacetime-runtime-$$"
spacetime_state_directory="$project_root/$spacetime_state_relative"
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

  SPACETIME_STATE_RELATIVE="$spacetime_state_relative" \
    "$project_root/scripts/spacetime.sh" delete "$runtime_database" \
      --server "$SEA_SPACETIME_DOCKER_URL" --yes >/dev/null 2>&1 || true

  rm -rf "$runtime_directory"
  rm -rf "$spacetime_state_directory"
}
trap cleanup EXIT

test -x "$game_binary"
curl --fail --silent --max-time 2 "$SEA_SPACETIME_LOCAL_URL/v1/ping" >/dev/null

# The smoke scenario sinks its seeded NPC. Give each run an isolated database
# and delete it with the same local CLI identity during cleanup.
SPACETIME_STATE_RELATIVE="$spacetime_state_relative" \
"$project_root/scripts/spacetime.sh" publish "$runtime_database" \
  --server "$SEA_SPACETIME_DOCKER_URL" \
  --yes \
  --module-path server/spacetimedb/spacetimedb >/dev/null

if original_token=$(defaults read "$preference_domain" "$token_key" 2>/dev/null); then
  had_original_token=true
fi

defaults write "$preference_domain" "$token_key" -string "invalid-local-runtime-test-token"

"$game_binary" -batchmode -nographics \
  -seaDatabaseName "$runtime_database" \
  -seaProfile "$runtime_profile" \
  -seaRuntimeMoveTest -seaRuntimeCombatTest -seaRuntimeProgressionTest \
  -seaRuntimeTacticalTest \
  -seaRuntimeEvidencePath "$runtime_evidence" \
  -logFile "$runtime_log" >/dev/null 2>&1 &
game_pid=$!

# The run sails four scenarios end to end: out of the harbour, onto a hostile, over her
# wreck for the loot, then out to a storm and home again on a repair. A common is back on
# the water thirty seconds after she sinks, and the probe waits that out, so the ceiling is
# generous; a healthy run breaks out of this loop in about two minutes.
validated=false
for _ in {1..420}; do
  if rg -q "Sea client ready\." "$runtime_log" 2>/dev/null \
    && rg -q "Sea runtime observed progressive sailing\." "$runtime_log" 2>/dev/null \
    && rg -q "Sea runtime observed authoritative manual magazine combat\." "$runtime_log" 2>/dev/null \
    && rg -q "Sea runtime observed NPC sinking, atomic loot, gold, and NPC respawn\." "$runtime_log" 2>/dev/null \
    && rg -q "Sea runtime observed tactical ability, storm damage, and progressive repair\." "$runtime_log" 2>/dev/null; then
    validated=true
    break
  fi

  if ! kill -0 "$game_pid" 2>/dev/null; then
    break
  fi

  sleep 1
done

if [ "$validated" != true ] || [ ! -s "$runtime_evidence" ]; then
  echo "Unity runtime did not demonstrate sailing, magazine combat, and tactical recovery." >&2
  rg -n "Sea runtime|Rejected|Reducer|Exception|Fatal" "$runtime_log" >&2 || true
  tail -n 120 "$runtime_log" >&2 || true
  exit 1
fi

rg -q "Cached identity rejected; retrying anonymously\." "$runtime_log"
rg -q "Sea client ready\." "$runtime_log"
rg -q "Sea runtime observed progressive sailing\." "$runtime_log"
rg -q "Sea runtime observed authoritative manual magazine combat\." "$runtime_log"
rg -q "Sea runtime observed NPC sinking, atomic loot, gold, and NPC respawn\." "$runtime_log"
rg -q "Sea runtime observed tactical ability, storm damage, and progressive repair\." "$runtime_log"
node - "$runtime_evidence" <<'NODE'
const fs = require("node:fs");
const evidence = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
const passed = evidence.schemaVersion === 1 &&
  evidence.movementRequired && evidence.movementObserved &&
  evidence.combatRequired && evidence.combatObserved &&
  evidence.progressionRequired && evidence.progressionObserved &&
  evidence.tacticalRequired && evidence.tacticalObserved &&
  evidence.runtimeErrors === 0;
if (!passed) {
  console.error(JSON.stringify(evidence, null, 2));
  process.exit(1);
}
NODE
if rg -q "No runtime-compatible shader|ArgumentNullException: Value cannot be null.*shader|Unhandled Exception|Fatal error" "$runtime_log"; then
  echo "Unity runtime reported a fatal or shader error." >&2
  tail -n 120 "$runtime_log" >&2
  exit 1
fi
echo "Unity runtime demonstrated sailing, combat, NPC sinking, loot, gold, respawn, hazards, abilities, and repair."
