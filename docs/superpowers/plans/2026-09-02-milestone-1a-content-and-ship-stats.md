# Milestone 1a: Content and Ship Stats Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move all game content into JSON under the module, seed Havenmere and the Tier 1 ship content from it, add the `Hull` and `ShipStats` tables with the add-then-cap stat pipeline, replace `Level`/`Experience` with `MapRank` and a private `AccountId`, and prove Math section 12 with tests.

**Architecture:** JSON files in `server/spacetimedb/spacetimedb/Content/Data/` are the content source of truth. A Node generator (`scripts/generate-content.mjs`) turns them into a committed, generated C# file (`Generated/ContentCatalog.g.cs`) so the wasm module does no runtime JSON parsing; a drift check keeps the two in sync. The pure domain project gets the content record types, `ContentCatalog.Validate`, `SectorRules`, and `ShipStatRules` (integer basis-point math so floors are exact); the module gets the new tables, seeding from the catalog, and `EnsureHull`/`RecomputeStats` wired into `LoadPlayer`.

**Tech Stack:** SpacetimeDB 2.8.3 C# module (net8.0, wasi), xunit 2.9.3 + FsCheck.Xunit 3.4.0, Node 22 (`node --test`), Unity 6000.3.23f1 client, TanStack admin, pnpm scripts, `scripts/dotnet.sh` (dotnet inside Docker).

---

## Decisions

Settled in the grilling interview on 2026-09-02. Later sub-phases build on these; do not reopen them inside 1a.

- `AccountId` lives in a private `PlayerAccount` table (not on `PlayerProgression`). It is a non-nullable string that stays `""` until Milestone 5 links Better Auth.
- The map definition table is `MapDef`. `NpcDef.ExperienceReward` and `EncounterReward.Experience` stay until 1b/1d remove experience for good.
- 1a only computes and stores `ShipStats`; combat keeps reading `Ship` fields until 1b wires the stat sheet in.
- Content is JSON under `Content/Data/`; a committed generated C# catalog is the module input. No runtime JSON parsing in wasm. `pnpm quality:content` guards drift in `ci:fast` and `check`.
- The HUD level label becomes `MAP RANK n` and the experience bar is removed. Nothing else in the HUD changes until 1e.
- Unity subscribes to `hull_def`, `cannon_def`, `stat_caps` and owner-filtered `hull` and `ship_stats`. `sector` is not subscribed until 1e.
- `pnpm server:reset` wipes local data; nothing is preserved.
- `UpgradeCannon` and its `WorldRules` constants are deleted; 1c's dock reducers replace it.
- The starter `Hull` row copies the definition name (`Sloop`); renaming arrives with the dock in 1c.
- All of Milestone 1 lands on one branch, `leonardomso/milestone-1`, as one PR. Each sub-phase is one conventional commit. The PR opens as a draft after the 1a commit so CI runs per sub-phase, and goes ready at 1f.
- 1a is executed subagent-driven (one fresh subagent per task, review between tasks). Plans for 1b–1f are written one at a time after the previous sub-phase is committed.
- Decisions are recorded here; `PLAN.md` and `docs/SEA_5_GAP_ANALYSIS.md` are amended where they disagree (Task 11).
- Settled 2026-09-02 (mid-1a): Milestone 1 runs unattended from 1a through 1e with the same per-task gates (spec, code-quality, thermo-nuclear) and an architecture pass per sub-phase. Each new sub-phase plan gets exactly one batched question round before work starts. 1f includes an automated bug hunt: local SpacetimeDB, WebGL build driven in Chrome with scripted input, console/network/database checks, screenshots; the feel review is the user's.
- Settled 2026-09-02 (mid-1a, hardening): every task from Task 7 onward gets a fourth review gate, a performance reviewer (complexity, allocations, loops, indexed access, type safety) whose findings are fixed before approval. Each sub-phase ends, after its architecture pass, with a hardening pass: (1) mutation testing with the existing Docker Stryker script, run one source file at a time (never the whole domain in one run, to stay inside memory), killing survivors with new tests until each file passes the existing 90% break-at, over every file the domain project compiles; (2) a BenchmarkDotNet project for domain hot paths plus the existing server scale-smoke thresholds, with fixes for what the numbers show; (3) SpacetimeDB integration tests (existing `tests/integration` project against a local server) for every reducer the sub-phase adds, happy path and edge cases. The scripted Chrome playtest in 1f is kept as the end-to-end suite. Hardening lands as its own conventional commits (`test(...)`, `perf(...)`) after the sub-phase's feature commit.

## Conventions for every task

- Repo root is `/Users/leonardomaldonado/orca/workspaces/sea/hraesvelg`. Always use absolute paths. Never `cd` into another checkout.
- Never run bare `git stash` / `git stash pop` (shared stash stack).
- `.editorconfig`: 4-space C#, file-scoped namespaces, braces on their own line, `System` usings first, LF line endings, final newline. Tests may use underscores in method names.
- Handwritten C# files stay at or below 500 lines. Files under a `Generated/` directory are exempt.
- Run the smallest relevant test while developing:
  `./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~<ClassName>"` (first run restores packages; allow up to 10 minutes; pass `timeout: 600000` to the Bash tool).
- Node scripts must be run as `/bin/bash -c "node /abs/path"` if a bare `node` call fails with `compdef ... invalid subscript range` in the zsh wrapper.
- AGENTS.md requires one conventional commit per sub-phase. Intermediate tasks make `wip:` commits so nothing is lost; the last task squashes them into the single commit `feat(content): add Havenmere content and ship stats`.
- Commit trailers (every commit, including `wip:` ones):
  ```
  Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01VVAL5L8X2uHhKGrESphkhF
  ```

## File structure

Create:
- `server/spacetimedb/spacetimedb/Content/Data/{maps,hulls,cannons,ammo,abilities,npcs,stat_caps}.json` — content source of truth.
- `scripts/lib/content-catalog.mjs` — pure: load JSON, validate shape, emit C#. `scripts/lib/content-catalog.test.mjs` — its tests.
- `scripts/generate-content.mjs` — CLI writing `Generated/ContentCatalog.g.cs` (or `--out <path>`).
- `scripts/check-generated-content.sh` — drift check (`pnpm quality:content`).
- `server/spacetimedb/spacetimedb/Generated/ContentCatalog.g.cs` — generated `ContentCatalog.CreateDefault()`.
- `server/spacetimedb/spacetimedb/Domain/ContentValidation.cs` — `ContentCatalog.Validate`.
- `server/spacetimedb/spacetimedb/Domain/ContentValidation.Maps.cs` — map, terrain, object, and current validation (added by review; keeps both files under 500 lines).
- `server/spacetimedb/spacetimedb/Domain/SectorRules.cs` — sector ids, world-to-sector mapping, terrain symbols.
- `server/spacetimedb/spacetimedb/Domain/ShipStatRules.cs` — add-then-cap, Combat Power, drop order, stat sheet.
- `server/spacetimedb/spacetimedb/Schema/ContentTables.cs` — `MapDef`, `Sector`, `HullDef`, `CannonDef`, `AmmoDef`, `AbilityDefinition`, `NpcDef`, `StatCaps`.
- `server/spacetimedb/spacetimedb/Schema/DockTables.cs` — `Hull`, `ShipStats`, `PlayerAccount` and their visibility filters.
- `server/spacetimedb/spacetimedb/Content/WorldSeed.cs` — world objects, NPCs, environment (moved out of `ContentSeed.cs`).
- `server/spacetimedb/spacetimedb/Simulation/ShipStatsSystem.cs` — `EnsureHull`, `RecomputeStats`.
- Tests: `server/spacetimedb/tests/ContentCatalogTests.cs`, `SectorRulesTests.cs`, `ShipStatRulesTests.cs`, `BalanceTests.cs`.

Modify:
- `server/spacetimedb/spacetimedb/Domain/ContentDefinitions.cs` — new content record types (full rewrite).
- `server/spacetimedb/spacetimedb/Domain/ProgressionRules.cs`, `Domain/WorldRules.cs` — remove level and cannon-upgrade code.
- `server/spacetimedb/spacetimedb/Schema/Tables.cs` — remove definition tables, reshape `PlayerProgression`.
- `server/spacetimedb/spacetimedb/Content/ContentSeed.cs` — seed from the catalog (full rewrite).
- `server/spacetimedb/spacetimedb/Reducers/LifecycleReducers.cs`, `Reducers/ChannelReducers.cs`, `Reducers/CombatReducers.cs`, `Simulation/*.cs` — table renames and progression changes.
- `server/spacetimedb/domain/Sea.Server.Domain.csproj`, `server/spacetimedb/tests/Sea.Server.Tests.csproj`.
- `server/spacetimedb/tests/ReplayRulesTests.cs`, `ProgressionRulesTests.cs`, `WorldRulesTests.cs`; delete `ContentValidationTests.cs`.
- `scripts/test-world-schema.sh`, `package.json`, `AGENTS.md`, `PLAN.md`.
- Unity: `Assets/Domain/SeaSubscriptionPlan.cs`, `Assets/Domain/SeaHudViewModel.cs`, `Assets/Networking/SeaConnectionClientState.cs`, `Assets/UI/SeaHudController.cs`, `Assets/UI/SeaHud.uxml`, `Assets/Presentation/SeaRuntimeProgressionProbe.cs`, `Assets/Tests/EditMode/SeaRuntimeAndCombatTests.cs`.
- Admin: `apps/admin/src/lib/operations.ts`.
- Generated bindings: `apps/game-unity/Assets/Generated/SpacetimeDB/**`, `packages/contracts/src/generated/**` (regenerated, never hand-edited).

---

### Task 1: Record the base commit and add the content JSON files

**Files:**
- Create: `server/spacetimedb/spacetimedb/Content/Data/maps.json`
- Create: `server/spacetimedb/spacetimedb/Content/Data/hulls.json`
- Create: `server/spacetimedb/spacetimedb/Content/Data/cannons.json`
- Create: `server/spacetimedb/spacetimedb/Content/Data/ammo.json`
- Create: `server/spacetimedb/spacetimedb/Content/Data/abilities.json`
- Create: `server/spacetimedb/spacetimedb/Content/Data/npcs.json`
- Create: `server/spacetimedb/spacetimedb/Content/Data/stat_caps.json`

- [ ] **Step 1: Record the base commit for the final squash**

Run:
```bash
mkdir -p /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg/.cache
git -C /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg rev-parse HEAD > /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg/.cache/plan-1a-base
cat /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg/.cache/plan-1a-base
```
Expected: a 40-character SHA (the `.cache/` directory is git-ignored).

- [ ] **Step 2: Create `maps.json`**

Units: 1 square = 10 world units. Havenmere is 20×20 squares covering world −100..100 on both axes. Sector (x, y) covers world `[-100 + 10x, -90 + 10x)` on X and the same on Y. Terrain symbols: `.` water, `~` shallow, `#` land. Row index = Y (row 0 is the south edge, Y = −100..−90), column index = X. Every blocking object's centre sits on `#`; the port sector (10, 10) is water.

```json
{
  "maps": [
    {
      "mapId": 1,
      "code": "1/1",
      "name": "Havenmere",
      "biome": "sea",
      "mapRank": 1,
      "width": 20,
      "height": 20,
      "pvpMode": "optional",
      "materialId": "oak",
      "portName": "Port Lowell",
      "portX": 0,
      "portY": 0,
      "portRadius": 10,
      "terrainRows": [
        "....................",
        "....................",
        "....................",
        "...##.......#.......",
        "...##....~..#..##...",
        "........~~~....###..",
        "........~~~....##...",
        "......##............",
        "....................",
        "....................",
        "....................",
        "............###.....",
        "............###.....",
        "....###.......~.....",
        "....###.......~~....",
        "....##........~.##..",
        "..........#.....#...",
        "..........#.........",
        "....................",
        "...................."
      ],
      "objects": [
        { "entityId": 1, "kind": "harbor", "x": 0, "y": 0, "radius": 8, "blocksMovement": false },
        { "entityId": 2, "kind": "island", "x": 35, "y": 20, "radius": 12, "blocksMovement": true },
        { "entityId": 3, "kind": "reef", "x": -30, "y": -25, "radius": 10, "blocksMovement": true },
        { "entityId": 4, "kind": "island", "x": -46, "y": 43, "radius": 16, "blocksMovement": true },
        { "entityId": 5, "kind": "island", "x": 61, "y": -48, "radius": 15, "blocksMovement": true },
        { "entityId": 6, "kind": "island", "x": -63, "y": -58, "radius": 11, "blocksMovement": true },
        { "entityId": 7, "kind": "island", "x": 4, "y": 70, "radius": 9, "blocksMovement": true },
        { "entityId": 8, "kind": "reef", "x": 24, "y": -61, "radius": 8, "blocksMovement": true },
        { "entityId": 9, "kind": "reef", "x": 68, "y": 58, "radius": 9, "blocksMovement": true },
        { "entityId": 11, "kind": "shoal", "x": -4, "y": -42, "radius": 15, "blocksMovement": false, "intensity": 0.7 },
        { "entityId": 12, "kind": "shoal", "x": 48, "y": 45, "radius": 12, "blocksMovement": false, "intensity": 0.8 },
        { "entityId": 13, "kind": "storm", "x": -72, "y": 3, "radius": 14, "blocksMovement": false, "directionDegrees": 72, "movementSpeed": 1.5, "intensity": 1 }
      ],
      "currents": [
        { "zoneId": 1, "x": -55, "y": 35, "radius": 28, "directionDegrees": 70, "strength": 1.25 },
        { "zoneId": 2, "x": 55, "y": -45, "radius": 24, "directionDegrees": 235, "strength": 1 }
      ]
    }
  ]
}
```

- [ ] **Step 3: Create `hulls.json`**

```json
{
  "hulls": [
    {
      "id": "hull_t1",
      "name": "Sloop",
      "tier": 1,
      "hitPoints": 1600,
      "armorFront": 0.15,
      "armorSides": 0.08,
      "armorBack": 0.03,
      "cannonSlots": 8,
      "speedSquaresPerSecond": 2.4,
      "turnDegreesPerSecond": 60,
      "magazine": 3,
      "costGold": 0,
      "mapRankRequired": 1
    }
  ]
}
```

- [ ] **Step 4: Create `cannons.json`**

```json
{
  "cannons": [
    {
      "id": "cannon_t1",
      "name": "Iron Cannon",
      "tier": 1,
      "damage": 20,
      "reloadSeconds": 3.0,
      "rangeSquares": 8,
      "costGold": 500
    }
  ]
}
```

- [ ] **Step 5: Create `ammo.json`**

The first block of fields is the Math section 4 model. The `hullDamage` … `appliedStatusCode` fields are the legacy per-component model that the current combat code still reads; sub-phase 1b retires them.

```json
{
  "ammunition": [
    {
      "id": "round",
      "code": "Round",
      "name": "Round Shot",
      "damageMultiplier": 1.0,
      "reloadMultiplier": 1.0,
      "goldPerVolley": 10,
      "effect": "None",
      "effectMagnitude": 0,
      "effectDurationSeconds": 0,
      "rangeLimitSquares": 0,
      "hullDamage": 25,
      "sailDamage": 5,
      "cannonDamage": 5,
      "crewDamage": 2,
      "rangeMultiplier": 1.0,
      "appliedStatus": "flooding",
      "appliedStatusCode": "Flooding"
    },
    {
      "id": "chain",
      "code": "Chain",
      "name": "Chain Shot",
      "damageMultiplier": 0.7,
      "reloadMultiplier": 1.0,
      "goldPerVolley": 40,
      "effect": "Slow",
      "effectMagnitude": 0.3,
      "effectDurationSeconds": 4,
      "rangeLimitSquares": 0,
      "hullDamage": 5,
      "sailDamage": 28,
      "cannonDamage": 2,
      "crewDamage": 2,
      "rangeMultiplier": 0.9,
      "appliedStatus": "slowed",
      "appliedStatusCode": "Slowed"
    },
    {
      "id": "grapeshot",
      "code": "Grapeshot",
      "name": "Grape Shot",
      "damageMultiplier": 0.6,
      "reloadMultiplier": 0.9,
      "goldPerVolley": 40,
      "effect": "SlowReload",
      "effectMagnitude": 0.5,
      "effectDurationSeconds": 3,
      "rangeLimitSquares": 4,
      "hullDamage": 4,
      "sailDamage": 3,
      "cannonDamage": 4,
      "crewDamage": 30,
      "rangeMultiplier": 0.55,
      "appliedStatus": "none",
      "appliedStatusCode": "None"
    },
    {
      "id": "incendiary",
      "code": "Incendiary",
      "name": "Fire Shot",
      "damageMultiplier": 0.85,
      "reloadMultiplier": 1.1,
      "goldPerVolley": 60,
      "effect": "Burn",
      "effectMagnitude": 0.006,
      "effectDurationSeconds": 5,
      "rangeLimitSquares": 0,
      "hullDamage": 14,
      "sailDamage": 8,
      "cannonDamage": 8,
      "crewDamage": 5,
      "rangeMultiplier": 0.85,
      "appliedStatus": "burning",
      "appliedStatusCode": "Burning"
    }
  ]
}
```

- [ ] **Step 6: Create `abilities.json`** (legacy abilities, unchanged values, retired in 1b)

```json
{
  "abilities": [
    { "id": "full_sail", "code": "FullSail", "cooldownTicks": 200, "durationTicks": 50 },
    { "id": "brace", "code": "Brace", "cooldownTicks": 180, "durationTicks": 40 },
    { "id": "emergency_pump", "code": "EmergencyPump", "cooldownTicks": 300, "durationTicks": 50 },
    { "id": "smoke_screen", "code": "SmokeScreen", "cooldownTicks": 240, "durationTicks": 40 }
  ]
}
```

- [ ] **Step 7: Create `npcs.json`** (legacy combat values kept; Math section 7 NPC rebalancing lands in 1c)

```json
{
  "npcs": [
    {
      "id": "patrol",
      "code": "Patrol",
      "name": "Harbor Patrol",
      "tier": 1,
      "mapId": 1,
      "family": "common",
      "behavior": "patrol",
      "aggroRange": 0,
      "desiredRange": 45,
      "maximumSpeed": 10,
      "hull": 100,
      "cannonDamage": 18,
      "preferredAmmunition": "Round",
      "preferredWeakPoint": "Hull",
      "goldReward": 80,
      "experienceReward": 100
    },
    {
      "id": "raider",
      "code": "Raider",
      "name": "Reef Raider",
      "tier": 1,
      "mapId": 1,
      "family": "common",
      "behavior": "aggressive",
      "aggroRange": 65,
      "desiredRange": 18,
      "maximumSpeed": 14,
      "hull": 90,
      "cannonDamage": 20,
      "preferredAmmunition": "Chain",
      "preferredWeakPoint": "Sails",
      "goldReward": 100,
      "experienceReward": 125
    },
    {
      "id": "gunship",
      "code": "Gunship",
      "name": "Coastal Gunship",
      "tier": 1,
      "mapId": 1,
      "family": "veteran",
      "behavior": "aggressive",
      "aggroRange": 75,
      "desiredRange": 48,
      "maximumSpeed": 9,
      "hull": 130,
      "cannonDamage": 28,
      "preferredAmmunition": "Incendiary",
      "preferredWeakPoint": "Hull",
      "goldReward": 140,
      "experienceReward": 175
    }
  ]
}
```

- [ ] **Step 8: Create `stat_caps.json`** (Math section 13.2; nothing in code may hardcode these)

```json
{
  "statCaps": {
    "damageBonusCap": 0.25,
    "reloadBonusCap": 0.2,
    "magazineBonusCap": 2,
    "hitPointBonusCap": 0.25,
    "armorPointsCap": 15,
    "armorAbsoluteMax": 0.45,
    "speedBonusCap": 0.25,
    "turnBonusCap": 0.25,
    "rangeBonusCapSquares": 2,
    "repairAmountBonusCap": 0.5,
    "repairChannelBonusCap": 0.5,
    "combatPowerBudget": 45,
    "combatPowerArmorWeight": 1.4,
    "reloadFloorSeconds": 1.5,
    "fireMinIntervalSeconds": 1.0,
    "magazineRefillIdleSeconds": 15,
    "burnPerSecond": 0.006,
    "burnDurationSeconds": 5,
    "burnHealMultiplier": 0.5,
    "repairBaseAmount": 0.2,
    "repairChannelSeconds": 3.0,
    "repairCooldownSeconds": 15,
    "repairFatigue": 0.6,
    "repairFatigueWindowSeconds": 60,
    "repairCancelThreshold": 0.15,
    "kitHealAmount": 0.25,
    "kitCooldownSeconds": 45,
    "respawnSeconds": 8,
    "spawnShieldSeconds": 10,
    "npcHitPointMultipliers": [0.5, 1.0, 2.2, 5.0, 30.0, 120.0],
    "npcDpsMultipliers": [0.25, 0.4, 0.7, 0.9, 1.2, 1.5],
    "npcArmorByTier": [0.1, 0.1, 0.15, 0.2, 0.2, 0.2],
    "goldBase": 30,
    "goldGrowth": 1.6
  }
}
```

- [ ] **Step 9: Sanity-check the JSON parses**

Run:
```bash
for f in /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg/server/spacetimedb/spacetimedb/Content/Data/*.json; do python3 -m json.tool "$f" >/dev/null && echo "ok $(basename "$f")"; done
```
Expected: seven `ok` lines.

- [ ] **Step 10: Commit**

```bash
git -C /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg add server/spacetimedb/spacetimedb/Content/Data
git -C /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg commit -m "wip(content): add Havenmere content json

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01VVAL5L8X2uHhKGrESphkhF"
```

---

### Task 2: Content generator (JSON → generated C# catalog)

**Files:**
- Create: `scripts/lib/content-catalog.mjs`
- Create: `scripts/lib/content-catalog.test.mjs`
- Create: `scripts/generate-content.mjs`
- Create: `scripts/check-generated-content.sh`
- Create (generated): `server/spacetimedb/spacetimedb/Generated/ContentCatalog.g.cs`
- Modify: `package.json` (scripts block, lines 50–57)

The generator is the only thing that knows both the JSON key names and the C# property names. Every field spec is `{ json, cs, kind }`. Kinds: `string`, `bool`, `byte`, `uint`, `ulong`, `float`, `enum` (with `enumType`), `string[]`, `float[]`, `object[]` (with `type` and `fields`). Enum values in JSON are the C# member names (`"Round"`, `"Burn"`); the C# compiler is the final check on those.

- [ ] **Step 1: Write the failing generator tests**

Create `scripts/lib/content-catalog.test.mjs`:

