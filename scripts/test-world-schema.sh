#!/usr/bin/env bash
set -euo pipefail

runtime_directory="$(mktemp -d)"
database_url="$SEA_SPACETIME_LOCAL_URL/v1/database/sea-local/sql"
project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
. "$project_root/scripts/lib/local-ports.sh"

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

for table_name in world_state ship ship_status ship_channel cooldown volley inventory ammo_definition ability_definition npc_definition npc_ai respawn_work loot player_progression encounter_reward combat_event environment_state current_zone world_object; do
  query_table "$table_name"
done

"$project_root/scripts/spacetime.sh" describe sea-local \
  --server "$SEA_SPACETIME_DOCKER_URL" \
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
if (world.length !== 1 || world[0].tick_rate_hz !== 10 || world[0].content_version !== 4) {
  throw new Error("World state does not expose the 10 Hz versioned simulation contract.");
}

const ships = rows("ship");
if (
  ships.length < 1 ||
  !ships.every((ship) => ship.entity_id > 0) ||
  !ships.some((ship) => ship.is_active === true)
) {
  throw new Error("Unified active ship state is missing or invalid.");
}
if (!ships.some((ship) => ship.faction_code === 2)) {
  throw new Error("The unified ship table does not contain the seeded NPC ship.");
}
const npcShips = ships.filter((ship) => ship.faction_code === 2);
if (npcShips.length !== 12) throw new Error("Expected twelve persistent NPC ships.");
for (const archetypeCode of [1, 2, 3]) {
  if (npcShips.filter((ship) => ship.archetype_code === archetypeCode).length !== 4) {
    throw new Error(`Expected four NPC ships for archetype ${archetypeCode}.`);
  }
}
if (rows("npc_ai").length !== 12) throw new Error("Expected twelve NPC AI work rows.");

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
if (!sourceNames.has("IssueShipCommand")) {
  throw new Error("The authoritative ship command reducer is missing from the deployed module.");
}
for (const reducer of [
  "MoveTo", "SetCourse", "StopCourse", "SelectTarget", "ClearTarget", "SetAmmo",
  "FireBroadside", "ActivateAbility", "StartRepair", "CancelRepair", "StartBoarding",
  "CancelBoarding",
]) {
  if (sourceNames.has(reducer)) {
    throw new Error(`Legacy gameplay reducer ${reducer} is still deployed.`);
  }
}
for (const rewardContract of [
  "CombatContribution", "CombatEncounter", "EncounterReward", "EncounterRewardEvent",
]) {
  if (!sourceNames.has(rewardContract)) {
    throw new Error(`Shared reward contract ${rewardContract} is missing.`);
  }
}
if (!Array.isArray(rows("encounter_reward"))) {
  throw new Error("Reconnect-safe encounter reward history is unavailable.");
}
if (sourceNames.has("Engage")) {
  throw new Error("Prototype automatic engagement is still deployed.");
}

if (rows("ammo_definition").length !== 4) throw new Error("Expected four ammunition definitions.");
if (rows("ability_definition").length !== 4) throw new Error("Expected four ability definitions.");
const npcDefinitions = rows("npc_definition");
if (npcDefinitions.length !== 3) throw new Error("Expected three NPC definitions.");
if (npcDefinitions.some((definition) =>
  definition.maximum_speed <= 0 || definition.cannon_damage <= 0 ||
  definition.gold_reward <= 0 || definition.experience_reward <= 0)) {
  throw new Error("NPC combat and reward definitions must be positive.");
}
if (rows("environment_state").length !== 1) throw new Error("Expected one deterministic wind state.");
if (rows("current_zone").length !== 2) throw new Error("Expected two seeded current zones.");
const worldObjects = rows("world_object");
if (worldObjects.filter((item) => item.kind === "shoal").length !== 2) {
  throw new Error("Expected two active shoal hazards.");
}
const storms = worldObjects.filter((item) => item.kind === "storm");
if (storms.length !== 1 || storms[0].movement_speed <= 0 || storms[0].radius <= 0) {
  throw new Error("Expected one moving storm hazard.");
}
for (const table of ["ship_status", "ship_channel", "cooldown"]) {
  if (!Array.isArray(rows(table))) {
    throw new Error(`Tactical state table ${table} is unavailable.`);
  }
}
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
