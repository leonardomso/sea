#!/usr/bin/env bash
set -euo pipefail

runtime_directory="$(mktemp -d)"
database_url="http://127.0.0.1:3000/v1/database/sea-local/sql"

cleanup() {
  rm -rf "$runtime_directory"
}
trap cleanup EXIT

query_table() {
  local table_name="$1"
  curl --fail --silent --max-time 3 \
    --request POST \
    --header "content-type: text/plain" \
    --data "SELECT * FROM $table_name" \
    "$database_url" >"$runtime_directory/$table_name.json"
}

for table_name in world_state ship ammo_definition ability_definition npc_definition combat_event; do
  query_table "$table_name"
done

node - "$runtime_directory" <<'NODE'
const fs = require("node:fs");
const runtimeDirectory = process.argv[2];
const rows = (table) =>
  JSON.parse(fs.readFileSync(`${runtimeDirectory}/${table}.json`, "utf8"))[0]?.rows ?? [];

const world = rows("world_state");
if (world.length !== 1 || world[0][2] !== 10 || world[0][4] !== 1) {
  throw new Error("World state does not expose the 10 Hz versioned simulation contract.");
}

const ships = rows("ship");
if (ships.length < 1 || !ships.every((ship) => ship[0] > 0 && ship[10] === true)) {
  throw new Error("Unified active ship state is missing or invalid.");
}
if (!ships.some((ship) => ship[2] === "npc")) {
  throw new Error("The unified ship table does not contain the seeded NPC ship.");
}

if (rows("ammo_definition").length !== 4) throw new Error("Expected four ammunition definitions.");
if (rows("ability_definition").length !== 4) throw new Error("Expected four ability definitions.");
if (rows("npc_definition").length !== 3) throw new Error("Expected three NPC definitions.");
if (rows("combat_event").length > 100) throw new Error("Transient combat events are not bounded.");
NODE

legacy_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  --request POST \
  --header "content-type: text/plain" \
  --data "SELECT * FROM player_ship" \
  "$database_url")"
if [ "$legacy_status" = "200" ]; then
  echo "Legacy player_ship table still exists after unified-schema reset." >&2
  exit 1
fi

echo "Unified 10 Hz world schema and validated content are live."
