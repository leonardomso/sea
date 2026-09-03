import { readFileSync } from "node:fs";
import path from "node:path";

const pascal = (json) => json[0].toUpperCase() + json.slice(1);
const field = (json, kind, options = {}) => ({ json, cs: pascal(json), kind, ...options });

const WORLD_OBJECT_FIELDS = [
  field("entityId", "ulong"),
  field("kind", "string"),
  field("x", "float"),
  field("y", "float"),
  field("radius", "float"),
  field("blocksMovement", "bool"),
  field("directionDegrees", "float", { default: 0 }),
  field("movementSpeed", "float", { default: 0 }),
  field("intensity", "float", { default: 0 }),
];

const CURRENT_FIELDS = [
  field("zoneId", "ulong"),
  field("x", "float"),
  field("y", "float"),
  field("radius", "float"),
  field("directionDegrees", "float"),
  field("strength", "float"),
];

const MAP_FIELDS = [
  field("mapId", "byte"),
  field("code", "string"),
  field("name", "string"),
  field("biome", "string"),
  field("mapRank", "byte"),
  field("width", "byte"),
  field("height", "byte"),
  field("pvpMode", "string"),
  field("materialId", "string"),
  field("portName", "string"),
  field("portX", "float"),
  field("portY", "float"),
  field("portRadius", "float"),
  field("terrainRows", "string[]"),
  field("objects", "object[]", { type: "WorldObjectContent", fields: WORLD_OBJECT_FIELDS }),
  field("currents", "object[]", { type: "CurrentContent", fields: CURRENT_FIELDS }),
];

const HULL_FIELDS = [
  field("id", "string"),
  field("name", "string"),
  field("tier", "byte"),
  field("hitPoints", "uint"),
  field("armorFront", "float"),
  field("armorSides", "float"),
  field("armorBack", "float"),
  field("cannonSlots", "byte"),
  field("speedSquaresPerSecond", "float"),
  field("turnDegreesPerSecond", "float"),
  field("magazine", "byte"),
  field("costGold", "uint"),
  field("mapRankRequired", "byte"),
];

const CANNON_FIELDS = [
  field("id", "string"),
  field("name", "string"),
  field("tier", "byte"),
  field("damage", "uint"),
  field("reloadSeconds", "float"),
  field("rangeSquares", "byte"),
  field("costGold", "uint"),
];

const AMMO_FIELDS = [
  field("id", "string"),
  field("code", "enum", { enumType: "AmmunitionCode" }),
  field("name", "string"),
  field("damageMultiplier", "float"),
  field("reloadMultiplier", "float"),
  field("goldPerVolley", "uint"),
  field("effect", "enum", { enumType: "AmmoEffectCode" }),
  field("effectMagnitude", "float"),
  field("effectDurationSeconds", "float"),
  field("rangeLimitSquares", "byte"),
  field("rangeMultiplier", "float"),
];

const NPC_FIELDS = [
  field("id", "string"),
  field("code", "enum", { enumType: "ShipArchetypeCode" }),
  field("name", "string"),
  field("tier", "byte"),
  field("mapId", "byte"),
  field("family", "string"),
  field("behavior", "string"),
  field("aggroRange", "float"),
  field("desiredRange", "float"),
  field("maximumSpeed", "float"),
  field("hull", "uint"),
  field("cannonDamage", "uint"),
  field("preferredAmmunition", "enum", { enumType: "AmmunitionCode" }),
  field("goldReward", "uint"),
  field("experienceReward", "ulong"),
];

const STAT_CAPS_FIELDS = [
  field("damageBonusCap", "float"),
  field("reloadBonusCap", "float"),
  field("magazineBonusCap", "byte"),
  field("hitPointBonusCap", "float"),
  field("armorPointsCap", "float"),
  field("armorAbsoluteMax", "float"),
  field("speedBonusCap", "float"),
  field("turnBonusCap", "float"),
  field("rangeBonusCapSquares", "byte"),
  field("repairAmountBonusCap", "float"),
  field("repairChannelBonusCap", "float"),
  field("cannonSlotBonusCap", "byte"),
  field("combatPowerBudget", "float"),
  field("combatPowerArmorWeight", "float"),
  field("reloadFloorSeconds", "float"),
  field("fireMinIntervalSeconds", "float"),
  field("magazineRefillIdleSeconds", "float"),
  field("burnPerSecond", "float"),
  field("burnDurationSeconds", "float"),
  field("burnHealMultiplier", "float"),
  field("repairBaseAmount", "float"),
  field("repairChannelSeconds", "float"),
  field("repairCooldownSeconds", "float"),
  field("repairFatigue", "float"),
  field("repairFatigueWindowSeconds", "float"),
  field("repairCancelThreshold", "float"),
  field("kitHealAmount", "float"),
  field("kitCooldownSeconds", "float"),
  field("respawnSeconds", "float"),
  field("spawnShieldSeconds", "float"),
  field("npcHitPointMultipliers", "float[]"),
  field("npcDpsMultipliers", "float[]"),
  field("npcArmorByTier", "float[]"),
  field("goldBase", "uint"),
  field("goldGrowth", "float"),
];