```js
import assert from "node:assert/strict";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import { emitCatalog, loadContent, validateContent } from "./content-catalog.mjs";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const dataDir = path.join(repoRoot, "server/spacetimedb/spacetimedb/Content/Data");

test("the committed content passes shape validation", () => {
  const content = loadContent(dataDir);
  assert.deepEqual(validateContent(content), []);
});

test("the catalog emits every content family with C# literals", () => {
  const source = emitCatalog(loadContent(dataDir));
  assert.match(source, /public static partial class ContentCatalog/);
  assert.match(source, /public static GameContent CreateDefault\(\)/);
  assert.match(source, /Name = "Havenmere",/);
  assert.match(source, /HitPoints = 1600u,/);
  assert.match(source, /ArmorFront = 0\.15f,/);
  assert.match(source, /Code = AmmunitionCode\.Grapeshot,/);
  assert.match(source, /Effect = AmmoEffectCode\.Burn,/);
  assert.match(source, /BlocksMovement = true,/);
  assert.match(source, /DamageBonusCap = 0\.25f,/);
  assert.match(source, /NpcHitPointMultipliers = new float\[\]/);
  assert.match(source, /MapId = \(byte\)1,/);
  assert.match(source, /ExperienceReward = 100UL,/);
  assert.ok(source.endsWith("}\n"), "file ends with a single newline");
  assert.ok(!source.includes("\t"), "no tabs");
  assert.ok(!/ +\n/.test(source), "no trailing whitespace");
});

test("optional world object fields fall back to zero", () => {
  const source = emitCatalog(loadContent(dataDir));
  const harbor = source.slice(source.indexOf('Kind = "harbor"'), source.indexOf('Kind = "island"'));
  assert.match(harbor, /DirectionDegrees = 0f,/);
  assert.match(harbor, /MovementSpeed = 0f,/);
  assert.match(harbor, /Intensity = 0f,/);
});

test("validation reports missing, mistyped, unknown, and duplicate entries", () => {
  const content = loadContent(dataDir);
  const broken = {
    ...content,
    hulls: [
      { ...content.hulls[0], hitPoints: "1600", extra: 1 },
      { ...content.hulls[0], id: "hull_t1" },
    ],
    cannons: [{ id: "cannon_t1" }],
    ammunition: [{ ...content.ammunition[0], code: "round shot" }],
    statCaps: { ...content.statCaps, npcArmorByTier: [0.1, "x"] },
  };
  const errors = validateContent(broken);
  assert.ok(errors.includes("hulls[0].hitPoints: expected uint, got string"), errors.join("\n"));
  assert.ok(errors.includes("hulls[0]: unknown key 'extra'"), errors.join("\n"));
  assert.ok(errors.includes("hulls[1].id: duplicate id 'hull_t1'"), errors.join("\n"));
  assert.ok(errors.includes("cannons[0]: missing 'name'"), errors.join("\n"));
  assert.ok(errors.includes("ammunition[0].code: expected enum member name, got 'round shot'"), errors.join("\n"));
  assert.ok(errors.includes("statCaps.npcArmorByTier[1]: expected float, got string"), errors.join("\n"));
});

test("validation rejects a byte out of range and a non-finite float", () => {
  const content = loadContent(dataDir);
  const errors = validateContent({
    ...content,
    maps: [{ ...content.maps[0], width: 300 }],
    cannons: [{ ...content.cannons[0], reloadSeconds: Number.NaN }],
  });
  assert.ok(errors.includes("maps[0].width: expected byte, got 300"), errors.join("\n"));
  assert.ok(errors.includes("cannons[0].reloadSeconds: expected float, got NaN"), errors.join("\n"));
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `/bin/bash -c "cd /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg && node --test scripts/lib/content-catalog.test.mjs"`
Expected: FAIL with `Cannot find module '.../scripts/lib/content-catalog.mjs'`.

- [ ] **Step 3: Write the generator library**

Create `scripts/lib/content-catalog.mjs`:

```js
import { readFileSync } from "node:fs";
import path from "node:path";

const field = (json, cs, kind, options = {}) => ({ json, cs, kind, ...options });

const WORLD_OBJECT_FIELDS = [
  field("entityId", "EntityId", "ulong"),
  field("kind", "Kind", "string"),
  field("x", "X", "float"),
  field("y", "Y", "float"),
  field("radius", "Radius", "float"),
  field("blocksMovement", "BlocksMovement", "bool"),
  field("directionDegrees", "DirectionDegrees", "float", { fallback: 0 }),
  field("movementSpeed", "MovementSpeed", "float", { fallback: 0 }),
  field("intensity", "Intensity", "float", { fallback: 0 }),
];

const CURRENT_FIELDS = [
  field("zoneId", "ZoneId", "ulong"),
  field("x", "X", "float"),
  field("y", "Y", "float"),
  field("radius", "Radius", "float"),
  field("directionDegrees", "DirectionDegrees", "float"),
  field("strength", "Strength", "float"),
];

const MAP_FIELDS = [
  field("mapId", "MapId", "byte"),
  field("code", "Code", "string"),
  field("name", "Name", "string"),
  field("biome", "Biome", "string"),
  field("mapRank", "MapRank", "byte"),
  field("width", "Width", "byte"),
  field("height", "Height", "byte"),
  field("pvpMode", "PvpMode", "string"),
  field("materialId", "MaterialId", "string"),
  field("portName", "PortName", "string"),
  field("portX", "PortX", "float"),
  field("portY", "PortY", "float"),
  field("portRadius", "PortRadius", "float"),
  field("terrainRows", "TerrainRows", "string[]"),
  field("objects", "Objects", "object[]", { type: "WorldObjectContent", fields: WORLD_OBJECT_FIELDS }),
  field("currents", "Currents", "object[]", { type: "CurrentContent", fields: CURRENT_FIELDS }),
];

const HULL_FIELDS = [
  field("id", "Id", "string"),
  field("name", "Name", "string"),
  field("tier", "Tier", "byte"),
  field("hitPoints", "HitPoints", "uint"),
  field("armorFront", "ArmorFront", "float"),
  field("armorSides", "ArmorSides", "float"),
  field("armorBack", "ArmorBack", "float"),
  field("cannonSlots", "CannonSlots", "byte"),
  field("speedSquaresPerSecond", "SpeedSquaresPerSecond", "float"),
  field("turnDegreesPerSecond", "TurnDegreesPerSecond", "float"),
  field("magazine", "Magazine", "byte"),
  field("costGold", "CostGold", "uint"),
  field("mapRankRequired", "MapRankRequired", "byte"),
];

const CANNON_FIELDS = [
  field("id", "Id", "string"),
  field("name", "Name", "string"),
  field("tier", "Tier", "byte"),
  field("damage", "Damage", "uint"),
  field("reloadSeconds", "ReloadSeconds", "float"),
  field("rangeSquares", "RangeSquares", "byte"),
  field("costGold", "CostGold", "uint"),
];

const AMMO_FIELDS = [
  field("id", "Id", "string"),
  field("code", "Code", "enum", { enumType: "AmmunitionCode" }),
  field("name", "Name", "string"),
  field("damageMultiplier", "DamageMultiplier", "float"),
  field("reloadMultiplier", "ReloadMultiplier", "float"),
  field("goldPerVolley", "GoldPerVolley", "uint"),
  field("effect", "Effect", "enum", { enumType: "AmmoEffectCode" }),
  field("effectMagnitude", "EffectMagnitude", "float"),
  field("effectDurationSeconds", "EffectDurationSeconds", "float"),
  field("rangeLimitSquares", "RangeLimitSquares", "byte"),
  field("hullDamage", "HullDamage", "uint"),
  field("sailDamage", "SailDamage", "uint"),
  field("cannonDamage", "CannonDamage", "uint"),
  field("crewDamage", "CrewDamage", "uint"),
  field("rangeMultiplier", "RangeMultiplier", "float"),
  field("appliedStatus", "AppliedStatus", "string"),
  field("appliedStatusCode", "AppliedStatusCode", "enum", { enumType: "StatusCode" }),
];

const ABILITY_FIELDS = [
  field("id", "Id", "string"),
  field("code", "Code", "enum", { enumType: "AbilityCode" }),
  field("cooldownTicks", "CooldownTicks", "uint"),
  field("durationTicks", "DurationTicks", "uint"),
];

const NPC_FIELDS = [
  field("id", "Id", "string"),
  field("code", "Code", "enum", { enumType: "ShipArchetypeCode" }),
  field("name", "Name", "string"),
  field("tier", "Tier", "byte"),
  field("mapId", "MapId", "byte"),
  field("family", "Family", "string"),
  field("behavior", "Behavior", "string"),
  field("aggroRange", "AggroRange", "float"),
  field("desiredRange", "DesiredRange", "float"),
  field("maximumSpeed", "MaximumSpeed", "float"),
  field("hull", "Hull", "uint"),
  field("cannonDamage", "CannonDamage", "uint"),
  field("preferredAmmunition", "PreferredAmmunition", "enum", { enumType: "AmmunitionCode" }),
  field("preferredWeakPoint", "PreferredWeakPoint", "enum", { enumType: "WeakPointCode" }),
  field("goldReward", "GoldReward", "uint"),
  field("experienceReward", "ExperienceReward", "ulong"),
];

const STAT_CAPS_FIELDS = [
  field("damageBonusCap", "DamageBonusCap", "float"),
  field("reloadBonusCap", "ReloadBonusCap", "float"),
  field("magazineBonusCap", "MagazineBonusCap", "byte"),
  field("hitPointBonusCap", "HitPointBonusCap", "float"),
  field("armorPointsCap", "ArmorPointsCap", "float"),
  field("armorAbsoluteMax", "ArmorAbsoluteMax", "float"),
  field("speedBonusCap", "SpeedBonusCap", "float"),
  field("turnBonusCap", "TurnBonusCap", "float"),
  field("rangeBonusCapSquares", "RangeBonusCapSquares", "byte"),
  field("repairAmountBonusCap", "RepairAmountBonusCap", "float"),
  field("repairChannelBonusCap", "RepairChannelBonusCap", "float"),
  field("combatPowerBudget", "CombatPowerBudget", "float"),
  field("combatPowerArmorWeight", "CombatPowerArmorWeight", "float"),
  field("reloadFloorSeconds", "ReloadFloorSeconds", "float"),
  field("fireMinIntervalSeconds", "FireMinIntervalSeconds", "float"),
  field("magazineRefillIdleSeconds", "MagazineRefillIdleSeconds", "float"),
  field("burnPerSecond", "BurnPerSecond", "float"),
  field("burnDurationSeconds", "BurnDurationSeconds", "float"),
  field("burnHealMultiplier", "BurnHealMultiplier", "float"),
  field("repairBaseAmount", "RepairBaseAmount", "float"),
  field("repairChannelSeconds", "RepairChannelSeconds", "float"),
  field("repairCooldownSeconds", "RepairCooldownSeconds", "float"),
  field("repairFatigue", "RepairFatigue", "float"),
  field("repairFatigueWindowSeconds", "RepairFatigueWindowSeconds", "float"),
  field("repairCancelThreshold", "RepairCancelThreshold", "float"),
  field("kitHealAmount", "KitHealAmount", "float"),
  field("kitCooldownSeconds", "KitCooldownSeconds", "float"),
  field("respawnSeconds", "RespawnSeconds", "float"),
  field("spawnShieldSeconds", "SpawnShieldSeconds", "float"),
  field("npcHitPointMultipliers", "NpcHitPointMultipliers", "float[]"),
  field("npcDpsMultipliers", "NpcDpsMultipliers", "float[]"),
  field("npcArmorByTier", "NpcArmorByTier", "float[]"),
  field("goldBase", "GoldBase", "uint"),
  field("goldGrowth", "GoldGrowth", "float"),
];

/** One entry per JSON file: which root key it carries and how it maps to C#. */
export const CONTENT_FAMILIES = [
  { file: "maps.json", key: "maps", cs: "Maps", type: "MapContent", fields: MAP_FIELDS, idKey: "code" },
  { file: "hulls.json", key: "hulls", cs: "Hulls", type: "HullContent", fields: HULL_FIELDS, idKey: "id" },
  { file: "cannons.json", key: "cannons", cs: "Cannons", type: "CannonContent", fields: CANNON_FIELDS, idKey: "id" },
  { file: "ammo.json", key: "ammunition", cs: "Ammunition", type: "AmmunitionContent", fields: AMMO_FIELDS, idKey: "id" },
  { file: "abilities.json", key: "abilities", cs: "Abilities", type: "AbilityContent", fields: ABILITY_FIELDS, idKey: "id" },
  { file: "npcs.json", key: "npcs", cs: "Npcs", type: "NpcContent", fields: NPC_FIELDS, idKey: "id" },
  { file: "stat_caps.json", key: "statCaps", cs: "StatCaps", type: "StatCapsContent", fields: STAT_CAPS_FIELDS, single: true },
];

export function loadContent(dataDir) {
  const content = {};
  for (const family of CONTENT_FAMILIES) {
    const filePath = path.join(dataDir, family.file);
    const parsed = JSON.parse(readFileSync(filePath, "utf8"));
    if (!Object.hasOwn(parsed, family.key)) {
      throw new Error(`${family.file}: missing root key '${family.key}'`);
    }
    content[family.key] = parsed[family.key];
  }
  return content;
}

const ENUM_MEMBER = /^[A-Z][A-Za-z0-9]*$/;

function describe(value) {
  if (value === null) return "null";
  if (Array.isArray(value)) return "array";
  if (typeof value === "number") return String(value);
  return typeof value;
}

function isInteger(value, max) {
  return typeof value === "number" && Number.isInteger(value) && value >= 0 && value <= max;
}

function checkScalar(kind, value, spec, location, errors) {
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
        errors.push(`${location}: expected enum member name, got '${value}'`);
      }
      return;
    default:
      throw new Error(`Unknown scalar kind '${kind}'`);
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
      if (spec.fallback === undefined) errors.push(`${location}: missing '${spec.json}'`);
      continue;
    }
    const item = value[spec.json];
    const itemLocation = `${location}.${spec.json}`;
    if (spec.kind === "string[]" || spec.kind === "float[]") {
      if (!Array.isArray(item)) {
        errors.push(`${itemLocation}: expected array, got ${describe(item)}`);
        continue;
      }
      item.forEach((entry, index) =>
        checkScalar(spec.kind === "string[]" ? "string" : "float", entry, spec, `${itemLocation}[${index}]`, errors));
    } else if (spec.kind === "object[]") {
      if (!Array.isArray(item)) {
        errors.push(`${itemLocation}: expected array, got ${describe(item)}`);
        continue;
      }
      item.forEach((entry, index) => checkObject(spec.fields, entry, `${itemLocation}[${index}]`, errors));
    } else {
      checkScalar(spec.kind, item, spec, itemLocation, errors);
    }
  }
}

export function validateContent(content) {
  const errors = [];
  for (const family of CONTENT_FAMILIES) {
    const value = content[family.key];
    if (family.single) {
      checkObject(family.fields, value, family.key, errors);
      continue;
    }
    if (!Array.isArray(value)) {
      errors.push(`${family.key}: expected array, got ${describe(value)}`);
      continue;
    }
    const seen = new Set();
    value.forEach((entry, index) => {
      const location = `${family.key}[${index}]`;
      checkObject(family.fields, entry, location, errors);
      const id = entry?.[family.idKey];
      if (typeof id === "string") {
        if (seen.has(id)) errors.push(`${location}.${family.idKey}: duplicate id '${id}'`);
        seen.add(id);
      }
    });
  }
  return errors;
}

const INDENT = "    ";

function floatLiteral(value) {
  return `${value}f`;
}

function scalarLiteral(spec, value) {
  switch (spec.kind) {
    case "string":
      return JSON.stringify(value);
    case "bool":
      return value ? "true" : "false";
    case "byte":
      return `(byte)${value}`;
    case "uint":
      return `${value}u`;
    case "ulong":
      return `${value}UL`;
    case "float":
      return floatLiteral(value);
    case "enum":
      return `${spec.enumType}.${value}`;
    default:
      throw new Error(`Unknown scalar kind '${spec.kind}'`);
  }
}

function emitArray(elementType, items, emitItem, depth, lines) {
  const pad = INDENT.repeat(depth);
  lines.push(`${pad}{`);
  for (const item of items) {
    emitItem(item, depth + 1, lines);
  }
  lines.push(`${pad}}`);
}

function emitObjectBody(type, fields, value, depth, lines, trailer, prefix = "") {
  const pad = INDENT.repeat(depth);
  lines.push(`${pad}${prefix}new ${type}`);
  lines.push(`${pad}{`);
  const inner = INDENT.repeat(depth + 1);
  for (const spec of fields) {
    const item = Object.hasOwn(value, spec.json) ? value[spec.json] : spec.fallback;
    if (spec.kind === "string[]") {
      lines.push(`${inner}${spec.cs} = new string[]`);
      emitArray("string", item, (entry, d, out) => out.push(`${INDENT.repeat(d)}${JSON.stringify(entry)},`), depth + 1, lines);
      lines[lines.length - 1] += ",";
    } else if (spec.kind === "float[]") {
      lines.push(`${inner}${spec.cs} = new float[]`);
      emitArray("float", item, (entry, d, out) => out.push(`${INDENT.repeat(d)}${floatLiteral(entry)},`), depth + 1, lines);
      lines[lines.length - 1] += ",";
    } else if (spec.kind === "object[]") {
      lines.push(`${inner}${spec.cs} = new ${spec.type}[]`);
      emitArray(spec.type, item, (entry, d, out) => emitObjectBody(spec.type, spec.fields, entry, d, out, ","), depth + 1, lines);
      lines[lines.length - 1] += ",";
    } else {
      lines.push(`${inner}${spec.cs} = ${scalarLiteral(spec, item)},`);
    }
  }
  lines.push(`${pad}}${trailer}`);
}

