#!/usr/bin/env node
// Probes the deployed sea-local module against the world contract: every public table the
// generated bindings know is queryable with exactly those columns, the deployed public tables
// and reducers match the bindings, and the seeded rows follow the content catalog. Every
// expectation is derived from the bindings or the content JSON, so a schema or content change
// fails by naming the table that moved rather than by a stale literal.
import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { loadContent } from "./lib/content-catalog.mjs";
import { readWorldContract, seededNpcRoster, seededRowCounts } from "./lib/world-contract.mjs";

const [sqlUrl, schemaPath] = process.argv.slice(2);
if (!sqlUrl || !schemaPath) {
  console.error("usage: test-world-schema.mjs <sql-url> <describe-json-file>");
  process.exit(2);
}

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const contract = readWorldContract(path.join(repoRoot, "packages/contracts/src/generated"));
const content = loadContent(path.join(repoRoot, "server/spacetimedb/spacetimedb/Content/Data"));

const fail = (message) => {
  throw new Error(message);
};
const snakeCase = (name) => name.replace(/([a-z0-9])([A-Z])/g, "$1_$2").toLowerCase();
const sorted = (values) => [...values].sort();
const sameSet = (left, right) => JSON.stringify(sorted(left)) === JSON.stringify(sorted(right));

async function queryAll(table) {
  const response = await fetch(sqlUrl, {
    method: "POST",
    headers: { "content-type": "text/plain" },
    body: `SELECT * FROM ${table}`,
    signal: AbortSignal.timeout(3000),
  });
  if (!response.ok) {
    fail(`${table}: SQL request failed with HTTP ${response.status}.`);
  }
  const [result] = await response.json();
  const columns = result?.schema?.elements?.map((element) => element.name?.some) ?? [];
  const rows = (result?.rows ?? []).map((row) => Object.fromEntries(columns.map((column, index) => [column, row[index]])));
  return { columns, rows };
}

// Live columns must match the bindings for every public table.
const live = new Map();
for (const [table, columns] of contract.tables) {
  const result = await queryAll(table);
  if (!sameSet(result.columns, columns)) {
    fail(`${table}: live columns [${sorted(result.columns)}] differ from the bindings [${sorted(columns)}].`);
  }
  live.set(table, result.rows);
}
const rows = (table) => live.get(table);

// The deployed public surface (public tables, client-callable reducers) must be exactly what the bindings were generated from.
const describeText = readFileSync(schemaPath, "utf8");
const sections = Object.assign({}, ...JSON.parse(describeText.slice(describeText.indexOf("{"))).sections);
const publicTables = sections.Tables.filter((table) => "Public" in table.table_access).map((table) => snakeCase(table.source_name));
const publicReducers = sections.Reducers.filter((reducer) => "ClientCallable" in reducer.visibility).map((reducer) => snakeCase(reducer.source_name));
if (!sameSet(publicTables, [...contract.tables.keys()])) {
  fail(`Deployed public tables [${sorted(publicTables)}] differ from the bindings [${sorted(contract.tables.keys())}].`);
}
if (!sameSet(publicReducers, contract.reducers)) {
  fail(`Deployed public reducers [${sorted(publicReducers)}] differ from the bindings [${sorted(contract.reducers)}].`);
}

// The seed must match the content catalog the module was built with.
const [world, ...extraWorlds] = rows("world_state");
if (!world || extraWorlds.length > 0 || world.tick_rate_hz !== 10) {
  fail("world_state must expose exactly one 10 Hz simulation row.");
}
if (!Number.isInteger(world.content_version) || world.content_version <= 0) {
  fail("world_state.content_version must be a positive seed version.");
}
for (const [table, expected] of Object.entries(seededRowCounts(content))) {
  if (rows(table).length !== expected) {
    fail(`Expected ${expected} ${table} rows from the content catalog, found ${rows(table).length}.`);
  }
}

const [seedMap] = content.maps;
const [map] = rows("map_def");
if (map.code !== seedMap.code || map.width !== seedMap.width || map.height !== seedMap.height) {
  fail(`map_def ${map.code} ${map.width}x${map.height} differs from content map ${seedMap.code} ${seedMap.width}x${seedMap.height}.`);
}
if (rows("stat_caps")[0].combat_power_budget !== content.statCaps.combatPowerBudget) {
  fail(`stat_caps.combat_power_budget differs from the content catalog (${content.statCaps.combatPowerBudget}).`);
}

const ships = rows("ship");
if (!ships.some((ship) => ship.is_active === true)) {
  fail("No active ship rows are seeded.");
}
const npcShips = ships.filter((ship) => ship.faction_code === 2);
const archetypes = new Set(npcShips.map((ship) => ship.archetype_code));
if (archetypes.size !== content.npcs.length) {
  fail(`Expected ${content.npcs.length} NPC archetypes afloat, found ${archetypes.size}.`);
}
// The roster is not uniform: the patrol favours the commons, and the veteran is also the
// hull the named captain calls, so each archetype is counted against the seed's own arithmetic.
const roster = seededNpcRoster(content);
const codeOf = new Map(rows("npc_def").map((npc) => [npc.npc_id, npc.archetype_code]));
for (const [npcId, expected] of roster) {
  const archetype = codeOf.get(npcId);
  const count = npcShips.filter((ship) => ship.archetype_code === archetype).length;
  if (count !== expected) {
    fail(`Expected ${expected} ships of NPC ${npcId} (archetype ${archetype}), found ${count}.`);
  }
}

const objects = rows("world_object");
for (const kind of new Set(seedMap.objects.map((object) => object.kind))) {
  const expected = seedMap.objects.filter((object) => object.kind === kind).length;
  const actual = objects.filter((object) => object.kind === kind).length;
  if (actual !== expected) {
    fail(`Expected ${expected} ${kind} world objects from the content map, found ${actual}.`);
  }
}
if (objects.some((object) => object.kind === "storm" && (object.movement_speed <= 0 || object.radius <= 0))) {
  fail("Storms must move and cover a positive radius.");
}
if (rows("inventory").some((item) => item.ship_entity_id <= 0 || !item.item_id)) {
  fail("Inventory rows must belong to a ship and name an item.");
}
if (rows("combat_event").length > 100) {
  fail("combat_event is transient and must stay bounded.");
}

console.log(
  `World contract holds: ${contract.tables.size} public tables, ${contract.reducers.length} public reducers, seed rows match the content catalog.`,
);