/** One entry per JSON file: which root key it carries and how it maps to C#. */
export const CONTENT_FAMILIES = [
  { file: "maps.json", key: "maps", cs: "Maps", type: "MapContent", fields: MAP_FIELDS, idKey: "code" },
  { file: "hulls.json", key: "hulls", cs: "Hulls", type: "HullContent", fields: HULL_FIELDS, idKey: "id" },
  { file: "cannons.json", key: "cannons", cs: "Cannons", type: "CannonContent", fields: CANNON_FIELDS, idKey: "id" },
  { file: "ammo.json", key: "ammunition", cs: "Ammunition", type: "AmmunitionContent", fields: AMMO_FIELDS, idKey: "id" },
  { file: "npcs.json", key: "npcs", cs: "Npcs", type: "NpcContent", fields: NPC_FIELDS, idKey: "id" },
  { file: "stat_caps.json", key: "statCaps", cs: "StatCaps", type: "StatCapsContent", fields: STAT_CAPS_FIELDS, single: true },
];

export class ContentError extends Error {}

export function loadContent(dataDir) {
  const content = {};
  for (const family of CONTENT_FAMILIES) {
    const filePath = path.join(dataDir, family.file);
    let parsed;
    try {
      parsed = JSON.parse(readFileSync(filePath, "utf8"));
    } catch (error) {
      throw new ContentError(`${family.file}: ${error.message}`, { cause: error });
    }
    if (!Object.hasOwn(parsed, family.key)) {
      throw new ContentError(`${family.file}: missing root key '${family.key}'`);
    }
    content[family.key] = parsed[family.key];
  }
  return content;
}

const ENUM_MEMBER = /^[A-Z][A-Za-z0-9]*$/;

function describe(value) {
  if (value === null) return "null";
  if (Array.isArray(value)) return "array";
  if (typeof value === "object") return "object";
  if (typeof value === "string") return JSON.stringify(value);
  return String(value);
}

function isInteger(value, max) {
  return typeof value === "number" && Number.isInteger(value) && value >= 0 && value <= max;
}

function checkScalar(kind, value, location, errors) {
  switch (kind) {
    case "string":
      if (typeof value !== "string") errors.push(`${location}: expected string, got ${describe(value)}`);
      return;
    case "bool":
      if (typeof value !== "boolean") errors.push(`${location}: expected bool, got ${describe(value)}`);
      return;
    case "byte":
      if (!isInteger(value, 255)) errors.push(`${location}: expected byte, got ${describe(value)}`);
      return;
    case "uint":
      if (!isInteger(value, 4294967295)) errors.push(`${location}: expected uint, got ${describe(value)}`);
      return;
    case "ulong":
      if (!isInteger(value, Number.MAX_SAFE_INTEGER)) errors.push(`${location}: expected ulong, got ${describe(value)}`);
      return;
    case "float":
      if (typeof value !== "number" || !Number.isFinite(value)) {
        errors.push(`${location}: expected float, got ${describe(value)}`);
      }
      return;
    case "enum":
      if (typeof value !== "string" || !ENUM_MEMBER.test(value)) {
        errors.push(`${location}: expected enum member name, got ${describe(value)}`);
      }
      return;
    default:
      throw new Error(`Unknown scalar kind '${kind}'`);
  }
}

function checkArray(elementSpec, value, location, errors) {
  if (!Array.isArray(value)) {
    errors.push(`${location}: expected array, got ${describe(value)}`);
    return;
  }
  value.forEach((entry, index) => checkValue(elementSpec, entry, `${location}[${index}]`, errors));
}

function checkValue(spec, value, location, errors) {
  switch (spec.kind) {
    case "string[]":
      return checkArray({ kind: "string" }, value, location, errors);
    case "float[]":
      return checkArray({ kind: "float" }, value, location, errors);
    case "object[]":
      return checkArray({ ...spec, kind: "object" }, value, location, errors);
    case "object":
      return checkObject(spec.fields, value, location, errors);
    default:
      return checkScalar(spec.kind, value, location, errors);
  }
}

