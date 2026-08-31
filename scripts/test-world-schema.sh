#!/usr/bin/env bash
set -euo pipefail

runtime_directory="$(mktemp -d)"
database_url="http://127.0.0.1:3000/v1/database/sea-local/sql"
project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

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

for table_name in world_state ship volley inventory ammo_definition ability_definition npc_definition combat_event environment_state current_zone; do
  query_table "$table_name"
done

"$project_root/scripts/spacetime.sh" describe sea-local \
  --server http://host.docker.internal:3000 \
  --anonymous \
  --json >"$runtime_directory/schema.json"

node - "$runtime_directory" <<'NODE'
const fs = require("node:fs");
const runtimeDirectory = process.argv[2];
const rows = (table) => {
  const result = JSON.parse(fs.readFileSync(`${runtimeDirectory}/${table}.json`, "utf8"))[0];
  const columns = result?.schema?.elements?.map((element) => element.name?.some) ?? [];
  return (result?.rows ?? []).map((row) =>
    Object.fromEntries(columns.map((column, index) => [column, row[index]])));
};
const columns = (table) => {
  const result = JSON.parse(fs.readFileSync(`${runtimeDirectory}/${table}.json`, "utf8"))[0];
  return result?.schema?.elements?.map((element) => element.name?.some) ?? [];
};

const world = rows("world_state");
if (world.length !== 1 || world[0].tick_rate_hz !== 10 || world[0].content_version !== 1) {
  throw new Error("World state does not expose the 10 Hz versioned simulation contract.");
}

const ships = rows("ship");
if (ships.length < 1 || !ships.every((ship) => ship.entity_id > 0 && ship.is_active === true)) {
  throw new Error("Unified active ship state is missing or invalid.");
}
if (!ships.some((ship) => ship.faction === "npc")) {
  throw new Error("The unified ship table does not contain the seeded NPC ship.");
}

const volleyColumns = columns("volley");
for (const field of ["hull_damage", "sail_damage", "cannon_damage", "crew_damage"]) {
  if (!volleyColumns.includes(field)) {
    throw new Error(`Volley schema is missing frozen launch field ${field}.`);
  }
}
const inventoryRows = rows("inventory");
if (inventoryRows.some((item) => item.ship_entity_id <= 0 || !item.item_id)) {
  throw new Error("Player inventory rows do not reference a valid ship and item.");
}

const schemaText = fs.readFileSync(`${runtimeDirectory}/schema.json`, "utf8");
const schema = JSON.parse(schemaText.slice(schemaText.indexOf("{")));
const sourceNames = new Set();
const visit = (value) => {
  if (Array.isArray(value)) return value.forEach(visit);
  if (!value || typeof value !== "object") return;
  if (typeof value.source_name === "string") sourceNames.add(value.source_name);
  Object.values(value).forEach(visit);
};
visit(schema);
if (!sourceNames.has("FireBroadside") || !sourceNames.has("SetAmmo")) {
  throw new Error("Manual broadside reducers are missing from the deployed module.");
}
if (sourceNames.has("Engage")) {
  throw new Error("Prototype automatic engagement is still deployed.");
}

if (rows("ammo_definition").length !== 4) throw new Error("Expected four ammunition definitions.");
if (rows("ability_definition").length !== 4) throw new Error("Expected four ability definitions.");
if (rows("npc_definition").length !== 3) throw new Error("Expected three NPC definitions.");
if (rows("environment_state").length !== 1) throw new Error("Expected one deterministic wind state.");
if (rows("current_zone").length !== 2) throw new Error("Expected two seeded current zones.");
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
