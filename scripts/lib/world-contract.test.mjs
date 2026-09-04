import assert from "node:assert/strict";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { loadContent } from "./content-catalog.mjs";
import { readWorldContract, seededNpcRoster, seededRowCounts } from "./world-contract.mjs";

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
    npcs: [
      { id: "skiff", tier: 1 },
      { id: "crab", tier: 1 },
      { id: "fancy", tier: 2 },
    ],
  });
  assert.deepEqual(counts, {
    map_def: 2,
    sector: 7,
    current_zone: 2,
    hull_def: 1,
    cannon_def: 2,
    ammo_def: 3,
    npc_def: 3,
    npc_ai: 12,
    stat_caps: 1,
    environment_state: 1,
  });
});

test("the seeded roster follows the patrol cadence and the captain's escorts", () => {
  const roster = seededNpcRoster({
    npcs: [
      { id: "skiff", tier: 1 },
      { id: "crab", tier: 1 },
      { id: "fancy", tier: 2 },
      { id: "red_mary", tier: 4, callsForHelp: true },
    ],
  });

  // Twelve patrol slots: every fifth one is the veteran, the rest alternate between the commons.
  assert.equal(roster.get("skiff"), 5);
  assert.equal(roster.get("crab"), 5);
  // Two veteran patrol slots plus the two hulls moored beside the named captain.
  assert.equal(roster.get("fancy"), 4);
  assert.equal(roster.get("red_mary"), 1);
});

test("a catalog with no veteran leaves the patrol to the commons alone", () => {
  const roster = seededNpcRoster({ npcs: [{ id: "skiff", tier: 1 }] });
  assert.deepEqual([...roster], [["skiff", 12]]);
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