function checkObject(fields, value, location, errors) {
  if (value === null || typeof value !== "object" || Array.isArray(value)) {
    errors.push(`${location}: expected object, got ${describe(value)}`);
    return;
  }
  const known = new Set(fields.map((spec) => spec.json));
  for (const key of Object.keys(value)) {
    if (!known.has(key)) errors.push(`${location}: unknown key '${key}'`);
  }
  for (const spec of fields) {
    const present = Object.hasOwn(value, spec.json);
    if (!present) {
      if (!Object.hasOwn(spec, "default")) errors.push(`${location}: missing '${spec.json}'`);
      continue;
    }
    checkValue(spec, value[spec.json], `${location}.${spec.json}`, errors);
  }
}

const FAMILY_SPECS = CONTENT_FAMILIES.map((f) => ({
  json: f.key,
  cs: f.cs,
  type: f.type,
  fields: f.fields,
  kind: f.single ? "object" : "object[]",
}));

export function validateContent(content) {
  const errors = [];
  CONTENT_FAMILIES.forEach((family, index) => {
    const value = content[family.key];
    checkValue(FAMILY_SPECS[index], value, family.key, errors);
    if (family.single || !Array.isArray(value)) return;
    const seen = new Set();
    value.forEach((entry, entryIndex) => {
      const id = entry?.[family.idKey];
      if (typeof id === "string") {
        if (seen.has(id)) errors.push(`${family.key}[${entryIndex}].${family.idKey}: duplicate id '${id}'`);
        seen.add(id);
      }
    });
  });
  return errors;
}

const INDENT = "    ";

function floatLiteral(value) {
  return `${value}f`;
}

const SCALARS = {
  string: (value) => JSON.stringify(value),
  bool: (value) => (value ? "true" : "false"),
  byte: (value) => `(byte)${value}`,
  uint: (value) => `${value}u`,
  ulong: (value) => `${value}UL`,
  float: (value) => floatLiteral(value),
  enum: (value, spec) => `${spec.enumType}.${value}`,
};

function scalarLiteral(spec, value) {
  const format = SCALARS[spec.kind];
  if (!format) throw new Error(`Unknown scalar kind '${spec.kind}'`);
  return format(value, spec);
}

const indent = (lines) => lines.map((line) => INDENT + line);
const comma = (lines) => [...lines.slice(0, -1), `${lines.at(-1)},`];
const block = (header, items) => [header, "{", ...indent(items.flatMap(comma)), "}"];

function valueLines(spec, item) {
  switch (spec.kind) {
    case "object":
      return block(`new ${spec.type}`, spec.fields.map((f) => assignLines(f, item)));
    case "object[]":
      return block(`new ${spec.type}[]`, item.map((entry) => valueLines({ ...spec, kind: "object" }, entry)));
    case "string[]":
      return block("new string[]", item.map((entry) => [JSON.stringify(entry)]));
    case "float[]":
      return block("new float[]", item.map((entry) => [floatLiteral(entry)]));
    default:
      return [scalarLiteral(spec, item)];
  }
}

function assignLines(spec, owner) {
  const item = Object.hasOwn(owner, spec.json) ? owner[spec.json] : spec.default;
  const [head, ...rest] = valueLines(spec, item);
  return [`${spec.cs} = ${head}`, ...rest];
}

const HEADER = [
  "// <auto-generated>",
  "//     Generated by scripts/generate-content.mjs from server/spacetimedb/spacetimedb/Content/Data/*.json.",
  "//     Do not edit by hand. Run `pnpm content:generate` after changing the JSON.",
  "// </auto-generated>",
  "",
  "namespace Sea.Server;",
  "",
];

// GameContent is not a special case: it is an object whose fields are the families.
const GAME_CONTENT = { kind: "object", type: "GameContent", fields: FAMILY_SPECS };

export function emitCatalog(content) {
  const value = valueLines(GAME_CONTENT, content);
  const method = [
    `public static GameContent CreateDefault() => ${value[0]}`,
    ...value.slice(1, -1),
    `${value.at(-1)};`,
  ];
  return `${[...HEADER, "public static partial class ContentCatalog", "{", ...indent(method), "}"].join("\n")}\n`;
}

export function buildCatalog(dataDir) {
  const content = loadContent(dataDir);
  const errors = validateContent(content);
  if (errors.length > 0) {
    throw new AggregateError(errors.map((message) => new Error(message)), "invalid content");
  }
  return emitCatalog(content);
}