export function emitCatalog(content) {
  const lines = [
    "// <auto-generated>",
    "//     Generated by scripts/generate-content.mjs from server/spacetimedb/spacetimedb/Content/Data/*.json.",
    "//     Do not edit by hand. Run `pnpm content:generate` after changing the JSON.",
    "// </auto-generated>",
    "",
    "namespace Sea.Server;",
    "",
    "public static partial class ContentCatalog",
    "{",
    `${INDENT}public static GameContent CreateDefault() => new GameContent`,
    `${INDENT}{`,
  ];
  for (const family of CONTENT_FAMILIES) {
    const value = content[family.key];
    if (family.single) {
      emitObjectBody(family.type, family.fields, value, 2, lines, ",", `${family.cs} = `);
      continue;
    }
    lines.push(`${INDENT.repeat(2)}${family.cs} = new ${family.type}[]`);
    emitArray(family.type, value, (entry, d, out) => emitObjectBody(family.type, family.fields, entry, d, out, ","), 2, lines);
    lines[lines.length - 1] += ",";
  }
  lines.push(`${INDENT}};`);
  lines.push("}");
  return `${lines.join("\n")}\n`;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `/bin/bash -c "cd /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg && node --test scripts/lib/content-catalog.test.mjs"`
Expected: `# pass 5`, `# fail 0`.

- [ ] **Step 5: Write the CLI and the drift check**

Create `scripts/generate-content.mjs`:

```js
import { mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import { emitCatalog, loadContent, validateContent } from "./lib/content-catalog.mjs";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const dataDir = path.join(repoRoot, "server/spacetimedb/spacetimedb/Content/Data");
const defaultOutput = path.join(repoRoot, "server/spacetimedb/spacetimedb/Generated/ContentCatalog.g.cs");

const outIndex = process.argv.indexOf("--out");
const outputPath = outIndex === -1 ? defaultOutput : path.resolve(process.argv[outIndex + 1]);

const content = loadContent(dataDir);
const errors = validateContent(content);
if (errors.length > 0) {
  console.error(errors.join("\n"));
  process.exit(1);
}

mkdirSync(path.dirname(outputPath), { recursive: true });
writeFileSync(outputPath, emitCatalog(content));
console.log(`Wrote ${path.relative(repoRoot, outputPath)}`);
```

Create `scripts/check-generated-content.sh`:

```sh
#!/usr/bin/env sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)

cd "$repo_root"
mkdir -p .cache
generated=$(mktemp "$repo_root/.cache/content-catalog.XXXXXX")
trap 'rm -f -- "$generated"' EXIT HUP INT TERM

node scripts/generate-content.mjs --out "$generated" >/dev/null
diff -u server/spacetimedb/spacetimedb/Generated/ContentCatalog.g.cs "$generated"

echo "Generated content catalog matches the committed JSON."
```

Run: `chmod +x /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg/scripts/check-generated-content.sh`

- [ ] **Step 6: Add the pnpm scripts**

In `package.json`, add these two entries next to `quality:scripts` (keep the alphabetical-ish grouping the file already uses):

```json
    "content:generate": "node scripts/generate-content.mjs",
    "quality:content": "./scripts/check-generated-content.sh",
```

Then append ` && pnpm quality:content` to the end of both the `ci:fast` and `check` script strings. Verify with:

Run: `grep -n "quality:content\|content:generate" /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg/package.json`
Expected: four matching lines (two definitions, one in `ci:fast`, one in `check`).

- [ ] **Step 7: Generate the catalog and run the drift check**

Run:
```bash
/bin/bash -c "cd /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg && pnpm content:generate && pnpm quality:content && pnpm quality:scripts"
```
Expected: `Wrote server/spacetimedb/spacetimedb/Generated/ContentCatalog.g.cs`, then `Generated content catalog matches the committed JSON.`, then all `node --test` suites pass.

Spot-check the output: `sed -n '1,40p' /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg/server/spacetimedb/spacetimedb/Generated/ContentCatalog.g.cs` should show the header, `namespace Sea.Server;`, and `Maps = new MapContent[]` with braces on their own lines.

- [ ] **Step 8: Commit**

```bash
git -C /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg add scripts/lib/content-catalog.mjs scripts/lib/content-catalog.test.mjs scripts/generate-content.mjs scripts/check-generated-content.sh server/spacetimedb/spacetimedb/Generated/ContentCatalog.g.cs package.json
git -C /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg commit -m "wip(content): add content catalog generator

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01VVAL5L8X2uHhKGrESphkhF"
```

---

**Review amendments (applied during execution; the branch is authoritative over the code blocks above).**

The thermo-nuclear review of Task 2 restructured the generator without changing a byte of the emitted `ContentCatalog.g.cs`:

- `scripts/lib/content-catalog.mjs`: the emitter is line-returning (`indent`/`comma`/`block`/`valueLines`/`assignLines`); `GameContent` is emitted through the same object path as every family via `FAMILY_SPECS`/`GAME_CONTENT`, so `single` only selects a kind. `cs` names are derived with `pascal(json)` (override via `{ cs }`), optional fields use `{ default }` detected with `Object.hasOwn`, `checkArray` takes an element spec, `loadContent` throws `ContentError` for malformed JSON or a missing root key, and `buildCatalog(dataDir)` is the single entry point (throws `AggregateError` on validation errors).
- `scripts/generate-content.mjs` accepts only `--out <path>`; anything else prints usage and exits 2. `ContentError` and `AggregateError` print one line per problem and exit 1.
- `scripts/lib/content-catalog.test.mjs` is fixture-driven: only two tests touch the shipped JSON; literal forms, escaping, defaults, and error wording are asserted against an in-test `FIXTURE`.
- `package.json`: `pnpm quality:scripts` was added to `ci:fast` before `quality:content`.


### Task 3: Domain content types, validation, and sector rules

**Files:**
- Modify (full rewrite): `server/spacetimedb/spacetimedb/Domain/ContentDefinitions.cs`
- Create: `server/spacetimedb/spacetimedb/Domain/ContentValidation.cs`
- Create: `server/spacetimedb/spacetimedb/Domain/SectorRules.cs`
- Modify: `server/spacetimedb/domain/Sea.Server.Domain.csproj`
- Modify: `server/spacetimedb/tests/Sea.Server.Tests.csproj`
- Delete: `server/spacetimedb/tests/ContentValidationTests.cs`
- Create: `server/spacetimedb/tests/ContentCatalogTests.cs`
- Create: `server/spacetimedb/tests/SectorRulesTests.cs`

After this task the module project will not compile (the old `ContentSeed.cs` still references `AmmunitionContent` and `CombatContent`); that is expected and is fixed in Task 7. Only the domain and test projects are built here.

- [ ] **Step 1: Wire the generated catalog and the JSON into the projects**

Replace the `<ItemGroup>` in `server/spacetimedb/domain/Sea.Server.Domain.csproj` with:

```xml
  <ItemGroup>
    <Compile Include="../spacetimedb/Domain/*.cs" Link="Domain/%(Filename)%(Extension)" />
    <Compile Include="../spacetimedb/Generated/ContentCatalog.g.cs" Link="Generated/ContentCatalog.g.cs" />
    <Compile Include="../spacetimedb/CombatRules.cs" Link="CombatRules.cs" />
    <Compile Include="../spacetimedb/TacticalRules.cs" Link="TacticalRules.cs" />
  </ItemGroup>
```

Add a second `<ItemGroup>` to `server/spacetimedb/tests/Sea.Server.Tests.csproj`, after the package references:

```xml
  <ItemGroup>
    <None Include="../spacetimedb/Content/Data/*.json" Link="Content/Data/%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

- [ ] **Step 2: Write the failing sector tests**

Create `server/spacetimedb/tests/SectorRulesTests.cs`:

```csharp
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class SectorRulesTests
{
    private static MapContent Havenmere() => ContentCatalog.CreateDefault().Maps[0];

    [Fact]
    public void Sector_id_packs_map_row_and_column()
    {
        Assert.Equal(0x01_0C_0DUL, SectorRules.SectorId(1, 13, 12));
        Assert.Equal(0x01_00_00UL, SectorRules.SectorId(1, 0, 0));
    }

    [Theory]
    [InlineData(0f, 0f, 10, 10)]
    [InlineData(-100f, -100f, 0, 0)]
    [InlineData(99.9f, 99.9f, 19, 19)]
    [InlineData(35f, 20f, 13, 12)]
    [InlineData(-30f, -25f, 7, 7)]
    public void World_positions_map_to_ten_unit_sectors(float x, float y, int column, int row)
    {
        Assert.Equal(new SectorCoordinate(column, row), SectorRules.SectorOf(Havenmere(), x, y));
    }

    [Theory]
    [InlineData(-100f, 0f, true)]
    [InlineData(100f, 0f, false)]
    [InlineData(0f, -100.01f, false)]
    public void Contains_uses_a_half_open_map_extent(float x, float y, bool expected)
    {
        Assert.Equal(expected, SectorRules.Contains(Havenmere(), x, y));
    }

    [Theory]
    [InlineData('.', TerrainCode.Water)]
    [InlineData('~', TerrainCode.Shallow)]
    [InlineData('#', TerrainCode.Land)]
    public void Terrain_symbols_parse(char symbol, TerrainCode expected)
    {
        Assert.True(SectorRules.TryParseTerrain(symbol, out var terrain));
        Assert.Equal(expected, terrain);
    }

    [Fact]
    public void Unknown_terrain_symbol_is_rejected()
    {
        Assert.False(SectorRules.TryParseTerrain('x', out _));
    }

    [Fact]
    public void Havenmere_port_sits_on_water_and_the_first_island_on_land()
    {
        var map = Havenmere();
        Assert.Equal(TerrainCode.Water, SectorRules.TerrainAt(map, 10, 10));
        Assert.Equal(TerrainCode.Land, SectorRules.TerrainAt(map, 13, 12));
    }
}
```

- [ ] **Step 3: Write the failing content catalog tests**

Delete the old file: `git -C /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg rm -q server/spacetimedb/tests/ContentValidationTests.cs`

Create `server/spacetimedb/tests/ContentCatalogTests.cs`:

```csharp
using System.Text.Json;
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class ContentCatalogTests
{
    private static readonly string DataDirectory = Path.Combine(AppContext.BaseDirectory, "Content", "Data");

    private static GameContent Default() => ContentCatalog.CreateDefault();

    private static JsonDocument Load(string file) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(DataDirectory, file)));

    [Fact]
    public void Default_catalog_is_valid()
    {
        Assert.Empty(ContentCatalog.Validate(Default()));
    }

    [Theory]
    [InlineData("maps.json", "maps")]
    [InlineData("hulls.json", "hulls")]
    [InlineData("cannons.json", "cannons")]
    [InlineData("ammo.json", "ammunition")]
    [InlineData("abilities.json", "abilities")]
    [InlineData("npcs.json", "npcs")]
    [InlineData("stat_caps.json", "statCaps")]
    public void Every_content_file_has_exactly_one_root_key(string file, string key)
    {
        using var document = Load(file);
        var property = Assert.Single(document.RootElement.EnumerateObject());
        Assert.Equal(key, property.Name);
    }

    public static TheoryData<string, string, string, string[]> CatalogIds()
    {
        var content = Default();
        return new TheoryData<string, string, string, string[]>
        {
            { "maps.json", "maps", "code", content.Maps.Select(map => map.Code).ToArray() },
            { "hulls.json", "hulls", "id", content.Hulls.Select(hull => hull.Id).ToArray() },
            { "cannons.json", "cannons", "id", content.Cannons.Select(cannon => cannon.Id).ToArray() },
            { "ammo.json", "ammunition", "id", content.Ammunition.Select(ammo => ammo.Id).ToArray() },
            { "abilities.json", "abilities", "id", content.Abilities.Select(ability => ability.Id).ToArray() },
            { "npcs.json", "npcs", "id", content.Npcs.Select(npc => npc.Id).ToArray() },
        };
    }

    [Theory]
    [MemberData(nameof(CatalogIds))]
    public void Json_ids_match_the_generated_catalog(string file, string key, string idProperty, string[] expected)
    {
        using var document = Load(file);
        var ids = document.RootElement.GetProperty(key)
            .EnumerateArray()
            .Select(element => element.GetProperty(idProperty).GetString())
            .ToArray();

        Assert.Equal(expected, ids);
    }

    [Fact]
    public void Stat_caps_json_matches_the_generated_catalog()
    {
        using var document = Load("stat_caps.json");
        var caps = document.RootElement.GetProperty("statCaps");
        var generated = Default().StatCaps;

        Assert.Equal(caps.GetProperty("combatPowerBudget").GetSingle(), generated.CombatPowerBudget);
        Assert.Equal(caps.GetProperty("damageBonusCap").GetSingle(), generated.DamageBonusCap);
        Assert.Equal(caps.GetProperty("reloadFloorSeconds").GetSingle(), generated.ReloadFloorSeconds);
        Assert.Equal(caps.GetProperty("armorAbsoluteMax").GetSingle(), generated.ArmorAbsoluteMax);
        Assert.Equal(
            caps.GetProperty("npcHitPointMultipliers").GetArrayLength(),
            generated.NpcHitPointMultipliers.Count);
    }

    [Fact]
    public void Havenmere_terrain_matches_json()
    {
        using var document = Load("maps.json");
        var rows = document.RootElement.GetProperty("maps")[0]
            .GetProperty("terrainRows")
            .EnumerateArray()
            .Select(row => row.GetString())
            .ToArray();

        Assert.Equal(rows, Default().Maps[0].TerrainRows);
        Assert.Equal(20, rows.Length);
    }

    [Fact]
    public void Zero_hit_point_hull_is_rejected()
    {
        var content = Default();
        var errors = ContentCatalog.Validate(content with { Hulls = [content.Hulls[0] with { HitPoints = 0 }] });
        Assert.Contains("hull_t1: hit points must be positive.", errors);
    }

    [Fact]
    public void Duplicate_ammunition_is_rejected()
    {
        var content = Default();
        var errors = ContentCatalog.Validate(content with { Ammunition = [.. content.Ammunition, content.Ammunition[0]] });
        Assert.Contains("Duplicate ammunition id 'round'.", errors);
    }

    [Fact]
    public void Missing_round_shot_is_rejected()
    {
        var content = Default();
        var errors = ContentCatalog.Validate(content with
        {
            Ammunition = content.Ammunition.Where(ammo => ammo.Code != AmmunitionCode.Round).ToList(),
        });
        Assert.Contains("Ammunition must include the Round baseline.", errors);
    }

    [Fact]
    public void Short_terrain_row_is_rejected()
    {
        var content = Default();
        var map = content.Maps[0];
        var errors = ContentCatalog.Validate(content with
        {
            Maps = [map with { TerrainRows = [.. map.TerrainRows.Take(19), "..................."] }],
        });
        Assert.Contains("Map 1/1: terrain row 19 has 19 columns, expected 20.", errors);
    }

    [Fact]
    public void Port_on_land_is_rejected()
    {
        var content = Default();
        var errors = ContentCatalog.Validate(content with { Maps = [content.Maps[0] with { PortX = 35f, PortY = 20f }] });
        Assert.Contains("Map 1/1: the port sector (13, 12) must be water.", errors);
    }

    [Fact]
    public void Blocking_object_off_land_is_rejected()
    {
        var content = Default();
        var map = content.Maps[0];
        var moved = map.Objects.Select(item => item.EntityId == 2 ? item with { X = 0f, Y = 0f } : item).ToList();
        var errors = ContentCatalog.Validate(content with { Maps = [map with { Objects = moved }] });
        Assert.Contains("Map 1/1: object 2 blocks movement but its sector (10, 10) is not land.", errors);
    }

    [Fact]
    public void Cannon_reload_below_the_floor_is_rejected()
    {
        var content = Default();
        var errors = ContentCatalog.Validate(content with { Cannons = [content.Cannons[0] with { ReloadSeconds = 1f }] });
        Assert.Contains("cannon_t1: reload 1s is below the floor 1.5s.", errors);
    }

    [Fact]
    public void Armor_absolute_max_outside_the_unit_interval_is_rejected()
    {
        var content = Default();
        var errors = ContentCatalog.Validate(content with { StatCaps = content.StatCaps with { ArmorAbsoluteMax = 1f } });
        Assert.Contains("StatCaps: armor absolute max must be between 0 and 1.", errors);
    }

    [Fact]
    public void Npc_on_an_unknown_map_is_rejected()
    {
        var content = Default();
        var errors = ContentCatalog.Validate(content with { Npcs = [content.Npcs[0] with { MapId = 9 }] });
        Assert.Contains("patrol: map 9 does not exist.", errors);
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail to compile**

Run: `./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~SectorRulesTests"` (from the repo root)
Expected: build errors such as `The type or namespace name 'MapContent' could not be found` and `'GameContent' does not exist`.

- [ ] **Step 5: Rewrite `ContentDefinitions.cs` with the new content records**

Replace the whole file `server/spacetimedb/spacetimedb/Domain/ContentDefinitions.cs` with:

```csharp
namespace Sea.Server;

public enum TerrainCode : byte
{
    Water = 0,
    Shallow = 1,
    Land = 2,
}

public enum AmmoEffectCode : byte
{
    None = 0,
    Slow = 1,
    Burn = 2,
    SlowReload = 3,
}

public sealed record WorldObjectContent
{
    public required ulong EntityId { get; init; }
    public required string Kind { get; init; }
    public required float X { get; init; }
    public required float Y { get; init; }
    public required float Radius { get; init; }
    public required bool BlocksMovement { get; init; }
    public required float DirectionDegrees { get; init; }
    public required float MovementSpeed { get; init; }
    public required float Intensity { get; init; }
}

public sealed record CurrentContent
{
    public required ulong ZoneId { get; init; }
    public required float X { get; init; }
    public required float Y { get; init; }
    public required float Radius { get; init; }
    public required float DirectionDegrees { get; init; }
    public required float Strength { get; init; }
}

public sealed record MapContent
{
    public required byte MapId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Biome { get; init; }
    public required byte MapRank { get; init; }
    public required byte Width { get; init; }
    public required byte Height { get; init; }
    public required string PvpMode { get; init; }
    public required string MaterialId { get; init; }
    public required string PortName { get; init; }
    public required float PortX { get; init; }
    public required float PortY { get; init; }
    public required float PortRadius { get; init; }
    public required IReadOnlyList<string> TerrainRows { get; init; }
    public required IReadOnlyList<WorldObjectContent> Objects { get; init; }
    public required IReadOnlyList<CurrentContent> Currents { get; init; }
}

public sealed record HullContent
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required byte Tier { get; init; }
    public required uint HitPoints { get; init; }
    public required float ArmorFront { get; init; }
    public required float ArmorSides { get; init; }
    public required float ArmorBack { get; init; }
    public required byte CannonSlots { get; init; }
    public required float SpeedSquaresPerSecond { get; init; }
    public required float TurnDegreesPerSecond { get; init; }
    public required byte Magazine { get; init; }
    public required uint CostGold { get; init; }
    public required byte MapRankRequired { get; init; }
}

public sealed record CannonContent
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required byte Tier { get; init; }
    public required uint Damage { get; init; }
    public required float ReloadSeconds { get; init; }
    public required byte RangeSquares { get; init; }
    public required uint CostGold { get; init; }
}

public sealed record AmmunitionContent
{
    public required string Id { get; init; }
    public required AmmunitionCode Code { get; init; }
    public required string Name { get; init; }
    public required float DamageMultiplier { get; init; }
    public required float ReloadMultiplier { get; init; }
    public required uint GoldPerVolley { get; init; }
    public required AmmoEffectCode Effect { get; init; }
    public required float EffectMagnitude { get; init; }
    public required float EffectDurationSeconds { get; init; }
    public required byte RangeLimitSquares { get; init; }
    public required uint HullDamage { get; init; }
    public required uint SailDamage { get; init; }
    public required uint CannonDamage { get; init; }
    public required uint CrewDamage { get; init; }
    public required float RangeMultiplier { get; init; }
    public required string AppliedStatus { get; init; }
    public required StatusCode AppliedStatusCode { get; init; }
}

public sealed record AbilityContent
{
    public required string Id { get; init; }
    public required AbilityCode Code { get; init; }
    public required uint CooldownTicks { get; init; }
    public required uint DurationTicks { get; init; }
}

public sealed record NpcContent
{
    public required string Id { get; init; }
    public required ShipArchetypeCode Code { get; init; }
    public required string Name { get; init; }
    public required byte Tier { get; init; }
    public required byte MapId { get; init; }
    public required string Family { get; init; }
    public required string Behavior { get; init; }
    public required float AggroRange { get; init; }
    public required float DesiredRange { get; init; }
    public required float MaximumSpeed { get; init; }
    public required uint Hull { get; init; }
    public required uint CannonDamage { get; init; }
    public required AmmunitionCode PreferredAmmunition { get; init; }
    public required WeakPointCode PreferredWeakPoint { get; init; }
    public required uint GoldReward { get; init; }
    public required ulong ExperienceReward { get; init; }
}

public sealed record StatCapsContent
{
    public required float DamageBonusCap { get; init; }
    public required float ReloadBonusCap { get; init; }
    public required byte MagazineBonusCap { get; init; }
    public required float HitPointBonusCap { get; init; }
    public required float ArmorPointsCap { get; init; }
    public required float ArmorAbsoluteMax { get; init; }
    public required float SpeedBonusCap { get; init; }
    public required float TurnBonusCap { get; init; }
    public required byte RangeBonusCapSquares { get; init; }
    public required float RepairAmountBonusCap { get; init; }
    public required float RepairChannelBonusCap { get; init; }
    public required float CombatPowerBudget { get; init; }
    public required float CombatPowerArmorWeight { get; init; }
    public required float ReloadFloorSeconds { get; init; }
    public required float FireMinIntervalSeconds { get; init; }
    public required float MagazineRefillIdleSeconds { get; init; }
    public required float BurnPerSecond { get; init; }
    public required float BurnDurationSeconds { get; init; }
    public required float BurnHealMultiplier { get; init; }
    public required float RepairBaseAmount { get; init; }
    public required float RepairChannelSeconds { get; init; }
    public required float RepairCooldownSeconds { get; init; }
    public required float RepairFatigue { get; init; }
    public required float RepairFatigueWindowSeconds { get; init; }
    public required float RepairCancelThreshold { get; init; }
    public required float KitHealAmount { get; init; }
    public required float KitCooldownSeconds { get; init; }
    public required float RespawnSeconds { get; init; }
    public required float SpawnShieldSeconds { get; init; }
    public required IReadOnlyList<float> NpcHitPointMultipliers { get; init; }
    public required IReadOnlyList<float> NpcDpsMultipliers { get; init; }
    public required IReadOnlyList<float> NpcArmorByTier { get; init; }
    public required uint GoldBase { get; init; }
    public required float GoldGrowth { get; init; }
}

public sealed record GameContent
{
    public required IReadOnlyList<MapContent> Maps { get; init; }
    public required IReadOnlyList<HullContent> Hulls { get; init; }
    public required IReadOnlyList<CannonContent> Cannons { get; init; }
    public required IReadOnlyList<AmmunitionContent> Ammunition { get; init; }
    public required IReadOnlyList<AbilityContent> Abilities { get; init; }
    public required IReadOnlyList<NpcContent> Npcs { get; init; }
    public required StatCapsContent StatCaps { get; init; }
}
```

- [ ] **Step 6: Create `SectorRules.cs`**

Create `server/spacetimedb/spacetimedb/Domain/SectorRules.cs`:

```csharp
namespace Sea.Server;

public readonly record struct SectorCoordinate(int X, int Y);

public static class SectorRules
{
    public const float SquareSizeUnits = 10f;

    public static ulong SectorId(byte mapId, int x, int y) =>
        ((ulong)mapId << 16) | ((ulong)(byte)y << 8) | (byte)x;

    public static float OriginX(MapContent map) => -map.Width * SquareSizeUnits / 2f;

    public static float OriginY(MapContent map) => -map.Height * SquareSizeUnits / 2f;

    public static bool Contains(MapContent map, float worldX, float worldY) =>
        worldX >= OriginX(map) &&
        worldX < OriginX(map) + map.Width * SquareSizeUnits &&
        worldY >= OriginY(map) &&
        worldY < OriginY(map) + map.Height * SquareSizeUnits;

    public static SectorCoordinate SectorOf(MapContent map, float worldX, float worldY)
    {
        var x = (int)Math.Floor((worldX - OriginX(map)) / SquareSizeUnits);
        var y = (int)Math.Floor((worldY - OriginY(map)) / SquareSizeUnits);
        return new SectorCoordinate(
            Math.Clamp(x, 0, map.Width - 1),
            Math.Clamp(y, 0, map.Height - 1));
    }

    public static bool TryParseTerrain(char symbol, out TerrainCode terrain)
    {
        switch (symbol)
        {
            case '.':
                terrain = TerrainCode.Water;
                return true;
            case '~':
                terrain = TerrainCode.Shallow;
                return true;
            case '#':
                terrain = TerrainCode.Land;
                return true;
            default:
                terrain = TerrainCode.Water;
                return false;
        }
    }

    public static TerrainCode TerrainAt(MapContent map, int x, int y) =>
        TryParseTerrain(map.TerrainRows[y][x], out var terrain)
            ? terrain
            : throw new ArgumentOutOfRangeException(nameof(x), $"Unknown terrain symbol at ({x}, {y}).");
}
```

- [ ] **Step 7: Create `ContentValidation.cs`**

Create `server/spacetimedb/spacetimedb/Domain/ContentValidation.cs`:

```csharp
using System.Globalization;

namespace Sea.Server;

public static partial class ContentCatalog
{
    public static IReadOnlyList<string> Validate(GameContent content)
    {
        var errors = new List<string>();
        ValidateStatCaps(content.StatCaps, errors);
        ValidateMaps(content.Maps, errors);
        ValidateHulls(content.Hulls, content.StatCaps, errors);
        ValidateCannons(content.Cannons, content.StatCaps, errors);
        ValidateAmmunition(content.Ammunition, errors);
        ValidateAbilities(content.Abilities, errors);
        ValidateNpcs(content.Npcs, content.Maps, errors);
        return errors;
    }

    private static void ValidateStatCaps(StatCapsContent caps, List<string> errors)
    {
        if (caps.DamageBonusCap <= 0f || caps.HitPointBonusCap <= 0f || caps.ArmorPointsCap <= 0f ||
            caps.SpeedBonusCap <= 0f || caps.TurnBonusCap <= 0f || caps.RepairAmountBonusCap <= 0f)
        {
            errors.Add("StatCaps: bonus caps must be positive.");
        }

        if (caps.ReloadBonusCap <= 0f || caps.ReloadBonusCap >= 1f ||
            caps.RepairChannelBonusCap <= 0f || caps.RepairChannelBonusCap >= 1f)
        {
            errors.Add("StatCaps: reload and repair channel caps must be between 0 and 1.");
        }

        if (caps.ArmorAbsoluteMax <= 0f || caps.ArmorAbsoluteMax >= 1f)
        {
            errors.Add("StatCaps: armor absolute max must be between 0 and 1.");
        }

        if (caps.CombatPowerBudget <= 0f || caps.CombatPowerArmorWeight <= 0f)
        {
            errors.Add("StatCaps: combat power budget and armor weight must be positive.");
        }

        if (caps.ReloadFloorSeconds <= 0f || caps.RepairChannelSeconds <= 0f ||
            caps.RepairBaseAmount <= 0f || caps.RepairBaseAmount >= 1f)
        {
            errors.Add("StatCaps: reload floor, repair channel, and repair base amount must be positive.");
        }

        if (caps.NpcHitPointMultipliers.Count != 6 || caps.NpcDpsMultipliers.Count != 6 ||
            caps.NpcArmorByTier.Count != 6)
        {
            errors.Add("StatCaps: NPC multiplier lists must have 6 entries.");
        }

        if (caps.GoldBase == 0 || caps.GoldGrowth <= 1f)
        {
            errors.Add("StatCaps: gold base must be positive and gold growth above 1.");
        }
    }

    private static void ValidateMaps(IReadOnlyList<MapContent> maps, List<string> errors)
    {
        if (maps.Count == 0)
        {
            errors.Add("At least one map is required.");
            return;
        }

        var ids = new HashSet<byte>();
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var map in maps)
        {
            var label = $"Map {map.Code}";
            if (!ids.Add(map.MapId))
            {
                errors.Add($"Duplicate map id {map.MapId}.");
            }

            if (string.IsNullOrWhiteSpace(map.Code) || !codes.Add(map.Code))
            {
                errors.Add($"Duplicate or empty map code '{map.Code}'.");
            }

            if (map.MapRank == 0)
            {
                errors.Add($"{label}: map rank must be positive.");
            }

            if (string.IsNullOrWhiteSpace(map.Name) || string.IsNullOrWhiteSpace(map.PortName))
            {
                errors.Add($"{label}: map and port names must not be empty.");
            }

            if (map.PortRadius <= 0f)
            {
                errors.Add($"{label}: port radius must be positive.");
            }

            if (map.Width == 0 || map.Height == 0)
            {
                errors.Add($"{label}: width and height must be positive.");
                continue;
            }

            if (!ValidateTerrain(map, label, errors))
            {
                continue;
            }

            var port = SectorRules.SectorOf(map, map.PortX, map.PortY);
            if (!SectorRules.Contains(map, map.PortX, map.PortY) ||
                SectorRules.TerrainAt(map, port.X, port.Y) != TerrainCode.Water)
            {
                errors.Add($"{label}: the port sector ({port.X}, {port.Y}) must be water.");
            }

            ValidateObjects(map, label, errors);
            ValidateCurrents(map, label, errors);
        }
    }

    private static bool ValidateTerrain(MapContent map, string label, List<string> errors)
    {
        if (map.TerrainRows.Count != map.Height)
        {
            errors.Add($"{label}: expected {map.Height} terrain rows, found {map.TerrainRows.Count}.");
            return false;
        }

        var valid = true;
        for (var y = 0; y < map.Height; y++)
        {
            var row = map.TerrainRows[y];
            if (row.Length != map.Width)
            {
                errors.Add($"{label}: terrain row {y} has {row.Length} columns, expected {map.Width}.");
                valid = false;
                continue;
            }

            for (var x = 0; x < map.Width; x++)
            {
                if (!SectorRules.TryParseTerrain(row[x], out _))
                {
                    errors.Add($"{label}: unknown terrain symbol '{row[x]}' at ({x}, {y}).");
                    valid = false;
                }
            }
        }

        return valid;
    }

    private static void ValidateObjects(MapContent map, string label, List<string> errors)
    {
        var ids = new HashSet<ulong>();
        foreach (var item in map.Objects)
        {
            if (!ids.Add(item.EntityId))
            {
                errors.Add($"{label}: duplicate object entity id {item.EntityId}.");
            }

            if (string.IsNullOrWhiteSpace(item.Kind) || item.Radius <= 0f)
            {
                errors.Add($"{label}: object {item.EntityId} needs a kind and a positive radius.");
            }

            if (!SectorRules.Contains(map, item.X, item.Y))
            {
                errors.Add($"{label}: object {item.EntityId} lies outside the map.");
                continue;
            }

            var sector = SectorRules.SectorOf(map, item.X, item.Y);
            if (item.BlocksMovement && SectorRules.TerrainAt(map, sector.X, sector.Y) != TerrainCode.Land)
            {
                errors.Add(
                    $"{label}: object {item.EntityId} blocks movement but its sector ({sector.X}, {sector.Y}) is not land.");
            }
        }
    }

    private static void ValidateCurrents(MapContent map, string label, List<string> errors)
    {
        var ids = new HashSet<ulong>();
        foreach (var current in map.Currents)
        {
            if (!ids.Add(current.ZoneId))
            {
                errors.Add($"{label}: duplicate current zone id {current.ZoneId}.");
            }

            if (current.Radius <= 0f || current.Strength <= 0f)
            {
                errors.Add($"{label}: current zone {current.ZoneId} needs a positive radius and strength.");
            }
        }
    }

    private static void ValidateHulls(IReadOnlyList<HullContent> hulls, StatCapsContent caps, List<string> errors)
    {
        if (hulls.Count == 0)
        {
            errors.Add("At least one hull is required.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hull in hulls)
        {
            if (string.IsNullOrWhiteSpace(hull.Id) || !ids.Add(hull.Id))
            {
                errors.Add($"Duplicate or empty hull id '{hull.Id}'.");
            }

            if (hull.HitPoints == 0)
            {
                errors.Add($"{hull.Id}: hit points must be positive.");
            }

            if (!ArmorFaceIsValid(hull.ArmorFront, caps) || !ArmorFaceIsValid(hull.ArmorSides, caps) ||
                !ArmorFaceIsValid(hull.ArmorBack, caps))
            {
                errors.Add($"{hull.Id}: armor faces must be between 0 and {Format(caps.ArmorAbsoluteMax)}.");
            }

            if (hull.CannonSlots == 0 || hull.Magazine == 0 || hull.Tier == 0 ||
                hull.SpeedSquaresPerSecond <= 0f || hull.TurnDegreesPerSecond <= 0f)
            {
                errors.Add($"{hull.Id}: slots, magazine, tier, speed, and turn must be positive.");
            }

            if (string.IsNullOrWhiteSpace(hull.Name))
            {
                errors.Add($"{hull.Id}: name is empty.");
            }
        }
    }

    private static bool ArmorFaceIsValid(float face, StatCapsContent caps) =>
        face >= 0f && face <= caps.ArmorAbsoluteMax;

    private static void ValidateCannons(IReadOnlyList<CannonContent> cannons, StatCapsContent caps, List<string> errors)
    {
        if (cannons.Count == 0)
        {
            errors.Add("At least one cannon is required.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cannon in cannons)
        {
            if (string.IsNullOrWhiteSpace(cannon.Id) || !ids.Add(cannon.Id))
            {
                errors.Add($"Duplicate or empty cannon id '{cannon.Id}'.");
            }

            if (cannon.Damage == 0 || cannon.RangeSquares == 0 || cannon.Tier == 0)
            {
                errors.Add($"{cannon.Id}: damage, range, and tier must be positive.");
            }

            if (cannon.ReloadSeconds < caps.ReloadFloorSeconds)
            {
                errors.Add(
                    $"{cannon.Id}: reload {Format(cannon.ReloadSeconds)}s is below the floor {Format(caps.ReloadFloorSeconds)}s.");
            }

            if (string.IsNullOrWhiteSpace(cannon.Name))
            {
                errors.Add($"{cannon.Id}: name is empty.");
            }
        }
    }

    private static void ValidateAmmunition(IReadOnlyList<AmmunitionContent> ammunition, List<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var codes = new HashSet<AmmunitionCode>();
        foreach (var ammo in ammunition)
        {
            if (string.IsNullOrWhiteSpace(ammo.Id))
            {
                errors.Add("Ammunition id is empty.");
            }
            else if (!ids.Add(ammo.Id))
            {
                errors.Add($"Duplicate ammunition id '{ammo.Id}'.");
            }

            if (ammo.Code == AmmunitionCode.None || !codes.Add(ammo.Code))
            {
                errors.Add($"{ammo.Id}: ammunition code must be unique and not None.");
            }

            if (ammo.DamageMultiplier <= 0f || ammo.ReloadMultiplier <= 0f || ammo.RangeMultiplier <= 0f)
            {
                errors.Add($"{ammo.Id}: multipliers must be positive.");
            }

            if (ammo.EffectDurationSeconds < 0f || ammo.EffectMagnitude < 0f)
            {
                errors.Add($"{ammo.Id}: effect magnitude and duration must not be negative.");
            }

            if (string.IsNullOrWhiteSpace(ammo.Name))
            {
                errors.Add($"{ammo.Id}: name is empty.");
            }
        }

        if (!codes.Contains(AmmunitionCode.Round))
        {
            errors.Add("Ammunition must include the Round baseline.");
        }
    }

    private static void ValidateAbilities(IReadOnlyList<AbilityContent> abilities, List<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var codes = new HashSet<AbilityCode>();
        foreach (var ability in abilities)
        {
            if (string.IsNullOrWhiteSpace(ability.Id) || !ids.Add(ability.Id) || !codes.Add(ability.Code))
            {
                errors.Add($"Duplicate or empty ability '{ability.Id}'.");
            }

            if (ability.CooldownTicks == 0 || ability.DurationTicks == 0)
            {
                errors.Add($"{ability.Id}: cooldown and duration ticks must be positive.");
            }
        }
    }

    private static void ValidateNpcs(IReadOnlyList<NpcContent> npcs, IReadOnlyList<MapContent> maps, List<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var codes = new HashSet<ShipArchetypeCode>();
        var mapIds = new HashSet<byte>(maps.Select(map => map.MapId));
        foreach (var npc in npcs)
        {
            if (string.IsNullOrWhiteSpace(npc.Id) || !ids.Add(npc.Id) || !codes.Add(npc.Code))
            {
                errors.Add($"Duplicate or empty NPC '{npc.Id}'.");
            }

            if (!mapIds.Contains(npc.MapId))
            {
                errors.Add($"{npc.Id}: map {npc.MapId} does not exist.");
            }

            if (npc.Tier == 0 || npc.MaximumSpeed <= 0f || npc.Hull == 0 || npc.CannonDamage == 0 ||
                npc.GoldReward == 0 || npc.ExperienceReward == 0)
            {
                errors.Add($"{npc.Id}: tier, speed, hull, damage, and rewards must be positive.");
            }

            if (string.IsNullOrWhiteSpace(npc.Name))
            {
                errors.Add($"{npc.Id}: name is empty.");
            }
        }
    }

    private static string Format(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
```

- [ ] **Step 8: Run the sector and content tests**

Run: `./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~SectorRulesTests|FullyQualifiedName~ContentCatalogTests"`
Expected: all tests pass (6 sector tests, 21 content tests including theory rows).

If `Havenmere_terrain_matches_json` or the port/object validation fails, the terrain grid is wrong. Do not edit the tests. Fix `maps.json` so that every `blocksMovement: true` object's centre is on `#` (sector column = `floor((x + 100) / 10)`, row = `floor((y + 100) / 10)`) and rerun `pnpm content:generate`.

- [ ] **Step 9: Check the size rule and formatting for the new files**

Run:
```bash
wc -l /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg/server/spacetimedb/spacetimedb/Domain/ContentDefinitions.cs /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg/server/spacetimedb/spacetimedb/Domain/ContentValidation.cs /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg/server/spacetimedb/tests/ContentCatalogTests.cs
```
Expected: every count at or below 500.

- [ ] **Step 10: Commit**

```bash
git -C /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg add server/spacetimedb/spacetimedb/Domain/ContentDefinitions.cs server/spacetimedb/spacetimedb/Domain/ContentValidation.cs server/spacetimedb/spacetimedb/Domain/SectorRules.cs server/spacetimedb/domain/Sea.Server.Domain.csproj server/spacetimedb/tests/Sea.Server.Tests.csproj server/spacetimedb/tests/ContentCatalogTests.cs server/spacetimedb/tests/SectorRulesTests.cs
git -C /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg commit -m "wip(content): add content records, validation, and sector rules

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01VVAL5L8X2uHhKGrESphkhF"
```

---

**Review amendments (applied during execution; the branch is authoritative over the code blocks above).**

The code-quality and thermo-nuclear reviews of Task 3 hardened the validator and sector rules. Public signatures used by later tasks are unchanged (`SectorRules.SectorId(byte, int, int)`, `TerrainAt(map, x, y)`, `SectorOf`, `Contains`, `ContentCatalog.Validate`, `TerrainCode`, `AmmoEffectCode`, `WorldObjectContent.BlocksMovement`).

- `Domain/HotPathCodes.cs` now owns `TerrainCode`, `AmmoEffectCode`, and `TryParseTerrain` beside the other code enums; `SectorRules.TryParseTerrain` delegates to it.
- `Domain/ContentValidation.cs` starts with `ArgumentNullException.ThrowIfNull`. Uniqueness goes through private `IdSet`/`CodeSet` helpers (no `HashSet.Add` inside `||` chains). Every numeric rule is one of `Positive`/`NotNegative`/`UnitInterval`/`AtMost`/`Between`, written NaN-rejecting (`!(value > 0f)`), and names exactly one field. Ids are cross-checked against `HotPathCodes` (`AmmunitionId`, `StatusId`, `TryParseAbility`, `ShipArchetype`); `AbilityCode.None` and `ShipArchetypeCode.PlayerSloop` are rejected as NPC/ability sentinels. `NpcArmorByTier` values are bounded by `ArmorAbsoluteMax`; the remaining StatCaps fields have positivity rules; `AggroRange` is `NotNegative` (the shipped `patrol` is passive), `CostGold`/`GoldPerVolley`/`RangeLimitSquares` are deliberately unvalidated (0 is meaningful).
- `Domain/ContentValidation.Maps.cs` (new, split along the map seam) validates maps: label built after the code is validated, world-extent guard (`SectorRules.OriginX/Y == WorldRules.MapMin`), port checked with `TrySectorOf` (off-map ports report "lies outside the map"), object kinds parsed with `HotPathCodes.TryParseWorldObject` and `BlocksMovement` cross-checked against `HotPathCodes.BlocksMovement`, radii bounded by `SpatialRules.MaximumWorldInfluenceRadius`/`MaximumCurrentRadius`, headings within [0, 360]; terrain-independent checks run even when a terrain row is invalid.
- `Domain/SectorRules.cs`: `TrySectorOf(map, x, y, out sector)` is the total primitive; `Contains`/`SectorOf` wrap it (documented: half-open where `WorldRules.IsInsideMap` is closed; `SectorOf` clamps). `SectorId(byte, SectorCoordinate)` is primary and the `int` overload uses `checked` casts. `TerrainAt` throws `InvalidOperationException` for unknown symbols.
- Tests: the four JSON-vs-catalog tests and the `Content/Data/*.json` `<None>` item group in `Sea.Server.Tests.csproj` were deleted (`pnpm quality:content` is the authoritative drift check). `ContentCatalogTests` and `SectorRulesTests` cache `ContentCatalog.CreateDefault()` statically and pin one regression test per closed hole (null content, NaN, duplicate code behind duplicate id, zero desired range, id/code mismatch, unknown kind, blocksMovement disagreement, oversized radius, off-map port, `SectorId` overflow, out-of-range heading).

Deferred, contract-affecting (for the architecture pass after Task 11): replace `WorldObjectContent.Kind` with a `WorldObjectCode` column and derive `BlocksMovement`; resolve `AmmoEffectCode` versus `AppliedStatusCode` before 1b reads either; consider a `ContentError(Subject, Message)` record instead of raw strings; `SectorOf` has no production caller and may be deleted after Task 7.


### Task 4: Ship stat rules (add then cap, Combat Power budget)

**Files:**
- Create: `server/spacetimedb/spacetimedb/Domain/ShipStatRules.cs`
- Create: `server/spacetimedb/tests/ShipStatRulesTests.cs`
- Create: `server/spacetimedb/tests/ShipStatArbitraries.cs`

Design (Math section 5 and 6): bonuses from every source are summed, then each stat is clamped to its cap. Combat Power is `100 x damage + 100 x reload + slot bonus percent + 100 x hit points + 1.4 x armor points` and must not exceed the budget (45). If it does, sources are dropped from the end of the fixed order HullVariant, Plates, Sails, Crew, Skills, Buffs until it fits; dropped sources count as inactive. Damage, reload, and hit points use integer basis points so the result is identical on every platform.

- [ ] **Step 1: Write the failing unit tests**

Create `server/spacetimedb/tests/ShipStatRulesTests.cs`:

```csharp
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class ShipStatRulesTests
{
    private static readonly GameContent Content = ContentCatalog.CreateDefault();

    private static ShipLoadout Tier1Loadout() => new(
        Content.Hulls[0],
        Content.Cannons[0],
        Content.Hulls[0].CannonSlots,
        AmmoDamageMultiplier: 1f,
        AmmoReloadMultiplier: 1f);

    private static BonusSource Source(BonusSourceKind kind, StatBonuses bonuses) => new(kind, bonuses);

    [Fact]
    public void Tier_1_baseline_matches_the_design_sheet()
    {
        var sheet = ShipStatRules.Compute(Tier1Loadout(), Array.Empty<BonusSource>(), Content.StatCaps);

        Assert.Equal(160u, sheet.VolleyDamage);
        Assert.Equal(3000u, sheet.ReloadMilliseconds);
        Assert.Equal((byte)3, sheet.Magazine);
        Assert.Equal(1600u, sheet.MaxHitPoints);
        Assert.Equal(0.15f, sheet.ArmorFront, 3);
        Assert.Equal(0.08f, sheet.ArmorSides, 3);
        Assert.Equal(0.03f, sheet.ArmorBack, 3);
        Assert.Equal(2.4f, sheet.SpeedSquaresPerSecond, 3);
        Assert.Equal(60f, sheet.TurnDegreesPerSecond, 3);
        Assert.Equal((byte)8, sheet.RangeSquares);
        Assert.Equal(0.2f, sheet.RepairAmount, 3);
        Assert.Equal(3000u, sheet.RepairChannelMilliseconds);
        Assert.Equal(0f, sheet.CombatPowerUsed);
        Assert.Equal(0f, sheet.CombatPowerInactive);
        Assert.Equal(1f, sheet.FightScore, 3);
    }

    [Fact]
    public void Bonuses_are_added_then_capped()
    {
        var sources = new[]
        {
            Source(BonusSourceKind.Plates, StatBonuses.None with { Damage = 0.15f }),
            Source(BonusSourceKind.Skills, StatBonuses.None with { Damage = 0.20f }),
        };

        var sheet = ShipStatRules.Compute(Tier1Loadout(), sources, Content.StatCaps);

        Assert.Equal(200u, sheet.VolleyDamage);
        Assert.Equal(25f, sheet.CombatPowerUsed);
        Assert.Equal(0f, sheet.CombatPowerInactive);
    }

    [Fact]
    public void Reload_never_drops_below_the_floor()
    {
        var loadout = Tier1Loadout() with { Cannon = Content.Cannons[0] with { ReloadSeconds = 1.6f } };
        var sources = new[] { Source(BonusSourceKind.Sails, StatBonuses.None with { Reload = 0.20f }) };

        var sheet = ShipStatRules.Compute(loadout, sources, Content.StatCaps);

        Assert.Equal(1500u, sheet.ReloadMilliseconds);
    }

    [Fact]
    public void Ammo_multipliers_scale_volley_and_reload()
    {
        var loadout = Tier1Loadout() with { AmmoDamageMultiplier = 0.7f, AmmoReloadMultiplier = 1.1f };

        var sheet = ShipStatRules.Compute(loadout, Array.Empty<BonusSource>(), Content.StatCaps);

        Assert.Equal(112u, sheet.VolleyDamage);
        Assert.Equal(3300u, sheet.ReloadMilliseconds);
    }

    [Fact]
    public void Over_budget_sources_are_dropped_from_the_end_of_the_order()
    {
        var plates = Source(BonusSourceKind.Plates, StatBonuses.None with { HitPoints = 0.25f });
        var hullVariant = Source(BonusSourceKind.HullVariant, StatBonuses.None with { Damage = 0.25f });

        var forward = ShipStatRules.Compute(Tier1Loadout(), new[] { hullVariant, plates }, Content.StatCaps);
        var reversed = ShipStatRules.Compute(Tier1Loadout(), new[] { plates, hullVariant }, Content.StatCaps);

        Assert.Equal(forward, reversed);
        Assert.Equal(25f, forward.CombatPowerUsed);
        Assert.Equal(25f, forward.CombatPowerInactive);
        Assert.Equal(200u, forward.VolleyDamage);
        Assert.Equal(1600u, forward.MaxHitPoints);
    }

    [Fact]
    public void Armor_points_respect_the_absolute_maximum()
    {
        var loadout = Tier1Loadout() with { Hull = Content.Hulls[0] with { ArmorFront = 0.40f } };
        var sources = new[] { Source(BonusSourceKind.Plates, StatBonuses.None with { ArmorPoints = 15f }) };

        var sheet = ShipStatRules.Compute(loadout, sources, Content.StatCaps);

        Assert.Equal(0.45f, sheet.ArmorFront, 3);
        Assert.Equal(0.23f, sheet.ArmorSides, 3);
        Assert.Equal(21f, sheet.CombatPowerUsed);
    }

    [Fact]
    public void Extra_cannon_slots_cost_their_share_of_the_hull()
    {
        var sources = new[] { Source(BonusSourceKind.Crew, StatBonuses.None with { ExtraCannonSlots = 2 }) };

        var sheet = ShipStatRules.Compute(Tier1Loadout(), sources, Content.StatCaps);

        Assert.Equal(25f, sheet.CombatPowerUsed);
    }

    [Fact]
    public void Negative_bonuses_are_ignored()
    {
        var sources = new[] { Source(BonusSourceKind.Buffs, StatBonuses.None with { Damage = -0.5f, Magazine = -2 }) };

        var sheet = ShipStatRules.Compute(Tier1Loadout(), sources, Content.StatCaps);

        Assert.Equal(160u, sheet.VolleyDamage);
        Assert.Equal((byte)3, sheet.Magazine);
        Assert.Equal(0f, sheet.CombatPowerUsed);
    }
}
```

- [ ] **Step 2: Write the failing property test**

Create `server/spacetimedb/tests/ShipStatArbitraries.cs`:

```csharp
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public readonly record struct GeneratedBonusSources(BonusSource[] Sources);

public static class ShipStatArbitraries
{
    private static Gen<float> Ratio(float min, float max) =>
        Gen.Choose(0, 1_000_000).Select(value => min + (max - min) * (value / 1_000_000f));

    private static Gen<StatBonuses> Bonuses() =>
        from damage in Ratio(-0.5f, 1.5f)
        from reload in Ratio(-0.5f, 1.5f)
        from magazine in Gen.Choose(-2, 6)
        from hitPoints in Ratio(-0.5f, 1.5f)
        from armor in Ratio(-10f, 40f)
        from speed in Ratio(-0.5f, 1.5f)
        from turn in Ratio(-0.5f, 1.5f)
        from range in Gen.Choose(-2, 6)
        from repairAmount in Ratio(-0.5f, 1.5f)
        from repairChannel in Ratio(-0.5f, 1.5f)
        from slots in Gen.Choose(-1, 4)
        select new StatBonuses(
            damage, reload, magazine, hitPoints, armor, speed, turn, range, repairAmount, repairChannel, slots);

    private static Gen<BonusSource> Source() =>
        from kind in Gen.Choose(0, 5)
        from bonuses in Bonuses()
        select new BonusSource((BonusSourceKind)kind, bonuses);

    public static Arbitrary<GeneratedBonusSources> Sources() =>
        Arb.From(Source().ListOf().Select(list => new GeneratedBonusSources(list.ToArray())));
}

public sealed class ShipStatPropertyTests
{
    private static readonly GameContent Content = ContentCatalog.CreateDefault();

    [Property(Arbitrary = new[] { typeof(ShipStatArbitraries) }, MaxTest = 300)]
    public void Add_then_cap_never_exceeds_any_cap(GeneratedBonusSources generated)
    {
        var caps = Content.StatCaps;
        var hull = Content.Hulls[0];
        var cannon = Content.Cannons[0];
        var loadout = new ShipLoadout(hull, cannon, hull.CannonSlots, 1f, 1f);

        var sheet = ShipStatRules.Compute(loadout, generated.Sources, caps);

        Assert.InRange(sheet.VolleyDamage, 160u, 200u);
        Assert.InRange(sheet.ReloadMilliseconds, 2400u, 3000u);
        Assert.InRange(sheet.Magazine, (byte)3, (byte)5);
        Assert.InRange(sheet.MaxHitPoints, 1600u, 2000u);
        Assert.InRange(sheet.ArmorFront, 0.15f, 0.30f + 1e-4f);
        Assert.InRange(sheet.ArmorSides, 0.08f, 0.23f + 1e-4f);
        Assert.InRange(sheet.ArmorBack, 0.03f, 0.18f + 1e-4f);
        Assert.InRange(sheet.SpeedSquaresPerSecond, 2.4f, 3f + 1e-4f);
        Assert.InRange(sheet.TurnDegreesPerSecond, 60f, 75f + 1e-3f);
        Assert.InRange(sheet.RangeSquares, (byte)8, (byte)10);
        Assert.InRange(sheet.RepairAmount, 0.2f, 0.3f + 1e-4f);
        Assert.InRange(sheet.RepairChannelMilliseconds, 1500u, 3000u);
        Assert.InRange(sheet.CombatPowerUsed, 0f, caps.CombatPowerBudget);
        Assert.True(sheet.FightScore >= 1f - 1e-4f);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~ShipStat"`
Expected: build errors `The type or namespace name 'ShipStatRules' could not be found`, `'StatBonuses'`, `'BonusSource'`.

- [ ] **Step 4: Implement `ShipStatRules.cs`**

Create `server/spacetimedb/spacetimedb/Domain/ShipStatRules.cs`:

```csharp
namespace Sea.Server;

public enum BonusSourceKind : byte
{
    HullVariant = 0,
    Plates = 1,
    Sails = 2,
    Crew = 3,
    Skills = 4,
    Buffs = 5,
}

public readonly record struct StatBonuses(
    float Damage,
    float Reload,
    int Magazine,
    float HitPoints,
    float ArmorPoints,
    float Speed,
    float Turn,
    int RangeSquares,
    float RepairAmount,
    float RepairChannel,
    int ExtraCannonSlots)
{
    public static readonly StatBonuses None = default;

    public StatBonuses Add(StatBonuses other) => new(
        Damage + other.Damage,
        Reload + other.Reload,
        Magazine + other.Magazine,
        HitPoints + other.HitPoints,
        ArmorPoints + other.ArmorPoints,
        Speed + other.Speed,
        Turn + other.Turn,
        RangeSquares + other.RangeSquares,
        RepairAmount + other.RepairAmount,
        RepairChannel + other.RepairChannel,
        ExtraCannonSlots + other.ExtraCannonSlots);
}

public readonly record struct BonusSource(BonusSourceKind Kind, StatBonuses Bonuses);

public sealed record ShipLoadout(
    HullContent Hull,
    CannonContent Cannon,
    int CannonCount,
    float AmmoDamageMultiplier,
    float AmmoReloadMultiplier);

public readonly record struct ShipStatSheet(
    uint VolleyDamage,
    uint ReloadMilliseconds,
    byte Magazine,
    uint MaxHitPoints,
    float ArmorFront,
    float ArmorSides,
    float ArmorBack,
    float SpeedSquaresPerSecond,
    float TurnDegreesPerSecond,
    byte RangeSquares,
    float RepairAmount,
    uint RepairChannelMilliseconds,
    float CombatPowerUsed,
    float CombatPowerInactive,
    float FightScore);

public static class ShipStatRules
{
    private const long Scale = 10_000;

    public static ShipStatSheet Compute(ShipLoadout loadout, IReadOnlyList<BonusSource> sources, StatCapsContent caps)
    {
        var ordered = sources.OrderBy(source => (byte)source.Kind).ToList();
        var budgetCentis = Centis(caps.CombatPowerBudget);
        var inactiveCentis = 0L;
        var active = Cap(Sum(ordered), caps);

        while (ordered.Count > 0 && CombatPowerCentis(active, loadout.Hull, caps) > budgetCentis)
        {
            var dropped = ordered[^1];
            ordered.RemoveAt(ordered.Count - 1);
            inactiveCentis += CombatPowerCentis(Cap(dropped.Bonuses, caps), loadout.Hull, caps);
            active = Cap(Sum(ordered), caps);
        }

        var baseline = Sheet(loadout, StatBonuses.None, caps, 0, 0);
        var sheet = Sheet(loadout, active, caps, CombatPowerCentis(active, loadout.Hull, caps), inactiveCentis);
        return sheet with { FightScore = FightScore(sheet, baseline) };
    }

    public static StatBonuses Cap(StatBonuses total, StatCapsContent caps) => new(
        Math.Clamp(total.Damage, 0f, caps.DamageBonusCap),
        Math.Clamp(total.Reload, 0f, caps.ReloadBonusCap),
        Math.Clamp(total.Magazine, 0, caps.MagazineBonusCap),
        Math.Clamp(total.HitPoints, 0f, caps.HitPointBonusCap),
        Math.Clamp(total.ArmorPoints, 0f, caps.ArmorPointsCap),
        Math.Clamp(total.Speed, 0f, caps.SpeedBonusCap),
        Math.Clamp(total.Turn, 0f, caps.TurnBonusCap),
        Math.Clamp(total.RangeSquares, 0, caps.RangeBonusCapSquares),
        Math.Clamp(total.RepairAmount, 0f, caps.RepairAmountBonusCap),
        Math.Clamp(total.RepairChannel, 0f, caps.RepairChannelBonusCap),
        Math.Max(total.ExtraCannonSlots, 0));

    public static long CombatPowerCentis(StatBonuses capped, HullContent hull, StatCapsContent caps) =>
        BasisPoints(capped.Damage)
        + BasisPoints(capped.Reload)
        + BasisPoints(capped.HitPoints)
        + Round(Scale * (double)capped.ExtraCannonSlots / hull.CannonSlots)
        + Round(caps.CombatPowerArmorWeight * (double)capped.ArmorPoints * 100.0);

    public static float ArmorFace(float baseFace, float armorPoints, StatCapsContent caps) =>
        Math.Min(caps.ArmorAbsoluteMax, baseFace + armorPoints / 100f);

    public static float EffectiveHitPoints(uint maxHitPoints, float armor) => maxHitPoints / (1f - armor);

    public static float SustainedDps(ShipStatSheet sheet) => sheet.VolleyDamage * 1000f / sheet.ReloadMilliseconds;

    public static float FightScore(ShipStatSheet sheet, ShipStatSheet baseline) =>
        SustainedDps(sheet) * EffectiveHitPoints(sheet.MaxHitPoints, sheet.ArmorSides)
        / (SustainedDps(baseline) * EffectiveHitPoints(baseline.MaxHitPoints, baseline.ArmorSides));

    public static long BasisPoints(float value) => Round(value * (double)Scale);

    public static long Milliseconds(float seconds) => Round(seconds * 1000.0);

    private static long Centis(float value) => Round(value * 100.0);

    private static long Round(double value) => (long)Math.Round(value, MidpointRounding.AwayFromZero);

    private static StatBonuses Sum(List<BonusSource> sources)
    {
        var total = StatBonuses.None;
        foreach (var source in sources)
        {
            total = total.Add(source.Bonuses);
        }

        return total;
    }

    private static ShipStatSheet Sheet(
        ShipLoadout loadout,
        StatBonuses bonuses,
        StatCapsContent caps,
        long usedCentis,
        long inactiveCentis)
    {
        var hull = loadout.Hull;
        var cannon = loadout.Cannon;

        var volley = (long)loadout.CannonCount * cannon.Damage
            * BasisPoints(loadout.AmmoDamageMultiplier)
            * (Scale + BasisPoints(bonuses.Damage))
            / (Scale * Scale);
        var reload = Math.Max(
            Milliseconds(caps.ReloadFloorSeconds),
            Milliseconds(cannon.ReloadSeconds)
                * BasisPoints(loadout.AmmoReloadMultiplier)
                * (Scale - BasisPoints(bonuses.Reload))
                / (Scale * Scale));
        var maxHitPoints = hull.HitPoints * (Scale + BasisPoints(bonuses.HitPoints)) / Scale;
        var channel = Milliseconds(caps.RepairChannelSeconds) * (Scale - BasisPoints(bonuses.RepairChannel)) / Scale;

        return new ShipStatSheet(
            checked((uint)volley),
            checked((uint)reload),
            (byte)Math.Clamp(hull.Magazine + bonuses.Magazine, 1, byte.MaxValue),
            checked((uint)maxHitPoints),
            ArmorFace(hull.ArmorFront, bonuses.ArmorPoints, caps),
            ArmorFace(hull.ArmorSides, bonuses.ArmorPoints, caps),
            ArmorFace(hull.ArmorBack, bonuses.ArmorPoints, caps),
            hull.SpeedSquaresPerSecond * (1f + bonuses.Speed),
            hull.TurnDegreesPerSecond * (1f + bonuses.Turn),
            (byte)Math.Clamp(cannon.RangeSquares + bonuses.RangeSquares, 1, byte.MaxValue),
            caps.RepairBaseAmount * (1f + bonuses.RepairAmount),
            checked((uint)channel),
            usedCentis / 100f,
            inactiveCentis / 100f,
            1f);
    }
}
```

- [ ] **Step 5: Run the tests**

Run: `./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~ShipStat"`
Expected: 9 passing (8 facts plus the property).

If `Over_budget_sources_are_dropped_from_the_end_of_the_order` fails on `Assert.Equal(forward, reversed)`: the sort must be stable and by `Kind` only. `Enumerable.OrderBy` is stable; do not replace it with `List.Sort`.

- [ ] **Step 6: Commit**

```bash
git -C /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg add server/spacetimedb/spacetimedb/Domain/ShipStatRules.cs server/spacetimedb/tests/ShipStatRulesTests.cs server/spacetimedb/tests/ShipStatArbitraries.cs
git -C /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg commit -m "wip(content): add ship stat rules with caps and combat power budget

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01VVAL5L8X2uHhKGrESphkhF"
```

**Review amendments (applied during execution; the branch is authoritative over the code blocks above).** The code-quality and thermo-nuclear reviews found defects in the task text itself, fixed in `wip(content): harden ship stat rules`:

- `BonusSource` is `(BonusSourceKind Kind, ulong SourceId, StatBonuses Bonuses)`; `Compute` orders by kind then `SourceId`, so same-kind sources (three Plates) replay identically regardless of row order.
- Sources only add: each source is floored at zero per field before summing, and non-finite values count as zero. Debuffs are statuses, not bonus sources. Milestone 2 hull-variant penalties (Merchant slots −2, HP ×0.90) will be base-stat modifiers on the hull definition, not bonus sources.
- `ExtraCannonSlots` now applies (volley uses `CannonCount + ExtraCannonSlots`) and is capped by the eleventh cap `cannonSlotBonusCap: 3` in `stat_caps.json`, `StatCapsContent.CannonSlotBonusCap` (byte), the generator spec, validation, and the regenerated catalog. Task 7's `StatCaps` table and both cap mappings carry the new column (patched above).
- `CombatPowerInactive` is the capped-total power minus `CombatPowerUsed`, floored at zero, so used plus inactive equals the power of the fully capped sum.
- `ShipLoadout` validates in `init` accessors (`Hull` non-null with `CannonSlots >= 1`, `CannonCount >= 1`, finite positive ratios) with `ArgumentOutOfRangeException`, so both `new` and `with` are checked; `Compute` null-checks its inputs.
- Public `ShipStatRules` surface is `Compute`, `SustainedDps`, `EffectiveHitPoints`; everything else is private. `StatBonuses.None` is a property (analyzer CA1805).
- Structure: prefix-scan drop loop, single sheet construction from a private `DerivedStats`, `BonusScale`/`PowerScale` constants with `PowerFromRatio` so `CombatPowerCentis` reads like Math §2.3, `ScaleUp`/`ScaleDown` helpers, positional named-argument construction in `Add`/`NonNegative`/`Cap` (transposition-safe and exhaustive), `Cap` as `Math.Min` because `NonNegative` owns the lower bound, `checked` integer sums in `Add` and `checked` scaling products so absurd inputs throw instead of wrapping, and a null guard on `Cannon` matching the other loadout members.
- Tests assert against `caps` rather than literals and add order-invariance, monotonicity, finiteness, and used-plus-inactive properties with cap-straddling arbitraries and distinct `SourceId`s.

---

### Task 5: Math section 12 balance tests

**Files:**
- Create: `server/spacetimedb/tests/BalanceTests.cs`

These tests read the content tables through `ContentCatalog.CreateDefault()` and the rules from Task 4. They pin the design numbers: base versus base TTK 32 to 38 s, TTK with two repairs 42 to 50 s, FightScore at most 1.60 across every legal Combat Power spend, no ammo out-DPSes Round Shot by more than 20 percent, and a Common NPC dies in at most 20 s while a base ship survives it for 60 s.

- [ ] **Step 1: Write the tests**

Create `server/spacetimedb/tests/BalanceTests.cs`:

```csharp
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class BalanceTests
{
    private static readonly GameContent Content = ContentCatalog.CreateDefault();

    private static ShipLoadout BaseLoadout(AmmunitionContent? ammo = null)
    {
        var hull = Content.Hulls[0];
        var cannon = Content.Cannons[0];
        return new ShipLoadout(
            hull,
            cannon,
            hull.CannonSlots,
            ammo?.DamageMultiplier ?? 1f,
            ammo?.ReloadMultiplier ?? 1f);
    }

    private static ShipStatSheet BaseSheet() =>
        ShipStatRules.Compute(BaseLoadout(), Array.Empty<BonusSource>(), Content.StatCaps);

    [Fact]
    public void Section_12_1_base_versus_base_lasts_32_to_38_seconds()
    {
        var sheet = BaseSheet();
        var dps = ShipStatRules.SustainedDps(sheet);
        var effectiveHitPoints = ShipStatRules.EffectiveHitPoints(sheet.MaxHitPoints, sheet.ArmorSides);
        var timeToKill = effectiveHitPoints / dps;

        Assert.InRange(timeToKill, 32f, 38f);
    }

    [Fact]
    public void Section_12_1_two_repairs_extend_the_fight_to_42_to_50_seconds()
    {
        var caps = Content.StatCaps;
        var sheet = BaseSheet();
        var dps = ShipStatRules.SustainedDps(sheet);
        var effectiveHitPoints = ShipStatRules.EffectiveHitPoints(sheet.MaxHitPoints, sheet.ArmorSides);
        var healedFraction = caps.RepairBaseAmount * (1f + caps.RepairFatigue);
        var healedEffective = healedFraction * sheet.MaxHitPoints / (1f - sheet.ArmorSides);

        var timeToKill = (effectiveHitPoints + healedEffective) / dps;

        Assert.InRange(timeToKill, 42f, 50f);
    }

    [Fact]
    public void Section_12_2_fight_score_never_exceeds_1_60_within_the_budget()
    {
        var caps = Content.StatCaps;
        var loadout = BaseLoadout();
        var maxScore = 0f;

        for (var damage = 0; damage <= 25; damage++)
        {
            for (var reload = 0; reload <= 20; reload++)
            {
                for (var hitPoints = 0; hitPoints <= 25; hitPoints++)
                {
                    for (var armor = 0; armor <= 15; armor++)
                    {
                        if (damage + reload + hitPoints + 1.4f * armor > 45f + 1e-3f)
                        {
                            continue;
                        }

                        var bonuses = new StatBonuses(
                            damage / 100f, reload / 100f, 0, hitPoints / 100f, armor, 0f, 0f, 0, 0f, 0f, 0);
                        var sheet = ShipStatRules.Compute(
                            loadout, new[] { new BonusSource(BonusSourceKind.Plates, 1, bonuses) }, caps);

                        Assert.Equal(0f, sheet.CombatPowerInactive);
                        Assert.True(sheet.FightScore <= 1.60f, $"d={damage} r={reload} h={hitPoints} a={armor} score={sheet.FightScore}");
                        maxScore = Math.Max(maxScore, sheet.FightScore);
                    }
                }
            }
        }

        Assert.InRange(maxScore, 1.5f, 1.60f);
    }

    [Fact]
    public void Section_12_4_no_ammo_beats_round_shot_by_more_than_20_percent()
    {
        var caps = Content.StatCaps;
        var round = Content.Ammunition.Single(ammo => ammo.Code == AmmunitionCode.Round);
        var roundSheet = ShipStatRules.Compute(BaseLoadout(round), Array.Empty<BonusSource>(), caps);
        var roundDps = ShipStatRules.SustainedDps(roundSheet);

        foreach (var ammo in Content.Ammunition)
        {
            var sheet = ShipStatRules.Compute(BaseLoadout(ammo), Array.Empty<BonusSource>(), caps);
            var effectDamage = ammo.Effect == AmmoEffectCode.Burn
                ? ammo.EffectMagnitude * roundSheet.MaxHitPoints * ammo.EffectDurationSeconds
                : 0f;
            var sustained = (sheet.VolleyDamage + effectDamage) * 1000f / sheet.ReloadMilliseconds;

            Assert.True(sustained <= 1.2f * roundDps, $"{ammo.Id}: {sustained} > {1.2f * roundDps}");
        }
    }

    [Fact]
    public void Section_12_5_a_common_npc_dies_in_20_seconds_and_cannot_kill_a_repairing_base_ship_in_60()
    {
        var caps = Content.StatCaps;
        var player = BaseSheet();
        var playerDps = ShipStatRules.SustainedDps(player);
        var playerEffective = ShipStatRules.EffectiveHitPoints(player.MaxHitPoints, player.ArmorSides);

        var npcHitPoints = caps.NpcHitPointMultipliers[0] * playerEffective;
        var npcArmor = caps.NpcArmorByTier[0];
        var npcDps = caps.NpcDpsMultipliers[0] * playerDps;

        var timeToKillNpc = npcHitPoints / (playerDps * (1f - npcArmor));
        Assert.InRange(timeToKillNpc, 10f, 20f);

        var incomingOverMinute = npcDps * 60f;
        var repairBudget = caps.RepairBaseAmount * (1f + caps.RepairFatigue) * player.MaxHitPoints;
        Assert.True(incomingOverMinute < playerEffective + repairBudget);
    }
}
```

- [ ] **Step 2: Run the tests**

Run: `./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~BalanceTests"`
Expected: 5 passing. The exhaustive 12.2 loop runs about 130,000 combinations and should finish in under 10 seconds.

If `Section_12_2` fails with a score above 1.60, the content numbers are wrong, not the test. Check `stat_caps.json` against Math section 5 (caps 0.25 / 0.20 / 0.25 / 15 points, budget 45, armor weight 1.4) and `hulls.json` sides armor 0.08.

- [ ] **Step 3: Commit**

```bash
git -C /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg add server/spacetimedb/tests/BalanceTests.cs
git -C /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg commit -m "wip(content): pin Math section 12 balance numbers as tests

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01VVAL5L8X2uHhKGrESphkhF"
```

**Review amendments (applied during execution; the branch is authoritative over the code blocks above).** The code-quality and thermo-nuclear reviews found two defects in the task text, fixed in `wip(content): tighten Math section 12 balance tests`: (1) §12.5 no longer divides the Common kill time by `(1 − npcArmor)`; Math §7.1 defines Common HP as `0.50 × P_EHP`, already in effective-HP units, and the doc's own 16.3 s (T1) and 18.9 s (T5) figures carry no armor factor, so the band is `16–20 s` and the armor divisor would have failed spec-conformant T3+ content. Whether combat applies `NpcArmorByTier` at hit time is a 1b design question. (2) §12.5's repair budget is the 60-second on-cooldown ceiling (Math §6.3, four repairs, `0.20 × (1 + 0.6 + 0.36 + 0.216) = 0.4352`), derived from caps by `HealedFraction(caps, repairs)` and `RepairsWithin(caps, 60f)`; §12.1 uses `HealedFraction(caps, 2) = 0.32`. Also: the §12.2 sweep takes its bounds and budget predicate from `StatCaps` in integer centis (no literal 25/20/25/15/1.4/45, no epsilon) and its floor is `1.575` (integer-scaled maximum 1.58125 at damage 10 / reload 20 / HP 15 / armor 0); §12.4 composes `ShipStatRules.SustainedDps` plus the burn term through `SustainedDpsWithEffect` and pins `BurnPerSecond`/`BurnDurationSeconds` against the ammo row; Round Shot is the required baseline (no nullable ammo parameter); a shared fixture `server/spacetimedb/tests/Tier1.cs` (`Content`, `Caps`, `Hull`, `Cannon`, `Round`, `Loadout(ammo)`, `Loadout()`, `Sheet()`) replaces the private copies in `ShipStatRulesTests.cs`, `ShipStatArbitraries.cs` and `BalanceTests.cs`; and `ShipStatRules` gained an additive `EffectiveHitPoints(ShipStatSheet)` overload. Test names and the public surface are unchanged.

---

### Task 6: Replay hash pin for a no-command run

**Files:**
- Modify: `server/spacetimedb/tests/ReplayRulesTests.cs`

- [ ] **Step 1: Add the test**

Add this test inside the existing `ReplayRulesTests` class (it already declares `Parameters = new(12f, 3f, 4f, 360f)`):

```csharp
    [Fact]
    public void No_command_run_keeps_the_initial_state_and_a_stable_hash()
    {
        var initial = new SailingState(0f, 0f, 0f, 0f);

        var result = ReplayRules.Run(100, initial, Array.Empty<ReplayCommand>(), Parameters, 0.1f);

        Assert.Equal(initial, result.State);
        Assert.Equal(9594698449054650917UL, result.StateHash);
    }
```


- [ ] **Step 2: Run it**

Run: `./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~ReplayRulesTests"`
Expected: all replay tests pass, including the new one. The hash value was recorded from the current `master` before this milestone; if it differs, stop and report it, because that means the sailing replay changed, which this milestone must not do.

- [ ] **Step 3: Commit**

```bash
git -C /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg add server/spacetimedb/tests/ReplayRulesTests.cs
git -C /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg commit -m "wip(content): pin the no-command replay hash

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01VVAL5L8X2uHhKGrESphkhF"
```

**Review amendments (applied during execution; the branch is authoritative over the code blocks above).** The quality review showed the no-command run from the zero state is a fixed point of the integrator, so its hash stays unchanged under wrong physics. A second fact, `Recorded_command_log_replays_to_a_pinned_hash`, pins the file's existing `Commands()` log to `3073545830116257169UL` (100 ticks, final state about (-16.06, 63.41, 328.87, 4.0)), covering turn, acceleration, re-course and stop. The no-command pin carries a provenance comment. `Array.Empty` became `[]` with no `using System;`, matching the file. Follow-up recorded for hardening: `ReplayRules` has no production caller, so these pins guard `SailingRules` math but not the `SailingSystem` tick wiring.

---

### Task 7: Content tables, dock tables, and data-driven seeding

**Files:**
- Create: `server/spacetimedb/spacetimedb/Schema/ContentTables.cs`
- Create: `server/spacetimedb/spacetimedb/Schema/DockTables.cs`
- Create: `server/spacetimedb/spacetimedb/Content/ContentRows.cs`
- Create: `server/spacetimedb/spacetimedb/Content/WorldSeed.cs`
- Rewrite: `server/spacetimedb/spacetimedb/Content/ContentSeed.cs`
- Modify: `server/spacetimedb/spacetimedb/Schema/Tables.cs:119-127` (PlayerProgression) and `:388-439` (delete the four definition tables)
- Modify: `server/spacetimedb/spacetimedb/Reducers/LifecycleReducers.cs:20` (ContentVersion)
- Modify: `server/spacetimedb/spacetimedb/Reducers/CombatReducers.cs:74-86`
- Modify (rename only): `Commands/CommandSnapshots.cs`, `Reducers/CombatReducers.cs`, `Simulation/DamageSystem.cs`, `Simulation/LootSystem.cs`, `Simulation/NpcSystem.cs`, `Simulation/RespawnSystem.cs` (all under `server/spacetimedb/spacetimedb/`)

The module still does not compile at the end of this task: `ProgressionSystem.cs`, `SimulationTick.cs`, `ChannelReducers.cs` and `LifecycleReducers.cs` still use `Level`, `Experience` and `LevelDefinition`. Task 8 fixes those, and `pnpm server:build` is run at the end of Task 8. Do not try to build the module in this task.

- [ ] **Step 1: Create the content definition tables**

Create `server/spacetimedb/spacetimedb/Schema/ContentTables.cs`:

```csharp
using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Table(Accessor = "MapDef", Public = true)]
    public partial struct MapDef
    {
        [PrimaryKey]
        public byte MapId;
        [Unique]
        public string Code;
        public string Name;
        public string Biome;
        public byte MapRank;
        public byte Width;
        public byte Height;
        public string PvpMode;
        public string MaterialId;
        public string PortName;
        public float PortX;
        public float PortY;
        public float PortRadius;
    }

    [SpacetimeDB.Table(Accessor = "Sector", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByMap", Columns = new[] { nameof(MapId) })]
    public partial struct Sector
    {
        [PrimaryKey]
        public ulong SectorId;
        public byte MapId;
        public byte X;
        public byte Y;
        public byte TerrainCode;
    }

    [SpacetimeDB.Table(Accessor = "HullDef", Public = true)]
    public partial struct HullDef
    {
        [PrimaryKey]
        public string HullDefId;
        public string Name;
        public byte Tier;
        public uint HitPoints;
        public float ArmorFront;
        public float ArmorSides;
        public float ArmorBack;
        public byte CannonSlots;
        public float SpeedSquaresPerSecond;
        public float TurnDegreesPerSecond;
        public byte Magazine;
        public uint CostGold;
        public byte MapRankRequired;
    }

    [SpacetimeDB.Table(Accessor = "CannonDef", Public = true)]
    public partial struct CannonDef
    {
        [PrimaryKey]
        public string CannonDefId;
        public string Name;
        public byte Tier;
        public uint Damage;
        public float ReloadSeconds;
        public byte RangeSquares;
        public uint CostGold;
    }

    [SpacetimeDB.Table(Accessor = "AmmoDef", Public = true)]
    public partial struct AmmoDef
    {
        [PrimaryKey]
        public string AmmoId;
        [Unique]
        public byte AmmoCode;
        public string Name;
        public float DamageMultiplier;
        public float ReloadMultiplier;
        public uint GoldPerVolley;
        public byte EffectCode;
        public float EffectMagnitude;
        public float EffectDurationSeconds;
        public byte RangeLimitSquares;
        public uint HullDamage;
        public uint SailDamage;
        public uint CannonDamage;
        public uint CrewDamage;
        public float RangeMultiplier;
        public string AppliedStatus;
        public byte AppliedStatusCode;
    }

    [SpacetimeDB.Table(Accessor = "AbilityDefinition", Public = true)]
    public partial struct AbilityDefinition
    {
        [PrimaryKey]
        public string AbilityId;
        [Unique]
        public byte AbilityCode;
        public uint CooldownTicks;
        public uint DurationTicks;
    }

    [SpacetimeDB.Table(Accessor = "NpcDef", Public = true)]
    public partial struct NpcDef
    {
        [PrimaryKey]
        public string NpcId;
        [Unique]
        public byte ArchetypeCode;
        public string Name;
        public byte Tier;
        public byte MapId;
        public string Family;
        public string Behavior;
        public float AggroRange;
        public float DesiredRange;
        public float MaximumSpeed;
        public uint Hull;
        public uint CannonDamage;
        public byte PreferredAmmoCode;
        public byte PreferredWeakPointCode;
        public uint GoldReward;
        public ulong ExperienceReward;
    }

    [SpacetimeDB.Table(Accessor = "StatCaps", Public = true)]
    public partial struct StatCaps
    {
        [PrimaryKey]
        public byte Id;
        public float DamageBonusCap;
        public float ReloadBonusCap;
        public byte MagazineBonusCap;
        public float HitPointBonusCap;
        public float ArmorPointsCap;
        public float ArmorAbsoluteMax;
        public float SpeedBonusCap;
        public float TurnBonusCap;
        public byte RangeBonusCapSquares;
        public float RepairAmountBonusCap;
        public float RepairChannelBonusCap;
        public byte CannonSlotBonusCap;
        public float CombatPowerBudget;
        public float CombatPowerArmorWeight;
        public float ReloadFloorSeconds;
        public float FireMinIntervalSeconds;
        public float MagazineRefillIdleSeconds;
        public float BurnPerSecond;
        public float BurnDurationSeconds;
        public float BurnHealMultiplier;
        public float RepairBaseAmount;
        public float RepairChannelSeconds;
        public float RepairCooldownSeconds;
        public float RepairFatigue;
        public float RepairFatigueWindowSeconds;
        public float RepairCancelThreshold;
        public float KitHealAmount;
        public float KitCooldownSeconds;
        public float RespawnSeconds;
        public float SpawnShieldSeconds;
        public List<float> NpcHitPointMultipliers;
        public List<float> NpcDpsMultipliers;
        public List<float> NpcArmorByTier;
        public uint GoldBase;
        public float GoldGrowth;
    }
}
```

- [ ] **Step 2: Create the dock tables**

Create `server/spacetimedb/spacetimedb/Schema/DockTables.cs`:

```csharp
using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Table(Accessor = "Hull", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByOwner", Columns = new[] { nameof(Owner) })]
    public partial struct Hull
    {
        [PrimaryKey]
        [AutoInc]
        public ulong HullId;
        public Identity Owner;
        public string HullDefId;
        public string Name;
        public string CannonDefId;
        public byte CannonCount;
    }

    [SpacetimeDB.Table(Accessor = "ShipStats", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByOwner", Columns = new[] { nameof(Owner) })]
    public partial struct ShipStats
    {
        [PrimaryKey]
        public ulong HullId;
        public Identity Owner;
        public uint VolleyDamage;
        public uint ReloadMilliseconds;
        public byte Magazine;
        public uint MaxHitPoints;
        public float ArmorFront;
        public float ArmorSides;
        public float ArmorBack;
        public float SpeedSquaresPerSecond;
        public float TurnDegreesPerSecond;
        public byte RangeSquares;
        public float RepairAmount;
        public uint RepairChannelMilliseconds;
        public float CombatPowerUsed;
        public float CombatPowerInactive;
        public float FightScore;
    }

    [SpacetimeDB.Table(Accessor = "PlayerAccount")]
    public partial struct PlayerAccount
    {
        [PrimaryKey]
        public Identity Owner;
        public string AccountId;
    }

#pragma warning disable STDB_UNSTABLE
    [SpacetimeDB.ClientVisibilityFilter]
    public static readonly Filter HullOwnerFilter =
        new Filter.Sql("SELECT * FROM hull WHERE hull.owner = :sender");

    [SpacetimeDB.ClientVisibilityFilter]
    public static readonly Filter ShipStatsOwnerFilter =
        new Filter.Sql("SELECT * FROM ship_stats WHERE ship_stats.owner = :sender");
#pragma warning restore STDB_UNSTABLE
}
```

- [ ] **Step 3: Replace PlayerProgression and delete the old definition tables in Tables.cs**

In `server/spacetimedb/spacetimedb/Schema/Tables.cs` replace the `PlayerProgression` struct (lines 119–127) with:

```csharp
    [SpacetimeDB.Table(Accessor = "PlayerProgression", Public = true)]
    public partial struct PlayerProgression
    {
        [PrimaryKey]
        public Identity Owner;
        public byte MapRank;
        public uint Gold;
    }
```

Then delete the four structs `AmmoDefinition`, `AbilityDefinition`, `NpcDefinition` and `LevelDefinition` (lines 388–439, from the `[SpacetimeDB.Table(Accessor = "AmmoDefinition", Public = true)]` attribute through the closing brace of `LevelDefinition`). The file must end with the `CurrentZone` struct's closing brace, one blank line, and the class's closing `}`. Verify:

```bash
grep -n 'AmmoDefinition\|NpcDefinition\|LevelDefinition\|AbilityDefinition' server/spacetimedb/spacetimedb/Schema/Tables.cs
```

Expected: no output.

- [ ] **Step 4: Create the row-to-content mapping helpers**

Create `server/spacetimedb/spacetimedb/Content/ContentRows.cs`:

```csharp
using Sea.Server;

public static partial class Module
{
    private static AmmunitionContent AmmoContentFrom(AmmoDef row) => new()
    {
        Id = row.AmmoId,
        Code = (AmmunitionCode)row.AmmoCode,
        Name = row.Name,
        DamageMultiplier = row.DamageMultiplier,
        ReloadMultiplier = row.ReloadMultiplier,
        GoldPerVolley = row.GoldPerVolley,
        Effect = (AmmoEffectCode)row.EffectCode,
        EffectMagnitude = row.EffectMagnitude,
        EffectDurationSeconds = row.EffectDurationSeconds,
        RangeLimitSquares = row.RangeLimitSquares,
        HullDamage = row.HullDamage,
        SailDamage = row.SailDamage,
        CannonDamage = row.CannonDamage,
        CrewDamage = row.CrewDamage,
        RangeMultiplier = row.RangeMultiplier,
        AppliedStatus = row.AppliedStatus,
        AppliedStatusCode = (StatusCode)row.AppliedStatusCode,
    };

    private static HullContent HullContentFrom(HullDef row) => new()
    {
        Id = row.HullDefId,
        Name = row.Name,
        Tier = row.Tier,
        HitPoints = row.HitPoints,
        ArmorFront = row.ArmorFront,
        ArmorSides = row.ArmorSides,
        ArmorBack = row.ArmorBack,
        CannonSlots = row.CannonSlots,
        SpeedSquaresPerSecond = row.SpeedSquaresPerSecond,
        TurnDegreesPerSecond = row.TurnDegreesPerSecond,
        Magazine = row.Magazine,
        CostGold = row.CostGold,
        MapRankRequired = row.MapRankRequired,
    };

    private static CannonContent CannonContentFrom(CannonDef row) => new()
    {
        Id = row.CannonDefId,
        Name = row.Name,
        Tier = row.Tier,
        Damage = row.Damage,
        ReloadSeconds = row.ReloadSeconds,
        RangeSquares = row.RangeSquares,
        CostGold = row.CostGold,
    };

    private static StatCapsContent StatCapsFrom(StatCaps row) => new()
    {
        DamageBonusCap = row.DamageBonusCap,
        ReloadBonusCap = row.ReloadBonusCap,
        MagazineBonusCap = row.MagazineBonusCap,
        HitPointBonusCap = row.HitPointBonusCap,
        ArmorPointsCap = row.ArmorPointsCap,
        ArmorAbsoluteMax = row.ArmorAbsoluteMax,
        SpeedBonusCap = row.SpeedBonusCap,
        TurnBonusCap = row.TurnBonusCap,
        RangeBonusCapSquares = row.RangeBonusCapSquares,
        RepairAmountBonusCap = row.RepairAmountBonusCap,
        RepairChannelBonusCap = row.RepairChannelBonusCap,
        CannonSlotBonusCap = row.CannonSlotBonusCap,
        CombatPowerBudget = row.CombatPowerBudget,
        CombatPowerArmorWeight = row.CombatPowerArmorWeight,
        ReloadFloorSeconds = row.ReloadFloorSeconds,
        FireMinIntervalSeconds = row.FireMinIntervalSeconds,
        MagazineRefillIdleSeconds = row.MagazineRefillIdleSeconds,
        BurnPerSecond = row.BurnPerSecond,
        BurnDurationSeconds = row.BurnDurationSeconds,
        BurnHealMultiplier = row.BurnHealMultiplier,
        RepairBaseAmount = row.RepairBaseAmount,
        RepairChannelSeconds = row.RepairChannelSeconds,
        RepairCooldownSeconds = row.RepairCooldownSeconds,
        RepairFatigue = row.RepairFatigue,
        RepairFatigueWindowSeconds = row.RepairFatigueWindowSeconds,
        RepairCancelThreshold = row.RepairCancelThreshold,
        KitHealAmount = row.KitHealAmount,
        KitCooldownSeconds = row.KitCooldownSeconds,
        RespawnSeconds = row.RespawnSeconds,
        SpawnShieldSeconds = row.SpawnShieldSeconds,
        NpcHitPointMultipliers = row.NpcHitPointMultipliers,
        NpcDpsMultipliers = row.NpcDpsMultipliers,
        NpcArmorByTier = row.NpcArmorByTier,
        GoldBase = row.GoldBase,
        GoldGrowth = row.GoldGrowth,
    };
}
```

- [ ] **Step 5: Move the world seeding into WorldSeed.cs**

Create `server/spacetimedb/spacetimedb/Content/WorldSeed.cs` with this content. `SeedNpc`, `InsertCurrentZone` and `BuildCurrentFieldState` are moved verbatim from the current `ContentSeed.cs` (print them with `sed -n '102,133p;156,223p' server/spacetimedb/spacetimedb/Content/ContentSeed.cs` before rewriting that file). `SeedWorld` and `SeedEnvironment` are rewritten as shown:

```csharp
using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void SeedWorld(ReducerContext ctx)
    {
        var content = ContentCatalog.CreateDefault();
        var map = content.Maps[0];
        foreach (var item in map.Objects)
        {
            InsertWorldObject(
                ctx,
                item.EntityId,
                item.Kind,
                item.X,
                item.Y,
                item.Radius,
                item.BlocksMovement,
                item.DirectionDegrees,
                item.MovementSpeed,
                item.Intensity);
        }

        var entityId = 10ul;
        foreach (var definition in content.Npcs)
        {
            for (var index = 0; index < 4; index++)
            {
                SeedNpc(ctx, entityId, definition, index);
                entityId++;
            }
        }
    }

    // SeedNpc: paste verbatim from the old ContentSeed.cs (lines 102-133).

    private static void SeedEnvironment(ReducerContext ctx)
    {
        const ulong seed = 0x5EA2026;
        var wind = EnvironmentRules.WindForEpoch(seed, 0);
        ctx.Db.EnvironmentState.Insert(new EnvironmentState
        {
            Id = 1,
            Seed = seed,
            WindEpoch = 0,
            WindDirectionDegrees = wind.DirectionDegrees,
            WindStrength = wind.Strength,
            NextWindChangeTick = EnvironmentRules.WindEpochTicks,
        });

        var map = ContentCatalog.CreateDefault().Maps[0];
        var zones = new List<CurrentZone>(map.Currents.Count);
        foreach (var current in map.Currents)
        {
            zones.Add(InsertCurrentZone(
                ctx,
                current.ZoneId,
                current.X,
                current.Y,
                current.Radius,
                current.DirectionDegrees,
                current.Strength));
        }

        ctx.Db.CurrentFieldState.Insert(BuildCurrentFieldState(zones));
    }

    // InsertCurrentZone and BuildCurrentFieldState: paste verbatim from the old ContentSeed.cs (lines 156-223).
}
```

Replace the two `// ...paste verbatim...` comments with the moved method bodies. The `InsertCurrentZone` first parameter after `ctx` is a `ulong` zone id; `CurrentContent.ZoneId` is `ulong`, so no cast is needed. World object ids 1–9, 11, 12, 13 and NPC ids 10, 14–25 do not collide because `maps.json` uses exactly the ids the old hard-coded calls used.

- [ ] **Step 6: Rewrite ContentSeed.cs to insert the new definition rows**

Replace the whole of `server/spacetimedb/spacetimedb/Content/ContentSeed.cs` with:

```csharp
using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void SeedContent(ReducerContext ctx)
    {
        var content = ContentCatalog.CreateDefault();
        var errors = ContentCatalog.Validate(content);
        if (errors.Count != 0)
        {
            throw new InvalidOperationException(string.Join(" ", errors));
        }

        foreach (var map in content.Maps)
        {
            SeedMap(ctx, map);
        }

        foreach (var hull in content.Hulls)
        {
            ctx.Db.HullDef.Insert(new HullDef
            {
                HullDefId = hull.Id,
                Name = hull.Name,
                Tier = hull.Tier,
                HitPoints = hull.HitPoints,
                ArmorFront = hull.ArmorFront,
                ArmorSides = hull.ArmorSides,
                ArmorBack = hull.ArmorBack,
                CannonSlots = hull.CannonSlots,
                SpeedSquaresPerSecond = hull.SpeedSquaresPerSecond,
                TurnDegreesPerSecond = hull.TurnDegreesPerSecond,
                Magazine = hull.Magazine,
                CostGold = hull.CostGold,
                MapRankRequired = hull.MapRankRequired,
            });
        }

        foreach (var cannon in content.Cannons)
        {
            ctx.Db.CannonDef.Insert(new CannonDef
            {
                CannonDefId = cannon.Id,
                Name = cannon.Name,
                Tier = cannon.Tier,
                Damage = cannon.Damage,
                ReloadSeconds = cannon.ReloadSeconds,
                RangeSquares = cannon.RangeSquares,
                CostGold = cannon.CostGold,
            });
        }

        foreach (var ammunition in content.Ammunition)
        {
            ctx.Db.AmmoDef.Insert(new AmmoDef
            {
                AmmoId = ammunition.Id,
                AmmoCode = (byte)ammunition.Code,
                Name = ammunition.Name,
                DamageMultiplier = ammunition.DamageMultiplier,
                ReloadMultiplier = ammunition.ReloadMultiplier,
                GoldPerVolley = ammunition.GoldPerVolley,
                EffectCode = (byte)ammunition.Effect,
                EffectMagnitude = ammunition.EffectMagnitude,
                EffectDurationSeconds = ammunition.EffectDurationSeconds,
                RangeLimitSquares = ammunition.RangeLimitSquares,
                HullDamage = ammunition.HullDamage,
                SailDamage = ammunition.SailDamage,
                CannonDamage = ammunition.CannonDamage,
                CrewDamage = ammunition.CrewDamage,
                RangeMultiplier = ammunition.RangeMultiplier,
                AppliedStatus = ammunition.AppliedStatus,
                AppliedStatusCode = (byte)ammunition.AppliedStatusCode,
            });
        }

        foreach (var ability in content.Abilities)
        {
            ctx.Db.AbilityDefinition.Insert(new AbilityDefinition
            {
                AbilityId = ability.Id,
                AbilityCode = (byte)ability.Code,
                CooldownTicks = ability.CooldownTicks,
                DurationTicks = ability.DurationTicks,
            });
        }

        foreach (var npc in content.Npcs)
        {
            ctx.Db.NpcDef.Insert(new NpcDef
            {
                NpcId = npc.Id,
                ArchetypeCode = (byte)npc.Code,
                Name = npc.Name,
                Tier = npc.Tier,
                MapId = npc.MapId,
                Family = npc.Family,
                Behavior = npc.Behavior,
                AggroRange = npc.AggroRange,
                DesiredRange = npc.DesiredRange,
                MaximumSpeed = npc.MaximumSpeed,
                Hull = npc.Hull,
                CannonDamage = npc.CannonDamage,
                PreferredAmmoCode = (byte)npc.PreferredAmmunition,
                PreferredWeakPointCode = (byte)npc.PreferredWeakPoint,
                GoldReward = npc.GoldReward,
                ExperienceReward = npc.ExperienceReward,
            });
        }

        var caps = content.StatCaps;
        ctx.Db.StatCaps.Insert(new StatCaps
        {
            Id = 1,
            DamageBonusCap = caps.DamageBonusCap,
            ReloadBonusCap = caps.ReloadBonusCap,
            MagazineBonusCap = caps.MagazineBonusCap,
            HitPointBonusCap = caps.HitPointBonusCap,
            ArmorPointsCap = caps.ArmorPointsCap,
            ArmorAbsoluteMax = caps.ArmorAbsoluteMax,
            SpeedBonusCap = caps.SpeedBonusCap,
            TurnBonusCap = caps.TurnBonusCap,
            RangeBonusCapSquares = caps.RangeBonusCapSquares,
            RepairAmountBonusCap = caps.RepairAmountBonusCap,
            RepairChannelBonusCap = caps.RepairChannelBonusCap,
            CannonSlotBonusCap = caps.CannonSlotBonusCap,
            CombatPowerBudget = caps.CombatPowerBudget,
            CombatPowerArmorWeight = caps.CombatPowerArmorWeight,
            ReloadFloorSeconds = caps.ReloadFloorSeconds,
            FireMinIntervalSeconds = caps.FireMinIntervalSeconds,
            MagazineRefillIdleSeconds = caps.MagazineRefillIdleSeconds,
            BurnPerSecond = caps.BurnPerSecond,
            BurnDurationSeconds = caps.BurnDurationSeconds,
            BurnHealMultiplier = caps.BurnHealMultiplier,
            RepairBaseAmount = caps.RepairBaseAmount,
            RepairChannelSeconds = caps.RepairChannelSeconds,
            RepairCooldownSeconds = caps.RepairCooldownSeconds,
            RepairFatigue = caps.RepairFatigue,
            RepairFatigueWindowSeconds = caps.RepairFatigueWindowSeconds,
            RepairCancelThreshold = caps.RepairCancelThreshold,
            KitHealAmount = caps.KitHealAmount,
            KitCooldownSeconds = caps.KitCooldownSeconds,
            RespawnSeconds = caps.RespawnSeconds,
            SpawnShieldSeconds = caps.SpawnShieldSeconds,
            NpcHitPointMultipliers = caps.NpcHitPointMultipliers.ToList(),
            NpcDpsMultipliers = caps.NpcDpsMultipliers.ToList(),
            NpcArmorByTier = caps.NpcArmorByTier.ToList(),
            GoldBase = caps.GoldBase,
            GoldGrowth = caps.GoldGrowth,
        });
    }

    private static void SeedMap(ReducerContext ctx, MapContent map)
    {
        ctx.Db.MapDef.Insert(new MapDef
        {
            MapId = map.MapId,
            Code = map.Code,
            Name = map.Name,
            Biome = map.Biome,
            MapRank = map.MapRank,
            Width = map.Width,
            Height = map.Height,
            PvpMode = map.PvpMode,
            MaterialId = map.MaterialId,
            PortName = map.PortName,
            PortX = map.PortX,
            PortY = map.PortY,
            PortRadius = map.PortRadius,
        });

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                ctx.Db.Sector.Insert(new Sector
                {
                    SectorId = SectorRules.SectorId(map.MapId, x, y),
                    MapId = map.MapId,
                    X = (byte)x,
                    Y = (byte)y,
                    TerrainCode = (byte)SectorRules.TerrainAt(map, x, y),
                });
            }
        }
    }
}
```

- [ ] **Step 7: Rename AmmoDefinition and NpcDefinition callers**

```bash
cd /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg/server/spacetimedb/spacetimedb
sed -i '' 's/AmmoDefinition/AmmoDef/g' Commands/CommandSnapshots.cs Reducers/CombatReducers.cs Simulation/DamageSystem.cs
sed -i '' 's/NpcDefinition/NpcDef/g' Simulation/LootSystem.cs Simulation/NpcSystem.cs Simulation/RespawnSystem.cs
grep -rn 'AmmoDefinition\|NpcDefinition' . --include='*.cs' --exclude-dir=Generated --exclude-dir=obj --exclude-dir=bin
```

Expected: the final grep prints nothing.

- [ ] **Step 8: Replace the ammunition initializer in BroadsideDamage**

In `server/spacetimedb/spacetimedb/Reducers/CombatReducers.cs`, the `BroadsideDamage` helper currently builds an `AmmunitionContent` with a 9-field object initializer (lines 74–86). Replace that initializer with the mapping helper so the method reads:

```csharp
    private static CombatDamage BroadsideDamage(Ship ship, AmmoDef ammunition, WeakPoint weakPoint)
    {
        return CombatRules.DamageProfile(
            AmmoContentFrom(ammunition),
            weakPoint,
            ship.CannonDamage,
            ship.Cannons,
            ship.MaxCannons);
    }
```

Keep the original parameter list, return type and the four trailing arguments exactly as they are in the file (print lines 68–92 first and change only the `new AmmunitionContent { ... }` expression to `AmmoContentFrom(ammunition)`).

- [ ] **Step 9: Bump the content version**

In `server/spacetimedb/spacetimedb/Reducers/LifecycleReducers.cs` change `ContentVersion = 4,` to `ContentVersion = 5,`.

- [ ] **Step 10: Commit the work in progress**

```bash
cd /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg
git add server/spacetimedb/spacetimedb
git commit -m "wip(content): add content and dock tables with data-driven seeding

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01VVAL5L8X2uHhKGrESphkhF"
```

**Review amendments (applied during execution; the branch is authoritative over the code blocks above).** The thermo-nuclear and performance reviews rejected the shape above, fixed in `wip(content): read content from a static catalog`: (1) the module reads content from one static catalog, `Content/Catalog.cs` (`Catalog.Content = ContentCatalog.CreateDefault()` plus `Catalog.AmmunitionByCode` and `Catalog.NpcByArchetypeCode`, 256-slot arrays indexed by byte code and built once by the domain-tested `Domain/ContentIndex.cs`); the seeded content tables are the client projection only, so `SeedContent` returns `void`, `SeedWorld`/`SeedEnvironment` take only the context, and `SeedPlayerInventory`/`SeedNpcInventory` no longer rebuild the catalog per login. (2) Every hot-path content read goes through the catalog: `CommandSnapshots` parses the ammo id with `HotPathCodes.TryParseAmmunition` and probes the array, `CombatReducers`/`DamageSystem` index by `SelectedAmmoCode`/`Volley.AmmoCode`, `NpcSystem`/`LootSystem`/`RespawnSystem` index by `Ship.ArchetypeCode`, and `BroadsideDamage` takes `AmmunitionContent` directly; `Content/ContentRows.cs` and its row→content helpers do not exist. (3) Content→row mapping is a static `From(content)` factory on each table struct in `Schema/ContentTables.cs`, so `ContentSeed.cs` is one loop per family (67 lines). (4) The ability table is `AbilityDef`. (5) `PlayerProgression` has an owner visibility filter. (6) `SpawnRules.TryFindSafePosition` takes an `IReadOnlyList<SpawnBlocker>` and loops without a closure; `SeedWorld` builds the blocker list once and passes it to every NPC spawn (`NavigationState.FindSafeSpawn(blockers, seed)`), while player connect keeps the context overload. (7) Tests added: `ContentIndexTests` (self-lookup, empty slots, collisions), spawn-blocker and exhaustion cases in `SailingRulesTests`, duplicate map id/map code/NPC archetype code rejections in `ContentCatalogTests`, and `SectorRulesTests` proving every default-map cell has a distinct id and a defined terrain (500 tests). Task 9's `RecomputeStats` must read `Catalog.Content` (hull/cannon by id, baseline ammo via `Catalog.AmmunitionByCode[(byte)AmmunitionCode.Round]`, `Catalog.Content.StatCaps`), never content rows. A follow-up commit, `wip(content): index abilities by code and drop the npc archetype id column`, added `Catalog.AbilityByCode` (one generic `ContentIndex.ByCode` builder behind three wrappers) so the module reads no content row at all after `Init`, and deleted the write-only `NpcAi.ArchetypeId` column together with its admin column entries (`Ship.ArchetypeCode` is the NPC identity). Deferred: generating the table structs from the content specs, `StatCaps` list flattening, chunk-grid/map-size coupling, content reseed on republish, deriving `HotPathCodes` parsers from the catalog, and the duplicate clock/inventory fetches per broadside.

---

### Task 8: Replace level and experience with map rank and gold

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Domain/ProgressionRules.cs:1-50`
- Modify: `server/spacetimedb/spacetimedb/Simulation/ProgressionSystem.cs`
- Modify: `server/spacetimedb/spacetimedb/Simulation/EncounterSettlementSystem.cs:106`
- Modify: `server/spacetimedb/spacetimedb/Simulation/LootSystem.cs:74-80`
- Modify: `server/spacetimedb/spacetimedb/Simulation/SimulationTick.cs:282-300`
- Modify: `server/spacetimedb/spacetimedb/Reducers/LifecycleReducers.cs:105-111`
- Modify: `server/spacetimedb/spacetimedb/Reducers/ChannelReducers.cs:66-82`
- Modify: `server/spacetimedb/spacetimedb/Domain/WorldRules.cs:18-22,116-120`
- Modify: `server/spacetimedb/tests/ProgressionRulesTests.cs:8-69`
- Modify: `server/spacetimedb/tests/WorldRulesTests.cs:40-41,97-111,178-185`

- [ ] **Step 1: Write the failing gold saturation tests**

In `server/spacetimedb/tests/ProgressionRulesTests.cs`, delete the four tests `Experience_uses_data_driven_level_boundaries`, `Grant_uses_the_same_level_rule_as_production`, `Grant_saturates_experience_and_gold` and `Level_selection_uses_the_highest_eligible_level_regardless_of_row_order` (everything between the class's opening brace and the `[Fact]` attribute of `Loot_winner_is_nearest_then_lowest_entity_id`). Insert in their place:

```csharp
    [Theory]
    [InlineData(0u, 100u, 100u)]
    [InlineData(uint.MaxValue - 5, 10u, uint.MaxValue)]
    [InlineData(uint.MaxValue, 1u, uint.MaxValue)]
    public void Gold_addition_saturates(uint current, uint amount, uint expected)
    {
        Assert.Equal(expected, ProgressionRules.AddGoldSaturating(current, amount));
    }

    [Fact]
    public void Contribution_addition_saturates()
    {
        Assert.Equal(30ul, ProgressionRules.AddSaturating(10, 20));
        Assert.Equal(ulong.MaxValue, ProgressionRules.AddSaturating(ulong.MaxValue - 1, 5));
    }

    [Fact]
    public void Boarding_contribution_is_a_fixed_constant()
    {
        Assert.Equal(25ul, ProgressionRules.BoardingContribution);
    }

```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~ProgressionRulesTests"` (timeout 600000)
Expected: build error `'ProgressionRules' does not contain a definition for 'AddGoldSaturating'`.

- [ ] **Step 3: Trim ProgressionRules to contributions and gold**

In `server/spacetimedb/spacetimedb/Domain/ProgressionRules.cs` replace everything from the top of the file down to (not including) the line `public readonly record struct LootCandidate(ulong EntityId, float Distance);` with:

```csharp
namespace Sea.Server;

public static class ProgressionRules
{
    public const ulong BoardingContribution = 25;

    public static ulong AddSaturating(ulong current, ulong amount) =>
        ulong.MaxValue - current < amount ? ulong.MaxValue : current + amount;

    public static uint AddGoldSaturating(uint current, uint amount) =>
        uint.MaxValue - current < amount ? uint.MaxValue : current + amount;
}

```

The `LootCandidate`, `LootClaimSelection`, `LootRules`, `RespawnState` and `RespawnRules` definitions below stay unchanged.

- [ ] **Step 4: Remove the cannon upgrade constants and helpers from WorldRules**

In `server/spacetimedb/spacetimedb/Domain/WorldRules.cs` delete these five constants (lines 18–22):

```csharp
    public const uint InitialProgressionLevel = 1;
    public const uint InitialCannonUpgradeLevel = 0;
    public const uint CannonUpgradeBaseCost = 100;
    public const uint CannonUpgradeCostStep = 100;
    public const uint CannonDamagePerUpgrade = 5;
```

and these two methods at the bottom of the class (lines 116–120), leaving `ApplyDamage` as the last member:

```csharp
    public static uint CannonUpgradeCost(uint upgradeLevel) =>
        checked(CannonUpgradeBaseCost + upgradeLevel * CannonUpgradeCostStep);

    public static uint CannonDamageAfterUpgrade(uint damage, uint upgradeLevel) =>
        checked(damage + CannonDamagePerUpgrade * upgradeLevel);
```

In `server/spacetimedb/tests/WorldRulesTests.cs` delete:
- the two asserts `Assert.Equal(1u, WorldRules.InitialProgressionLevel);` and `Assert.Equal(0u, WorldRules.InitialCannonUpgradeLevel);` (lines 40–41),
- the theory `CannonUpgradeCost_is_deterministic` together with its `[Theory]` and three `[InlineData]` attributes, and the fact `CannonDamageAfterUpgrade_adds_the_fixed_upgrade_bonus` (lines 97–111),
- the fact `CheckedUpgradeArithmeticRejectsOverflow` (lines 178–185).

- [ ] **Step 5: Run the domain tests**

Run: `./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~ProgressionRulesTests|FullyQualifiedName~WorldRulesTests"` (timeout 600000)
Expected: all pass (3 new progression tests plus the loot and respawn tests, and the remaining world rules tests).

- [ ] **Step 6: Rewrite ProgressionSystem to award gold only**

Replace the whole of `server/spacetimedb/spacetimedb/Simulation/ProgressionSystem.cs` with:

```csharp
using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void RecordCombatProgress(
        ReducerContext ctx,
        ulong sourceEntityId,
        Ship defender,
        CombatDamage damage)
    {
        if (sourceEntityId == 0 || defender.FactionCode != (byte)FactionCode.Npc ||
            ctx.Db.PlayerOwnership.ShipEntityId.Find(sourceEntityId) is null)
        {
            return;
        }

        var applied = (ulong)damage.Hull + damage.Sails + damage.Cannons + damage.Crew;
        if (applied > 0)
        {
            AddContribution(ctx, defender.EncounterId, sourceEntityId, applied, boarding: 0);
        }
    }

    private static void RecordBoardingProgress(
        ReducerContext ctx,
        ulong sourceEntityId,
        Ship target)
    {
        if (target.FactionCode != (byte)FactionCode.Npc ||
            ctx.Db.PlayerOwnership.ShipEntityId.Find(sourceEntityId) is null)
        {
            return;
        }

        AddContribution(
            ctx,
            target.EncounterId,
            sourceEntityId,
            damage: 0,
            boarding: ProgressionRules.BoardingContribution);
    }

    private static void AddContribution(
        ReducerContext ctx,
        ulong encounterId,
        ulong contributorEntityId,
        ulong damage,
        ulong boarding)
    {
        if (encounterId == 0)
        {
            return;
        }

        foreach (var existing in ctx.Db.CombatContribution.ByEncounterContributor.Filter(
                     (encounterId, contributorEntityId)))
        {
            var updated = existing;
            updated.Damage = ProgressionRules.AddSaturating(updated.Damage, damage);
            updated.Boarding = ProgressionRules.AddSaturating(updated.Boarding, boarding);
            ctx.Db.CombatContribution.ContributionId.Update(updated);
            return;
        }

        ctx.Db.CombatContribution.Insert(new CombatContribution
        {
            EncounterId = encounterId,
            ContributorEntityId = contributorEntityId,
            Damage = damage,
            Boarding = boarding,
            Support = 0,
        });
    }

    private static void AwardGold(ReducerContext ctx, ulong shipEntityId, uint gold)
    {
        if (gold == 0 ||
            ctx.Db.PlayerOwnership.ShipEntityId.Find(shipEntityId) is not
                PlayerOwnership ownership ||
            ctx.Db.PlayerProgression.Owner.Find(ownership.Owner) is not
                PlayerProgression progression)
        {
            return;
        }

        progression.Gold = ProgressionRules.AddGoldSaturating(progression.Gold, gold);
        ctx.Db.PlayerProgression.Owner.Update(progression);
    }
}
```

- [ ] **Step 7: Update the two AwardProgression callers**

In `server/spacetimedb/spacetimedb/Simulation/EncounterSettlementSystem.cs` replace line 106:

```csharp
        AwardProgression(ctx, grant.EntityId, grant.Experience, grant.Gold);
```

with:

```csharp
        AwardGold(ctx, grant.EntityId, grant.Gold);
```

The `EncounterReward` insert that follows keeps its `Experience` field; it is removed in Milestone 1d together with the reward contract.

In `server/spacetimedb/spacetimedb/Simulation/LootSystem.cs` replace the call at lines 74–80:

```csharp
        AwardProgression(
            ctx,
            claimant,
            experience: loot.Quantity / 4,
            gold: string.Equals(loot.LootType, "gold", StringComparison.Ordinal)
                ? loot.Quantity
                : 0);
```

with:

```csharp
        AwardGold(
            ctx,
            claimant,
            string.Equals(loot.LootType, "gold", StringComparison.Ordinal)
                ? loot.Quantity
                : 0);
```

- [ ] **Step 8: Update EnsureProgression and remove FindProgression**

In `server/spacetimedb/spacetimedb/Simulation/SimulationTick.cs` replace `FindProgression` and `EnsureProgression` (lines 282–300) with:

```csharp
    private static void EnsureProgression(ReducerContext ctx, Identity owner)
    {
        if (ctx.Db.PlayerProgression.Owner.Find(owner) is null)
        {
            ctx.Db.PlayerProgression.Insert(new PlayerProgression
            {
                Owner = owner,
                MapRank = 1,
                Gold = 0,
            });
        }

        if (ctx.Db.PlayerAccount.Owner.Find(owner) is null)
        {
            ctx.Db.PlayerAccount.Insert(new PlayerAccount
            {
                Owner = owner,
                AccountId = "",
            });
        }
    }
```

- [ ] **Step 9: Use EnsureProgression on the first-load path**

In `server/spacetimedb/spacetimedb/Reducers/LifecycleReducers.cs` replace the inline insert in `LoadPlayer` (lines 105–111):

```csharp
        ctx.Db.PlayerProgression.Insert(new PlayerProgression
        {
            Owner = ctx.Sender,
            Level = 1,
            Experience = 0,
            Gold = 0,
        });
```

with:

```csharp
        EnsureProgression(ctx, ctx.Sender);
```

- [ ] **Step 10: Remove the UpgradeCannon reducer**

In `server/spacetimedb/spacetimedb/Reducers/ChannelReducers.cs` delete the `UpgradeCannon` reducer (lines 66–82, from `[SpacetimeDB.Reducer]` through its closing brace, plus the blank line before it). The `CancelChannel` reducer's closing brace is then followed by the class's closing brace.

- [ ] **Step 11: Confirm nothing else references the removed members**

```bash
cd /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg
grep -rn 'LevelDefinition\|LevelThreshold\|ProgressionGrant\|ProgressionState\|AwardProgression\|FindProgression\|\.Experience\b\|\.Level\b\|BoardingExperience\|DamageExperience' server/spacetimedb/spacetimedb --include='*.cs' --exclude-dir=Generated --exclude-dir=obj --exclude-dir=bin
```

Expected: only `EncounterSettlementSystem.cs` lines that read `grant.Experience` and write `Experience = grant.Experience` into `EncounterReward`. Anything else is a missed edit.

- [ ] **Step 12: Build the module**

Run: `pnpm server:build` (timeout 600000)
Expected: `Build succeeded.` with 0 errors. If the build reports an unused-variable or nullability warning as an error, fix the line it names; do not suppress the warning.

- [ ] **Step 13: Run the whole domain test suite**

Run: `pnpm server:test` (timeout 600000)
Expected: all tests pass.

- [ ] **Step 14: Commit the work in progress**

```bash
cd /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg
git add server/spacetimedb
git commit -m "wip(progression): replace level and experience with map rank and gold

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01VVAL5L8X2uHhKGrESphkhF"
```

**Review amendments (applied during execution; the branch is authoritative over the code blocks above).** The code-quality, thermo-nuclear and performance reviews reshaped the module side of this task, fixed in `wip(progression): fold contribution and gold into settlement`: (1) `Simulation/ProgressionSystem.cs` does not exist; contribution recording and gold awards live in `Simulation/EncounterSettlementSystem.cs` (175 lines), which owns the whole record → settle → award pipeline. (2) `RecordCombatProgress`, `RecordBoardingProgress` and `AddContribution` are one `RecordContribution(ctx, encounterId, contributorEntityId, damage, boarding)` whose only guard is `encounterId == 0 || (damage == 0 && boarding == 0)`; the player-attacking-NPC check belongs to the callers: `DamageSystem.ApplyDamage` hoists a single `PlayerOwnership.ShipEntityId.Find` into an `attackerIsPlayer` bool that drives both the contribution call and the engagement branch, and `ChannelSystem.ResolveBoarding` checks only `target.FactionCode == Npc` because boarding channels are created solely through `IssueShipCommand`, which requires the sender's `PlayerOwnership`. `CombatDamage` gained `ulong Total`. (3) `AwardGold(ctx, Identity owner, uint gold)` throws when the progression row is missing (unreachable once `LoadPlayer` has run); settlement passes the ownership it already resolved and `LootSystem` resolves ownership inside the `"gold"` branch. (4) `EnsureProgression` became `EnsurePlayerProgression` and `EnsurePlayerAccount` in `Reducers/LifecycleReducers.cs` beside their only caller, `LoadPlayer`; the dead `FindPlayerShip` was deleted from `SimulationTick.cs`. (5) The MA0016 analyzer rule is suppressed with the inline `#pragma warning disable/restore MA0016` house style around the three `List<float>` columns of `StatCaps` in `Schema/ContentTables.cs` (as `SimulationTables.cs` already does), not in `.editorconfig`. (6) Tests: exact-fit and zero-amount rows for both saturating adders (the `<`→`<=` mutant otherwise survives), `CombatDamage.Total` widening, and the boarding-constant test is labelled a balance guard (498 tests). Deferred: the client, admin and bindings still reference `level_definition`, `Experience` and `upgrade_cannon` until Task 10 (the Unity runtime probe and `scripts/test-unity-runtime.sh` must key on `Gold`); `scripts/test-world-schema.sh` still names the pre-Task-7 content tables; `seed/world.json` has no consumer; `CombatContribution` cannot express a composite unique index on `(EncounterId, ContributorEntityId)` in the C# SDK; three benchmark candidates (sustained combat, settlement fan-out, login storm) are recorded for hardening.

---

### Task 9: Starter hull and ShipStats on load

**Files:**
- Create: `server/spacetimedb/spacetimedb/Simulation/ShipStatsSystem.cs`
- Modify: `server/spacetimedb/spacetimedb/Reducers/LifecycleReducers.cs` (`LoadPlayer`, both paths)

The `Ship` row keeps its legacy hard-coded combat constants in this milestone (PLAN.md 1a: "the pipeline is real before gear exists"). `ShipStats` is computed and stored so 1b can wire it into combat; nothing reads it yet on the server.

- [ ] **Step 1: Create the stats system**

Create `server/spacetimedb/spacetimedb/Simulation/ShipStatsSystem.cs`:

```csharp
using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private const string StarterHullDefId = "hull_t1";
    private const string StarterCannonDefId = "cannon_t1";
    private const string BaselineAmmoId = "round";

    private static void EnsureHull(ReducerContext ctx, Identity owner)
    {
        foreach (var existing in ctx.Db.Hull.ByOwner.Filter(owner))
        {
            RecomputeStats(ctx, existing);
            return;
        }

        var hullDef = ctx.Db.HullDef.HullDefId.Find(StarterHullDefId) ??
            throw new InvalidOperationException("The starter hull definition is missing.");
        var hull = ctx.Db.Hull.Insert(new Hull
        {
            HullId = 0,
            Owner = owner,
            HullDefId = StarterHullDefId,
            Name = hullDef.Name,
            CannonDefId = StarterCannonDefId,
            CannonCount = hullDef.CannonSlots,
        });
        RecomputeStats(ctx, hull);
    }

    private static void RecomputeStats(ReducerContext ctx, Hull hull)
    {
        var hullDef = ctx.Db.HullDef.HullDefId.Find(hull.HullDefId) ??
            throw new InvalidOperationException($"Hull definition '{hull.HullDefId}' is missing.");
        var cannonDef = ctx.Db.CannonDef.CannonDefId.Find(hull.CannonDefId) ??
            throw new InvalidOperationException($"Cannon definition '{hull.CannonDefId}' is missing.");
        var ammo = ctx.Db.AmmoDef.AmmoId.Find(BaselineAmmoId) ??
            throw new InvalidOperationException("The baseline ammunition definition is missing.");
        var caps = ctx.Db.StatCaps.Id.Find(1) ??
            throw new InvalidOperationException("Stat caps are missing.");

        var loadout = new ShipLoadout(
            HullContentFrom(hullDef),
            CannonContentFrom(cannonDef),
            hull.CannonCount,
            ammo.DamageMultiplier,
            ammo.ReloadMultiplier);
        var sheet = ShipStatRules.Compute(loadout, Array.Empty<BonusSource>(), StatCapsFrom(caps));
        var stats = new ShipStats
        {
            HullId = hull.HullId,
            Owner = hull.Owner,
            VolleyDamage = sheet.VolleyDamage,
            ReloadMilliseconds = sheet.ReloadMilliseconds,
            Magazine = sheet.Magazine,
            MaxHitPoints = sheet.MaxHitPoints,
            ArmorFront = sheet.ArmorFront,
            ArmorSides = sheet.ArmorSides,
            ArmorBack = sheet.ArmorBack,
            SpeedSquaresPerSecond = sheet.SpeedSquaresPerSecond,
            TurnDegreesPerSecond = sheet.TurnDegreesPerSecond,
            RangeSquares = sheet.RangeSquares,
            RepairAmount = sheet.RepairAmount,
            RepairChannelMilliseconds = sheet.RepairChannelMilliseconds,
            CombatPowerUsed = sheet.CombatPowerUsed,
            CombatPowerInactive = sheet.CombatPowerInactive,
            FightScore = sheet.FightScore,
        };

        if (ctx.Db.ShipStats.HullId.Find(hull.HullId) is null)
        {
            ctx.Db.ShipStats.Insert(stats);
        }
        else
        {
            ctx.Db.ShipStats.HullId.Update(stats);
        }
    }
}
```

- [ ] **Step 2: Call EnsureHull from both LoadPlayer paths**

In `server/spacetimedb/spacetimedb/Reducers/LifecycleReducers.cs`, `LoadPlayer` now reads:

```csharp
    [SpacetimeDB.Reducer]
    public static void LoadPlayer(ReducerContext ctx)
    {
        if (ctx.Db.PlayerOwnership.Owner.Find(ctx.Sender) is PlayerOwnership ownership)
        {
            SetLoadedConnectionState(ctx, ref ownership, true);
            EnsureProgression(ctx, ctx.Sender);
            EnsureHull(ctx, ctx.Sender);
            EnsureCommandState(ctx, ctx.Sender, ownership.ShipEntityId);
            SynchronizePlayerClock(ctx, ctx.Sender);
            return;
        }

        var entityId = AllocateEntityId(ctx);
        var spawn = FindSafeSpawn(ctx, IdentitySeed(ctx.Sender));
        var ship = CreateShip(entityId, "player_sloop", "player", spawn.X, spawn.Y);
        ctx.Db.Ship.Insert(ship);
        InsertShipMovement(ctx, ship);
        ctx.Db.PlayerOwnership.Insert(new PlayerOwnership
        {
            Owner = ctx.Sender,
            ShipEntityId = entityId,
            IsConnected = true,
        });
        AdjustConnectedPlayerCount(ctx, 1);
        EnsureProgression(ctx, ctx.Sender);
        EnsureHull(ctx, ctx.Sender);
        EnsureCommandState(ctx, ctx.Sender, entityId);
        SeedPlayerInventory(ctx, entityId);
        AppendEvent(ctx, entityId, "player_loaded", $"entity_id={entityId}");
        SynchronizePlayerClock(ctx, ctx.Sender);
    }
```

Only the two `EnsureHull(ctx, ctx.Sender);` lines are new.

- [ ] **Step 3: Build, format and size-check the module**

Run: `pnpm server:build` (timeout 600000)
Expected: `Build succeeded.`

Run: `pnpm quality:dotnet-format`
Expected: exit 0. If it reports formatting differences, apply them with `./scripts/dotnet.sh format server/spacetimedb/spacetimedb/spacetimedb.csproj` and re-run.

Run: `pnpm quality:csharp-size`
Expected: exit 0 with no file over 500 lines. If `ContentDefinitions.cs` or `ContentValidation.cs` is over the limit, move the `StatCapsContent` record into a new `Domain/StatCapsContent.cs` (the domain csproj globs `Domain/*.cs`, so no csproj change is needed).

- [ ] **Step 4: Run the full domain suite**

Run: `pnpm server:test` (timeout 600000)
Expected: all tests pass.

- [ ] **Step 5: Commit the work in progress**

```bash
cd /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg
git add server/spacetimedb
git commit -m "wip(stats): compute starter hull ship stats on load

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01VVAL5L8X2uHhKGrESphkhF"
```


**Review amendments (applied during execution; the branch is authoritative over the code blocks above).** The code-quality, thermo-nuclear and performance reviews reshaped Task 9 in `wip(content): harden ship stats seeding` and `wip(content): polish ship stats seeding`: (1) `RecomputeStats` is `RecomputeShipStats` in `Simulation/ShipStatsSystem.cs` (43 lines) and does exactly three things: resolve content, `ShipStatRules.Compute`, upsert. (2) The 15-field row initializer is a `ShipStats.From(Hull hull, ShipStatSheet sheet)` factory in `Schema/DockTables.cs`, next to the columns, matching the `From(content)` factories in `Schema/ContentTables.cs`. (3) Hull and cannon ids resolve through `Catalog.HullById` / `Catalog.CannonById` (`IReadOnlyDictionary<string, T>`, ordinal, built once by `ContentIndex.ById<T>` which rejects duplicate ids); the linear `FindById` helpers do not exist. (4) Starter content is resolved at module load as `Catalog.StarterHull`, `Catalog.StarterCannon` and `Catalog.BaselineAmmunition` (Round Shot), so a renamed content id fails at type initialization rather than on first login; the ids are private constants on `Catalog`, and `ContentCatalogTests.Starter_loadout_ids_match_the_module_constants` is the build-time tripwire (the check is deliberately not in `Init`, which early-returns on an existing `WorldState`). (5) `EnsureHull` in `Reducers/LifecycleReducers.cs` recomputes every owned hull and inserts the starter only when the player owns none, so the loop already has the shape the 1c dock needs. (6) The upsert skips the `Update` when the recomputed row equals the stored one via the generated field-wise `IEquatable<ShipStats>` (keep `.Equals`, the generated `==` boxes), so a steady-state reconnect writes nothing. (7) `ShipStatRules.Compute` allocates nothing for an empty bonus list (`Ordered`/`Prefix` helpers, shared read-only `EmptyPrefix`); measured 456 B → 0 B per call. (8) Tests: `ContentIndexTests.ById_finds_every_entry_by_its_own_id`, `ContentIndexTests.Two_hulls_sharing_an_id_are_rejected`, and the starter tripwire (501 tests). Deferred to the hardening pass: `Compute` with one or more sources still allocates (stackalloc insertion sort over ≤ 8 sources), caching the no-bonus `Derive` baseline, the provably-null `Find` on the first-login insert path, and a login-storm host-call benchmark.

---

### Task 10: Regenerate bindings and update the clients and runtime check

**Files:**
- Regenerate: `apps/game-unity/Assets/Generated/SpacetimeDB/**` and `packages/contracts/src/generated/**`
- Modify: `apps/game-unity/Assets/Domain/SeaSubscriptionPlan.cs:20-23`
- Modify: `apps/game-unity/Assets/Networking/SeaConnectionClientState.cs:11,43-44,57-59,104-122,196`
- Modify: `apps/game-unity/Assets/Domain/SeaHudViewModel.cs:16-20,53-57,99-106,139-140`
- Modify: `apps/game-unity/Assets/UI/SeaHudController.cs:215-235,333-339`
- Modify: `apps/game-unity/Assets/UI/SeaHud.uxml:114-118`
- Modify: `apps/game-unity/Assets/UI/SeaHud.uss:274`
- Modify: `apps/game-unity/Assets/Presentation/SeaRuntimeProgressionProbe.cs:16,45-47,75`
- Modify: `apps/game-unity/Assets/Tests/EditMode/SeaRuntimeAndCombatTests.cs:256-258,272,294`
- Modify: `apps/admin/src/lib/operations.ts:20`
- Modify: `scripts/test-world-schema.sh`

- [ ] **Step 1: Regenerate both binding sets**

```bash
cd /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg
pnpm server:generate:csharp && pnpm server:generate:typescript && pnpm quality:bindings
```

Expected: both generators finish, and `git status --short apps/game-unity/Assets/Generated packages/contracts/src/generated` shows new files for `MapDef`, `Sector`, `HullDef`, `CannonDef`, `AmmoDef`, `NpcDef`, `StatCaps`, `Hull`, `ShipStats`, deleted files for `AmmoDefinition`, `NpcDefinition`, `LevelDefinition` and the `UpgradeCannon` reducer, and a modified `PlayerProgression`. `quality:bindings` exits 0.

- [ ] **Step 2: Update the Unity subscription plan**

In `apps/game-unity/Assets/Domain/SeaSubscriptionPlan.cs` replace lines 20–23:

```csharp
                "SELECT * FROM ammo_definition",
                "SELECT * FROM ability_definition",
                "SELECT * FROM npc_definition",
                "SELECT * FROM level_definition",
```

with:

```csharp
                "SELECT * FROM ammo_def",
                "SELECT * FROM ability_definition",
                "SELECT * FROM npc_def",
                "SELECT * FROM hull_def",
                "SELECT * FROM cannon_def",
                "SELECT * FROM stat_caps",
                $"SELECT * FROM hull WHERE owner = {ownerSqlLiteral}",
                $"SELECT * FROM ship_stats WHERE owner = {ownerSqlLiteral}",
```

- [ ] **Step 3: Remove level handling from the client state**

In `apps/game-unity/Assets/Networking/SeaConnectionClientState.cs`:
- delete line 11 `private readonly Dictionary<uint, ulong> levelThresholds = new();`
- delete the `TryGetLevelThreshold` method (lines 43–44 and the blank line after it)
- delete the three `connection.Db.LevelDefinition.On...` registrations (lines 57–59)
- delete `HandleLevelDefinitionInserted`, `HandleLevelDefinitionUpdated`, `HandleLevelDefinitionDeleted` and `StoreLevelDefinition` (lines 104–122)
- delete `levelThresholds.Clear();` in `NotifyPresentationReset` (line 196)

Then `grep -n 'Level' apps/game-unity/Assets/Networking/SeaConnectionClientState.cs` must print nothing.

- [ ] **Step 4: Replace experience with map rank in the HUD view model**

In `apps/game-unity/Assets/Domain/SeaHudViewModel.cs`:

Replace the snapshot fields at lines 16–20:

```csharp
        public ulong Experience { get; set; }
        public ulong CurrentLevelExperience { get; set; }
        public ulong NextLevelExperience { get; set; }
        public uint Level { get; set; } = 1;
        public uint Gold { get; set; }
```

with:

```csharp
        public byte MapRank { get; set; } = 1;
        public uint Gold { get; set; }
```

Delete the properties `public string ExperienceText { get; private set; }` (line 53) and `public float ExperienceProgress { get; private set; }` (line 57).

In the object initializer, delete the `ExperienceText = ...` line (99) and the four-line `ExperienceProgress = LevelRatio(...)` expression (103–106), and change line 100 to:

```csharp
                LevelText = $"MAP RANK {source.MapRank}",
```

Delete the `LevelRatio` helper (lines 139–140 plus the blank line after it).

- [ ] **Step 5: Update the HUD controller**

In `apps/game-unity/Assets/UI/SeaHudController.cs` replace the progression block (lines 215–235):

```csharp
            var progression = connection.Connection.Db.PlayerProgression.Owner.Find(connection.LocalIdentity);
            if (progression != null)
            {
                snapshot.Level = progression.Level;
                snapshot.Experience = progression.Experience;
                snapshot.Gold = progression.Gold;
                if (connection.TryGetLevelThreshold(progression.Level, out var currentThreshold))
                {
                    snapshot.CurrentLevelExperience = currentThreshold;
                }

                if (connection.TryGetLevelThreshold(progression.Level + 1, out var nextThreshold))
                {
                    snapshot.NextLevelExperience = nextThreshold;
                }

                if (snapshot.NextLevelExperience == 0)
                {
                    snapshot.NextLevelExperience = Math.Max(snapshot.Experience, snapshot.CurrentLevelExperience);
                }
            }
```

with:

```csharp
            var progression = connection.Connection.Db.PlayerProgression.Owner.Find(connection.LocalIdentity);
            if (progression != null)
            {
                snapshot.MapRank = progression.MapRank;
                snapshot.Gold = progression.Gold;
            }
```

In `Apply`, delete `SetText("experience-text", model.ExperienceText);` (line 336) and `SetProgress("player-experience", model.ExperienceProgress);` (line 339).

- [ ] **Step 6: Remove the experience meter from the HUD document**

In `apps/game-unity/Assets/UI/SeaHud.uxml` delete lines 114–118:

```xml
                <ui:VisualElement class="meter-heading">
                    <ui:Label text="EXPERIENCE" class="micro-label" />
                    <ui:Label name="experience-text" text="—" class="instrument-value" />
                </ui:VisualElement>
                <ui:ProgressBar name="player-experience" low-value="0" high-value="1" class="player-meter xp-bar" />
```

In `apps/game-unity/Assets/UI/SeaHud.uss` delete line 274 (`.xp-bar .unity-progress-bar__progress { ... }`).

On line 106 of the same file change the `level-label` default from `text="LEVEL 1"` to `text="MAP RANK 1"`.

- [ ] **Step 7: Make the progression probe watch gold only**

In `apps/game-unity/Assets/Presentation/SeaRuntimeProgressionProbe.cs`:
- delete line 16 `private ulong progressionInitialExperience;`
- delete line 46 `progression.Experience > progressionInitialExperience &&`
- delete line 75 `progressionInitialExperience = progression.Experience;`

The milestone condition becomes: loot observed, gold increased, and the encounter id changed.

- [ ] **Step 8: Update the EditMode tests**

In `apps/game-unity/Assets/Tests/EditMode/SeaRuntimeAndCombatTests.cs`:
- delete lines 256–258 (`Experience = 1250,`, `CurrentLevelExperience = 1000,`, `NextLevelExperience = 2000,`) and add `MapRank = 1,` in their place
- delete line 272 `Assert.That(model.ExperienceProgress, Is.EqualTo(0.25f));` and add `Assert.That(model.LevelText, Is.EqualTo("MAP RANK 1"));`
- in the `requiredElements` array (line 294) change `"player-hull", "player-experience",` to `"player-hull",`

- [ ] **Step 9: Confirm the Unity sources no longer reference removed bindings**

```bash
cd /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg
grep -rn 'AmmoDefinition\|NpcDefinition\|LevelDefinition\|UpgradeCannon\|\.Experience\b\|player-experience\|experience-text' apps/game-unity/Assets --include='*.cs' --include='*.uxml' --include='*.uss' --exclude-dir=Generated
```

Expected: only `SeaConnectionController.cs` (`reward.Experience` from `EncounterReward`, which keeps the field) and any `EncounterReward`-typed test fixtures. Fix any other hit.

- [ ] **Step 10: Update the admin column map**

In `apps/admin/src/lib/operations.ts` change line 20 to:

```ts
	player_progression: ["owner", "map_rank", "gold"],
```

- [ ] **Step 11: Update the runtime world-schema check**

In `scripts/test-world-schema.sh`:

Replace the table loop line:

```bash
for table_name in world_state ship ship_status ship_channel cooldown volley inventory ammo_definition ability_definition npc_definition npc_ai respawn_work loot player_progression encounter_reward combat_event environment_state current_zone world_object; do
```

with:

```bash
for table_name in world_state ship ship_status ship_channel cooldown volley inventory map_def sector hull_def cannon_def ammo_def ability_definition npc_def stat_caps npc_ai respawn_work loot player_progression encounter_reward combat_event environment_state current_zone world_object; do
```

Change `world[0].content_version !== 4` to `world[0].content_version !== 5` and the error text to `"World state does not expose the 10 Hz versioned simulation contract (content version 5)."`.

Replace the definition-count block:

```js
if (rows("ammo_definition").length !== 4) throw new Error("Expected four ammunition definitions.");
if (rows("ability_definition").length !== 4) throw new Error("Expected four ability definitions.");
const npcDefinitions = rows("npc_definition");
if (npcDefinitions.length !== 3) throw new Error("Expected three NPC definitions.");
if (npcDefinitions.some((definition) =>
  definition.maximum_speed <= 0 || definition.cannon_damage <= 0 ||
  definition.gold_reward <= 0 || definition.experience_reward <= 0)) {
  throw new Error("NPC combat and reward definitions must be positive.");
}
```

with:

```js
if (rows("ammo_def").length !== 4) throw new Error("Expected four ammunition definitions.");
if (rows("ability_definition").length !== 4) throw new Error("Expected four ability definitions.");
const npcDefinitions = rows("npc_def");
if (npcDefinitions.length !== 3) throw new Error("Expected three NPC definitions.");
if (npcDefinitions.some((definition) =>
  definition.maximum_speed <= 0 || definition.cannon_damage <= 0 ||
  definition.gold_reward <= 0 || definition.experience_reward <= 0)) {
  throw new Error("NPC combat and reward definitions must be positive.");
}
const maps = rows("map_def");
if (maps.length !== 1 || maps[0].code !== "1/1" || maps[0].width !== 20 || maps[0].height !== 20) {
  throw new Error("Expected the single Havenmere map definition (1/1, 20x20).");
}
if (rows("sector").length !== 400) throw new Error("Expected 400 Havenmere sectors.");
if (rows("hull_def").length !== 1) throw new Error("Expected one hull definition.");
if (rows("cannon_def").length !== 1) throw new Error("Expected one cannon definition.");
const caps = rows("stat_caps");
if (caps.length !== 1 || caps[0].combat_power_budget !== 45) {
  throw new Error("Expected one stat caps row with a 45 point Combat Power budget.");
}
const progressionColumns = columns("player_progression");
if (!progressionColumns.includes("map_rank") || progressionColumns.includes("level") ||
  progressionColumns.includes("experience")) {
  throw new Error("player_progression must expose map_rank and drop level and experience.");
}
```

Also add `"UpgradeCannon"` to the legacy reducer array so the check fails if it comes back:

```js
  "CancelBoarding", "UpgradeCannon",
```

- [ ] **Step 12: Reset the local database and run the runtime checks**

```bash
cd /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg
pnpm infra:up
pnpm server:reset
pnpm runtime:test:world
pnpm server:test:integration
```

(timeout 600000 for each). Expected: `server:reset` publishes `sea-local` without errors, `runtime:test:world` prints `Unified 10 Hz world schema and validated content are live.`, and the integration script exits 0. `server:reset` wipes the local database volume; that is intended because the schema changed.

- [ ] **Step 13: Run the Unity EditMode tests**

Run: `pnpm unity:test` (timeout 600000)
Expected: exit 0 and `apps/game-unity/Build/test-results.xml` reports 0 failures. Do not commit `apps/game-unity/Build`.

- [ ] **Step 14: Commit the work in progress**

```bash
cd /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg
git add apps/game-unity/Assets packages/contracts/src/generated apps/admin/src/lib/operations.ts scripts/test-world-schema.sh
git commit -m "wip(client): regenerate bindings and show map rank

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01VVAL5L8X2uHhKGrESphkhF"
```

Check `git status --short` afterwards: it must not list `.cache/`, `apps/game-unity/Build/`, or any `obj/`/`bin/` path as staged.

---

**Review amendments (Task 10, commits 410f39b, 42044d2, 2068f73).** The admin dashboard decodes SQL rows from `result.schema.elements[].name.some` instead of positional column lists; the previous lists were misaligned on `ship`, `npc_ai`, `world_object` and `combat_event`, and `faction === "player"` never matched (`faction_code` is a u8). Every client subscription now has a handwritten reader (`SeaHudSnapshotReader.cs`, a partial of the HUD controller, owns all table reads; the controller only applies a snapshot), except `environment_state`, `combat_event` and `current_zone`, which are pre-existing and deferred to 1e. `SeaSubscriptionTests` pins the subscription plan against the generated bindings by reflection, replacing the string greps in `scripts/check-unity-source.sh`. The HUD ledger shows "MAP RANK n", gold as `N0 ¤`, and combat power as `used / budget CP`; `LevelText` became `MapRankText`; the XP clause left `CommandStatus`. The runtime gate string is "Sea runtime observed NPC sinking, atomic loot, gold, and NPC respawn." in both the probe and `scripts/test-unity-runtime.sh`, and the probe requires a `Loot` row deletion rather than gold alone. `scripts/test-world-schema.sh` row counts are table-driven and `hull`/`ship_stats` are existence-checked. `npc_def` was dropped from the integration client subscription (unread there).

### Task 11: Documentation, full check, and squash

**Files:**
- Modify: `AGENTS.md:57-58`
- Modify: `PLAN.md:96-97,233-234`
- Modify: `docs/SEA_5_GAP_ANALYSIS.md:96`
- Add to git: `docs/superpowers/plans/2026-09-02-milestone-1a-content-and-ship-stats.md` (this plan; currently untracked)

- [ ] **Step 1: Document the content pipeline in AGENTS.md**

Replace the bullet at lines 57–58:

```markdown
- Treat generated C# and TypeScript bindings as generated files. Never edit
  them by hand; regenerate and commit both sides with schema changes.
```

with:

```markdown
- Treat generated C# and TypeScript bindings as generated files. Never edit
  them by hand; regenerate and commit both sides with schema changes.
- Game content lives in `server/spacetimedb/spacetimedb/Content/Data/*.json`.
  `server/spacetimedb/spacetimedb/Generated/ContentCatalog.g.cs` is generated
  from it by `pnpm content:generate`; never edit it by hand, and run
  `pnpm quality:content` before committing content changes.
```

- [ ] **Step 2: Record the AccountId decision in PLAN.md**

Replace lines 96–97:

```markdown
- `PlayerProgression` gains `MapRank` and `AccountId`. `Level` and `Experience` go.
  `LevelDefinition` goes.
```

with:

```markdown
- `PlayerProgression` gains `MapRank`; a private `PlayerAccount` row holds
  `AccountId` so it never reaches other clients. `Level` and `Experience` go.
  `LevelDefinition` goes.
```

Replace lines 233–234:

```markdown
2. Identity stays anonymous and local until Milestone 5. `AccountId` is added
   in sub-phase 1a so Better Auth attaches later without a schema reset.
```

with:

```markdown
2. Identity stays anonymous and local until Milestone 5. A private
   `PlayerAccount` table with `AccountId` is added in sub-phase 1a so Better
   Auth attaches later without a schema reset.
```

In `docs/SEA_5_GAP_ANALYSIS.md` line 96 (the `Identity` row), replace the sentence

```
`Player` gets an `AccountId` column now so Better Auth attaches without a schema reset.
```

with

```
A private `PlayerAccount` table gets an `AccountId` column now so Better Auth attaches without a schema reset.
```

Verify: `grep -n "PlayerAccount" PLAN.md docs/SEA_5_GAP_ANALYSIS.md` prints three lines (PLAN.md twice, gap analysis once).

- [ ] **Step 3: Run the full check**

Run: `pnpm check` (timeout 600000)
Expected: exit 0. This runs the static quality chain, `quality:scripts`, `quality:dotnet-format` and `quality:content` (added in Task 2). The Unity and runtime checks already ran in Task 10; re-run `pnpm runtime:test:world` and `pnpm unity:test` here only if anything under `server/` or `apps/game-unity/` changed since.

- [ ] **Step 4: Squash the wip commits into the sub-phase commit**

```bash
cd /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg
git add AGENTS.md PLAN.md docs/SEA_5_GAP_ANALYSIS.md docs/superpowers/plans/2026-09-02-milestone-1a-content-and-ship-stats.md
git commit -m "wip(docs): record the content pipeline and account table

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01VVAL5L8X2uHhKGrESphkhF"
git log --oneline "$(cat .cache/plan-1a-base)"..HEAD
```

Expected: only `wip(...)` commits since the base recorded in Task 1. Then:

```bash
git reset --soft "$(cat .cache/plan-1a-base)"
git status --short
git commit -m "feat(content): add Havenmere content and ship stats

Embed the Milestone 1a content as JSON with a generated C# catalog,
add Map, Sector, HullDef, CannonDef, AmmoDef, NpcDef and StatCaps tables,
compute ShipStats for a starter hull on load, replace Level and
Experience with MapRank and a private PlayerAccount row, and cover the
Math section 12 balance rules with tests.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01VVAL5L8X2uHhKGrESphkhF"
git log --oneline -3
```

`git status --short` before the commit must list only source, test, content, generated-binding, script and doc files. If `.cache/plan-1a-base` shows up, it is untracked and must stay so (do not add it).

- [ ] **Step 5: Final verification**

```bash
git -C /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg status --short
git -C /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg diff --stat "$(cat /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg/.cache/plan-1a-base)"..HEAD | tail -1
```

Expected: a clean tree and one commit ahead of the base.

- [ ] **Step 6: Push and open the draft milestone PR**

The branch was renamed to `leonardomso/milestone-1` before 1a started and has no remote counterpart yet. Push it and open a draft PR against `master`; later sub-phases push to the same branch and the PR goes ready after 1f.

```bash
cd /Users/leonardomaldonado/orca/workspaces/sea/hraesvelg
git branch --show-current
git push -u origin leonardomso/milestone-1
gh pr create --draft --base master --head leonardomso/milestone-1 \
  --title "Milestone 1: Havenmere, ships, and the vertical slice" \
  --body-file - <<'EOF'
## Summary

Milestone 1 of `PLAN.md`, landed one sub-phase per commit on this branch. Sub-phases are marked as they land.

- [x] 1a `feat(content)`: Havenmere content as JSON with a generated catalog; `MapDef`, `Sector`, `HullDef`, `CannonDef`, `AmmoDef`, `NpcDef`, `StatCaps`; `Hull` and `ShipStats` with add-then-cap; `MapRank` and a private `PlayerAccount` replace `Level`/`Experience`; Math section 12 tests.
- [ ] 1b
- [ ] 1c
- [ ] 1d
- [ ] 1e
- [ ] 1f

Design and decisions: `PLAN.md`, `docs/superpowers/plans/2026-09-02-milestone-1a-content-and-ship-stats.md`.

## Test plan

- [x] `pnpm check`
- [x] `pnpm server:test`
- [x] `pnpm runtime:test:world` and `pnpm server:test:integration` against a fresh `pnpm server:reset`
- [x] `pnpm unity:test`

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01VVAL5L8X2uHhKGrESphkhF
EOF
gh pr view --web=false
```

Expected: `git branch --show-current` prints `leonardomso/milestone-1`; the push succeeds; `gh pr create` prints the PR URL and `gh pr view` shows it as a draft. Report the URL to the user.
