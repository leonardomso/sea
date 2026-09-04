import assert from "node:assert/strict";
import { cpSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import { buildCatalog, emitCatalog, loadContent, validateContent } from "./content-catalog.mjs";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const dataDir = path.join(repoRoot, "server/spacetimedb/spacetimedb/Content/Data");

// One minimal, valid entry per content family, hand-written so the regex assertions below
// are legible and don't depend on the shape of the committed game data.
const FIXTURE = {
  maps: [
    {
      mapId: 1,
      code: "1/1",
      name: 'A"B',
      biome: "sea",
      mapRank: 1,
      width: 10,
      height: 10,
      pvpMode: "optional",
      materialId: "oak",
      portName: "Port",
      portX: 1.5,
      portY: 0,
      portRadius: 5,
      terrainRows: ["."],
      objects: [
        {
          entityId: 1,
          kind: "harbor",
          x: 0,
          y: 0,
          radius: 1,
          blocksMovement: true,
          // directionDegrees / movementSpeed / intensity omitted: exercise the zero fallback.
        },
      ],
      currents: [],
    },
  ],
  hulls: [
    {
      id: "hull_t1",
      name: "Sloop",
      tier: 1,
      hitPoints: 100,
      armorFront: 0.1,
      armorSides: 0.1,
      armorBack: 0.1,
      cannonSlots: 2,
      speedSquaresPerSecond: 1,
      turnDegreesPerSecond: 1,
      magazine: 1,
      costGold: 100,
      mapRankRequired: 1,
    },
  ],
  cannons: [
    {
      id: "cannon_t1",
      name: "Culverin",
      tier: 1,
      damage: 10,
      reloadSeconds: 1,
      rangeSquares: 5,
      costGold: 50,
    },
  ],
  ammunition: [
    {
      id: "ammo_round",
      code: "Round",
      name: "Round Shot",
      damageMultiplier: 1,
      reloadMultiplier: 1,
      goldPerVolley: 1,
      effect: "None",
      effectMagnitude: 0,
      effectDurationSeconds: 0,
      rangeLimitSquares: 5,
      rangeMultiplier: 1,
    },
  ],
  npcs: [
    {
      id: "npc_scout",
      code: "Sloop",
      name: "Scout",
      tier: 1,
      mapId: 1,
      family: "pirate",
      behavior: "aggressive",
      aggroRangeSquares: 1,
      desiredRangeSquares: 1,
      maximumSpeedSquares: 1,
      hull: 100,
      cannonDamage: 1,
      preferredAmmunition: "Round",
      goldReward: 7,
      experienceReward: 7,
    },
  ],
  statCaps: {
    damageBonusCap: 0.1,
    reloadBonusCap: 0.1,
    magazineBonusCap: 1,
    hitPointBonusCap: 0.1,
    armorPointsCap: 0.1,
    armorAbsoluteMax: 0.1,
    speedBonusCap: 0.1,
    turnBonusCap: 0.1,
    rangeBonusCapSquares: 1,
    repairAmountBonusCap: 0.1,
    repairChannelBonusCap: 0.1,
    cannonSlotBonusCap: 1,
    combatPowerBudget: 0.1,
    combatPowerArmorWeight: 0.1,
    reloadFloorSeconds: 0.1,
    fireMinIntervalSeconds: 0.1,
    magazineRefillIdleSeconds: 0.1,
    burnPerSecond: 0.1,
    burnDurationSeconds: 0.1,
    burnHealMultiplier: 0.1,
    repairBaseAmount: 0.1,
    repairChannelSeconds: 0.1,
    repairCooldownSeconds: 0.1,
    repairFatigue: 0.1,
    repairFatigueWindowSeconds: 0.1,
    repairCancelThreshold: 0.1,
    kitHealAmount: 0.1,
    kitCooldownSeconds: 0.1,
    respawnSeconds: 0.1,
    spawnShieldSeconds: 0.1,
    portCastOffSeconds: 0.1,
    npcHitPointMultipliers: [1, 1],
    npcDpsMultipliers: [1, 1],
    npcArmorByTier: [0.1, 0.1],
    goldBase: 1,
    goldGrowth: 0.1,
  },
};

test("the committed content passes shape validation", () => {
  const content = loadContent(dataDir);
  assert.deepEqual(validateContent(content), []);
});

test("the fixture content passes shape validation", () => {
  assert.deepEqual(validateContent(FIXTURE), []);
});

test("the catalog emits a well-formed ContentCatalog partial class", () => {
  const source = emitCatalog(loadContent(dataDir));
  assert.match(source, /public static partial class ContentCatalog/);
  assert.match(source, /public static GameContent CreateDefault\(\)/);
  assert.ok(source.endsWith("}\n"), "file ends with a single newline");
  assert.ok(!source.includes("\t"), "no tabs");
  assert.ok(!/ +\n/.test(source), "no trailing whitespace");
});

test("the emitter renders each scalar kind's C# literal form", () => {
  const source = emitCatalog(FIXTURE);
  assert.match(source, /MapId = \(byte\)1,/);
  assert.match(source, /PortX = 1\.5f,/);
  assert.match(source, /GoldReward = 7u,/);
  assert.match(source, /ExperienceReward = 7UL,/);
  assert.match(source, /Code = AmmunitionCode\.Round,/);
  assert.match(source, /Name = "A\\"B",/);
});

test("optional world object fields fall back to zero", () => {
  const source = emitCatalog(FIXTURE);
  assert.match(source, /Kind = "harbor",[\s\S]*?DirectionDegrees = 0f,/);
  assert.match(source, /Kind = "harbor",[\s\S]*?MovementSpeed = 0f,/);
  assert.match(source, /Kind = "harbor",[\s\S]*?Intensity = 0f,/);
});

test("validation reports missing, mistyped, unknown, and duplicate entries", () => {
  const broken = {
    ...FIXTURE,
    hulls: [
      { ...FIXTURE.hulls[0], hitPoints: "1600", extra: 1 },
      { ...FIXTURE.hulls[0], id: "hull_t1" },
    ],
    cannons: [{ id: "cannon_t1" }],
    ammunition: [{ ...FIXTURE.ammunition[0], code: "round shot" }],
    statCaps: { ...FIXTURE.statCaps, npcArmorByTier: [0.1, "x"] },
  };
  const errors = validateContent(broken);
  assert.ok(errors.includes(`hulls[0].hitPoints: expected uint, got "1600"`), errors.join("\n"));
  assert.ok(errors.includes("hulls[0]: unknown key 'extra'"), errors.join("\n"));
  assert.ok(errors.includes("hulls[1].id: duplicate id 'hull_t1'"), errors.join("\n"));
  assert.ok(errors.includes("cannons[0]: missing 'name'"), errors.join("\n"));
  assert.ok(errors.includes(`ammunition[0].code: expected enum member name, got "round shot"`), errors.join("\n"));
  assert.ok(errors.includes(`statCaps.npcArmorByTier[1]: expected float, got "x"`), errors.join("\n"));
});

test("validation rejects a byte out of range and a non-finite float", () => {
  const errors = validateContent({
    ...FIXTURE,
    maps: [{ ...FIXTURE.maps[0], width: 300 }],
    cannons: [{ ...FIXTURE.cannons[0], reloadSeconds: Number.NaN }],
  });
  assert.ok(errors.includes("maps[0].width: expected byte, got 300"), errors.join("\n"));
  assert.ok(errors.includes("cannons[0].reloadSeconds: expected float, got NaN"), errors.join("\n"));
});

test("buildCatalog throws an AggregateError when content is invalid", () => {
  const tmpDir = mkdtempSync(path.join(os.tmpdir(), "content-catalog-"));
  try {
    cpSync(dataDir, tmpDir, { recursive: true });
    const hullsPath = path.join(tmpDir, "hulls.json");
    const hulls = JSON.parse(readFileSync(hullsPath, "utf8"));
    hulls.hulls[0].hitPoints = "not-a-number";
    writeFileSync(hullsPath, JSON.stringify(hulls));

    assert.throws(
      () => buildCatalog(tmpDir),
      (error) => {
        assert.ok(error instanceof AggregateError);
        assert.ok(error.errors.length > 0);
        assert.ok(error.errors[0] instanceof Error);
        return true;
      },
    );
  } finally {
    rmSync(tmpDir, { recursive: true, force: true });
  }
});
