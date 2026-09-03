import assert from "node:assert/strict";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { loadContent } from "./content-catalog.mjs";
import { NPC_SHIPS_PER_DEFINITION, readWorldContract, seededRowCounts } from "./world-contract.mjs";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const generatedDir = path.join(repoRoot, "packages/contracts/src/generated");
const dataDir = path.join(repoRoot, "server/spacetimedb/spacetimedb/Content/Data");

test("the generated bindings describe every public table with SQL column names", () => {
  const { tables, reducers } = readWorldContract(generatedDir);
  assert.ok(tables.size >= 30, `only ${tables.size} tables`);
  assert.ok(tables.get("ship").includes("entity_id"));
  assert.ok(tables.get("ship").includes("hull"), "columns without a .name() keep the field name");
  assert.ok(tables.get("player_progression").includes("map_rank"));
  assert.equal(tables.has("player_ship"), false);
  assert.ok(reducers.includes("issue_ship_command"));
});

test("reading a contract without generated bindings fails loudly", () => {
  assert.throws(() => readWorldContract(path.join(repoRoot, "scripts")), /ENOENT/);
});

test("seeded row counts follow the content catalog", () => {
  const counts = seededRowCounts({
    maps: [
      { width: 3, height: 2, currents: [1, 2] },
      { width: 1, height: 1, currents: [] },
    ],
    hulls: [1],
    cannons: [1, 2],
    ammunition: [1, 2, 3],
    npcs: [1, 2, 3],
  });
  assert.deepEqual(counts, {
    map_def: 2,
    sector: 7,
    current_zone: 2,
    hull_def: 1,
    cannon_def: 2,
    ammo_def: 3,
    npc_def: 3,
    npc_ai: 3 * NPC_SHIPS_PER_DEFINITION,
    stat_caps: 1,
    environment_state: 1,
  });
});

test("the shipped content seeds every table the contract publishes for it", () => {
  const { tables } = readWorldContract(generatedDir);
  for (const table of Object.keys(seededRowCounts(loadContent(dataDir)))) {
    assert.ok(tables.has(table), `${table} is not a public table`);
  }
});

test("the admin dashboard only reads tables and columns the module publishes", async () => {
  const { dashboardPanels, dashboardTables } = await import("../../apps/admin/src/lib/dashboard-panels.ts");
  const { tables } = readWorldContract(generatedDir);
  for (const table of dashboardTables) {
    assert.ok(tables.has(table), `${table} is not a public table`);
  }
  for (const [panel, { table, columns }] of Object.entries(dashboardPanels)) {
    assert.ok(dashboardTables.includes(table), `${panel} reads ${table}, which the dashboard never fetches`);
    for (const column of columns) {
      assert.ok(tables.get(table).includes(column), `${panel}: ${table}.${column} is not a published column`);
    }
  }
});
