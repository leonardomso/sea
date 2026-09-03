import { existsSync, readFileSync } from "node:fs";
import path from "node:path";

const TABLE = /__table\(\{\s*name: '([a-z0-9_]+)'/g;
const REDUCER = /__reducerSchema\("([a-z0-9_]+)"/g;
const ROW_FIELD = /^\s+(\w+): __t\.(.*)$/gm;
const SQL_NAME = /\.name\("([a-z0-9_]+)"\)/;

/** WorldSeed.cs spawns this many ships for every NPC definition in the content catalog. */
export const NPC_SHIPS_PER_DEFINITION = 4;

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
    npc_ai: content.npcs.length * NPC_SHIPS_PER_DEFINITION,
    stat_caps: 1,
    environment_state: 1,
  };
}
