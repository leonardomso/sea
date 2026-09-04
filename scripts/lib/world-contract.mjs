import { existsSync, readFileSync } from "node:fs";
import path from "node:path";

const TABLE = /__table\(\{\s*name: '([a-z0-9_]+)'/g;
const REDUCER = /__reducerSchema\("([a-z0-9_]+)"/g;
const ROW_FIELD = /^\s+(\w+): __t\.(.*)$/gm;
const SQL_NAME = /\.name\("([a-z0-9_]+)"\)/;

/** WorldSeed.cs keeps this many hostiles patrolling before the named captain is seeded. */
export const COMMON_SPAWN_SLOTS = 12;

/** Section 5.3's cadence: one sail in five is a veteran rather than a common. */
export const VETERAN_EVERY_SLOTS = 5;

/** NpcRules.CallHelpCount: the hulls the named captain keeps moored beside her. */
export const NAMED_ESCORT_COUNT = 2;

const COMMON_TIER = 1;
const VETERAN_TIER = 2;

/**
 * The roster WorldSeed.cs puts on the water, keyed by NPC id: the patrol slots with their
 * veteran cadence, the named captain, and the escorts seeded with her. The map does not carry
 * the same number of every archetype, so the count has to be derived the way the seed derives it.
 */
export function seededNpcRoster(content) {
  const commons = content.npcs.filter((npc) => npc.tier === COMMON_TIER);
  const veterans = content.npcs.filter((npc) => npc.tier === VETERAN_TIER);
  const named = content.npcs.find((npc) => npc.callsForHelp);
  if (commons.length === 0) {
    throw new Error("world contract: the catalog has no common enemy to patrol with");
  }

  const roster = new Map();
  const add = (npc, count) => roster.set(npc.id, (roster.get(npc.id) ?? 0) + count);
  for (let slot = 0; slot < COMMON_SPAWN_SLOTS; slot++) {
    const isVeteranSlot =
      veterans.length > 0 && slot % VETERAN_EVERY_SLOTS === VETERAN_EVERY_SLOTS - 1;
    add(isVeteranSlot ? veterans[0] : commons[slot % commons.length], 1);
  }

  if (named && veterans.length > 0) {
    add(named, 1);
    add(veterans[0], NAMED_ESCORT_COUNT);
  }

  return roster;
}

/** How many hostile hulls the seed leaves afloat in total. */
export function seededNpcShipCount(content) {
  let total = 0;
  for (const count of seededNpcRoster(content).values()) {
    total += count;
  }

  return total;
}

/**
 * The public world schema as `spacetime generate` emitted it: every public table with its SQL
 * column names, plus the public reducer names. Reading the generated TypeScript keeps the
 * contract in step with the module without a database; `pnpm quality:bindings` keeps the
 * generated code in step with the module.
 */
export function readWorldContract(generatedDir) {
  const index = readFileSync(path.join(generatedDir, "index.ts"), "utf8");
  const tables = new Map();
  for (const [, name] of index.matchAll(TABLE)) {
    tables.set(name, readColumns(generatedDir, name));
  }
  if (tables.size === 0) {
    throw new Error(`world contract: ${generatedDir} declares no tables`);
  }
  const reducers = [...index.matchAll(REDUCER)].map(([, name]) => name);
  return { tables, reducers };
}

function readColumns(generatedDir, table) {
  const file = path.join(generatedDir, `${table}_table.ts`);
  if (!existsSync(file)) {
    throw new Error(`world contract: ${table} has no generated row file`);
  }
  const columns = [...readFileSync(file, "utf8").matchAll(ROW_FIELD)].map(
    ([, field, rest]) => SQL_NAME.exec(rest)?.[1] ?? field,
  );
  if (columns.length === 0) {
    throw new Error(`world contract: ${table} declares no columns`);
  }
  return columns;
}

/** Rows the world seed guarantees for one content catalog, keyed by public table. */
export function seededRowCounts(content) {
  const sum = (items, size) => items.reduce((total, item) => total + size(item), 0);
  return {
    map_def: content.maps.length,
    sector: sum(content.maps, (map) => map.width * map.height),
    current_zone: sum(content.maps, (map) => map.currents.length),
    hull_def: content.hulls.length,
    cannon_def: content.cannons.length,
    ammo_def: content.ammunition.length,
    npc_def: content.npcs.length,
    npc_ai: seededNpcShipCount(content),
    stat_caps: 1,
    environment_state: 1,
  };
}
