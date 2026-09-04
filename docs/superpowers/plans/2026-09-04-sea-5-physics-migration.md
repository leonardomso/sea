# SEA_5 Physics Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `docs/SEA_5_PHYSICS.md` the one source of truth for how ships move, shoot and see, replacing the SEA_2_MATH movement and range numbers everywhere in the server, the content data and the Unity client, keeping only the 0.25 speed bonus cap from `stat_caps.json`.

**Architecture:** The world stops being measured in two units. Today a position is in "world units", a range is in "squares", and `SectorRules.SquareSizeUnits = 10` converts between them; that seam is the direct cause of at least one live bug. After this plan there is one unit, the square, the map is 400 x 400 squares with (0,0) at the top-left, and no conversion exists to get wrong. Movement stops being a physics integration and becomes route following: the server builds a waypoint route once, and every tick walks a fixed distance along it. Speed stops being spread across four files and becomes one pure function, `SpeedRules.Effective`. Land stops being a list of circles and becomes a 1-square bitmask that both A* and the drift check read.

**Tech Stack:** C# 12 / .NET 8 (SpacetimeDB module and the pure `Sea.Server.Domain` library), xUnit + FsCheck for server tests, Node for `scripts/generate-content.mjs`, Unity 6 (macOS + WebGL) with NUnit EditMode tests, pnpm as the task runner.

---

## Before you start

Read these, in this order. They are short and the plan assumes you know them.

1. `AGENTS.md` — the rules of this repository. The ones this plan leans on hardest:
   - Generated files (`ContentCatalog.g.cs`, the C# and TypeScript bindings) are **never** edited by hand. Change the JSON or the generator.
   - A handwritten C# file stays **at or under 500 lines**.
   - Pure rules live in `Domain/` and must compile without SpacetimeDB or Unity.
   - No full-table scans on a hot path. A ship row is written **once per tick**.
   - Deterministic replay must keep working: the same seed and the same command log produce the same state hash.
   - **Do not lower a gate to make it pass.**
   - Never add AI attribution to a commit, a pull request or a file.
2. `docs/SEA_5_PHYSICS.md` — the specification this plan implements. Section numbers below (§4, §5.1, §13) refer to it.
3. `docs/SEA_2_MATH.md` §2.4, §2.5, §3, §5.7, §7.1 — still authoritative for damage, reload, HP, armour, Combat Power and boarding scores.
4. `docs/STATUS.md` §4 — the two performance gates that are currently missed, and why.

### Which document wins

The user has decided this and it goes into the repository in Phase 0:

- **SEA_5_PHYSICS wins wherever it speaks.** Movement, speed, wind, storms, currents, heading, ranges, view distance, boarding distance, map edges, NPC distances, rate limits.
- **SEA_2_MATH stays authoritative wherever SEA_5 is silent.** Damage, reload, magazine, HP, armour values, Combat Power budget, boarding scores and haul, enemy multipliers, economy.
- **SEA_3_MECHANICS stays authoritative for outcomes.** What boarding *does* after the roll, what a map change into a Safe Haven looks like.
- **One exception, chosen by the user:** the speed bonus cap stays **0.25** (`stat_caps.json.speedBonusCap`), not SEA_5's 0.20. Phase 0 amends SEA_5 to say 0.25 so the document and the code agree.

### Where the numbers conflict, and who wins

| Thing | SEA_2_MATH | SEA_5_PHYSICS | Result |
|---|---|---|---|
| Map size | 20 x 20 squares, 10 units per square | 400 x 400 squares | **400 x 400 squares, 1 unit = 1 square** |
| Origin | centre, -100..+100 | top-left, 0..400 | **top-left** |
| Hull speed | 2.4 .. 1.8 sq/s | 5.6 .. 4.4 sq/s | **SEA_5** |
| Turn rate | 60 .. 32 deg/s | no turning at all | **SEA_5: the stat is deleted** |
| Acceleration | implied | none | **SEA_5: deleted** |
| Cannon range | 8 .. 10 sq | 18 .. 30 sq | **SEA_5** |
| Speed bonus cap | 0.25 | 0.20 | **0.25 (user's call), SEA_5 amended** |
| Wind | +/-15% on a random 0.2..0.8 strength, 30 s epoch | flat +/-10%, three fixed bands a day | **SEA_5** |
| Storm | cuts turn rate | cuts speed x0.85 | **SEA_5** |
| Combined environment | clamped to +/-25% | not clamped (§5.3 works out to 0.65x) | **SEA_5: the clamp is deleted** |
| Boarding cooldown | 30 s success / 60 s fail | 60 s player / 15 s NPC | **Both: 60 s after boarding a player, 15 s after an NPC, and SEA_3's "a player can be boarded at most once per 5 min" stays** |
| Critical hits | not mentioned | not mentioned | **New, user's call: 10% chance, x1.5 damage** |

### Two conventions this plan fixes in place

**The square is the unit.** After Phase 1 there is no such thing as a world unit. A position, a range, a radius and a distance are all in squares. `SectorRules.SquareSizeUnits` and `SectorRules.UnitsFromSquares` are deleted, not kept at 1.0, so nobody can reintroduce the conversion.

**Heading is a compass bearing.** 0 degrees is north (-Y, up the screen), 90 is east (+X), 180 is south, 270 is west. Direction from a heading is `x = sin(h)`, `y = -cos(h)`. This makes SEA_5 §13 test 14 ("ship sailed east, heading stays 90") true by construction. The old code used `atan2(dx, dy)` with `y = cos(h)`, which on a Y-down map pointed 0 degrees at south; every heading call site changes in Phase 1.

---

## File Structure

### Created

| File | Responsibility |
|---|---|
| `server/spacetimedb/spacetimedb/Domain/GeometryRules.cs` | Distance, squared distance, angle normalisation, heading between two points, segment-circle test. The one home for maths that four files currently each own a copy of. |
| `server/spacetimedb/spacetimedb/Domain/LandMask.cs` | A 1-square land bitmask for one map: cell lookup, segment line-of-sight (DDA), nearest-water search. |
| `server/spacetimedb/spacetimedb/Domain/RouteRules.cs` | The route type and the tick step: walk a fixed distance along a waypoint list. |
| `server/spacetimedb/spacetimedb/Domain/PathfindingRules.cs` | 8-direction A* on the land mask plus the string-pull that turns cells into <= 32 waypoints. |
| `server/spacetimedb/spacetimedb/Domain/SpeedRules.cs` | SEA_5 §5.1 as one pure function. The only place effective speed is computed. |
| `server/spacetimedb/spacetimedb/Domain/RangeRules.cs` | Range by tier, the +10% add-then-cap bonus, the 0.5 sq grace, view distance and subscription margin. |
| `server/spacetimedb/spacetimedb/Domain/CriticalHitRules.cs` | The deterministic 10% / x1.5 critical roll. |
| `server/spacetimedb/spacetimedb/Domain/MoveRateRules.cs` | The 8 `MoveTo` per second fixed-window limiter. |
| `server/spacetimedb/spacetimedb/Domain/TrustScoreRules.cs` | Score, penalties, recovery, review threshold. |
| `server/spacetimedb/spacetimedb/Domain/MapEdgeRules.cs` | The 6 sq edge band, the 8 sq entry inset, which edge leads where. |
| `server/spacetimedb/spacetimedb/Domain/BoardingRules.cs` | Distance and cooldown gates, the SEA_2 §5.7 score and chance, the SEA_3 §4.3 outcome. |
| `server/spacetimedb/spacetimedb/Simulation/RouteSystem.cs` | Building and storing a route on `MoveTo`; the reducer side of pathfinding. |
| `apps/game-unity/Assets/Domain/SeaRouteRules.cs` | The client mirror of `RouteRules`. |
| `server/spacetimedb/spacetimedb/Content/Data/maps/havenmere.json` etc. | One authored file per map. |
| `scripts/rasterize-maps.mjs` | Turns authored island/reef shapes into the generated land mask. |

### Modified

| File | Change |
|---|---|
| `Domain/WorldRules.cs` | 0..400 in squares, `SecondsPerTick`, the map-bounds helpers. |
| `Domain/SectorRules.cs` | Conversion deleted; a chart square is now `floor(x), floor(y)`. |
| `Domain/SpatialRules.cs` | Chunk size 50 sq so the 8 x 8 grid still covers the map. |
| `Domain/ChartCoordinates.cs` | Ruler recomputed for 400 sq and a Y-down origin. |
| `Domain/EnvironmentRules.cs` | Flat 0.10 wind on tick-derived 8-hour bands; storm and current constants. |
| `Domain/NavigationRules.cs` | Circle detour deleted; only the mask-backed helpers survive. |
| `Domain/EffectRules.cs` | `RangeLimitSquares` compared against a distance that is now genuinely in squares; the speed floor moves to `SpeedRules`. |
| `Domain/CommandPolicy.cs` | New rejection codes `NoPath` and `RateLimited`; boarding and map change become available. |
| `Domain/ContentDefinitions.cs` | `Width`/`Height` become `ushort`; `RangeSquares` becomes `byte` at 18..30; turn/acceleration stats removed; boarding fields added. |
| `Domain/ReplicationRules.cs` | Tolerances restated in squares; route-aware publishing. |
| `TacticalRules.cs` | Storm hits speed, not turn rate; the dead `WeaponEffectiveness` is removed; storm drift stops at the map edge instead of wrapping. |
| `CombatRules.cs` | Range grace, critical hits, ranges by tier. |
| `Schema/Tables.cs` | Ship loses the kinematic columns and gains route state; new `ShipRoute`, `MapLandMask`, `PlayerTrust`, `ChunkMovement` tables. |
| `Simulation/SailingSystem.cs` | Integration replaced by route stepping; drift applies to stopped ships and stops at land. |
| `apps/game-unity/Assets/Domain/SeaLocalShipPrediction.cs` | Predicts along the route with the server's effective speed. |
| `apps/game-unity/Assets/Presentation/SeaWorldView*.cs` | Draws the route, animates heading over 400 ms, uses the new ruler. |
| `Content/Data/*.json` | Five hulls, five cannons, three maps, new ranges and speeds. |

### Deleted

| File | Why |
|---|---|
| `Domain/SailingRules.cs` | `HandlingRules`, `SailingParameters`, `SailingState`, `AuthoritativeSailingStep`, the braking curve, the turn limiter and the thrust-alignment term are all SEA_5 §4.2 "no inertia". Nothing survives except helpers that move to `GeometryRules`. |
| `apps/game-unity/Assets/Domain/SeaSailingRules.cs` | The hand-maintained mirror of the above. Replaced by `SeaRouteRules.cs`. |
| `tests/.../SailingRulesTests.cs`, `ShipStopsAtTheMarkTests.cs` | They test behaviour SEA_5 removes. Replaced by `RouteRulesTests.cs`. |

---

## How to run things

```sh
pnpm ci:fast                 # static checks and repository invariants, no Docker
pnpm server:test             # the server unit, property and replay tests
pnpm content:generate        # regenerates ContentCatalog.g.cs from Content/Data
pnpm server:build            # builds the SpacetimeDB module
pnpm unity:test              # Unity EditMode tests
pnpm check                   # lint and format
pnpm verify                  # the normal gate
pnpm verify:full             # verify plus the four-client world and the 100-client scale run
```

To run one server test class:

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~RouteRulesTests"
```

A red build at the end of a task means the task is not done. Do not move to the next task.

---

# Phase 0 — Make the documents agree

Nothing compiles differently after this phase. It exists so that every later phase can point at a document instead of at a conversation.

### Task 0.1: Amend SEA_5 for the 0.25 speed bonus cap

**Files:**
- Modify: `docs/SEA_5_PHYSICS.md` (§4.4 table, §5.1 formula, §5.2 Bonuses row, §5.3 extremes, §13 test 10, §15 constants)

- [ ] **Step 1: Change the §5.1 formula line**

Find:

```
        × (1 + min(0.20, sum of speed bonuses))     add, then cap
```

Replace with:

```
        × (1 + min(0.25, sum of speed bonuses))     add, then cap
```

- [ ] **Step 2: Change the §5.2 Bonuses row**

Find `capped at **+20%**` and replace with `capped at **+25%** (this is the one number Sea keeps from SEA_2_MATH; see \`stat_caps.json.speedBonusCap\`)`.

- [ ] **Step 3: Change the §4.4 table's last column**

Replace the `Max with +20% cap` column header with `Max with +25% cap` and the five values with:

| Hull | Base speed (sq/s) | Max with +25% cap |
|---|---|---|
| Skiff | 5.6 | 7.00 |
| Sloop | 5.3 | 6.63 |
| Brig | 5.0 | 6.25 |
| Frigate | 4.7 | 5.88 |
| Galleon | 4.4 | 5.50 |

- [ ] **Step 4: Change the §5.3 fastest-ship line**

Find:

```
- Fastest possible ship: Skiff 5.6 × 1.20 × 1.10 = **7.39 sq/s**, plus up to 0.3 sq/s of current.
```

Replace with:

```
- Fastest possible ship: Skiff 5.6 × 1.25 × 1.10 = **7.70 sq/s**, plus up to 0.3 sq/s of current.
```

- [ ] **Step 5: Change §13 test 10**

Find `| 10 | Build with +35% speed bonuses | moves at exactly base × 1.20 |` and replace the expectation with `moves at exactly base × 1.25`.

- [ ] **Step 6: Change the §15 constant**

Find `SPEED_BONUS_CAP` and set its value to `0.25`.

- [ ] **Step 7: Commit**

```bash
git add docs/SEA_5_PHYSICS.md
git commit -m "docs(physics): keep the 0.25 speed bonus cap in SEA_5

SEA_5 was drafted with a 0.20 cap while stat_caps.json has shipped 0.25
since Milestone 1. The cap is the one SEA_2_MATH number Sea keeps, so the
document moves to the code rather than the other way round."
```

### Task 0.2: Write down which document wins

**Files:**
- Modify: `AGENTS.md`, `docs/SEA_5_PHYSICS.md`, `docs/SEA_2_MATH.md`, `docs/SEA_5_GAP_ANALYSIS.md`

- [ ] **Step 1: Add the authority note to `AGENTS.md`**

Add this section immediately after the existing documentation list:

```markdown
### Which design document wins

- `docs/SEA_5_PHYSICS.md` is authoritative for everything it covers: movement,
  effective speed, wind, storms, currents, heading and armour faces, ranges,
  view distance, boarding distance, map edges, NPC distances and the client
  rate limits.
- `docs/SEA_2_MATH.md` is authoritative wherever SEA_5 is silent: damage,
  reload, magazine, hit points, armour values, the Combat Power budget,
  boarding scores and haul, enemy multipliers and the economy.
- `docs/SEA_3_MECHANICS.md` is authoritative for what an action *does* once
  the physics have allowed it.
- One exception: the speed bonus cap is 0.25, from `stat_caps.json`.

If a number appears in two documents and this list does not settle it, the
code does not get written until a person settles it.
```

- [ ] **Step 2: Add a one-line banner to the top of `docs/SEA_5_PHYSICS.md`**

Directly under the title:

```markdown
> Authoritative for movement, speed, environment, ranges, view, edges and rate
> limits. SEA_2_MATH stays authoritative where this document is silent. See
> "Which design document wins" in `AGENTS.md`.
```

- [ ] **Step 3: Add the mirror banner to `docs/SEA_2_MATH.md`**

```markdown
> Authoritative for damage, reload, hit points, armour, Combat Power, boarding
> scores and the economy. SEA_5_PHYSICS overrides this document for movement,
> speed, environment, ranges and view. See "Which design document wins" in
> `AGENTS.md`.
```

- [ ] **Step 4: Add a banner to `docs/SEA_5_GAP_ANALYSIS.md`**

Two files now begin with `SEA_5`. Rather than renaming a file other documents
link to, say what this one is:

```markdown
> This is a resolution log, not a design document. It records gaps that were
> found and how they were closed. Nothing here overrides `SEA_5_PHYSICS.md`.
```

- [ ] **Step 5: Commit**

```bash
git add AGENTS.md docs/SEA_5_PHYSICS.md docs/SEA_2_MATH.md docs/SEA_5_GAP_ANALYSIS.md
git commit -m "docs: record which design document wins on a conflict

SEA_5_PHYSICS contradicts SEA_2_MATH in about fifteen places while its own
preamble said SEA_2_MATH wins. Splitting authority by subject rather than by
document removes the contradiction and gives every later change one place to
check."
```

### Task 0.3: Write down the decisions SEA_5 leaves open

SEA_5 §8.1 names a critical flag but leaves its numbers to SEA_2_MATH, which
never gives them. §5.2 and §12.5 date the wind bands off the wall clock, which
deterministic replay cannot use. §9 says when a boarding may start but not what
it does. §10.2 sends a crossing into a harbour map through "the countdown in
SEA_3", and SEA_3_MECHANICS has no such countdown. All four are decided; they
need to be in the document before code depends on them.

**Files:**
- Modify: `docs/SEA_5_PHYSICS.md` (§5.2, §8, §9.1, §10.2, §12.5, §15)

- [ ] **Step 1: Replace the §5.2 Wind row**

Find `Each map has one wind direction per time band (00:00 / 08:00 / 16:00 UTC)` and replace the sentence with:

```
Each map has one wind direction per 8-hour band. The band is derived from the
world tick counter, not from the wall clock: band = floor(tick / 288000), which
is 8 hours at 10 Hz. Wall-clock time cannot be used because a replay of the
same command log has to produce the same wind.
```

- [ ] **Step 2: Add a §8.4 for critical hits**

```markdown
### 8.4 Critical hits

| Rule | Value |
|---|---|
| Chance per volley | **10%** |
| Damage multiplier | **x1.5**, applied after armour |
| Who can crit | Players and NPCs alike |
| Determinism | The roll is a pure function of the world seed, the tick, the attacker id and the defender id. Two replays of the same log crit on the same volleys. |

The client shows a critical volley with a larger damage number. It never
predicts one; the number comes from the server with the damage.
```

- [ ] **Step 3: Point §9 at SEA_3 for the outcome**

Add to the end of §9.1:

```
What a successful or failed boarding *does* — the haul, the hands lost, the
3 s silence, the 10% of Max HP — is SEA_3_MECHANICS §4.3 and SEA_2_MATH §5.7.
This section only decides when boarding is physically allowed: within 4 sq,
off cooldown, target below the boarding threshold.

Cooldowns: 60 s after boarding a player, 15 s after boarding an NPC. SEA_3's
rule that a given player can be boarded at most once every 5 minutes still
applies and is a separate timer on the victim.
```

- [ ] **Step 4: Say what a crossing into a harbour map does**

Find `Changing into a harbor map uses the countdown in SEA_3.` in §10.2 and
replace it with:

```
Confirming is instant on every map. SEA_3 has no map-change countdown -- only
the duel countdown and the cast-off channel -- so there is nothing to defer to.
If one is ever added to SEA_3, it applies here.
```

And in §12.5, replace `Time bands (00:00 / 08:00 / 16:00 UTC)` with
`Time bands (every 288000 ticks, which is 8 hours at 10 Hz)`, for the same
replay reason as §5.2.

- [ ] **Step 5: Add the new constants to §15**

```
CRIT_CHANCE            0.10
CRIT_MULTIPLIER        1.5
WIND_BAND_TICKS        288000        (8 hours at 10 Hz)
BOARD_THRESHOLD        0.50          (fraction of the target's Max HP)
```

- [ ] **Step 6: Commit**

```bash
git add docs/SEA_5_PHYSICS.md
git commit -m "docs(physics): close the four gaps SEA_5 left open

Critical hits, the wind band clock, the boarding outcome and the harbour
crossing countdown were all decided in review but written down nowhere. Wind
and storms move to the tick counter because a replay of the same log has to
blow the same way, and the countdown SEA_5 defers to SEA_3 does not exist, so
a crossing is instant until it does."
```

---

# Phase 1 — One unit, one origin

This is the largest mechanical change in the plan and everything else waits on
it. At the end of it the world is 400 x 400 squares with (0,0) at the top-left,
there is no unit conversion anywhere, and headings are compass bearings.

The existing Havenmere content is scaled x20 here as a **bridge** so the build
stays green. Phase 8 throws that away and replaces it with hand-authored maps.
Do not spend time making the scaled map good.

### Task 1.1: Give the codebase one home for geometry

Four files own a private copy of `NormalizeAngle` and three own `Distance`.
Before anything moves, they get one home, so the later phases have one place
to change.

**Files:**
- Create: `server/spacetimedb/spacetimedb/Domain/GeometryRules.cs`
- Test: `server/spacetimedb/tests/GeometryRulesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class GeometryRulesTests
{
    [Theory]
    [InlineData(0f, -10f, 0f)]     // straight up the screen is north
    [InlineData(10f, 0f, 90f)]     // to the right is east
    [InlineData(0f, 10f, 180f)]    // down the screen is south
    [InlineData(-10f, 0f, 270f)]   // to the left is west
    public void HeadingToIsACompassBearing(float deltaX, float deltaY, float expected)
    {
        var heading = GeometryRules.HeadingTo(100f, 100f, 100f + deltaX, 100f + deltaY);

        Assert.Equal(expected, heading, 3);
    }

    [Fact]
    public void HeadingToHoldsTheOldBearingWhenThereIsNowhereToGo()
    {
        Assert.Equal(41f, GeometryRules.HeadingTo(5f, 5f, 5f, 5f, 41f), 3);
    }

    [Fact]
    public void DirectionRoundTripsThroughHeading()
    {
        var (x, y) = GeometryRules.Direction(90f);

        Assert.Equal(1f, x, 3);
        Assert.Equal(0f, y, 3);
    }

    [Theory]
    [InlineData(370f, 10f)]
    [InlineData(-10f, 350f)]
    [InlineData(720f, 0f)]
    public void NormalizeAngleLandsInZeroToThreeSixty(float input, float expected)
    {
        Assert.Equal(expected, GeometryRules.NormalizeAngle(input), 3);
    }

    [Theory]
    [InlineData(350f, -10f)]
    [InlineData(190f, -170f)]
    [InlineData(180f, 180f)]
    public void NormalizeSignedAngleLandsInMinusOneEightyToOneEighty(float input, float expected)
    {
        Assert.Equal(expected, GeometryRules.NormalizeSignedAngle(input), 3);
    }

    [Fact]
    public void DistanceIsPlainPythagoras()
    {
        Assert.Equal(5f, GeometryRules.Distance(0f, 0f, 3f, 4f), 4);
        Assert.Equal(25f, GeometryRules.DistanceSquared(0f, 0f, 3f, 4f), 4);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~GeometryRulesTests"
```

Expected: build error, `The name 'GeometryRules' does not exist in the current context`.

- [ ] **Step 3: Write `GeometryRules`**

```csharp
namespace Sea.Server;

/// <summary>
/// The plane the whole game is played on. One unit is one square; there is no
/// other unit. Heading is a compass bearing: 0 is north (up the screen, -Y),
/// 90 is east, 180 south, 270 west.
/// </summary>
/// <remarks>
/// Every angle and distance in the simulation comes through here. Four files
/// used to keep a private NormalizeAngle and three a private Distance, which is
/// how a Y-down map ended up with 0 degrees pointing south.
/// </remarks>
public static class GeometryRules
{
    private const float DegreesPerRadian = 180f / MathF.PI;
    private const float NoMovementSquared = 0.000001f;

    public static float Distance(float fromX, float fromY, float toX, float toY) =>
        MathF.Sqrt(DistanceSquared(fromX, fromY, toX, toY));

    public static float DistanceSquared(float fromX, float fromY, float toX, float toY)
    {
        var deltaX = toX - fromX;
        var deltaY = toY - fromY;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    /// <summary>
    /// The bearing from one point to another. When the two points are the same
    /// there is no bearing to give, so the caller's current one is kept: a ship
    /// that has arrived keeps pointing the way she came in.
    /// </summary>
    public static float HeadingTo(
        float fromX,
        float fromY,
        float toX,
        float toY,
        float currentHeadingDegrees = 0f)
    {
        var deltaX = toX - fromX;
        var deltaY = toY - fromY;
        if ((deltaX * deltaX) + (deltaY * deltaY) <= NoMovementSquared)
        {
            return NormalizeAngle(currentHeadingDegrees);
        }

        return NormalizeAngle(MathF.Atan2(deltaX, -deltaY) * DegreesPerRadian);
    }

    /// <summary>The unit vector a hull on <paramref name="headingDegrees"/> travels along.</summary>
    public static (float X, float Y) Direction(float headingDegrees) =>
        (TrigonometryRules.SinDegrees(headingDegrees), -TrigonometryRules.CosDegrees(headingDegrees));

    public static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        return angle < 0f ? angle + 360f : angle;
    }

    public static float NormalizeSignedAngle(float angle)
    {
        angle = NormalizeAngle(angle);
        return angle > 180f ? angle - 360f : angle;
    }

    public static bool SegmentIntersectsCircle(
        float startX,
        float startY,
        float endX,
        float endY,
        float centerX,
        float centerY,
        float radius)
    {
        var segmentX = endX - startX;
        var segmentY = endY - startY;
        var lengthSquared = (segmentX * segmentX) + (segmentY * segmentY);
        var projection = lengthSquared <= NoMovementSquared
            ? 0f
            : Math.Clamp(
                (((centerX - startX) * segmentX) + ((centerY - startY) * segmentY)) / lengthSquared,
                0f,
                1f);
        var closestX = startX + (segmentX * projection);
        var closestY = startY + (segmentY * projection);
        return DistanceSquared(closestX, closestY, centerX, centerY) < radius * radius;
    }
}
```

- [ ] **Step 4: Run the test and watch it pass**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~GeometryRulesTests"
```

Expected: `Passed! - Failed: 0, Passed: 12`.

- [ ] **Step 5: Commit**

```bash
git add server/spacetimedb/spacetimedb/Domain/GeometryRules.cs server/spacetimedb/tests/GeometryRulesTests.cs
git commit -m "feat(domain): give the simulation one home for geometry

Distance and angle helpers were copied into four files, each with its own
convention. Heading now means one thing everywhere: a compass bearing on a
Y-down chart."
```

### Task 1.2: Move the world to 400 x 400 squares from the top-left

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Domain/WorldRules.cs`
- Test: `server/spacetimedb/tests/WorldRulesTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `WorldRulesTests.cs`:

```csharp
[Fact]
public void TheMapIsFourHundredSquaresFromTheTopLeft()
{
    Assert.Equal(0f, WorldRules.MapMin);
    Assert.Equal(400f, WorldRules.MapMax);
    Assert.Equal(400f, WorldRules.MapSizeSquares);
}

[Fact]
public void ATickIsATenthOfASecond()
{
    Assert.Equal(0.1f, WorldRules.SecondsPerTick, 6);
}

[Theory]
[InlineData(0f, 0f, true)]
[InlineData(400f, 400f, true)]
[InlineData(-0.01f, 200f, false)]
[InlineData(200f, 400.01f, false)]
public void InsideTheMapIsZeroToFourHundredOnBothAxes(float x, float y, bool expected)
{
    Assert.Equal(expected, WorldRules.IsInsideMap(x, y));
}

[Fact]
public void ClampToMapPullsAPointBackInside()
{
    var (x, y) = WorldRules.ClampToMap(-5f, 900f);

    Assert.Equal(0f, x, 4);
    Assert.Equal(400f, y, 4);
}
```

- [ ] **Step 2: Run it and watch it fail**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~WorldRulesTests"
```

Expected: FAIL, `Assert.Equal() Failure: Expected 0, Actual -100`.

- [ ] **Step 3: Change the constants**

In `Domain/WorldRules.cs` replace the bounds block with:

```csharp
    /// <summary>
    /// Every map is this many squares on a side (SEA_5 §3.1). One square is one
    /// unit; there is no second unit and no conversion. (0,0) is the top-left
    /// corner, x grows east, y grows south.
    /// </summary>
    public const float MapSizeSquares = 400f;

    public const float MapMin = 0f;
    public const float MapMax = MapSizeSquares;

    public const uint TickRateHz = 10;

    /// <summary>How much time one tick of the simulation covers.</summary>
    public const float SecondsPerTick = 1f / TickRateHz;

    /// <summary>Water a ship may not be fired on in, in squares (SEA_5 §10.3).</summary>
    public const float HarborSafeRadiusSquares = 30f;

    public static bool IsInsideMap(float x, float y) =>
        x >= MapMin && x <= MapMax && y >= MapMin && y <= MapMax;

    public static (float X, float Y) ClampToMap(float x, float y) =>
        (Math.Clamp(x, MapMin, MapMax), Math.Clamp(y, MapMin, MapMax));
```

Delete `VisionRadius` (it moves to `RangeRules` in Phase 6) and `CollisionPadding`
(SEA_5 §4.1.6: ships never collide). Replace `WorldRules.Distance`,
`WorldRules.IsInRange` and `WorldRules.AdvanceTowards` bodies with calls into
`GeometryRules`, or delete them and fix the call sites; `IsInRange` stays because
combat uses it, and becomes:

```csharp
    public static bool IsInRange(float fromX, float fromY, float toX, float toY, float range) =>
        GeometryRules.DistanceSquared(fromX, fromY, toX, toY) <= range * range;
```

- [ ] **Step 4: Run the whole server suite and see exactly what broke**

```sh
pnpm server:test 2>&1 | tail -60
```

Expected: a long list of failures. That list is the work for the rest of this
phase. Save it:

```sh
pnpm server:test 2>&1 | grep -E "^\s+(Failed|X) " | sort -u > /tmp/phase1-failures.txt
wc -l /tmp/phase1-failures.txt
```

- [ ] **Step 5: Commit the constant change on its own**

```bash
git add server/spacetimedb/spacetimedb/Domain/WorldRules.cs server/spacetimedb/tests/WorldRulesTests.cs
git commit -m "feat(domain): move the world to 400 squares from the top-left

SEA_5 §3.1 and §3.3. The tree does not build after this commit; the next
commits in this phase take the conversion out of every call site."
```

### Task 1.3: Delete the unit conversion

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Domain/SectorRules.cs`
- Test: `server/spacetimedb/tests/SectorRulesTests.cs`

- [ ] **Step 1: Find every caller first**

```sh
grep -rn "SquareSizeUnits\|UnitsFromSquares" server apps scripts --include='*.cs' --include='*.mjs' --include='*.ts'
```

Write the list down. Each one is a place where a square was being multiplied
by ten; after this task each one is a plain read.

- [ ] **Step 2: Write the failing test**

Replace the conversion tests in `SectorRulesTests.cs` with:

```csharp
[Theory]
[InlineData(0f, 0f, 0, 0)]
[InlineData(0.9f, 0.9f, 0, 0)]
[InlineData(1f, 1f, 1, 1)]
[InlineData(399.9f, 399.9f, 399, 399)]
public void AChartSquareIsJustTheWholePartOfAPosition(float x, float y, int column, int row)
{
    Assert.Equal(column, SectorRules.Column(x));
    Assert.Equal(row, SectorRules.Row(y));
}

[Fact]
public void ThereIsNoConversionLeftToGetWrong()
{
    var conversions = typeof(SectorRules)
        .GetMembers()
        .Select(member => member.Name)
        .Where(name => name.Contains("Units", StringComparison.Ordinal))
        .ToArray();

    Assert.Empty(conversions);
}
```

- [ ] **Step 3: Run it and watch it fail**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~SectorRulesTests"
```

Expected: FAIL, `Assert.Empty() Failure` listing `SquareSizeUnits` and `UnitsFromSquares`.

- [ ] **Step 4: Delete them**

In `Domain/SectorRules.cs`, delete `SquareSizeUnits` and `UnitsFromSquares`
outright, set `OriginX` and `OriginY` to `0f`, and simplify:

```csharp
    /// <summary>
    /// The chart square a position falls in. A position is already in squares,
    /// so this is only the whole part of it.
    /// </summary>
    /// <remarks>
    /// This class used to be the one documented crossing between world units and
    /// squares. There is no crossing any more: SEA_5 §3.3 stores positions in
    /// squares on the server and on the wire, so the conversion has been deleted
    /// rather than set to 1.0, to stop anyone reintroducing it.
    /// </remarks>
    public static int Column(float x) => (int)MathF.Floor(x);

    public static int Row(float y) => (int)MathF.Floor(y);
```

Then fix every call site from Step 1. Every one of them is a deletion of
`* SectorRules.SquareSizeUnits` or of a `UnitsFromSquares(...)` wrapper.

- [ ] **Step 5: Run the sector tests**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~SectorRulesTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add server/spacetimedb/spacetimedb/Domain/SectorRules.cs server/spacetimedb/tests/SectorRulesTests.cs
git commit -m "refactor(domain): delete the square-to-unit conversion

SEA_5 §3.3 stores positions in squares everywhere, so the crossing point has
nothing left to convert. It is deleted rather than set to 1.0 so it cannot come
back: an ammunition range limit in squares was being compared against a
distance in units, which made grapeshot's slow-reload debuff unreachable."
```

### Task 1.4: Make the chunk grid cover 400 squares

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Domain/SpatialRules.cs`
- Test: `server/spacetimedb/tests/SpatialRulesTests.cs` (create if absent)

- [ ] **Step 1: Write the failing test**

```csharp
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class SpatialRulesTests
{
    [Fact]
    public void TheChunkGridCoversTheWholeMap()
    {
        Assert.Equal(
            WorldRules.MapSizeSquares,
            SpatialRules.ChunkSizeSquares * SpatialRules.ChunkCountPerAxis);
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(49.9f, 0)]
    [InlineData(50f, 1)]
    [InlineData(399.9f, 7)]
    [InlineData(400f, 7)]
    public void AChunkCoordinateNeverLeavesTheGrid(float position, int expected)
    {
        Assert.Equal(expected, SpatialRules.ChunkCoordinate(position));
    }

    [Fact]
    public void AShipSeesEveryChunkItsViewCanReach()
    {
        var bounds = SpatialRules.BoundsAround(200f, 200f, RangeRules.SubscriptionRadiusSquares);

        Assert.Equal(2, bounds.MinimumX);
        Assert.Equal(5, bounds.MaximumX);
        Assert.Equal(2, bounds.MinimumY);
        Assert.Equal(5, bounds.MaximumY);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~SpatialRulesTests"
```

Expected: FAIL, `Assert.Equal() Failure: Expected 400, Actual 200`.

- [ ] **Step 3: Change the chunk constants**

In `Domain/SpatialRules.cs`:

```csharp
    /// <summary>
    /// The map is cut into an 8 x 8 grid of chunks, so one chunk is 50 squares on
    /// a side. Keeping the count at eight keeps every chunk index and every
    /// subscription shape the module already has; only the size changes.
    /// </summary>
    public const float ChunkSizeSquares = 50f;

    public const int ChunkCountPerAxis = 8;

    /// <summary>The widest a storm reaches, so a chunk query can bound it (SEA_5 §5.2).</summary>
    public const float MaximumWorldInfluenceRadiusSquares = 40f;

    /// <summary>The widest a current zone reaches.</summary>
    public const float MaximumCurrentRadiusSquares = 40f;

    public static int ChunkCoordinate(float position) => Math.Clamp(
        (int)MathF.Floor(position / ChunkSizeSquares),
        0,
        ChunkCountPerAxis - 1);
```

`BoundsAround` and `BoundsForSegment` keep their shape; they only stop
subtracting the old `-100` origin.

- [ ] **Step 4: Run the test and watch it pass**

Expected: PASS. `RangeRules.SubscriptionRadiusSquares` does not exist yet, so
this test will not compile until Task 6.1. Add the constant now as a
placeholder-free stub in `Domain/RangeRules.cs`:

```csharp
namespace Sea.Server;

public static class RangeRules
{
    /// <summary>How far a captain can see, in squares (SEA_5 §7.5).</summary>
    public const float ViewDistanceSquares = 60f;

    /// <summary>
    /// Interest is subscribed a little wider than sight so a ship is already on
    /// the client when it becomes visible (SEA_5 §7.5).
    /// </summary>
    public const float SubscriptionMarginSquares = 5f;

    public const float SubscriptionRadiusSquares = ViewDistanceSquares + SubscriptionMarginSquares;
}
```

Task 6.1 fills the rest of this class in.

- [ ] **Step 5: Commit**

```bash
git add server/spacetimedb/spacetimedb/Domain/SpatialRules.cs server/spacetimedb/spacetimedb/Domain/RangeRules.cs server/spacetimedb/tests/SpatialRulesTests.cs
git commit -m "feat(domain): size the chunk grid for a 400-square map

One chunk becomes 50 squares so the 8x8 grid still covers the map and a 65 sq
interest radius still spans four chunks a side rather than the whole world."
```

### Task 1.5: Fix the chart ruler

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Domain/ChartCoordinates.cs`
- Modify: `apps/game-unity/Assets/Domain/SeaChartCoordinates.cs`
- Test: `server/spacetimedb/tests/ChartCoordinatesTests.cs`

The ruler is a letter-and-number grid the HUD and the `N` navigator use. It was
78 columns by 61 rows over 200 units, derived from the old bounds, and it read
its rows and columns the wrong way round. On a 400-square map a 40 x 40 ruler
gives one label per 10 squares, which is a readable chart square.

- [ ] **Step 1: Write the failing test**

Replace the whole of `server/spacetimedb/tests/ChartCoordinatesTests.cs` with:

```csharp
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class ChartCoordinatesTests
{
    [Fact]
    public void TheRulerIsFortyByForty()
    {
        Assert.Equal(40, ChartCoordinates.ColumnCount);
        Assert.Equal(40, ChartCoordinates.RowCount);
        Assert.Equal(10f, ChartCoordinates.CellWidthSquares, 4);
        Assert.Equal(10f, ChartCoordinates.CellHeightSquares, 4);
    }

    [Fact]
    public void ATopLeftPositionIsCellAOne()
    {
        Assert.Equal("A1", ChartCoordinates.LabelAt(0.5f, 0.5f));
    }

    [Fact]
    public void ABottomRightPositionIsTheLastCell()
    {
        Assert.Equal("AN40", ChartCoordinates.LabelAt(399.5f, 399.5f));
    }

    [Fact]
    public void ACellCentreRoundTripsBackToItsLabel()
    {
        Assert.True(ChartCoordinates.TryCellCenter("M12", out var x, out var y));
        Assert.Equal("M12", ChartCoordinates.LabelAt(x, y));
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~ChartCoordinatesTests"
```

Expected: FAIL, `Expected 40, Actual 78`.

- [ ] **Step 3: Change the ruler**

```csharp
    /// <summary>
    /// The chart ruler: forty columns lettered A..Z, AA..AN, forty rows numbered
    /// 1..40, so one ruler cell is ten squares. Columns run east from the
    /// left-hand edge and rows run south from the top, which is the same way the
    /// map is stored (SEA_5 §3.3).
    /// </summary>
    public const int ColumnCount = 40;

    public const int RowCount = 40;

    public const float CellWidthSquares = WorldRules.MapSizeSquares / ColumnCount;

    public const float CellHeightSquares = WorldRules.MapSizeSquares / RowCount;
```

`ColumnLabel(int index)` keeps its A..Z, AA.. behaviour. `LabelAt(x, y)` becomes
`ColumnLabel(clamped floor(x / CellWidthSquares)) + (clamped floor(y / CellHeightSquares) + 1)`.
`TryCellCenter` inverts that, returning the centre of the cell.

Mirror the same four constants and the same two methods in
`apps/game-unity/Assets/Domain/SeaChartCoordinates.cs`.

- [ ] **Step 4: Run both suites**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~ChartCoordinatesTests"
pnpm unity:test
```

Expected: both PASS.

- [ ] **Step 5: Commit**

```bash
git add server/spacetimedb/spacetimedb/Domain/ChartCoordinates.cs apps/game-unity/Assets/Domain/SeaChartCoordinates.cs server/spacetimedb/tests/ChartCoordinatesTests.cs
git commit -m "feat(chart): rule the 400-square map ten squares to a cell

The old ruler was derived from the -100..+100 bounds and read its rows off the
x axis. Forty by forty gives one label per ten squares, which is what the HUD
already calls a chart square."
```

### Task 1.6: Widen the content types and scale Havenmere as a bridge

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Domain/ContentDefinitions.cs`
- Modify: `server/spacetimedb/spacetimedb/Content/Data/maps.json`
- Modify: `scripts/generate-content.mjs`

`MapContent.Width` and `Height` are `byte`. 400 does not fit in a byte.

- [ ] **Step 1: Write the failing test**

Add to `server/spacetimedb/tests/ContentValidationTests.cs`:

```csharp
[Fact]
public void EveryMapIsFourHundredSquaresOnASide()
{
    foreach (var map in ContentCatalog.Content.Maps)
    {
        Assert.Equal(400, map.Width);
        Assert.Equal(400, map.Height);
    }
}

[Fact]
public void NoWorldObjectSitsOutsideItsMap()
{
    foreach (var map in ContentCatalog.Content.Maps)
    {
        foreach (var worldObject in map.Objects)
        {
            Assert.True(
                WorldRules.IsInsideMap(worldObject.PositionX, worldObject.PositionY),
                $"{map.Code} object {worldObject.Code} at " +
                $"({worldObject.PositionX}, {worldObject.PositionY}) is off the map");
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~ContentValidationTests"
```

Expected: FAIL, `Expected 400, Actual 20`.

- [ ] **Step 3: Widen the type**

In `Domain/ContentDefinitions.cs`:

```csharp
public sealed record MapContent
{
    public required byte MapId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }

    /// <summary>Squares across. SEA_5 §3.1 fixes this at 400 for every map.</summary>
    public required ushort Width { get; init; }

    /// <summary>Squares down.</summary>
    public required ushort Height { get; init; }

    public required float PortX { get; init; }
    public required float PortY { get; init; }
    public required float PortRadius { get; init; }
    public required IReadOnlyList<WorldObjectContent> Objects { get; init; }
    public required IReadOnlyList<CurrentContent> Currents { get; init; }
}
```

Delete `TerrainRows` from the record. The per-square grid is not authored any
more; Phase 2 generates it from the shapes and stores it as a bitmask.

- [ ] **Step 4: Scale the existing map by twenty as a bridge**

```sh
node -e '
const fs = require("fs");
const path = "server/spacetimedb/spacetimedb/Content/Data/maps.json";
const data = JSON.parse(fs.readFileSync(path, "utf8"));
const move = v => Math.round((v + 100) * 2 * 100) / 100;   // -100..100 -> 0..400
const grow = v => Math.round(v * 2 * 100) / 100;           // units -> squares, x2
for (const map of data.maps) {
  map.width = 400;
  map.height = 400;
  delete map.terrainRows;
  map.portX = move(map.portX);
  map.portY = move(map.portY);
  map.portRadius = grow(map.portRadius);
  for (const object of map.objects ?? []) {
    object.positionX = move(object.positionX);
    object.positionY = move(object.positionY);
    object.radius = grow(object.radius);
  }
  for (const current of map.currents ?? []) {
    current.positionX = move(current.positionX);
    current.positionY = move(current.positionY);
    current.radius = grow(current.radius);
    current.strength = Math.min(current.strength, 0.3);
  }
}
fs.writeFileSync(path, JSON.stringify(data, null, 2) + "\n");
'
```

This is a bridge, not the answer. Phase 8 deletes `maps.json` and writes three
maps by hand. Note in the commit body that it is temporary.

- [ ] **Step 5: Regenerate and run**

```sh
pnpm content:generate
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~ContentValidationTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add server/spacetimedb/spacetimedb/Domain/ContentDefinitions.cs server/spacetimedb/spacetimedb/Content/Data/maps.json server/spacetimedb/spacetimedb/Content/ContentCatalog.g.cs scripts/generate-content.mjs server/spacetimedb/tests/ContentValidationTests.cs
git commit -m "feat(content): widen a map to 400 squares on a side

Width and height were bytes, which cannot hold 400. Havenmere is scaled x20
here only so the tree builds; Phase 8 replaces every map by hand."
```

### Task 1.7: Get the tree green again

**Files:**
- Modify: whatever `/tmp/phase1-failures.txt` from Task 1.2 Step 4 lists.

- [ ] **Step 1: Work the list**

Almost every failure is one of four shapes:
1. A test asserting a position between -100 and 100. Move it into 0..400.
2. A test asserting a heading from the old `atan2(dx, dy)` convention. A course
   that used to read 90 now reads 90 only if it goes east; check the direction
   the test means and use the compass bearing.
3. A radius or range written in units. Divide by ten.
4. A call to a deleted member. Replace with the `GeometryRules` equivalent.

Do not change an assertion to match whatever the code now prints. Work out what
the test meant and write that.

- [ ] **Step 2: Run the whole suite**

```sh
pnpm server:test
```

Expected: `Passed! - Failed: 0`.

- [ ] **Step 3: Run the static gates**

```sh
pnpm ci:fast
```

Expected: exit 0.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "test(server): move every fixture onto the 400-square chart

Positions, radii and headings across the suite were written against the
-100..+100 world and the old south-facing zero bearing."
```

---

> ## Review gate after Phase 1
>
> Phase 1 touched almost every file in the module. Ask a person to run, over
> `HEAD~n..HEAD` for this phase:
>
> ```
> /thermo-nuclear-code-quality-review
> /improve-codebase-architecture
> ```
>
> Both skills carry `disable-model-invocation: true`; an agent cannot start
> them. Stop here, tell the user the phase is ready for review, and fix what
> comes back before starting Phase 2.

---

# Phase 2 — The land mask

Today land is a list of circles and movement asks "does this segment cross any
circle" with a LINQ `.Any()` over all of them. SEA_5 §4.1.5 wants A* on a
1-square grid, which needs a grid. This phase builds one: the authored shapes
stay (they are how a human draws an island and how the client renders it), and
the generator rasterises them into a bitmask that the simulation reads.

400 x 400 bits is 2,500 `ulong`s, 20 KB per map. It is loaded once at module
init and never written again.

### Task 2.1: The `LandMask` type

**Files:**
- Create: `server/spacetimedb/spacetimedb/Domain/LandMask.cs`
- Test: `server/spacetimedb/tests/LandMaskTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class LandMaskTests
{
    /// <summary>A 10 x 10 sea with a 2 x 2 island at cells (4,4)..(5,5).</summary>
    private static LandMask SmallSea()
    {
        var bits = new ulong[LandMask.WordCount(10)];
        var mask = new LandMask(10, bits);
        foreach (var cellY in new[] { 4, 5 })
        {
            foreach (var cellX in new[] { 4, 5 })
            {
                var index = (cellY * 10) + cellX;
                bits[index >> 6] |= 1UL << (index & 63);
            }
        }

        return mask;
    }

    [Fact]
    public void WaterIsWaterAndLandIsLand()
    {
        var mask = SmallSea();

        Assert.False(mask.IsLand(0.5f, 0.5f));
        Assert.True(mask.IsLand(4.5f, 4.5f));
        Assert.True(mask.IsLand(5.9f, 5.9f));
        Assert.False(mask.IsLand(6.1f, 6.1f));
    }

    [Fact]
    public void OutsideTheMapCountsAsLand()
    {
        var mask = SmallSea();

        Assert.True(mask.IsLand(-0.5f, 5f));
        Assert.True(mask.IsLand(5f, 10.5f));
    }

    [Fact]
    public void ASegmentClearOfTheIslandIsClear()
    {
        Assert.True(SmallSea().SegmentIsClear(0.5f, 0.5f, 9.5f, 0.5f));
    }

    [Fact]
    public void ASegmentThroughTheIslandIsNot()
    {
        Assert.False(SmallSea().SegmentIsClear(0.5f, 4.5f, 9.5f, 4.5f));
    }

    [Fact]
    public void ASegmentThatOnlyClipsTheIslandDiagonallyIsNot()
    {
        Assert.False(SmallSea().SegmentIsClear(0.5f, 0.5f, 9.5f, 9.5f));
    }

    [Fact]
    public void NearestWaterLeavesAPointThatIsAlreadyWaterAlone()
    {
        Assert.True(SmallSea().TryNearestWater(2f, 2f, 3f, out var x, out var y));
        Assert.Equal(2f, x, 4);
        Assert.Equal(2f, y, 4);
    }

    [Fact]
    public void NearestWaterMovesAPointOffTheIsland()
    {
        Assert.True(SmallSea().TryNearestWater(4.5f, 4.5f, 3f, out var x, out var y));

        Assert.False(SmallSea().IsLand(x, y));
        Assert.True(GeometryRules.Distance(4.5f, 4.5f, x, y) <= 3f);
    }

    [Fact]
    public void NearestWaterGivesUpWhenTheSearchIsTooSmall()
    {
        var bits = new ulong[LandMask.WordCount(4)];
        for (var index = 0; index < bits.Length; index++)
        {
            bits[index] = ulong.MaxValue;
        }

        Assert.False(new LandMask(4, bits).TryNearestWater(2f, 2f, 3f, out _, out _));
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~LandMaskTests"
```

Expected: build error, `The type or namespace name 'LandMask' could not be found`.

- [ ] **Step 3: Write `LandMask`**

```csharp
namespace Sea.Server;

/// <summary>
/// Where a map's land is, one bit per square. This is what movement means by
/// "land": the authored islands and reefs are shapes a person drew and the
/// client draws back, but nothing in the simulation asks a shape a question.
/// </summary>
/// <remarks>
/// A 400-square map is 160,000 bits, 2,500 words, 20 KB. It is built once when
/// content is loaded and never written again, so it can be shared by every
/// reader on the tick without a copy.
/// </remarks>
public sealed class LandMask
{
    private readonly ulong[] bits;

    public LandMask(int size, ulong[] bits)
    {
        ArgumentNullException.ThrowIfNull(bits);
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        var expected = WordCount(size);
        if (bits.Length != expected)
        {
            throw new ArgumentException(
                $"a {size} x {size} mask needs {expected} words, not {bits.Length}",
                nameof(bits));
        }

        Size = size;
        this.bits = bits;
    }

    /// <summary>Squares on a side.</summary>
    public int Size { get; }

    public static int WordCount(int size) => ((size * size) + 63) / 64;

    /// <summary>
    /// Whether a square is land. Anything off the map is land as well, so the
    /// map edge needs no separate check anywhere: a route cannot leave the sea
    /// and drift cannot push a hull past the border.
    /// </summary>
    public bool IsLandCell(int cellX, int cellY)
    {
        if (cellX < 0 || cellY < 0 || cellX >= Size || cellY >= Size)
        {
            return true;
        }

        var index = (cellY * Size) + cellX;
        return (bits[index >> 6] & (1UL << (index & 63))) != 0UL;
    }

    public bool IsLand(float x, float y) =>
        IsLandCell((int)MathF.Floor(x), (int)MathF.Floor(y));

    /// <summary>
    /// Whether a straight line from one point to another stays on water. This
    /// walks every square the line actually touches rather than sampling it, so
    /// a course cannot slip through the corner between two rocks.
    /// </summary>
    public bool SegmentIsClear(float startX, float startY, float endX, float endY)
    {
        var cellX = (int)MathF.Floor(startX);
        var cellY = (int)MathF.Floor(startY);
        if (IsLandCell(cellX, cellY))
        {
            return false;
        }

        var endCellX = (int)MathF.Floor(endX);
        var endCellY = (int)MathF.Floor(endY);
        var deltaX = endX - startX;
        var deltaY = endY - startY;
        var stepX = deltaX > 0f ? 1 : deltaX < 0f ? -1 : 0;
        var stepY = deltaY > 0f ? 1 : deltaY < 0f ? -1 : 0;
        var perCellX = stepX == 0 ? float.PositiveInfinity : MathF.Abs(1f / deltaX);
        var perCellY = stepY == 0 ? float.PositiveInfinity : MathF.Abs(1f / deltaY);
        var nextX = stepX == 0
            ? float.PositiveInfinity
            : (stepX > 0 ? cellX + 1 - startX : startX - cellX) * perCellX;
        var nextY = stepY == 0
            ? float.PositiveInfinity
            : (stepY > 0 ? cellY + 1 - startY : startY - cellY) * perCellY;

        while (cellX != endCellX || cellY != endCellY)
        {
            if (nextX < nextY)
            {
                cellX += stepX;
                nextX += perCellX;
            }
            else
            {
                cellY += stepY;
                nextY += perCellY;
            }

            if (IsLandCell(cellX, cellY))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The nearest square of water to a point, searched outward a ring at a
    /// time. SEA_5 §4.1.2 uses this to nudge a click that landed on an island;
    /// beyond <paramref name="searchSquares"/> the click is refused instead.
    /// </summary>
    public bool TryNearestWater(
        float x,
        float y,
        float searchSquares,
        out float waterX,
        out float waterY)
    {
        waterX = x;
        waterY = y;
        if (!IsLand(x, y))
        {
            return true;
        }

        var originX = (int)MathF.Floor(x);
        var originY = (int)MathF.Floor(y);
        var rings = (int)MathF.Ceiling(searchSquares);
        var bestSquared = searchSquares * searchSquares;
        var found = false;
        for (var ring = 1; ring <= rings; ring++)
        {
            for (var offsetY = -ring; offsetY <= ring; offsetY++)
            {
                for (var offsetX = -ring; offsetX <= ring; offsetX++)
                {
                    if (Math.Max(Math.Abs(offsetX), Math.Abs(offsetY)) != ring)
                    {
                        continue;
                    }

                    var cellX = originX + offsetX;
                    var cellY = originY + offsetY;
                    if (IsLandCell(cellX, cellY))
                    {
                        continue;
                    }

                    var centerX = cellX + 0.5f;
                    var centerY = cellY + 0.5f;
                    var distanceSquared = GeometryRules.DistanceSquared(x, y, centerX, centerY);
                    if (distanceSquared >= bestSquared)
                    {
                        continue;
                    }

                    bestSquared = distanceSquared;
                    waterX = centerX;
                    waterY = centerY;
                    found = true;
                }
            }

            if (found)
            {
                return true;
            }
        }

        return false;
    }
}
```

- [ ] **Step 4: Run the test and watch it pass**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~LandMaskTests"
```

Expected: `Passed! - Failed: 0, Passed: 8`.

- [ ] **Step 5: Commit**

```bash
git add server/spacetimedb/spacetimedb/Domain/LandMask.cs server/spacetimedb/tests/LandMaskTests.cs
git commit -m "feat(domain): give a map a one-square land mask

SEA_5 §4.1.5 routes on a one-square grid, so land has to be a grid. Twenty
kilobytes a map, read-only after load, and a line-of-sight walk that steps
every square the line touches instead of sampling it."
```

### Task 2.2: Rasterise the authored shapes into the mask

**Files:**
- Create: `scripts/rasterize-maps.mjs`
- Modify: `scripts/generate-content.mjs`
- Modify: `server/spacetimedb/spacetimedb/Domain/ContentDefinitions.cs`

- [ ] **Step 1: Write the rasteriser**

```javascript
// scripts/rasterize-maps.mjs
//
// Turns the island and reef shapes a person authored into the one-square land
// mask the simulation reads. Shapes stay authored because that is how a human
// draws a coastline and how the client renders one; the mask exists because
// A* needs a grid. They are the same land, expressed twice, and this file is
// the only place the two are allowed to disagree.

/** Object codes that stop a hull. Shoals slow a hull; they do not block it. */
const BLOCKING_CODES = new Set(["island", "reef"]);

/**
 * @param {{width: number, height: number, objects: Array<{code: string, positionX: number, positionY: number, radius: number}>}} map
 * @returns {BigUint64Array} one bit per square, row-major, bit set means land
 */
export function rasterizeMap(map) {
  const size = map.width;
  if (map.width !== map.height) {
    throw new Error(`${map.code}: the mask assumes a square map`);
  }

  const words = new BigUint64Array(Math.ceil((size * size) / 64));
  const setLand = (cellX, cellY) => {
    if (cellX < 0 || cellY < 0 || cellX >= size || cellY >= size) return;
    const index = cellY * size + cellX;
    words[index >> 6] |= 1n << BigInt(index & 63);
  };

  for (const shape of map.objects ?? []) {
    if (!BLOCKING_CODES.has(shape.code)) continue;
    const minX = Math.max(0, Math.floor(shape.positionX - shape.radius));
    const maxX = Math.min(size - 1, Math.ceil(shape.positionX + shape.radius));
    const minY = Math.max(0, Math.floor(shape.positionY - shape.radius));
    const maxY = Math.min(size - 1, Math.ceil(shape.positionY + shape.radius));
    for (let cellY = minY; cellY <= maxY; cellY++) {
      for (let cellX = minX; cellX <= maxX; cellX++) {
        // A square is land when its centre is inside the shape. Half a square
        // of slop at a coastline is invisible at 32 px a square and it keeps
        // the mask from swallowing the water beside a rock.
        const deltaX = cellX + 0.5 - shape.positionX;
        const deltaY = cellY + 0.5 - shape.positionY;
        if (deltaX * deltaX + deltaY * deltaY <= shape.radius * shape.radius) {
          setLand(cellX, cellY);
        }
      }
    }
  }

  return words;
}

/** The mask as the C# initialiser text ContentCatalog.g.cs embeds. */
export function maskLiteral(words) {
  return Array.from(words, (word) => `0x${word.toString(16)}UL`).join(", ");
}
```

- [ ] **Step 2: Emit the mask from the generator**

In `scripts/generate-content.mjs`, import the rasteriser and add a `LandMask`
property to every generated map:

```javascript
import { rasterizeMap, maskLiteral } from "./rasterize-maps.mjs";

// ... inside the map emitter, after Currents:
const mask = rasterizeMap(map);
lines.push(`            LandMaskSize = ${map.width},`);
lines.push(`            LandMaskBits = new ulong[] { ${maskLiteral(mask)} },`);
```

- [ ] **Step 3: Add the fields to `MapContent`**

```csharp
    /// <summary>Squares on a side of the generated land mask; equal to Width.</summary>
    public required int LandMaskSize { get; init; }

    /// <summary>
    /// The generated land mask, one bit per square. Produced by
    /// scripts/rasterize-maps.mjs from Objects; never authored by hand.
    /// </summary>
    public required IReadOnlyList<ulong> LandMaskBits { get; init; }
```

- [ ] **Step 4: Write the test that ties the two representations together**

Add to `server/spacetimedb/tests/ContentValidationTests.cs`:

```csharp
[Fact]
public void TheGeneratedMaskAgreesWithTheAuthoredIslands()
{
    foreach (var map in ContentCatalog.Content.Maps)
    {
        var mask = ContentCatalog.LandMaskFor(map.MapId);
        foreach (var shape in map.Objects)
        {
            if (shape.Code is not ("island" or "reef"))
            {
                continue;
            }

            Assert.True(
                mask.IsLand(shape.PositionX, shape.PositionY),
                $"{map.Code}: the centre of {shape.Code} at " +
                $"({shape.PositionX}, {shape.PositionY}) is not land in the mask");
        }
    }
}

[Fact]
public void EveryHarbourSitsOnOpenWater()
{
    foreach (var map in ContentCatalog.Content.Maps)
    {
        Assert.False(
            ContentCatalog.LandMaskFor(map.MapId).IsLand(map.PortX, map.PortY),
            $"{map.Code}: the harbour is inside land");
    }
}
```

`ContentCatalog.LandMaskFor` is a small hand-written helper beside the generated
file — it is not generated, so it lives in
`server/spacetimedb/spacetimedb/Content/ContentCatalogMasks.cs`:

```csharp
namespace Sea.Server;

/// <summary>
/// The land masks, built once from generated content and then read-only.
/// </summary>
public static partial class ContentCatalog
{
    private static readonly Dictionary<byte, LandMask> Masks = BuildMasks();

    public static LandMask LandMaskFor(byte mapId) => Masks[mapId];

    private static Dictionary<byte, LandMask> BuildMasks()
    {
        var masks = new Dictionary<byte, LandMask>();
        foreach (var map in Content.Maps)
        {
            masks[map.MapId] = new LandMask(map.LandMaskSize, map.LandMaskBits.ToArray());
        }

        return masks;
    }
}
```

- [ ] **Step 5: Regenerate and run**

```sh
pnpm content:generate
pnpm quality:content
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~ContentValidationTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add scripts/rasterize-maps.mjs scripts/generate-content.mjs server/spacetimedb/spacetimedb/Domain/ContentDefinitions.cs server/spacetimedb/spacetimedb/Content/ContentCatalogMasks.cs server/spacetimedb/spacetimedb/Content/ContentCatalog.g.cs server/spacetimedb/tests/ContentValidationTests.cs
git commit -m "feat(content): generate a land mask from the authored islands

Nobody types 160,000 characters of terrain by hand. Islands stay drawn as
shapes and the generator turns them into the grid A* needs, so the two can
never drift apart."
```

---

# Phase 3 — Routing

SEA_5 §4.1.5: straight line if it is clear, otherwise 8-direction A* on the
1-square grid with diagonal cost sqrt(2), string-pulled into as few water-only
segments as possible, at most 32 waypoints, and `NO_PATH` if there is none.

Performance matters here more than anywhere else in the plan: this runs on a
player command, up to 8 times a second per ship, on the same thread as the tick.
Three things keep it cheap:

1. **The straight-line check comes first** and answers almost every request
   without touching A* at all.
2. **Scratch buffers are allocated once** and reused, with a generation stamp
   instead of clearing 160,000 floats per call.
3. **There is a hard expansion budget.** Past it the request is refused rather
   than allowed to eat a tick.

### Task 3.1: The route type and the tick step

**Files:**
- Create: `server/spacetimedb/spacetimedb/Domain/RouteRules.cs`
- Test: `server/spacetimedb/tests/RouteRulesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class RouteRulesTests
{
    private static readonly RouteWaypoint[] StraightEast =
    {
        new(250f, 50f),
    };

    private static readonly RouteWaypoint[] Dogleg =
    {
        new(60f, 50f),
        new(60f, 90f),
    };

    [Fact]
    public void OneTickWalksExactlyOneTicksWorthOfSea()
    {
        var step = RouteRules.Advance(StraightEast, 0, 50f, 50f, 0f, 0.5f);

        Assert.Equal(50.5f, step.PositionX, 4);
        Assert.Equal(50f, step.PositionY, 4);
        Assert.Equal(90f, step.HeadingDegrees, 3);
        Assert.False(step.Arrived);
        Assert.Equal(0, step.WaypointIndex);
    }

    [Fact]
    public void ATickThatOvershootsAWaypointCarriesOnDownTheNextLeg()
    {
        // Sitting 1 square short of the corner with 3 squares of travel: 1 east
        // to the corner, then 2 south.
        var step = RouteRules.Advance(Dogleg, 0, 59f, 50f, 90f, 3f);

        Assert.Equal(60f, step.PositionX, 4);
        Assert.Equal(52f, step.PositionY, 4);
        Assert.Equal(180f, step.HeadingDegrees, 3);
        Assert.Equal(1, step.WaypointIndex);
        Assert.False(step.Arrived);
    }

    [Fact]
    public void TheLastWaypointStopsTheShipExactlyOnIt()
    {
        var step = RouteRules.Advance(StraightEast, 0, 249f, 50f, 90f, 5f);

        Assert.Equal(250f, step.PositionX, 4);
        Assert.Equal(50f, step.PositionY, 4);
        Assert.True(step.Arrived);
        Assert.Equal(1, step.WaypointIndex);
    }

    [Fact]
    public void AShipWithNoTravelLeftKeepsHerPlaceAndHerBearing()
    {
        var step = RouteRules.Advance(StraightEast, 0, 100f, 50f, 90f, 0f);

        Assert.Equal(100f, step.PositionX, 4);
        Assert.Equal(90f, step.HeadingDegrees, 3);
        Assert.False(step.Arrived);
    }

    [Fact]
    public void AFinishedRouteReportsArrivedAndKeepsTheOldBearing()
    {
        var step = RouteRules.Advance(StraightEast, 1, 250f, 50f, 90f, 5f);

        Assert.True(step.Arrived);
        Assert.Equal(250f, step.PositionX, 4);
        Assert.Equal(90f, step.HeadingDegrees, 3);
    }

    [Fact]
    public void ABrigSailsTwoHundredSquaresInFortySecondsAtFiveASecond()
    {
        var positionX = 50f;
        var positionY = 50f;
        var heading = 0f;
        var index = 0;
        var arrived = false;
        for (var tick = 0; tick < 400 && !arrived; tick++)
        {
            var step = RouteRules.Advance(
                StraightEast, index, positionX, positionY, heading, 5f * WorldRules.SecondsPerTick);
            positionX = step.PositionX;
            positionY = step.PositionY;
            heading = step.HeadingDegrees;
            index = step.WaypointIndex;
            arrived = step.Arrived;

            if (tick == 99)
            {
                // SEA_5 §13 test 1: 10.0 s in, x = 100 within 0.05.
                Assert.Equal(100f, positionX, 2);
            }
        }

        Assert.True(arrived);
        Assert.Equal(250f, positionX, 3);
        Assert.Equal(50f, positionY, 3);
    }

    [Fact]
    public void TravelCannotBeNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RouteRules.Advance(StraightEast, 0, 50f, 50f, 0f, -1f));
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~RouteRulesTests"
```

Expected: build error, `The type or namespace name 'RouteWaypoint' could not be found`.

- [ ] **Step 3: Write `RouteRules`**

```csharp
namespace Sea.Server;

/// <summary>One corner of a course, in squares.</summary>
public readonly record struct RouteWaypoint(float X, float Y);

/// <summary>Where a hull is after one tick of following her course.</summary>
public readonly record struct RouteStep(
    float PositionX,
    float PositionY,
    float HeadingDegrees,
    int WaypointIndex,
    bool Arrived);

/// <summary>
/// Following a course. A ship holds an ordered list of waypoints and walks a
/// fixed distance along it each tick, corner to corner, in straight lines.
/// </summary>
/// <remarks>
/// This replaces the whole of the old SailingRules: there is no acceleration,
/// no braking curve, no turning circle and no arrival radius, because SEA_5
/// §4.1.3 says position is exact linear interpolation and §4.2 says the game
/// has no inertia at all. A hull that reaches her last waypoint is standing on
/// it, not near it.
/// </remarks>
public static class RouteRules
{
    /// <summary>SEA_5 §4.1.5. A longer course than this is refused.</summary>
    public const int MaximumWaypoints = 32;

    public static RouteStep Advance(
        ReadOnlySpan<RouteWaypoint> route,
        int waypointIndex,
        float positionX,
        float positionY,
        float headingDegrees,
        float travel)
    {
        if (!float.IsFinite(travel) || travel < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(travel));
        }

        var heading = GeometryRules.NormalizeAngle(headingDegrees);
        var index = Math.Max(0, waypointIndex);
        while (index < route.Length)
        {
            var corner = route[index];
            var deltaX = corner.X - positionX;
            var deltaY = corner.Y - positionY;
            var remaining = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (remaining > 0f)
            {
                heading = GeometryRules.HeadingTo(positionX, positionY, corner.X, corner.Y, heading);
            }

            if (remaining > travel)
            {
                var fraction = travel / remaining;
                return new RouteStep(
                    positionX + (deltaX * fraction),
                    positionY + (deltaY * fraction),
                    heading,
                    index,
                    false);
            }

            // She passes the corner this tick, so she is put on it exactly and
            // the rest of the tick is spent on the next leg. Rounding a corner
            // costs nothing: SEA_5 §4.1.7, reversing is instant.
            positionX = corner.X;
            positionY = corner.Y;
            travel -= remaining;
            index++;
        }

        return new RouteStep(positionX, positionY, heading, index, true);
    }
}
```

- [ ] **Step 4: Run the test and watch it pass**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~RouteRulesTests"
```

Expected: `Passed! - Failed: 0, Passed: 7`.

- [ ] **Step 5: Commit**

```bash
git add server/spacetimedb/spacetimedb/Domain/RouteRules.cs server/spacetimedb/tests/RouteRulesTests.cs
git commit -m "feat(domain): follow a course instead of integrating one

SEA_5 §4.1.3 makes position exact linear interpolation along a waypoint list.
A tick walks a fixed distance and rounds as many corners as that distance
reaches, so a course takes exactly length / speed seconds however it bends."
```

### Task 3.2: A* and the string pull

**Files:**
- Create: `server/spacetimedb/spacetimedb/Domain/PathfindingRules.cs`
- Test: `server/spacetimedb/tests/PathfindingRulesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class PathfindingRulesTests
{
    private const int Size = 64;

    private static LandMask WithWall(int wallX, int gapY, int gapHeight)
    {
        var bits = new ulong[LandMask.WordCount(Size)];
        for (var cellY = 0; cellY < Size; cellY++)
        {
            if (cellY >= gapY && cellY < gapY + gapHeight)
            {
                continue;
            }

            var index = (cellY * Size) + wallX;
            bits[index >> 6] |= 1UL << (index & 63);
        }

        return new LandMask(Size, bits);
    }

    private static LandMask OpenSea() => new(Size, new ulong[LandMask.WordCount(Size)]);

    private static LandMask WalledLake()
    {
        var bits = new ulong[LandMask.WordCount(Size)];
        var mask = new LandMask(Size, bits);
        void Fill(int fromX, int fromY, int toX, int toY)
        {
            for (var cellY = fromY; cellY <= toY; cellY++)
            {
                for (var cellX = fromX; cellX <= toX; cellX++)
                {
                    var index = (cellY * Size) + cellX;
                    bits[index >> 6] |= 1UL << (index & 63);
                }
            }
        }

        Fill(40, 40, 50, 50);          // an island
        Fill(44, 44, 46, 46);          // ... with a lake cut back out of it
        for (var cellY = 44; cellY <= 46; cellY++)
        {
            for (var cellX = 44; cellX <= 46; cellX++)
            {
                var index = (cellY * Size) + cellX;
                bits[index >> 6] &= ~(1UL << (index & 63));
            }
        }

        return mask;
    }

    [Fact]
    public void OpenWaterIsOneSegment()
    {
        Span<RouteWaypoint> route = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];
        var scratch = new PathfindingScratch(Size);

        var outcome = PathfindingRules.TryBuildRoute(
            OpenSea(), scratch, 4f, 4f, 60f, 60f, route, out var count);

        Assert.Equal(PathOutcome.Direct, outcome);
        Assert.Equal(1, count);
        Assert.Equal(60f, route[0].X, 4);
        Assert.Equal(60f, route[0].Y, 4);
    }

    [Fact]
    public void AWallIsRoundedThroughItsGap()
    {
        Span<RouteWaypoint> route = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];
        var scratch = new PathfindingScratch(Size);
        var mask = WithWall(wallX: 32, gapY: 50, gapHeight: 4);

        var outcome = PathfindingRules.TryBuildRoute(
            mask, scratch, 4f, 4f, 60f, 4f, route, out var count);

        Assert.Equal(PathOutcome.Routed, outcome);
        Assert.InRange(count, 2, RouteRules.MaximumWaypoints);

        var fromX = 4f;
        var fromY = 4f;
        for (var index = 0; index < count; index++)
        {
            Assert.True(
                mask.SegmentIsClear(fromX, fromY, route[index].X, route[index].Y),
                $"leg {index} crosses land");
            fromX = route[index].X;
            fromY = route[index].Y;
        }

        Assert.Equal(60f, route[count - 1].X, 3);
        Assert.Equal(4f, route[count - 1].Y, 3);
    }

    [Fact]
    public void ALandLockedLakeIsRefused()
    {
        Span<RouteWaypoint> route = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];
        var scratch = new PathfindingScratch(Size);

        var outcome = PathfindingRules.TryBuildRoute(
            WalledLake(), scratch, 4f, 4f, 45.5f, 45.5f, route, out var count);

        Assert.Equal(PathOutcome.NoPath, outcome);
        Assert.Equal(0, count);
    }

    [Fact]
    public void AGoalOnLandIsRefused()
    {
        Span<RouteWaypoint> route = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];
        var scratch = new PathfindingScratch(Size);

        var outcome = PathfindingRules.TryBuildRoute(
            WithWall(32, 50, 4), scratch, 4f, 4f, 32.5f, 10.5f, route, out var count);

        Assert.Equal(PathOutcome.NoPath, outcome);
        Assert.Equal(0, count);
    }

    [Fact]
    public void TheSameRequestTwiceGivesTheSameRoute()
    {
        Span<RouteWaypoint> first = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];
        Span<RouteWaypoint> second = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];
        var scratch = new PathfindingScratch(Size);
        var mask = WithWall(32, 50, 4);

        PathfindingRules.TryBuildRoute(mask, scratch, 4f, 4f, 60f, 4f, first, out var firstCount);
        PathfindingRules.TryBuildRoute(mask, scratch, 4f, 4f, 60f, 4f, second, out var secondCount);

        Assert.Equal(firstCount, secondCount);
        for (var index = 0; index < firstCount; index++)
        {
            Assert.Equal(first[index], second[index]);
        }
    }

    [Fact]
    public void ScratchIsReusedRatherThanReallocated()
    {
        var scratch = new PathfindingScratch(Size);
        var mask = WithWall(32, 50, 4);
        Span<RouteWaypoint> route = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];

        PathfindingRules.TryBuildRoute(mask, scratch, 4f, 4f, 60f, 4f, route, out _);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var run = 0; run < 50; run++)
        {
            PathfindingRules.TryBuildRoute(mask, scratch, 4f, 4f, 60f, 4f, route, out _);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~PathfindingRulesTests"
```

Expected: build error, `The type or namespace name 'PathfindingScratch' could not be found`.

- [ ] **Step 3: Write the scratch**

```csharp
namespace Sea.Server;

/// <summary>
/// The working memory one A* search needs, sized for one map and reused for
/// every search on it.
/// </summary>
/// <remarks>
/// A 400-square map is 160,000 cells. Clearing four arrays that size on every
/// MoveTo would cost more than the search, so each cell carries the search
/// number that last wrote it and anything stamped with an older number reads
/// as untouched.
/// </remarks>
public sealed class PathfindingScratch
{
    internal readonly float[] Cost;
    internal readonly uint[] Stamp;
    internal readonly int[] CameFrom;
    internal readonly bool[] Closed;
    internal readonly int[] HeapCell;
    internal readonly float[] HeapScore;
    internal readonly RouteWaypoint[] Corners;
    internal uint Search;
    internal int HeapCount;

    public PathfindingScratch(int size)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        Size = size;
        var cells = size * size;
        Cost = new float[cells];
        Stamp = new uint[cells];
        CameFrom = new int[cells];
        Closed = new bool[cells];
        HeapCell = new int[cells];
        HeapScore = new float[cells];
        Corners = new RouteWaypoint[MaximumCorners];
    }

    /// <summary>The longest raw cell path a search will reconstruct.</summary>
    public const int MaximumCorners = 4096;

    public int Size { get; }
}
```

- [ ] **Step 4: Write `PathfindingRules`**

```csharp
namespace Sea.Server;

public enum PathOutcome : byte
{
    /// <summary>The straight line was clear; the route is one segment.</summary>
    Direct = 0,

    /// <summary>A* found a way round; the route has two or more segments.</summary>
    Routed = 1,

    /// <summary>There is no way there (SEA_5 §4.1.5, rejected with NO_PATH).</summary>
    NoPath = 2,
}

/// <summary>
/// Plotting a course round land: SEA_5 §4.1.5. The straight line is tried
/// first, then eight-direction A* on the one-square mask with diagonal cost
/// sqrt(2), then the cell path is pulled straight into as few water-only legs
/// as will fit in 32 waypoints.
/// </summary>
/// <remarks>
/// This runs on a player's command, on the tick thread, up to eight times a
/// second per ship. The straight-line test answers the overwhelming majority
/// of requests without a search at all; a search that has not finished within
/// <see cref="MaximumExpansions"/> cells is refused rather than allowed to
/// spend a tick, because a course that hard is a course into a lake.
/// </remarks>
public static class PathfindingRules
{
    /// <summary>How far a click may be nudged off land onto water (SEA_5 §4.1.2).</summary>
    public const float NudgeSearchSquares = 3f;

    /// <summary>The cell budget for one search.</summary>
    public const int MaximumExpansions = 20000;

    private const float DiagonalCost = 1.41421356f;

    private static readonly int[] NeighbourX = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] NeighbourY = { 0, 1, 1, 1, 0, -1, -1, -1 };
    private static readonly float[] NeighbourCost =
    {
        1f, DiagonalCost, 1f, DiagonalCost, 1f, DiagonalCost, 1f, DiagonalCost,
    };

    public static PathOutcome TryBuildRoute(
        LandMask mask,
        PathfindingScratch scratch,
        float startX,
        float startY,
        float goalX,
        float goalY,
        Span<RouteWaypoint> route,
        out int count)
    {
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentNullException.ThrowIfNull(scratch);
        count = 0;
        if (mask.Size != scratch.Size)
        {
            throw new ArgumentException("the scratch was sized for another map", nameof(scratch));
        }

        if (mask.IsLand(goalX, goalY) || mask.IsLand(startX, startY))
        {
            return PathOutcome.NoPath;
        }

        if (mask.SegmentIsClear(startX, startY, goalX, goalY))
        {
            route[0] = new RouteWaypoint(goalX, goalY);
            count = 1;
            return PathOutcome.Direct;
        }

        var cells = Search(mask, scratch, startX, startY, goalX, goalY);
        if (cells <= 0)
        {
            return PathOutcome.NoPath;
        }

        count = StringPull(mask, scratch.Corners.AsSpan(0, cells), route);
        return count == 0 ? PathOutcome.NoPath : PathOutcome.Routed;
    }

    /// <summary>
    /// A* from the start cell to the goal cell. Writes the cell path, start
    /// point first and goal point last, into the scratch corners and returns
    /// how many points it wrote, or 0 when there is no way through.
    /// </summary>
    private static int Search(
        LandMask mask,
        PathfindingScratch scratch,
        float startX,
        float startY,
        float goalX,
        float goalY)
    {
        var size = mask.Size;
        var startCell = (((int)MathF.Floor(startY)) * size) + (int)MathF.Floor(startX);
        var goalCell = (((int)MathF.Floor(goalY)) * size) + (int)MathF.Floor(goalX);
        unchecked
        {
            scratch.Search++;
        }

        var search = scratch.Search;
        scratch.HeapCount = 0;
        scratch.Cost[startCell] = 0f;
        scratch.Stamp[startCell] = search;
        scratch.CameFrom[startCell] = -1;
        scratch.Closed[startCell] = false;
        HeapPush(scratch, startCell, Heuristic(startCell, goalCell, size));

        var expansions = 0;
        var found = false;
        while (scratch.HeapCount > 0)
        {
            var cell = HeapPop(scratch);
            if (cell == goalCell)
            {
                found = true;
                break;
            }

            if (scratch.Stamp[cell] == search && scratch.Closed[cell])
            {
                continue;
            }

            scratch.Closed[cell] = true;
            if (++expansions > MaximumExpansions)
            {
                return 0;
            }

            var cellX = cell % size;
            var cellY = cell / size;
            for (var direction = 0; direction < 8; direction++)
            {
                var nextX = cellX + NeighbourX[direction];
                var nextY = cellY + NeighbourY[direction];
                if (mask.IsLandCell(nextX, nextY))
                {
                    continue;
                }

                // A hull does not cut the corner between two rocks, so a
                // diagonal step needs both of its sides open.
                if (NeighbourX[direction] != 0 && NeighbourY[direction] != 0 &&
                    (mask.IsLandCell(nextX, cellY) || mask.IsLandCell(cellX, nextY)))
                {
                    continue;
                }

                var next = (nextY * size) + nextX;
                if (scratch.Stamp[next] == search && scratch.Closed[next])
                {
                    continue;
                }

                var cost = scratch.Cost[cell] + NeighbourCost[direction];
                if (scratch.Stamp[next] == search && cost >= scratch.Cost[next])
                {
                    continue;
                }

                scratch.Stamp[next] = search;
                scratch.Closed[next] = false;
                scratch.Cost[next] = cost;
                scratch.CameFrom[next] = cell;
                HeapPush(scratch, next, cost + Heuristic(next, goalCell, size));
            }
        }

        if (!found)
        {
            return 0;
        }

        return Reconstruct(scratch, startCell, goalCell, size, startX, startY, goalX, goalY);
    }

    /// <summary>
    /// Walks the parents back from the goal and writes the path forward, with
    /// the ship's true position first and the true destination last so the
    /// first and last leg are not bent to a cell centre.
    /// </summary>
    private static int Reconstruct(
        PathfindingScratch scratch,
        int startCell,
        int goalCell,
        int size,
        float startX,
        float startY,
        float goalX,
        float goalY)
    {
        var length = 0;
        for (var cell = goalCell; cell != -1; cell = scratch.CameFrom[cell])
        {
            length++;
            if (length > PathfindingScratch.MaximumCorners - 2)
            {
                return 0;
            }

            if (cell == startCell)
            {
                break;
            }
        }

        var count = length + 1;
        scratch.Corners[0] = new RouteWaypoint(startX, startY);
        var write = count - 1;
        scratch.Corners[write] = new RouteWaypoint(goalX, goalY);
        write--;
        for (var cell = scratch.CameFrom[goalCell]; cell != -1 && write > 0; cell = scratch.CameFrom[cell])
        {
            scratch.Corners[write--] = new RouteWaypoint((cell % size) + 0.5f, (cell / size) + 0.5f);
        }

        return count;
    }

    /// <summary>
    /// Turns a cell path into the fewest straight legs that stay on water:
    /// from the current anchor, reach as far down the path as line of sight
    /// allows, keep that point, and start again from it.
    /// </summary>
    private static int StringPull(
        LandMask mask,
        ReadOnlySpan<RouteWaypoint> path,
        Span<RouteWaypoint> route)
    {
        var count = 0;
        var anchor = 0;
        while (anchor < path.Length - 1)
        {
            var furthest = anchor + 1;
            for (var candidate = path.Length - 1; candidate > anchor + 1; candidate--)
            {
                if (mask.SegmentIsClear(
                        path[anchor].X, path[anchor].Y, path[candidate].X, path[candidate].Y))
                {
                    furthest = candidate;
                    break;
                }
            }

            if (count == route.Length)
            {
                // More corners than SEA_5 §4.1.5 allows in one course.
                return 0;
            }

            route[count++] = path[furthest];
            anchor = furthest;
        }

        return count;
    }

    private static float Heuristic(int cell, int goalCell, int size)
    {
        // Octile distance: exact for an 8-direction grid, so A* never expands a
        // cell it did not have to.
        var deltaX = MathF.Abs((cell % size) - (goalCell % size));
        var deltaY = MathF.Abs((cell / size) - (goalCell / size));
        var smaller = MathF.Min(deltaX, deltaY);
        return (deltaX + deltaY) - ((2f - DiagonalCost) * smaller);
    }

    private static void HeapPush(PathfindingScratch scratch, int cell, float score)
    {
        var index = scratch.HeapCount++;
        scratch.HeapCell[index] = cell;
        scratch.HeapScore[index] = score;
        while (index > 0)
        {
            var parent = (index - 1) / 2;
            if (scratch.HeapScore[parent] <= scratch.HeapScore[index])
            {
                break;
            }

            Swap(scratch, parent, index);
            index = parent;
        }
    }

    private static int HeapPop(PathfindingScratch scratch)
    {
        var top = scratch.HeapCell[0];
        var last = --scratch.HeapCount;
        scratch.HeapCell[0] = scratch.HeapCell[last];
        scratch.HeapScore[0] = scratch.HeapScore[last];
        var index = 0;
        while (true)
        {
            var left = (index * 2) + 1;
            if (left >= last)
            {
                break;
            }

            var smallest = left;
            var right = left + 1;
            if (right < last && scratch.HeapScore[right] < scratch.HeapScore[left])
            {
                smallest = right;
            }

            if (scratch.HeapScore[index] <= scratch.HeapScore[smallest])
            {
                break;
            }

            Swap(scratch, index, smallest);
            index = smallest;
        }

        return top;
    }

    private static void Swap(PathfindingScratch scratch, int left, int right)
    {
        (scratch.HeapCell[left], scratch.HeapCell[right]) =
            (scratch.HeapCell[right], scratch.HeapCell[left]);
        (scratch.HeapScore[left], scratch.HeapScore[right]) =
            (scratch.HeapScore[right], scratch.HeapScore[left]);
    }
}
```

- [ ] **Step 5: Run the test and watch it pass**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~PathfindingRulesTests"
```

Expected: `Passed! - Failed: 0, Passed: 6`.

- [ ] **Step 6: Measure it before trusting it**

Add to `tests/performance/` (the `perf:domain` project) a benchmark that plots
the worst course on a real 400-square map:

```csharp
[Benchmark]
public int WorstCourseAcrossHavenmere()
{
    Span<RouteWaypoint> route = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];
    PathfindingRules.TryBuildRoute(
        havenmere, scratch, 4f, 4f, 396f, 396f, route, out var count);
    return count;
}
```

```sh
pnpm perf:domain
```

Expected: the mean is well under 1 ms and allocation is 0 B. If a corner-to-corner
course costs more than 2 ms, lower `MaximumExpansions` and say so in the commit
body; do not lower it silently.

- [ ] **Step 7: Commit**

```bash
git add server/spacetimedb/spacetimedb/Domain/PathfindingRules.cs server/spacetimedb/tests/PathfindingRulesTests.cs tests/performance
git commit -m "feat(domain): plot a course round land with A* and a string pull

SEA_5 §4.1.5. The straight line is tried first and answers nearly every
request; a search reuses one set of buffers with a generation stamp so it
allocates nothing, and refuses rather than overruns a tick when a course needs
more than twenty thousand cells."
```

---

# Phase 4 — Movement: routes replace inertia

The pure rules exist. This phase wires them into the module and deletes the old
ones.

### Task 4.1: Change the schema

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Schema/Tables.cs`

- [ ] **Step 1: Take the kinematic columns off `Ship`**

Delete these fields entirely:

```
Acceleration, Deceleration, TurnRateDegrees, DesiredHeadingDegrees,
WaypointX, WaypointY, HasWaypoint, IsStopping
```

Add these:

```csharp
    /// <summary>How far down her route she is; index into the ShipRoute points.</summary>
    public int RouteIndex;

    /// <summary>Whether she has a course at all. IsMoving is this and not frozen.</summary>
    public bool HasRoute;

    /// <summary>Bumped every time a new route is set, so a client can tell them apart.</summary>
    public uint RouteVersion;

    /// <summary>Her speed this tick in squares per second, from SpeedRules.Effective.</summary>
    public float EffectiveSpeedSquaresPerSecond;
```

`DestinationX` and `DestinationY` stay: they are the last waypoint, and the HUD
and the NPC rules both read them.

- [ ] **Step 2: Add the route table**

```csharp
/// <summary>
/// A ship's course. Public because every client that can see the ship draws
/// the same line (SEA_5 §4.3).
/// </summary>
/// <remarks>
/// The points live in their own row rather than on Ship because a course is
/// written once, when it is ordered, while a Ship row is written on every tick
/// she sails. Keeping the two apart means following a course does not rewrite
/// the course.
/// </remarks>
[Table(Name = "ShipRoute", Public = true)]
public partial struct ShipRoute
{
    [PrimaryKey]
    public ulong EntityId;

    public uint Version;
    public List<float> PointsX;
    public List<float> PointsY;
}
```

- [ ] **Step 3: Add the land-mask table**

```csharp
/// <summary>
/// A map's land, one bit per square. Private: the client draws the authored
/// island shapes, not the mask.
/// </summary>
[Table(Name = "MapLandMask", Public = false)]
public partial struct MapLandMask
{
    [PrimaryKey]
    public byte MapId;

    public int Size;
    public List<ulong> Bits;
}
```

- [ ] **Step 4: Regenerate the bindings**

```sh
pnpm server:build
pnpm server:generate:csharp
pnpm server:generate:typescript
```

Expected: both generators exit 0. Never hand-edit what they wrote.

- [ ] **Step 5: Commit**

```bash
git add server/spacetimedb/spacetimedb/Schema/Tables.cs server/spacetimedb/bindings apps
git commit -m "feat(schema): carry a route instead of a turning circle

Acceleration, deceleration, turn rate and the desired-heading column all
described inertia SEA_5 §4.2 removes. A course lives in its own public row so
that sailing along one does not rewrite it every tick."
```

### Task 4.2: Build a route when a course is ordered

**Files:**
- Create: `server/spacetimedb/spacetimedb/Simulation/RouteSystem.cs`
- Modify: `server/spacetimedb/spacetimedb/Domain/CommandPolicy.cs`
- Test: `server/spacetimedb/tests/CommandPolicyTests.cs`

- [ ] **Step 1: Add the two rejection codes**

In `Domain/CommandPolicy.cs`, extend `CommandRejectionCode` with the next two
free values (do not renumber the existing ones; the client reads them):

```csharp
    /// <summary>There is no way from here to there (SEA_5 §4.1.5).</summary>
    NoPath = 25,

    /// <summary>More than eight MoveTo in one second (SEA_5 §4.1.8).</summary>
    RateLimited = 26,
```

- [ ] **Step 2: Write the failing test**

Add to `server/spacetimedb/tests/CommandPolicyTests.cs`:

```csharp
[Fact]
public void ACourseIntoALandLockedLakeIsRejectedWithNoPath()
{
    Assert.Equal(25, (int)CommandRejectionCode.NoPath);
}

[Fact]
public void TooManyCoursesInOneSecondAreRejectedAsRateLimited()
{
    Assert.Equal(26, (int)CommandRejectionCode.RateLimited);
}
```

- [ ] **Step 3: Write `RouteSystem`**

```csharp
using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    /// <summary>
    /// One set of A* buffers for the whole module. The tick is single-threaded,
    /// so one is enough, and it means a MoveTo allocates nothing.
    /// </summary>
    private static PathfindingScratch? pathfindingScratch;

    private static PathfindingScratch ScratchFor(LandMask mask) =>
        pathfindingScratch is { } scratch && scratch.Size == mask.Size
            ? scratch
            : pathfindingScratch = new PathfindingScratch(mask.Size);

    /// <summary>
    /// Answering a click: SEA_5 §4.1.2. The point is pulled inside the map, then
    /// off land if it is close enough to water, then a course is plotted to it.
    /// The old course is replaced in one step, so a captain never sails a mixture
    /// of two orders.
    /// </summary>
    private static CommandRejectionCode SetCourse(
        ReducerContext ctx,
        ref Ship ship,
        float requestedX,
        float requestedY,
        ulong tick)
    {
        var mask = ContentCatalog.LandMaskFor(ship.MapId);
        var (clampedX, clampedY) = WorldRules.ClampToMap(requestedX, requestedY);
        if (!mask.TryNearestWater(
                clampedX, clampedY, PathfindingRules.NudgeSearchSquares, out var goalX, out var goalY))
        {
            return CommandRejectionCode.NoPath;
        }

        Span<RouteWaypoint> route = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];
        var outcome = PathfindingRules.TryBuildRoute(
            mask,
            ScratchFor(mask),
            ship.PositionX,
            ship.PositionY,
            goalX,
            goalY,
            route,
            out var count);
        if (outcome == PathOutcome.NoPath)
        {
            return CommandRejectionCode.NoPath;
        }

        StoreRoute(ctx, ref ship, route[..count], tick);
        return CommandRejectionCode.None;
    }

    private static void StoreRoute(
        ReducerContext ctx,
        ref Ship ship,
        ReadOnlySpan<RouteWaypoint> route,
        ulong tick)
    {
        var pointsX = new List<float>(route.Length);
        var pointsY = new List<float>(route.Length);
        foreach (var waypoint in route)
        {
            pointsX.Add(waypoint.X);
            pointsY.Add(waypoint.Y);
        }

        ship.RouteVersion++;
        ship.RouteIndex = 0;
        ship.HasRoute = route.Length > 0;
        ship.IsMoving = ship.HasRoute;
        ship.DestinationX = route.Length > 0 ? route[^1].X : ship.PositionX;
        ship.DestinationY = route.Length > 0 ? route[^1].Y : ship.PositionY;

        var stored = new ShipRoute
        {
            EntityId = ship.EntityId,
            Version = ship.RouteVersion,
            PointsX = pointsX,
            PointsY = pointsY,
        };
        if (ctx.Db.ShipRoute.EntityId.Find(ship.EntityId) is null)
        {
            ctx.Db.ShipRoute.Insert(stored);
        }
        else
        {
            ctx.Db.ShipRoute.EntityId.Update(stored);
        }
    }

    /// <summary>
    /// Stopping: SEA_5 §4.1.4. The course is gone and the ship is at rest in the
    /// same tick, wherever she happens to be. Sinking, freezing, a teleport and a
    /// map change all come through here.
    /// </summary>
    private static void ClearRoute(ReducerContext ctx, ref Ship ship)
    {
        ship.HasRoute = false;
        ship.IsMoving = false;
        ship.RouteIndex = 0;
        ship.DestinationX = ship.PositionX;
        ship.DestinationY = ship.PositionY;
        ship.EffectiveSpeedSquaresPerSecond = 0f;
        if (ctx.Db.ShipRoute.EntityId.Find(ship.EntityId) is not null)
        {
            ctx.Db.ShipRoute.EntityId.Delete(ship.EntityId);
        }
    }
}
```

- [ ] **Step 4: Point the `SetCourse` and `StopCourse` command handlers at it**

Find the existing handlers in `Simulation/CommandReducers.cs` (or whichever file
`grep -rn "ShipCommandKind.SetCourse" server` names) and replace the body that
set `DestinationX/Y` and called `ConfigureNavigationWaypoint` with a call to
`SetCourse` / `ClearRoute`, turning the returned rejection code into the command
result the client already understands.

- [ ] **Step 5: Run**

```sh
pnpm server:test
```

Expected: the command tests pass; the sailing tests still fail because
`SailingSystem` has not moved yet. That is Task 4.3.

- [ ] **Step 6: Commit**

```bash
git add server/spacetimedb/spacetimedb/Simulation/RouteSystem.cs server/spacetimedb/spacetimedb/Domain/CommandPolicy.cs server/spacetimedb/tests/CommandPolicyTests.cs
git commit -m "feat(server): plot and store a course when one is ordered

SEA_5 §4.1.2 and §4.1.4: a click is clamped, nudged off land, routed, and the
old course is replaced whole. A course that cannot be plotted is refused with
NO_PATH rather than half-obeyed."
```

### Task 4.3: Sail the route on the tick

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Simulation/SailingSystem.cs`
- Delete: `server/spacetimedb/spacetimedb/Domain/SailingRules.cs`
- Delete: `server/spacetimedb/tests/SailingRulesTests.cs`, `server/spacetimedb/tests/ShipStopsAtTheMarkTests.cs`

- [ ] **Step 1: Replace `ProcessMovingShip`**

```csharp
    private static void ProcessMovingShip(
        ReducerContext ctx,
        TickWorld world,
        ref ShipKinematics ship,
        ulong tick,
        float deltaSeconds)
    {
        RefreshEnvironment(ctx, world, ref ship, tick);

        var route = world.RouteFor(ctx, ship.EntityId);
        var step = RouteRules.Advance(
            route,
            ship.RouteIndex,
            ship.PositionX,
            ship.PositionY,
            ship.HeadingDegrees,
            ship.EffectiveSpeedSquaresPerSecond * deltaSeconds);

        ship.PositionX = step.PositionX;
        ship.PositionY = step.PositionY;
        ship.HeadingDegrees = step.HeadingDegrees;
        ship.RouteIndex = step.WaypointIndex;
        if (step.Arrived)
        {
            ship.HasRoute = false;
            ship.IsMoving = false;
            ship.EffectiveSpeedSquaresPerSecond = 0f;
        }

        ApplyCurrentDrift(ctx, ref ship, deltaSeconds);
        ship.ChunkX = SpatialRules.ChunkCoordinate(ship.PositionX);
        ship.ChunkY = SpatialRules.ChunkCoordinate(ship.PositionY);

        if (SimulationWorkRules.ShouldProcessLootPickup(ship.EntityId, tick) &&
            world.HasActiveLoot(ctx))
        {
            ProcessLootClaims(ctx, ship, tick);
        }
    }
```

`world.RouteFor` caches the `ShipRoute` row for the shard the way `TickWorld`
already caches blockers, and returns a `ReadOnlySpan<RouteWaypoint>` over a
pooled array so the tick allocates nothing.

- [ ] **Step 2: Make drift apply to a ship at rest and stop at land**

Bug 1.6 from the review: today only a moving ship drifts, and drift is clamped
to the map but not checked against land. SEA_5 §5.2 says "a stopped ship
drifts. Drift stops at land and at the map edge."

```csharp
    /// <summary>
    /// A current pushes every hull in it, sailing or anchored (SEA_5 §5.2). It
    /// cannot push one onto a rock or past the border: the mask counts anything
    /// off the map as land, so one test covers both.
    /// </summary>
    private static void ApplyCurrentDrift(
        ReducerContext ctx,
        ref ShipKinematics ship,
        float deltaSeconds)
    {
        if (ship.CurrentVelocityX == 0f && ship.CurrentVelocityY == 0f)
        {
            return;
        }

        var driftedX = ship.PositionX + (ship.CurrentVelocityX * deltaSeconds);
        var driftedY = ship.PositionY + (ship.CurrentVelocityY * deltaSeconds);
        var mask = ContentCatalog.LandMaskFor(ship.MapId);
        if (mask.IsLand(driftedX, driftedY))
        {
            // She holds the last water she was on rather than sliding along the
            // coast, which is what a captain sees: the current stops at the beach.
            return;
        }

        ship.PositionX = driftedX;
        ship.PositionY = driftedY;
    }
```

- [ ] **Step 3: Stop dropping ships at rest from the movement shard**

`ProcessMovementBatch` keeps a ship in the shard only while `ship.IsMoving`.
A ship at rest in a current still has to be stepped. Change the retention test
to:

```csharp
            // A hull stays in the shard while she is sailing or while a current
            // still has hold of her. Everything else costs the tick nothing.
            if (ship.IsMoving || ship.CurrentVelocityX != 0f || ship.CurrentVelocityY != 0f)
            {
                ships[writeIndex++] = ship;
            }
```

and change the per-tick guard from `if (ship.IsMoving)` to always calling
`ProcessMovingShip`, which now does nothing expensive for a ship with no route
and no current.

- [ ] **Step 4: Delete the old rules**

```sh
git rm server/spacetimedb/spacetimedb/Domain/SailingRules.cs
git rm server/spacetimedb/tests/SailingRulesTests.cs
git rm server/spacetimedb/tests/ShipStopsAtTheMarkTests.cs
grep -rn "SailingRules\|HandlingRules\|SailingParameters\|AuthoritativeSailingStep\|ArrivalRadius" server apps --include='*.cs'
```

Every hit is a call site to remove. `SegmentIntersectsCircle` moved to
`GeometryRules` in Task 1.1; point the survivors at it.

- [ ] **Step 5: Cut the circle detour out of `NavigationRules`**

Delete `TryFindDetour`, `CandidateScore`, `NearestBlocker`, `DetourClearance`,
`WaypointArrivalRadius`, `RingSamples` and the LINQ `SegmentIsClear`/`IsDestinationBlocked`
pair. What is left is nothing, so delete the file and its test as well; A* and
the mask do all of it now, and the LINQ `.Any()` over every blocker on the map
was a full scan on a command path.

- [ ] **Step 6: Run**

```sh
pnpm server:test
pnpm ci:fast
```

Expected: `Passed! - Failed: 0` and exit 0.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(server): sail a course instead of integrating a hull

Acceleration, the braking curve, the turning circle, the thrust-alignment term
and the 1.5-unit arrival radius are all gone; a tick walks the route and a hull
that reaches her last waypoint is standing on it. A current now moves a hull at
anchor as well and stops her at the beach instead of pushing her through it."
```

### Task 4.4: Rate-limit `MoveTo`

**Files:**
- Create: `server/spacetimedb/spacetimedb/Domain/MoveRateRules.cs`
- Test: `server/spacetimedb/tests/MoveRateRulesTests.cs`
- Modify: `Schema/Tables.cs`, `Simulation/RouteSystem.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class MoveRateRulesTests
{
    [Fact]
    public void TwelveCoursesInOneSecondGiveEightAndFourDrops()
    {
        var windowStart = 0UL;
        var used = 0u;
        var accepted = 0;
        var dropped = 0;
        for (var attempt = 0; attempt < 12; attempt++)
        {
            if (MoveRateRules.Allow(ref windowStart, ref used, tick: 100UL))
            {
                accepted++;
            }
            else
            {
                dropped++;
            }
        }

        Assert.Equal(8, accepted);
        Assert.Equal(4, dropped);
    }

    [Fact]
    public void TheNextSecondStartsFresh()
    {
        var windowStart = 100UL;
        var used = 8u;

        Assert.False(MoveRateRules.Allow(ref windowStart, ref used, 109UL));
        Assert.True(MoveRateRules.Allow(ref windowStart, ref used, 110UL));
        Assert.Equal(110UL, windowStart);
        Assert.Equal(1u, used);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: build error, `The name 'MoveRateRules' does not exist`.

- [ ] **Step 3: Write it**

```csharp
namespace Sea.Server;

/// <summary>
/// SEA_5 §4.1.8: at most eight MoveTo a second per ship. Extra requests are
/// dropped, never queued, and every drop is counted against the trust score.
/// </summary>
/// <remarks>
/// A fixed window rather than a leaky bucket, because the rule is written as
/// "eight per second" and a captain who sees eight answered and four refused
/// can work out what happened. The state is two fields on the ship's own row,
/// so the check costs nothing and needs no table of its own.
/// </remarks>
public static class MoveRateRules
{
    public const uint MaximumPerSecond = 8;

    public const ulong WindowTicks = WorldRules.TickRateHz;

    public static bool Allow(ref ulong windowStartTick, ref uint usedInWindow, ulong tick)
    {
        if (tick >= windowStartTick + WindowTicks)
        {
            windowStartTick = tick;
            usedInWindow = 0;
        }

        if (usedInWindow >= MaximumPerSecond)
        {
            return false;
        }

        usedInWindow++;
        return true;
    }
}
```

- [ ] **Step 4: Add the two columns and the check**

On `Ship`: `public ulong MoveWindowStartTick;` and `public uint MovesInWindow;`.
In `SetCourse`, before anything else:

```csharp
        var windowStart = ship.MoveWindowStartTick;
        var used = ship.MovesInWindow;
        var allowed = MoveRateRules.Allow(ref windowStart, ref used, tick);
        ship.MoveWindowStartTick = windowStart;
        ship.MovesInWindow = used;
        if (!allowed)
        {
            RecordTrustPenalty(ctx, ship.EntityId, TrustScoreRules.DroppedMovePenalty);
            return CommandRejectionCode.RateLimited;
        }
```

`RecordTrustPenalty` arrives in Phase 12; until then it is a call into a method
that only increments a counter on `Ship`. Add that method now in `RouteSystem.cs`:

```csharp
    private static void RecordTrustPenalty(ReducerContext ctx, ulong entityId, int penalty)
    {
        if (ctx.Db.Ship.EntityId.Find(entityId) is not Ship ship)
        {
            return;
        }

        ship.DroppedCommandCount += (uint)penalty;
        ctx.Db.Ship.EntityId.Update(ship);
    }
```

with `public uint DroppedCommandCount;` on `Ship`. Phase 12 moves the score
itself to its own table and leaves this counter as the raw feed.

- [ ] **Step 5: Run**

```sh
pnpm server:test
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(server): drop the ninth course in a second

SEA_5 §4.1.8. Eight a second is more than a hand can click and far less than a
script can send, so the limit costs a captain nothing and gives the trust score
its first real signal."
```

---

> ## Review gate after Phase 4
>
> Movement is now completely different code. Ask the user to run
> `/thermo-nuclear-code-quality-review` and `/improve-codebase-architecture`
> over the phase, and fix what comes back before Phase 5.

---

# Phase 5 — Effective speed and the environment

SEA_5 §5.1 is one formula. Today the same calculation is spread across
`TacticalRules`, `EnvironmentRules`, `EffectRules` and `SailingSystem`, each
holding a different constant, and three of the review's eight confirmed defects
live in that spread. This phase gives it one owner.

### Task 5.1: `SpeedRules`

**Files:**
- Create: `server/spacetimedb/spacetimedb/Domain/SpeedRules.cs`
- Test: `server/spacetimedb/tests/SpeedRulesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class SpeedRulesTests
{
    private static SpeedInputs Brig() => new(
        BaseSquaresPerSecond: 5.0f,
        BonusFraction: 0f,
        Hull: 100,
        MaxHull: 100,
        HeadingDegrees: 90f,
        WindDirectionDegrees: 0f,
        InStorm: false,
        DebuffMultiplier: 1f,
        IsFrozen: false);

    [Fact]
    public void ACleanShipInACrosswindMakesHerRatedSpeed()
    {
        Assert.Equal(5.0f, SpeedRules.Effective(Brig()), 4);
    }

    [Theory]
    [InlineData(100u, 1.00f)]
    [InlineData(51u, 1.00f)]
    [InlineData(50u, 0.92f)]
    [InlineData(26u, 0.92f)]
    [InlineData(25u, 0.85f)]
    [InlineData(1u, 0.85f)]
    public void HpStateHasThreeSteps(uint hull, float expected)
    {
        Assert.Equal(expected, SpeedRules.HpStateMultiplier(hull, 100), 4);
    }

    [Fact]
    public void DownwindIsTenPerCentAndUpwindIsTenPerCentTheOtherWay()
    {
        Assert.Equal(1.10f, SpeedRules.WindMultiplier(90f, 90f), 4);
        Assert.Equal(0.90f, SpeedRules.WindMultiplier(270f, 90f), 4);
        Assert.Equal(1.00f, SpeedRules.WindMultiplier(0f, 90f), 4);
    }

    [Fact]
    public void BonusesAddThenCapAtTwentyFivePerCent()
    {
        // SEA_5 §13 test 10, with the cap Sea keeps from stat_caps.json.
        var inputs = Brig() with { BonusFraction = 0.35f, HeadingDegrees = 0f };

        Assert.Equal(6.25f, SpeedRules.Effective(inputs), 4);
    }

    [Fact]
    public void AStormAndAHeadWindMultiplyTogether()
    {
        // SEA_5 §13 test 8.
        var inputs = Brig() with { InStorm = true, HeadingDegrees = 180f };

        Assert.Equal(5.0f * 0.85f * 0.90f, SpeedRules.Effective(inputs), 4);
    }

    [Fact]
    public void SlowsMultiplyButNeverBelowHalf()
    {
        var inputs = Brig() with { DebuffMultiplier = 0.2f, HeadingDegrees = 0f };

        Assert.Equal(2.5f, SpeedRules.Effective(inputs), 4);
    }

    [Fact]
    public void AFrozenShipMakesNoWayAtAll()
    {
        Assert.Equal(0f, SpeedRules.Effective(Brig() with { IsFrozen = true }), 6);
    }

    [Fact]
    public void TheFastestPossibleShipIsSevenPointSeven()
    {
        // SEA_5 §5.3, amended for the 0.25 cap.
        var skiff = new SpeedInputs(5.6f, 0.25f, 100, 100, 90f, 90f, false, 1f, false);

        Assert.Equal(7.70f, SpeedRules.Effective(skiff), 3);
    }

    [Fact]
    public void TheSlowestPossibleShipIsTwoPointEightSix()
    {
        var galleon = new SpeedInputs(4.4f, 0f, 20, 100, 180f, 0f, true, 1f, false);

        Assert.Equal(2.86f, SpeedRules.Effective(galleon), 2);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: build error, `The type or namespace name 'SpeedInputs' could not be found`.

- [ ] **Step 3: Write `SpeedRules`**

```csharp
namespace Sea.Server;

/// <summary>Everything that decides how fast a hull is moving this tick.</summary>
public readonly record struct SpeedInputs(
    float BaseSquaresPerSecond,
    float BonusFraction,
    uint Hull,
    uint MaxHull,
    float HeadingDegrees,
    float WindDirectionDegrees,
    bool InStorm,
    float DebuffMultiplier,
    bool IsFrozen);

/// <summary>
/// SEA_5 §5.1, and the only place effective speed is worked out.
/// </summary>
/// <remarks>
/// This used to be four calculations in four files. TacticalRules held the
/// debuff product and pointed the storm at the turn rate; EnvironmentRules held
/// a random wind strength on a thirty-second clock; EffectRules floored one
/// term at 0.1 so the product could reach 0.03; SailingSystem multiplied
/// whatever came out by whatever it had cached. Every one of those was a bug,
/// and none of them was visible from any of the others.
/// </remarks>
public static class SpeedRules
{
    /// <summary>
    /// SEA_5 §5.1 says 0.20; Sea keeps the 0.25 that stat_caps.json has shipped
    /// since Milestone 1. This is the one number SEA_2_MATH still wins.
    /// </summary>
    public const float BonusCap = 0.25f;

    public const float NormalHpMultiplier = 1.00f;
    public const float DamagedHpMultiplier = 0.92f;
    public const float BurningHpMultiplier = 0.85f;
    public const float DamagedHpFraction = 0.50f;
    public const float BurningHpFraction = 0.25f;

    /// <summary>Downwind is this much faster, upwind this much slower.</summary>
    public const float WindStrength = 0.10f;

    public const float StormMultiplier = 0.85f;

    /// <summary>Slows multiply, but never take a hull below half her way.</summary>
    public const float DebuffFloor = 0.50f;

    public static float HpStateMultiplier(uint hull, uint maxHull)
    {
        if (maxHull == 0)
        {
            return NormalHpMultiplier;
        }

        var fraction = (float)hull / maxHull;
        if (fraction <= BurningHpFraction)
        {
            return BurningHpMultiplier;
        }

        return fraction <= DamagedHpFraction ? DamagedHpMultiplier : NormalHpMultiplier;
    }

    /// <summary>
    /// The wind's direction is the way it blows, so a hull on the same bearing
    /// is running before it and gains, and one on the opposite bearing loses.
    /// </summary>
    public static float WindMultiplier(float headingDegrees, float windDirectionDegrees) =>
        1f + (WindStrength *
              TrigonometryRules.CosDegrees(headingDegrees - windDirectionDegrees));

    public static float Effective(in SpeedInputs inputs)
    {
        if (inputs.IsFrozen)
        {
            // Freeze is not a slow: the ship stops dead and keeps her course,
            // and picks it up again when it lifts (SEA_5 §5.2).
            return 0f;
        }

        var speed = inputs.BaseSquaresPerSecond;
        speed *= 1f + Math.Clamp(inputs.BonusFraction, 0f, BonusCap);
        speed *= HpStateMultiplier(inputs.Hull, inputs.MaxHull);
        speed *= WindMultiplier(inputs.HeadingDegrees, inputs.WindDirectionDegrees);
        if (inputs.InStorm)
        {
            speed *= StormMultiplier;
        }

        speed *= Math.Clamp(inputs.DebuffMultiplier, DebuffFloor, 1f);
        return MathF.Max(0f, speed);
    }
}
```

- [ ] **Step 4: Run the test and watch it pass**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~SpeedRulesTests"
```

Expected: `Passed! - Failed: 0, Passed: 14`.

- [ ] **Step 5: Commit**

```bash
git add server/spacetimedb/spacetimedb/Domain/SpeedRules.cs server/spacetimedb/tests/SpeedRulesTests.cs
git commit -m "feat(domain): work out effective speed in one place

SEA_5 §5.1 as one pure function. The debuff floor now applies to the product
rather than to one term, which is what stopped a chained and grapeshotted hull
from ending up at three per cent of her rated speed."
```

### Task 5.2: Wind on the tick clock

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Domain/EnvironmentRules.cs`
- Test: `server/spacetimedb/tests/EnvironmentRulesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void AWindBandIsEightHoursOfTicks()
{
    Assert.Equal(288000UL, EnvironmentRules.WindBandTicks);
}

[Theory]
[InlineData(0UL, 0UL)]
[InlineData(287999UL, 0UL)]
[InlineData(288000UL, 1UL)]
[InlineData(864000UL, 3UL)]
public void TheBandComesFromTheTickCounterNotTheClock(ulong tick, ulong band)
{
    Assert.Equal(band, EnvironmentRules.WindBand(tick));
}

[Fact]
public void TheSameSeedAndBandAlwaysGiveTheSameWind()
{
    var first = EnvironmentRules.WindForBand(seed: 12345UL, band: 7UL);
    var second = EnvironmentRules.WindForBand(seed: 12345UL, band: 7UL);

    Assert.Equal(first, second);
}

[Fact]
public void TheWindHasADirectionButNoStrengthToRoll()
{
    var wind = EnvironmentRules.WindForBand(seed: 99UL, band: 2UL);

    Assert.InRange(wind, 0f, 360f);
}

[Fact]
public void EveryBandLaysOutAtMostTwoStormsPerMap()
{
    for (var band = 0UL; band < 20UL; band++)
    {
        var storms = EnvironmentRules.StormsForBand(seed: 4242UL, band: band, mapId: 1);

        Assert.InRange(storms.Count, 0, 2);
    }
}

[Fact]
public void TheSameSeedAndBandAlwaysLayOutTheSameStorms()
{
    var first = EnvironmentRules.StormsForBand(4242UL, 5UL, 1);
    var second = EnvironmentRules.StormsForBand(4242UL, 5UL, 1);

    Assert.Equal(first, second);
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: FAIL, `WindEpochTicks` exists but `WindBandTicks` does not.

- [ ] **Step 3: Rewrite the wind**

```csharp
    /// <summary>
    /// Eight hours at 10 Hz. The band comes from the world tick counter rather
    /// than the wall clock, so replaying a command log blows the same wind
    /// (SEA_5 §5.2, as amended).
    /// </summary>
    public const ulong WindBandTicks = 288000UL;

    public static ulong WindBand(ulong tick) => tick / WindBandTicks;

    /// <summary>
    /// The wind's bearing for one band. Strength is not rolled: SEA_5 §5.1
    /// fixes it at 0.10 for every map and every band, so there is nothing here
    /// but a direction.
    /// </summary>
    public static float WindForBand(ulong seed, ulong band)
    {
        var state = Mix(seed ^ Mix(band));
        return (float)((state >> 40) / (double)(1UL << 24) * 360d);
    }
```

`Mix` is the private splitmix helper `EnvironmentRules` already uses for
`WindForEpoch`; it stays exactly as it is. If it is not there, take the one from
`CriticalHitRules` in Task 7.1 verbatim.

Delete `WindEpochTicks`, the `0.2 + ... * 0.6` strength roll, and
`WindSpeedMultiplier` — the last of those is now `SpeedRules.WindMultiplier`.
On `EnvironmentState`, `WindStrength` goes away and `WindEpoch` becomes
`WindBand`, and `NextWindChangeTick` goes with them: the band is derived, so
there is nothing left to schedule.

- [ ] **Step 4: Turn the whole time band on the boundary**

SEA_5 §12.5 rotates the wind *and* respawns the storms on the same boundary, and
broadcasts the change once. That is one comparison a tick, and it does nothing
on 287,999 ticks out of 288,000:

```csharp
    /// <summary>
    /// The eight-hour boundary. Wind turns and storms are laid out again, both
    /// from the same band number, so a replay of the same log gets the same
    /// weather (SEA_5 §12.5).
    /// </summary>
    private static void UpdateTimeBand(ReducerContext ctx, ulong tick)
    {
        if (ctx.Db.EnvironmentState.Id.Find(1) is not EnvironmentState environment)
        {
            return;
        }

        var band = EnvironmentRules.WindBand(tick);
        if (band == environment.WindBand)
        {
            return;
        }

        environment.WindBand = band;
        environment.WindDirectionDegrees = EnvironmentRules.WindForBand(environment.Seed, band);
        ctx.Db.EnvironmentState.Id.Update(environment);
        RespawnStorms(ctx, environment.Seed, band);
    }
```

`RespawnStorms` deletes the active storm `WorldObject` rows and lays out nought
to two new ones per map from `EnvironmentRules.StormsForBand(seed, band, mapId)`,
which is the same splitmix pattern as `WindForBand`: a count, a centre on water,
a drift bearing. Add the two storm constants beside the wind band, both from
SEA_5 §5.2:

```csharp
    /// <summary>A storm is this wide. It matches SpatialRules.MaximumWorldInfluenceRadiusSquares,
    /// which is what bounds the chunk query that finds one.</summary>
    public const float StormRadiusSquares = 40f;

    /// <summary>How fast a storm drifts across the chart.</summary>
    public const float StormDriftSquaresPerSecond = 0.5f;
```

Rename every `UpdateWind` call site to `UpdateTimeBand`.

- [ ] **Step 5: Run**

```sh
pnpm server:test
```

Expected: PASS, including the replay tests.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(environment): turn the weather on one eight-hour band

Wind was a random 0.2..0.8 strength on a thirty-second epoch, which is neither
SEA_5's flat ten per cent nor a wind a captain can plan around, and storms never
came back once they expired. Both now turn on the same boundary, derived from
the tick counter because a replay of the same log has to blow the same way."
```

### Task 5.3: Storms slow ships, and stop at the border

**Files:**
- Modify: `server/spacetimedb/spacetimedb/TacticalRules.cs`
- Test: `server/spacetimedb/tests/TacticalRulesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void TheStormIsNotAppliedHereBecauseSpeedRulesOwnsIt()
{
    // SEA_5 §5.1 puts the storm outside the debuff floor, so it is SpeedRules'
    // term and this returns only what the floor applies to.
    var modifiers = TacticalRules.Resolve(
        slowed: false, slowMagnitude: 0f, inShoal: false, repairing: false);

    Assert.Equal(1f, modifiers.SpeedMultiplier, 4);
}

[Fact]
public void AShoalAndASlowMultiplyTogether()
{
    var modifiers = TacticalRules.Resolve(
        slowed: true, slowMagnitude: 0.2f, inShoal: true, repairing: false);

    Assert.Equal(0.8f * TacticalRules.ShoalMultiplier, modifiers.SpeedMultiplier, 4);
}

[Fact]
public void AStormThatReachesTheBorderStopsThere()
{
    var (x, y) = TacticalRules.MoveStorm(
        positionX: 398f, positionY: 200f, directionDegrees: 90f,
        speedSquaresPerSecond: 0.5f, deltaSeconds: 20f);

    Assert.Equal(WorldRules.MapMax, x, 4);
    Assert.Equal(200f, y, 4);
}

[Fact]
public void ThereIsNoWeaponEffectivenessLeftToIgnore()
{
    Assert.DoesNotContain(
        "WeaponEffectiveness",
        typeof(TacticalModifiers).GetProperties().Select(property => property.Name));
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: FAIL - `Resolve` still takes an `inStorm` argument and points it at
the turn rate, and `MoveStorm` wraps a storm from one edge to the other.

- [ ] **Step 3: Rewrite `TacticalRules`**

```csharp
/// <summary>
/// What the water a hull is sitting in does to her, for everything the debuff
/// floor applies to.
/// </summary>
/// <remarks>
/// Turn rate is gone with the rest of the inertia model, and so is the weapon
/// effectiveness term, which no caller ever read. The storm is not here: SEA_5
/// §5.1 puts it outside the 0.50 floor, so SpeedRules owns it and this owns
/// only the terms the floor binds.
/// </remarks>
public readonly record struct TacticalModifiers(float SpeedMultiplier);

public static class TacticalRules
{
    /// <summary>Shallow water a tier-1 to tier-3 hull can cross, slowly.</summary>
    public const float ShoalMultiplier = 0.65f;

    /// <summary>A hull under repair holds station rather than running.</summary>
    public const float RepairingMultiplier = 0.5f;

    public static TacticalModifiers Resolve(
        bool slowed,
        float slowMagnitude,
        bool inShoal,
        bool repairing)
    {
        var multiplier = slowed ? 1f - Math.Clamp(slowMagnitude, 0f, 1f) : 1f;
        if (inShoal)
        {
            multiplier *= ShoalMultiplier;
        }

        if (repairing)
        {
            multiplier *= RepairingMultiplier;
        }

        return new TacticalModifiers(multiplier);
    }

    /// <summary>
    /// Where a storm has drifted to. A storm that reaches the border stops
    /// against it and stays until it blows out; it used to be teleported to the
    /// opposite edge, which put a squall on top of a harbour with no warning.
    /// </summary>
    public static (float X, float Y) MoveStorm(
        float positionX,
        float positionY,
        float directionDegrees,
        float speedSquaresPerSecond,
        float deltaSeconds)
    {
        var (directionX, directionY) = GeometryRules.Direction(directionDegrees);
        var travel = speedSquaresPerSecond * deltaSeconds;
        return WorldRules.ClampToMap(
            positionX + (directionX * travel),
            positionY + (directionY * travel));
    }
}
```

The multiplier this returns is `SpeedInputs.DebuffMultiplier`, which
`SpeedRules.Effective` clamps at the 0.50 floor. The storm reaches `Effective`
as `InStorm` and is multiplied outside that floor, which is exactly how SEA_5
§5.1 draws it.

- [ ] **Step 4: Delete `WrapMapCoordinate`**

```sh
grep -rn "WrapMapCoordinate" server --include='*.cs'
```

Remove it and every caller.

- [ ] **Step 5: Run**

```sh
pnpm server:test
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "fix(environment): let a storm slow a ship and stop at the border

The storm multiplier was pointed at the turn rate, which no longer exists, so
sailing through a squall cost nothing. A storm that ran off the east edge was
teleported to the west one, which could drop it on a harbour."
```

### Task 5.4: Recompute speed every tick

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Simulation/SailingSystem.cs`
- Modify: `server/spacetimedb/spacetimedb/Domain/EffectRules.cs`

- [ ] **Step 1: Fix the ammunition range unit bug**

`EffectRules` compares a distance against `ammunition.RangeLimitSquares`. Since
Phase 1 both are in squares, so the comparison is finally right. Add the test
that was never there:

```csharp
[Fact]
public void GrapeshotSlowsAReloadInsideItsShortRange()
{
    var grapeshot = ContentCatalog.Content.Ammunition.Single(a => a.Code == "grapeshot");

    Assert.True(EffectRules.AppliesAtRange(grapeshot, distanceSquares: 3.9f));
    Assert.False(EffectRules.AppliesAtRange(grapeshot, distanceSquares: 4.1f));
}
```

and delete `MinimumSpeedMultiplier` and `SpeedMultiplier` from `EffectRules` —
the floor belongs to `SpeedRules` now, applied once to the product.

- [ ] **Step 2: Replace `RefreshEnvironment`**

```csharp
    /// <summary>
    /// SEA_5 §5.1 says speed is recomputed every tick, so it is. The pieces it
    /// needs that are expensive to look up — which current zone a hull is in —
    /// are still refreshed on a stagger; the arithmetic is not.
    /// </summary>
    private static void RefreshEnvironment(
        ReducerContext ctx,
        TickWorld world,
        ref ShipKinematics ship,
        ulong tick)
    {
        if (SimulationWorkRules.ShouldRefreshCurrent(ship.EntityId, tick))
        {
            var current = CurrentVelocityAt(world.CurrentField(ctx), ship.PositionX, ship.PositionY);
            ship.CurrentVelocityX = current.X;
            ship.CurrentVelocityY = current.Y;
        }

        var windDirection = world.Environment(ctx) is EnvironmentState wind
            ? wind.WindDirectionDegrees
            : 0f;

        var inStorm = HazardRules.HasExposure(ship.EnvironmentExposureCode, HazardKind.Storm);
        var tactical = TacticalRules.Resolve(
            HazardRules.HasExposure(ship.MovementStatusMask, HazardKind.Slow),
            ship.MovementSlowMagnitude,
            HazardRules.HasExposure(ship.EnvironmentExposureCode, HazardKind.Shoal),
            ship.RepairChannelTicks > 0);

        ship.EffectiveSpeedSquaresPerSecond = SpeedRules.Effective(new SpeedInputs(
            ship.BaseSpeedSquaresPerSecond,
            ship.SpeedBonusFraction,
            ship.Hull,
            ship.MaxHull,
            ship.HeadingDegrees,
            windDirection,
            inStorm,
            tactical.SpeedMultiplier,
            ship.IsFrozen));
    }
```

Rename the `MaximumSpeed` / `TacticalMaximumSpeed` / `EffectiveMaximumSpeed`
trio on `Ship` down to `BaseSpeedSquaresPerSecond` (from the hull and its
bonuses, changed only when the fit changes) and
`EffectiveSpeedSquaresPerSecond` (recomputed every tick). Add
`SpeedBonusFraction` and `IsFrozen`.

- [ ] **Step 3: Run**

```sh
pnpm server:test
pnpm ci:fast
```

Expected: PASS, exit 0.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(server): recompute a hull's speed on every tick

Speed was cached and refreshed on a stagger, so a ship kept a stale figure for
up to 1.6 s after a storm, a slow or a hit changed it. Only the current-zone
lookup is staggered now; the arithmetic is free."
```

---

# Phase 6 — Heading, armour faces, range and sight

SEA_5 §6 and §7. Three things in this phase share one root: the heading
convention. Which face a shot lands on, which way a hull is drawn, and how far
she can see are all read off it.

### Task 6.1: Armour faces off the new bearing

**Files:**
- Modify: `server/spacetimedb/spacetimedb/CombatRules.cs`
- Test: `server/spacetimedb/tests/CombatRulesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void AShotFromDeadAheadHitsTheBow()
{
    // She is pointing north; the shooter is north of her, so at bearing 0 from her.
    Assert.Equal(
        ArmorFace.Front,
        CombatRules.FaceHit(defenderHeadingDegrees: 0f, bearingToAttackerDegrees: 0f));
}

[Fact]
public void AShotFromAsternHitsTheStern()
{
    Assert.Equal(
        ArmorFace.Back,
        CombatRules.FaceHit(defenderHeadingDegrees: 0f, bearingToAttackerDegrees: 180f));
}

[Theory]
[InlineData(90f)]
[InlineData(270f)]
[InlineData(46f)]
[InlineData(134f)]
public void EverythingInBetweenHitsTheSide(float bearing)
{
    Assert.Equal(ArmorFace.Side, CombatRules.FaceHit(0f, bearing));
}

[Fact]
public void TheBoundaryAtFortyFiveDegreesBelongsToTheBow()
{
    // SEA_5 §6.3: front is a 90-degree arc, so it owns both its edges.
    Assert.Equal(ArmorFace.Front, CombatRules.FaceHit(0f, 45f));
    Assert.Equal(ArmorFace.Front, CombatRules.FaceHit(0f, 315f));
}

[Fact]
public void TheFaceIsReadFromWhereTheShotCameFromNotWhereItLands()
{
    // A hull steering east, shot at from the north, is hit on her port side.
    Assert.Equal(ArmorFace.Side, CombatRules.FaceHit(defenderHeadingDegrees: 90f, 0f));
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: FAIL on the 45-degree boundary and on the east-heading case, because
the current code uses `>` at the boundary and derives the bearing with the old
`atan2(dx, dy)`.

- [ ] **Step 3: Fix `FaceHit`**

```csharp
    /// <summary>Front is 90 degrees, back is 90 degrees, the rest is side (SEA_5 §6.3).</summary>
    public const float FrontArcHalfWidthDegrees = 45f;
    public const float BackArcHalfWidthDegrees = 45f;

    /// <summary>
    /// Which face a shot lands on. The bearing is to the shooter, taken with
    /// GeometryRules.HeadingTo, so the compass convention is used once and the
    /// difference between the two is a plain signed angle.
    /// </summary>
    public static ArmorFace FaceHit(float defenderHeadingDegrees, float bearingToAttackerDegrees)
    {
        var relative = MathF.Abs(GeometryRules.NormalizeSignedAngle(
            bearingToAttackerDegrees - defenderHeadingDegrees));
        if (relative <= FrontArcHalfWidthDegrees)
        {
            return ArmorFace.Front;
        }

        return relative >= 180f - BackArcHalfWidthDegrees ? ArmorFace.Back : ArmorFace.Side;
    }
```

- [ ] **Step 4: Fix every caller to pass a bearing, not a heading**

```sh
grep -rn "FaceHit" server apps --include='*.cs'
```

Each call site must read:

```csharp
        var bearing = GeometryRules.HeadingTo(
            defender.PositionX, defender.PositionY, attacker.PositionX, attacker.PositionY);
        var face = CombatRules.FaceHit(defender.HeadingDegrees, bearing);
```

- [ ] **Step 5: Run**

```sh
pnpm server:test
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "fix(combat): read the armour face off the compass

The face was worked out from a heading taken with the old atan2 order, so a
hull steering east and shot at from the north took the hit on her bow. The
forty-five degree boundary now belongs to the bow, which is what a ninety
degree arc means."
```

### Task 6.2: `RangeRules` in squares

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Domain/RangeRules.cs`
- Test: `server/spacetimedb/tests/RangeRulesTests.cs`

The stub from Task 1.4 grows into the whole of SEA_5 §7.1, §7.2 and §7.5.

- [ ] **Step 1: Write the failing test**

```csharp
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class RangeRulesTests
{
    [Theory]
    [InlineData(1, 18f)]
    [InlineData(2, 21f)]
    [InlineData(3, 24f)]
    [InlineData(4, 27f)]
    [InlineData(5, 30f)]
    public void EachTierOfGunReachesFurther(byte tier, float squares)
    {
        Assert.Equal(squares, RangeRules.BaseRangeSquares(tier), 4);
    }

    [Fact]
    public void RangeBonusesAddThenCapAtTenPerCent()
    {
        Assert.Equal(19.8f, RangeRules.EffectiveRangeSquares(18f, 0.10f), 4);
        Assert.Equal(19.8f, RangeRules.EffectiveRangeSquares(18f, 0.40f), 4);
        Assert.Equal(18.9f, RangeRules.EffectiveRangeSquares(18f, 0.05f), 4);
    }

    [Fact]
    public void HalfASquareOfGraceIsAllowedOnTheEdge()
    {
        // SEA_5 §13 test 5: at 24.4 squares with a 24-square gun, the shot fires.
        Assert.True(RangeRules.IsWithinRange(distanceSquares: 24.4f, effectiveRangeSquares: 24f));
        Assert.False(RangeRules.IsWithinRange(24.6f, 24f));
    }

    [Fact]
    public void GraceOnlyForgivesTheShotItStarts()
    {
        // SEA_5 §7.2: the grace is checked once, when the trigger is pulled.
        Assert.Equal(0.5f, RangeRules.GraceSquares, 4);
    }

    [Fact]
    public void AShipSeesSixtySquaresAndSubscribesToFiveMore()
    {
        Assert.Equal(60f, RangeRules.ViewDistanceSquares, 4);
        Assert.Equal(65f, RangeRules.SubscriptionRadiusSquares, 4);
    }

    [Fact]
    public void AShotCrossesTheLongestRangeInUnderASecond()
    {
        Assert.True(30f / RangeRules.ProjectileSpeedSquaresPerSecond < 1f);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: FAIL, `BaseRangeSquares` does not exist.

- [ ] **Step 3: Write it**

```csharp
namespace Sea.Server;

/// <summary>How far a gun reaches and how far a captain sees (SEA_5 §7).</summary>
/// <remarks>
/// Everything here is in squares, which is the only unit the world has. The
/// numbers used to be in world units and the comparison against an ammunition's
/// RangeLimitSquares mixed the two, so a grapeshot's four-square reach was
/// tested against a forty-unit distance and never applied.
/// </remarks>
public static class RangeRules
{
    private static readonly float[] BaseRangesByTier = { 18f, 21f, 24f, 27f, 30f };

    /// <summary>Range gear adds together and stops at ten per cent (SEA_5 §7.1).</summary>
    public const float BonusCap = 0.10f;

    /// <summary>
    /// A shot fired at the edge is allowed half a square of slack, so a target
    /// that steps out between the click and the tick is still hit. It is checked
    /// when the trigger is pulled and never again: a shot already in the air
    /// cannot miss for range.
    /// </summary>
    public const float GraceSquares = 0.5f;

    /// <summary>What a captain can see (SEA_5 §7.5).</summary>
    public const float ViewDistanceSquares = 60f;

    /// <summary>
    /// The five squares past the horizon a client subscribes to, so a hull is
    /// already replicated when she sails into sight rather than popping in.
    /// </summary>
    public const float SubscriptionMarginSquares = 5f;

    public const float SubscriptionRadiusSquares =
        ViewDistanceSquares + SubscriptionMarginSquares;

    /// <summary>
    /// Fast enough that the longest shot on the map lands inside one second, so
    /// the flight is something a captain sees rather than something she leads.
    /// </summary>
    public const float ProjectileSpeedSquaresPerSecond = 40f;

    public static float BaseRangeSquares(byte tier) =>
        BaseRangesByTier[Math.Clamp(tier, (byte)1, (byte)5) - 1];

    public static float EffectiveRangeSquares(float baseRangeSquares, float bonusFraction) =>
        baseRangeSquares * (1f + Math.Clamp(bonusFraction, 0f, BonusCap));

    public static bool IsWithinRange(float distanceSquares, float effectiveRangeSquares) =>
        distanceSquares <= effectiveRangeSquares + GraceSquares;
}
```

- [ ] **Step 4: Point the firing path at it**

```sh
grep -rn "RangeSquares\|MaximumRange\|InRange" server/spacetimedb/spacetimedb --include='*.cs'
```

Replace every hand-rolled range comparison in `CombatRules`, the firing
reducer, `TargetingRules` and the NPC rules with
`RangeRules.IsWithinRange(GeometryRules.Distance(...), ...)`.

- [ ] **Step 5: Replace `VisionRadius` with `ViewDistanceSquares`**

`WorldRules.VisionRadius` was deleted in Task 1.2. Every caller now reads
`RangeRules.ViewDistanceSquares`, and the subscription query in
`Simulation/SubscriptionSystem.cs` reads `SubscriptionRadiusSquares`.

- [ ] **Step 6: Run**

```sh
pnpm server:test
pnpm ci:fast
```

Expected: PASS, exit 0.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(combat): measure range in squares with a half-square grace

SEA_5 §7.1. Eighteen to thirty squares by tier, bonuses added then capped at
ten per cent, and a shot at the very edge is allowed to fly. Sight is sixty
squares and a client holds five more so a hull is on screen before she is in
view."
```

### Task 6.3: Range and view debuffs, and the minimap ring

SEA_5 §7.5 and §7.6. A debuff takes flat squares off a range or a view and can
never take more than half. Sea has no such rule today: the ammunition path
multiplies instead, which makes one grapeshot worth 3 squares against a tier 1
gun and 5 against a tier 5, so the weaker gun is punished harder.

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Domain/RangeRules.cs`, `server/spacetimedb/spacetimedb/Domain/EffectRules.cs`
- Test: `server/spacetimedb/tests/RangeRulesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void TheMinimapShowsTwiceAsFarAsACaptainCanSee()
{
    Assert.Equal(120f, RangeRules.MinimapRadiusSquares, 4);
}

[Fact]
public void ARangeDebuffTakesFlatSquaresOff()
{
    Assert.Equal(24f, RangeRules.DebuffedSquares(30f, 6f), 4);
    Assert.Equal(12f, RangeRules.DebuffedSquares(18f, 6f), 4);
}

[Fact]
public void ARangeDebuffCannotTakeMoreThanHalf()
{
    Assert.Equal(15f, RangeRules.DebuffedSquares(30f, 25f), 4);
}

[Fact]
public void AViewDebuffUsesTheSameFloor()
{
    Assert.Equal(30f, RangeRules.DebuffedSquares(RangeRules.ViewDistanceSquares, 90f), 4);
}

[Fact]
public void NoDebuffLeavesTheRangeAlone()
{
    Assert.Equal(27f, RangeRules.DebuffedSquares(27f, 0f), 4);
}
```

- [ ] **Step 2: Run it and watch it fail**

Run: `./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~RangeRulesTests"`
Expected: FAIL, `MinimapRadiusSquares` does not exist.

- [ ] **Step 3: Add the two members to `RangeRules`**

```csharp
    /// <summary>
    /// Twice what a captain can see. A hull inside it is a dot on the minimap and
    /// nothing else: she cannot be selected or fired on until she is inside
    /// <see cref="ViewDistanceSquares"/> (SEA_5 §7.5).
    /// </summary>
    public const float MinimapRadiusSquares = ViewDistanceSquares * 2f;

    /// <summary>Half of base is as much as any debuff can take (SEA_5 §7.6).</summary>
    public const float DebuffFloorFraction = 0.50f;

    /// <summary>
    /// A range or view debuff subtracts flat squares, floored at half of base.
    /// Flat rather than proportional, because a fixed fraction would cost a tier
    /// 5 gun 3 squares where it costs a tier 1 gun 1.8, which is backwards: the
    /// cheap gun is the one that needs the room.
    /// </summary>
    public static float DebuffedSquares(float baseSquares, float subtractedSquares) =>
        MathF.Max(
            baseSquares * DebuffFloorFraction,
            baseSquares - MathF.Max(0f, subtractedSquares));
```

- [ ] **Step 4: Point the effect path at it**

In `EffectRules`, the range and view modifiers become flat squares. Find them
with:

```sh
grep -rn "RangeMultiplier\|ViewMultiplier\|RangeModifier" server/spacetimedb/spacetimedb --include='*.cs'
```

Every one becomes a subtraction through `RangeRules.DebuffedSquares`. The
ammunition JSON field that carried a multiplier becomes
`rangePenaltySquares`, and Task 8.2 authors it in squares.

- [ ] **Step 5: Point the minimap at it**

`SeaMinimapView` computes its ring from `RangeRules.MinimapRadiusSquares`
rather than from a hard-coded number.

- [ ] **Step 6: Run**

```sh
pnpm server:test
pnpm unity:test
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(combat): take range off in squares, not in per cent

SEA_5 §7.6. A flat penalty costs every tier the same water, where the old
multiplier charged the longest gun the most and the shortest gun the least. Half
of base is the floor, so no stack of debuffs can leave a gun unable to reach.
The minimap ring is twice the view distance and now says so in one place."
```

---

# Phase 7 — Firing and critical hits

SEA_5 §8. The magazine, the reload and the ammunition types already work and
this phase leaves them alone. What changes is the roll on top of a hit and the
behaviour of a held trigger.

### Task 7.1: `CriticalHitRules`

**Files:**
- Create: `server/spacetimedb/spacetimedb/Domain/CriticalHitRules.cs`
- Test: `server/spacetimedb/tests/CriticalHitRulesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class CriticalHitRulesTests
{
    [Fact]
    public void AboutOneShotInTenIsACritical()
    {
        var criticals = 0;
        for (var shot = 0UL; shot < 100_000UL; shot++)
        {
            if (CriticalHitRules.IsCritical(seed: 7UL, tick: shot, attackerId: 1UL, defenderId: 2UL))
            {
                criticals++;
            }
        }

        // Ten per cent of a hundred thousand, inside half a per cent either way.
        Assert.InRange(criticals, 9_500, 10_500);
    }

    [Fact]
    public void TheSameShotAlwaysRollsTheSameWay()
    {
        var first = CriticalHitRules.IsCritical(7UL, 4242UL, 11UL, 22UL);
        var second = CriticalHitRules.IsCritical(7UL, 4242UL, 11UL, 22UL);

        Assert.Equal(first, second);
    }

    [Fact]
    public void TwoShipsFiringOnTheSameTickRollSeparately()
    {
        var rolls = new HashSet<bool>();
        for (var attacker = 1UL; attacker <= 40UL; attacker++)
        {
            rolls.Add(CriticalHitRules.IsCritical(7UL, 100UL, attacker, 500UL));
        }

        Assert.Equal(2, rolls.Count);
    }

    [Fact]
    public void ACriticalIsHalfAgainAsMuchDamage()
    {
        Assert.Equal(150u, CriticalHitRules.Apply(100u, isCritical: true));
        Assert.Equal(100u, CriticalHitRules.Apply(100u, isCritical: false));
    }

    [Fact]
    public void ACriticalRoundsDownSoOneStaysOne()
    {
        Assert.Equal(1u, CriticalHitRules.Apply(1u, isCritical: true));
        Assert.Equal(4u, CriticalHitRules.Apply(3u, isCritical: true));
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: build error, `CriticalHitRules` does not exist.

- [ ] **Step 3: Write it**

```csharp
namespace Sea.Server;

/// <summary>
/// The one roll in the whole of combat. Everything else about a shot is
/// deterministic (SEA_5 §8.2), so this is the only place a fight is not.
/// </summary>
/// <remarks>
/// The roll is a hash of the world seed, the tick and both ships rather than a
/// running generator, because a running generator is state a replay has to
/// reproduce exactly and a hash is not. Two hulls firing on the same tick get
/// different answers because both entity ids go into the mix.
/// </remarks>
public static class CriticalHitRules
{
    public const float Chance = 0.10f;

    public const float Multiplier = 1.5f;

    public static bool IsCritical(ulong seed, ulong tick, ulong attackerId, ulong defenderId)
    {
        var state = Mix(seed ^ Mix(tick) ^ Mix(attackerId * 0x9E3779B97F4A7C15UL) ^ Mix(defenderId));

        // The top 24 bits give a uniform fraction with room to spare at 10 Hz.
        var roll = (state >> 40) / (float)(1UL << 24);
        return roll < Chance;
    }

    public static uint Apply(uint damage, bool isCritical) =>
        isCritical ? (uint)MathF.Round(damage * Multiplier, MidpointRounding.AwayFromZero) : damage;

    private static ulong Mix(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
```

- [ ] **Step 4: Run the test and watch it pass**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~CriticalHitRulesTests"
```

Expected: `Passed! - Failed: 0, Passed: 5`.

- [ ] **Step 5: Roll it in the damage path**

In whichever file `grep -rn "ResolveDamage" server/spacetimedb/spacetimedb --include='*.cs'`
names, after armour and before the hull is reduced:

```csharp
        var isCritical = CriticalHitRules.IsCritical(
            world.Seed, tick, attacker.EntityId, defender.EntityId);
        damage = CriticalHitRules.Apply(damage, isCritical);
```

and carry `isCritical` out on the combat event row the client already reads, so
the HUD can show it.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(combat): let one shot in ten land twice as hard

SEA_5 §8.1 as amended: ten per cent for half again the damage. The roll is a
hash of the seed, the tick and both hulls rather than a running generator, so a
replay of the same log lands the same criticals."
```

### Task 7.2: A held trigger keeps firing

**Files:**
- Modify: `apps/game-unity/Assets/Presentation/SeaCombatInput.cs`
- Modify: `server/spacetimedb/spacetimedb/Domain/CommandPolicy.cs`
- Test: `apps/game-unity/Assets/Tests/EditMode/SeaCombatInputTests.cs`

SEA_5 §7.3: holding `Q` fires as fast as the magazine allows. The magazine and
the reload stay exactly as they are — this is an input change, not a combat
change.

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void HoldingFireSendsOneCommandPerReadyVolley()
{
    var input = new SeaFireRepeater();
    var sent = 0;

    input.Update(held: true, readyVolleys: 3, () => sent++);

    Assert.AreEqual(3, sent);
}

[Test]
public void HoldingFireWithAnEmptyMagazineSendsNothing()
{
    var input = new SeaFireRepeater();
    var sent = 0;

    input.Update(held: true, readyVolleys: 0, () => sent++);

    Assert.AreEqual(0, sent);
}

[Test]
public void ReleasingAndPressingAgainIsStillOneCommandPerVolley()
{
    var input = new SeaFireRepeater();
    var sent = 0;

    input.Update(true, 1, () => sent++);
    input.Update(false, 1, () => sent++);
    input.Update(true, 1, () => sent++);

    Assert.AreEqual(2, sent);
}
```

- [ ] **Step 2: Run it and watch it fail**

```sh
pnpm unity:test
```

Expected: FAIL, `SeaFireRepeater` does not exist.

- [ ] **Step 3: Write it**

```csharp
namespace Sea.Client
{
    /// <summary>
    /// Turns a held fire key into one command per ready volley (SEA_5 §7.3).
    /// </summary>
    /// <remarks>
    /// The magazine is the rate limiter, so this asks for exactly what is ready
    /// and nothing more. Sending on every frame instead would put sixty commands
    /// a second on the wire for a magazine that can answer three.
    /// </remarks>
    public sealed class SeaFireRepeater
    {
        public void Update(bool held, int readyVolleys, System.Action fire)
        {
            if (!held)
            {
                return;
            }

            for (var volley = 0; volley < readyVolleys; volley++)
            {
                fire();
            }
        }
    }
}
```

- [ ] **Step 4: Wire it into `SeaCombatInput`**

Replace the `WasPressedThisFrame` test on the fire binding with
`IsPressed`, and route it through the repeater with the magazine count the HUD
already holds.

- [ ] **Step 5: Run**

```sh
pnpm unity:test
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(client): fire while the key is held

SEA_5 §7.3. The magazine is still the rate limiter, so a held key asks for
exactly the volleys that are ready rather than one command a frame."
```

---

> ## Review gate after Phase 7
>
> Combat is now finished. Ask the user to run
> `/thermo-nuclear-code-quality-review` and `/improve-codebase-architecture`
> over Phases 5 to 7 before the content work begins, because Phase 8 seeds
> numbers these rules read and a mistake there is much harder to see.

### Task 7.3: One typed hit event per volley

SEA_5 §8.1 wants one event per volley carrying attacker, defender, damage, the
critical flag, the armour face and the flight time. Sea raises a `CombatEvent`
with two strings, `EventType` and `Details`, which means two string allocations
on the hot path per volley and a client that parses text to draw a number.
§8.3 needs the flight time as a number, so the strings have to go.

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Schema/Tables.cs`, `server/spacetimedb/spacetimedb/Events/CombatEvents.cs`, `server/spacetimedb/spacetimedb/Simulation/DamageSystem.cs`
- Modify: `apps/game-unity/Assets/Presentation/SeaCombatView.cs`
- Test: `server/spacetimedb/tests/integration/HitEventTests.cs`

- [ ] **Step 1: Write the failing integration test**

```csharp
[Fact]
public void OneVolleyRaisesExactlyOneHitEvent()
{
    using var world = TestWorld.Start();
    var attacker = world.SpawnPlayerShip(1, 200f, 200f);
    var defender = world.SpawnNpc(1, 210f, 200f);

    world.IssueFire(attacker, defender);
    world.RunTicks(1);

    Assert.Single(world.HitEvents(attacker));
}

[Fact]
public void TheEventCarriesTheFaceTheCritFlagAndTheFlightTime()
{
    using var world = TestWorld.Start();
    var attacker = world.SpawnPlayerShip(1, 200f, 200f);
    var defender = world.SpawnNpc(1, 220f, 200f);
    world.SetHeading(defender, 90f);  // pointing east, away from the attacker

    world.IssueFire(attacker, defender);
    world.RunTicks(1);

    var hit = world.HitEvents(attacker).Single();
    Assert.Equal(defender, hit.DefenderEntityId);
    Assert.Equal((byte)ArmorFace.Back, hit.Face);
    Assert.True(hit.Damage > 0);
    // Twenty squares at forty squares a second.
    Assert.Equal(0.5f, hit.FlightSeconds, 3);
}

[Fact]
public void AnIslandBetweenTwoShipsDoesNotStopAShot()
{
    using var world = TestWorld.Start();
    // Gull Point sits between these two, and a course between them has to go
    // round it. A shot does not.
    var attacker = world.SpawnPlayerShip(1, 140f, 100f);
    var defender = world.SpawnNpc(1, 160f, 100f);
    Assert.False(ContentCatalog.LandMaskFor(1).SegmentIsClear(140f, 100f, 160f, 100f));

    world.IssueFire(attacker, defender);
    world.RunTicks(1);

    Assert.Single(world.HitEvents(attacker));
}

[Fact]
public void OutOfRangeTheMagazineKeepsLoadingAndFiringResumesByItself()
{
    using var world = TestWorld.Start();
    var attacker = world.SpawnPlayerShip(1, 200f, 200f);
    var defender = world.SpawnNpc(1, 260f, 200f);   // sixty squares: far out of reach

    world.IssueFireHeld(attacker, defender, held: true);
    world.RunTicks(40);
    Assert.Empty(world.HitEvents(attacker));
    Assert.Equal(defender, world.Ship(attacker).TargetEntityId);
    var loaded = world.Ship(attacker).LoadedVolleys;

    world.Teleport(defender, 210f, 200f);
    world.RunTicks(2);

    Assert.Single(world.HitEvents(attacker));
    Assert.True(loaded > 0);
}

[Fact]
public void ReleasingTheTriggerStopsTheFiringAndKeepsTheTarget()
{
    using var world = TestWorld.Start();
    var attacker = world.SpawnPlayerShip(1, 200f, 200f);
    var defender = world.SpawnNpc(1, 210f, 200f);

    world.IssueFireHeld(attacker, defender, held: true);
    world.RunTicks(2);
    world.IssueFireHeld(attacker, defender, held: false);
    var after = world.HitEvents(attacker).Count;
    world.RunTicks(60);

    Assert.Equal(after, world.HitEvents(attacker).Count);
    Assert.Equal(defender, world.Ship(attacker).TargetEntityId);
}
```

- [ ] **Step 2: Run it and watch it fail**

Run: `pnpm server:test`
Expected: FAIL — there is no `HitEvent` table.

- [ ] **Step 3: Add the table**

In `Schema/Tables.cs`, beside `CombatEvent`:

```csharp
    /// <summary>
    /// What one volley did, as numbers rather than as text. The server has already
    /// applied the damage when this is raised; the flight time is only how long
    /// the client waits before drawing the impact, so a shot and its number land
    /// together (SEA_5 §8.1, §8.3).
    /// </summary>
    [SpacetimeDB.Table(Accessor = "HitEvent", Public = true, Event = true)]
    public partial struct HitEvent
    {
        public ulong AttackerEntityId;
        public ulong DefenderEntityId;
        public uint Damage;
        public bool IsCritical;
        public byte Face;
        public float FlightSeconds;
        public ulong Tick;
    }
```

- [ ] **Step 4: Add the flight time to `RangeRules`**

```csharp
    /// <summary>
    /// How long a cannonball is in the air. Visual only: the damage was applied
    /// when the trigger was pulled, and no amount of sailing can undo it
    /// (SEA_5 §8.3, §8.4).
    /// </summary>
    public static float FlightSeconds(float distanceSquares) =>
        distanceSquares / ProjectileSpeedSquaresPerSecond;
```

- [ ] **Step 5: Raise it where damage is applied**

In `DamageSystem`, the volley resolution already knows the attacker, the
defender, the face from Task 6.1 and the damage from Task 7.1. Replace the
`AppendEvent(ctx, tick, attacker, "hit", $"...")` call with:

```csharp
        ctx.Db.HitEvent.Insert(new HitEvent
        {
            AttackerEntityId = attacker.EntityId,
            DefenderEntityId = defender.EntityId,
            Damage = damage,
            IsCritical = isCritical,
            Face = (byte)face,
            FlightSeconds = RangeRules.FlightSeconds(distanceSquares),
            Tick = tick,
        });
```

`AppendEvent` stays for the events that are not raised per volley. Nothing about
the shot is tested against land: SEA_5 §8.5 says islands, ships, towers and
storms never block a shot, so there is no line-of-sight check in the firing path
and `LandMask.SegmentIsClear` is used by routing only.

- [ ] **Step 6: Hold fire out of range without dropping anything**

The firing path returns without raising an event when the target is beyond
`IsWithinRange`. It must not clear `TargetEntityId`, must not clear the held
flag, and must not touch the reload, so the magazine fills while a captain
closes the distance and the next volley goes off by itself (SEA_5 §7.3).

- [ ] **Step 7: Draw it on the client**

`SeaCombatView` subscribes to `HitEvent`, draws the cannonballs, and shows the
damage number and the impact after `FlightSeconds`. A defender at zero hull is
already sunk on the server; the client holds the sinking animation for the same
`FlightSeconds` so a hull does not go down before the shot arrives (SEA_5 §8.3).

- [ ] **Step 8: Run**

```sh
pnpm server:test
pnpm unity:test
```

Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat(combat): raise one typed hit event per volley

SEA_5 §8.1. Damage, the critical flag, the armour face and the flight time were
formatted into two strings and parsed back by the client, which cost two
allocations a volley on the tick and could not carry a flight time at all. The
client now waits the ball out before drawing the number, and holds a sinking for
the same time, so a hull never goes down before the shot that sank her lands."
```

---

# Phase 8 — The content: five hulls, five cannons, three maps

Everything up to here has been rules with no world to apply them to but a
Havenmere scaled by twenty. This phase writes the real one.

### Task 8.1: Five hulls

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Content/Data/hulls.json`
- Test: `server/spacetimedb/tests/ContentCatalogTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Theory]
[InlineData("skiff", 1, 5.6f, 400u)]
[InlineData("sloop", 2, 5.3f, 700u)]
[InlineData("brig", 3, 5.0f, 1100u)]
[InlineData("frigate", 4, 4.7f, 1600u)]
[InlineData("galleon", 5, 4.4f, 2200u)]
public void EveryTierOfHullIsSeeded(string code, byte tier, float speed, uint maxHull)
{
    var hull = ContentCatalog.Content.Hulls.Single(h => h.Code == code);

    Assert.Equal(tier, hull.Tier);
    Assert.Equal(speed, hull.BaseSpeedSquaresPerSecond, 4);
    Assert.Equal(maxHull, hull.MaxHull);
}

[Fact]
public void ABiggerHullIsAlwaysSlowerAndTougher()
{
    var byTier = ContentCatalog.Content.Hulls.OrderBy(hull => hull.Tier).ToList();

    Assert.Equal(5, byTier.Count);
    for (var index = 1; index < byTier.Count; index++)
    {
        Assert.True(byTier[index].BaseSpeedSquaresPerSecond <
                    byTier[index - 1].BaseSpeedSquaresPerSecond);
        Assert.True(byTier[index].MaxHull > byTier[index - 1].MaxHull);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: FAIL, only `sloop` is seeded.

- [ ] **Step 3: Write the file**

Speeds are SEA_5 §4.4. Hull, armour and Combat Power are SEA_2_MATH §2.4, which
stays authoritative because SEA_5 is silent on them. `Acceleration`,
`Deceleration` and `TurnRateDegrees` are gone from the schema, so they are gone
from the JSON too.

```json
{
  "hulls": [
    {
      "code": "skiff",
      "name": "Skiff",
      "tier": 1,
      "baseSpeedSquaresPerSecond": 5.6,
      "maxHull": 400,
      "armorFront": 4,
      "armorSide": 3,
      "armorBack": 2,
      "cannonSlots": 2,
      "combatPower": 10
    },
    {
      "code": "sloop",
      "name": "Sloop",
      "tier": 2,
      "baseSpeedSquaresPerSecond": 5.3,
      "maxHull": 700,
      "armorFront": 7,
      "armorSide": 5,
      "armorBack": 3,
      "cannonSlots": 3,
      "combatPower": 25
    },
    {
      "code": "brig",
      "name": "Brig",
      "tier": 3,
      "baseSpeedSquaresPerSecond": 5.0,
      "maxHull": 1100,
      "armorFront": 11,
      "armorSide": 8,
      "armorBack": 5,
      "cannonSlots": 4,
      "combatPower": 45
    },
    {
      "code": "frigate",
      "name": "Frigate",
      "tier": 4,
      "baseSpeedSquaresPerSecond": 4.7,
      "maxHull": 1600,
      "armorFront": 16,
      "armorSide": 12,
      "armorBack": 7,
      "cannonSlots": 5,
      "combatPower": 70
    },
    {
      "code": "galleon",
      "name": "Galleon",
      "tier": 5,
      "baseSpeedSquaresPerSecond": 4.4,
      "maxHull": 2200,
      "armorFront": 22,
      "armorSide": 16,
      "armorBack": 10,
      "cannonSlots": 6,
      "combatPower": 100
    }
  ]
}
```

- [ ] **Step 4: Regenerate and run**

```sh
pnpm content:generate
pnpm server:test
```

Expected: PASS. `ContentCatalog.g.cs` changes; never edit it by hand.

- [ ] **Step 5: Commit**

```bash
git add server/spacetimedb/spacetimedb/Content server/spacetimedb/tests/ContentCatalogTests.cs
git commit -m "feat(content): seed all five hulls

Speeds from SEA_5 §4.4, hull, armour and combat power from SEA_2_MATH §2.4.
One seeded hull was enough to prove the stat pipeline; five are needed before
any of the speed or armour rules can be felt."
```

### Task 8.2: Five cannons

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Content/Data/cannons.json`
- Test: `server/spacetimedb/tests/ContentCatalogTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Theory]
[InlineData("iron_cannon", 1, 18f, 12u, 3.0f)]
[InlineData("bronze_cannon", 2, 21f, 20u, 3.2f)]
[InlineData("long_nine", 3, 24f, 30u, 3.4f)]
[InlineData("carronade", 4, 27f, 44u, 3.6f)]
[InlineData("great_gun", 5, 30f, 60u, 3.8f)]
public void EveryTierOfCannonIsSeeded(
    string code, byte tier, float range, uint damage, float reload)
{
    var cannon = ContentCatalog.Content.Cannons.Single(c => c.Code == code);

    Assert.Equal(tier, cannon.Tier);
    Assert.Equal(range, RangeRules.BaseRangeSquares(cannon.Tier), 4);
    Assert.Equal(damage, cannon.DamagePerShot);
    Assert.Equal(reload, cannon.ReloadSeconds, 4);
}

[Fact]
public void ACannonsRangeAlwaysMatchesItsTier()
{
    foreach (var cannon in ContentCatalog.Content.Cannons)
    {
        Assert.Equal(
            RangeRules.BaseRangeSquares(cannon.Tier), cannon.RangeSquares, 4);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: FAIL, only `iron_cannon` is seeded.

- [ ] **Step 3: Write the file**

Ranges are SEA_5 §7.1; damage and reload are SEA_2_MATH §2.5.

```json
{
  "cannons": [
    { "code": "iron_cannon",   "name": "Iron Cannon",   "tier": 1, "rangeSquares": 18, "damagePerShot": 12, "reloadSeconds": 3.0, "combatPower": 5 },
    { "code": "bronze_cannon", "name": "Bronze Cannon", "tier": 2, "rangeSquares": 21, "damagePerShot": 20, "reloadSeconds": 3.2, "combatPower": 12 },
    { "code": "long_nine",     "name": "Long Nine",     "tier": 3, "rangeSquares": 24, "damagePerShot": 30, "reloadSeconds": 3.4, "combatPower": 22 },
    { "code": "carronade",     "name": "Carronade",     "tier": 4, "rangeSquares": 27, "damagePerShot": 44, "reloadSeconds": 3.6, "combatPower": 35 },
    { "code": "great_gun",     "name": "Great Gun",     "tier": 5, "rangeSquares": 30, "damagePerShot": 60, "reloadSeconds": 3.8, "combatPower": 50 }
  ]
}
```

- [ ] **Step 4: Regenerate and run**

```sh
pnpm content:generate
pnpm server:test
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add server/spacetimedb/spacetimedb/Content server/spacetimedb/tests/ContentCatalogTests.cs
git commit -m "feat(content): seed all five cannons

Ranges from SEA_5 §7.1, damage and reload from SEA_2_MATH §2.5. A tier's range
is now asserted against RangeRules rather than repeated, so the two cannot
drift."
```

### Task 8.3: Havenmere at four hundred squares

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Content/Data/maps.json`
- Test: `server/spacetimedb/tests/MapContentTests.cs`

This throws away the ×20 bridge from Task 1.6 and authors the map at its real
scale. Havenmere is the starting map: it has to be open enough to learn to sail
in and interesting enough to fight in.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void HavenmereIsFourHundredSquaresEachWay()
{
    var map = ContentCatalog.Content.Maps.Single(m => m.Code == "havenmere");

    Assert.Equal(400, map.Width);
    Assert.Equal(400, map.Height);
}

[Fact]
public void TheHarbourSitsInOpenWater()
{
    var map = ContentCatalog.Content.Maps.Single(m => m.Code == "havenmere");
    var harbor = map.Objects.Single(o => o.Code == "harbor");
    var mask = ContentCatalog.LandMaskFor(map.Id);

    for (var angle = 0f; angle < 360f; angle += 15f)
    {
        var (dx, dy) = GeometryRules.Direction(angle);
        Assert.False(mask.IsLand(
            harbor.PositionX + (dx * WorldRules.HarborSafeRadiusSquares),
            harbor.PositionY + (dy * WorldRules.HarborSafeRadiusSquares)));
    }
}

[Fact]
public void EveryMapHasAWayFromOneEdgeToTheOther()
{
    // If A* cannot cross a map, some island chain has walled it in two.
    foreach (var map in ContentCatalog.Content.Maps)
    {
        var mask = ContentCatalog.LandMaskFor(map.Id);
        var scratch = new PathfindingScratch(mask.Size);
        Span<RouteWaypoint> route = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];

        var outcome = PathfindingRules.TryBuildRoute(
            mask, scratch, 8f, 8f, 392f, 392f, route, out var count);

        Assert.NotEqual(PathOutcome.NoPath, outcome);
        Assert.True(count > 0);
    }
}

[Fact]
public void NoIslandSitsOnTheStripAShipArrivesOn()
{
    // She arrives at the same distance along the edge she left at, so the whole
    // strip has to be water, not one landing point. An island here would put an
    // arriving captain inside land with no way to sail out of it.
    foreach (var map in ContentCatalog.Content.Maps)
    {
        var mask = ContentCatalog.LandMaskFor(map.Id);
        foreach (var neighbour in ContentCatalog.Content.Maps)
        {
            foreach (var exit in neighbour.Exits)
            {
                if (exit.ToMapId != map.Id)
                {
                    continue;
                }

                for (var along = WorldRules.MapMin; along <= WorldRules.MapMax; along += 0.5f)
                {
                    var (x, y) = MapEdgeRules.ArrivalPoint(exit.Edge, along);
                    Assert.False(mask.IsLand(x, y));
                }
            }
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: FAIL — the bridge produced a 400-square map whose harbour circle and
island radii were all multiplied by twenty, so the harbour has a 600-square
safe circle and the islands are absurd.

- [ ] **Step 3: Author Havenmere**

Delete the `terrainRows` field and the scaled objects, and write the map as
shapes at their real size. The shape of the map: a wide bay in the south-west
with the harbour in it, a chain of islands running north-east that a captain
learns to weave through, a reef field in the east where the veterans patrol,
and open water in the north where Red Mary sails.

```json
{
  "id": 1,
  "code": "havenmere",
  "name": "Havenmere",
  "coordinate": "1/1",
  "width": 400,
  "height": 400,
  "spawnX": 70,
  "spawnY": 330,
  "objects": [
    { "code": "harbor", "kind": "harbor", "name": "Port Lowell", "positionX": 70, "positionY": 330, "radius": 12 },

    { "code": "isle_lowell",   "kind": "island", "name": "Lowell Head",   "positionX": 40,  "positionY": 300, "radius": 18 },
    { "code": "isle_carrick",  "kind": "island", "name": "Carrick",       "positionX": 150, "positionY": 250, "radius": 26 },
    { "code": "isle_pennant",  "kind": "island", "name": "Pennant",       "positionX": 205, "positionY": 195, "radius": 20 },
    { "code": "isle_marrow",   "kind": "island", "name": "Marrow",        "positionX": 250, "positionY": 140, "radius": 24 },
    { "code": "isle_teal",     "kind": "island", "name": "Teal Rock",     "positionX": 118, "positionY": 118, "radius": 14 },
    { "code": "isle_far",      "kind": "island", "name": "Far Havenmere", "positionX": 330, "positionY": 250, "radius": 30 },

    { "code": "reef_east_a",  "kind": "reef", "positionX": 300, "positionY": 180, "radius": 12 },
    { "code": "reef_east_b",  "kind": "reef", "positionX": 322, "positionY": 205, "radius": 10 },
    { "code": "reef_east_c",  "kind": "reef", "positionX": 285, "positionY": 215, "radius": 9 },
    { "code": "reef_south",   "kind": "reef", "positionX": 190, "positionY": 340, "radius": 14 },

    { "code": "shoal_bay",    "kind": "shoal", "positionX": 105, "positionY": 300, "radius": 22 },
    { "code": "shoal_north",  "kind": "shoal", "positionX": 200, "positionY": 60,  "radius": 26 },

    { "code": "current_bay",   "kind": "current", "positionX": 120, "positionY": 320, "radius": 45, "velocityX": 0.18, "velocityY": -0.12 },
    { "code": "current_strait","kind": "current", "positionX": 230, "positionY": 200, "radius": 55, "velocityX": -0.10, "velocityY": -0.22 },
    { "code": "current_north", "kind": "current", "positionX": 300, "positionY": 80,  "radius": 60, "velocityX": 0.25, "velocityY": 0.08 }
  ],
  "exits": [
    { "edge": "north", "toMapId": 3 },
    { "edge": "east",  "toMapId": 2 }
  ]
}
```

Every current velocity is at or under 0.3 squares a second, which SEA_5 §5.2
caps. Every island is clear of the harbour's 30-square safe water and of both
exit spawn points, which the tests in Step 1 check rather than trust.

- [ ] **Step 4: Move the NPC patrol routes onto the new map**

`Content/Data/npcs.json` holds patrol waypoints in the old coordinates. Rewrite
them by hand at the new scale: the twelve patrol slots run the island chain
between Carrick and Marrow, and Red Mary and her two consorts sit in the open
water north of Far Havenmere at roughly (330, 90).

- [ ] **Step 5: Regenerate and run**

```sh
pnpm content:generate
pnpm server:test
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add server/spacetimedb/spacetimedb/Content server/spacetimedb/tests
git commit -m "feat(content): author Havenmere at four hundred squares

The scaled map from the unit change was a placeholder with a six-hundred-square
harbour in it. This is the map drawn at the scale it is played at: a bay to
learn in, an island chain to weave, a reef field for the veterans, and open
water in the north."
```

### Task 8.4: Gull Rocks and Brine Fields

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Content/Data/maps.json`, `npcs.json`
- Test: `server/spacetimedb/tests/MapContentTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void ThereAreThreeMaps()
{
    Assert.Equal(3, ContentCatalog.Content.Maps.Count);
}

[Theory]
[InlineData(1, "1/1")]
[InlineData(2, "1/2")]
[InlineData(3, "1/3")]
public void EachMapKnowsItsChartReference(byte id, string coordinate)
{
    Assert.Equal(coordinate, ContentCatalog.Content.Maps.Single(m => m.Id == id).Coordinate);
}

[Fact]
public void EveryExitLeadsToAMapThatLeadsBack()
{
    foreach (var map in ContentCatalog.Content.Maps)
    {
        foreach (var exit in map.Exits)
        {
            var neighbour = ContentCatalog.Content.Maps.Single(m => m.Id == exit.ToMapId);
            Assert.Contains(neighbour.Exits, back => back.ToMapId == map.Id);
        }
    }
}

[Fact]
public void ABiggerMapNumberHoldsHarderEnemies()
{
    var byMap = ContentCatalog.Content.Npcs
        .GroupBy(npc => npc.MapId)
        .ToDictionary(group => group.Key, group => group.Max(npc => npc.Tier));

    Assert.True(byMap[2] >= byMap[1]);
    Assert.True(byMap[3] >= byMap[2]);
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: FAIL, one map.

- [ ] **Step 3: Author the two maps**

Gull Rocks (1/2, east of Havenmere) is the crowded one: many small islands and
reefs, narrow water, tier 2 to 3 enemies, a harbour at (60, 200) reached
through a strait. Brine Fields (1/3, north of Havenmere) is the open one: three
large islands, wide sea, strong currents, tier 3 to 4 enemies and two storms,
harbour at (200, 340).

Write both with the same field shape as Havenmere in Task 8.3. Every exit must
have a matching exit on the other side, which the test in Step 1 enforces.
Havenmere's north exit goes to Brine Fields and Brine Fields' south exit comes
back to Havenmere; Havenmere's east exit goes to Gull Rocks and Gull Rocks'
west exit comes back. An exit names only the edge and the map beyond it: where
a captain arrives is her own position along that edge, so both arrival strips
have to be open water for their whole length. Keep the islands eight squares
clear of every edge that has a neighbour.

- [ ] **Step 4: Populate them**

Twelve patrol slots on Gull Rocks at tiers 2 and 3, and fifteen on Brine Fields
at tiers 3 and 4, using the enemy definitions already seeded plus two new ones
for the higher tiers. NPC multipliers come from SEA_2_MATH §7.1.

- [ ] **Step 5: Regenerate and run**

```sh
pnpm content:generate
pnpm server:test
pnpm ci:fast
```

Expected: PASS, exit 0. The `EveryMapHasAWayFromOneEdgeToTheOther` test from
Task 8.3 now runs over all three.

- [ ] **Step 6: Commit**

```bash
git add server/spacetimedb/spacetimedb/Content server/spacetimedb/tests
git commit -m "feat(content): open Gull Rocks and Brine Fields

Two maps so that crossing a map edge is something that can be sailed and tested
rather than described. Gull Rocks is close water that rewards knowing the
channels; Brine Fields is open sea with weather in it."
```

---

# Phase 9 — Zones, land and map edges

SEA_5 §10. Three maps exist, so crossing between them can finally be built.

### Task 9.1: `MapEdgeRules`

**Files:**
- Create: `server/spacetimedb/spacetimedb/Domain/MapEdgeRules.cs`
- Test: `server/spacetimedb/tests/MapEdgeRulesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class MapEdgeRulesTests
{
    [Theory]
    [InlineData(200f, 3f, MapEdge.North)]
    [InlineData(397f, 200f, MapEdge.East)]
    [InlineData(200f, 397f, MapEdge.South)]
    [InlineData(2f, 200f, MapEdge.West)]
    [InlineData(200f, 200f, MapEdge.None)]
    [InlineData(200f, 7f, MapEdge.None)]
    public void TheOuterSixSquaresAreTheCrossing(float x, float y, MapEdge expected)
    {
        Assert.Equal(expected, MapEdgeRules.EdgeAt(x, y));
    }

    [Fact]
    public void ACornerBelongsToWhicheverEdgeIsNearer()
    {
        // Two squares from the north edge and four from the west: she goes north.
        Assert.Equal(MapEdge.North, MapEdgeRules.EdgeAt(4f, 2f));
        Assert.Equal(MapEdge.West, MapEdgeRules.EdgeAt(2f, 4f));
    }

    [Fact]
    public void ArrivingPutsHerEightSquaresInFromTheOppositeEdge()
    {
        var (x, y) = MapEdgeRules.ArrivalPoint(MapEdge.North, alongAxis: 150f);

        Assert.Equal(150f, x, 4);
        Assert.Equal(WorldRules.MapMax - MapEdgeRules.SpawnInsetSquares, y, 4);
    }

    [Fact]
    public void SheArrivesWhereSheLeftAlongTheEdgeSoACrossingIsNotATeleport()
    {
        var (x, _) = MapEdgeRules.ArrivalPoint(MapEdge.North, alongAxis: 37f);

        Assert.Equal(37f, x, 4);
    }

    [Fact]
    public void ABorderWithNothingBeyondItHoldsHerJustInsideTheBand()
    {
        var (x, y) = MapEdgeRules.HoldInside(399f, 200f);

        Assert.Equal(WorldRules.MapMax - MapEdgeRules.BandSquares, x, 4);
        Assert.Equal(200f, y, 4);
    }

    [Fact]
    public void HoldingInsideLeavesAShipInOpenWaterAlone()
    {
        var (x, y) = MapEdgeRules.HoldInside(200f, 200f);

        Assert.Equal(200f, x, 4);
        Assert.Equal(200f, y, 4);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: build error, `MapEdgeRules` does not exist.

- [ ] **Step 3: Write it**

```csharp
namespace Sea.Server;

public enum MapEdge : byte
{
    None = 0,
    North = 1,
    East = 2,
    South = 3,
    West = 4,
}

/// <summary>Sailing off one chart and onto the next (SEA_5 §10.2).</summary>
/// <remarks>
/// The band is six squares wide, which is about a second of sailing, so a
/// captain who meant to cross has crossed and one who was following the coast
/// has time to turn. She arrives at the same place along the far edge she left
/// at, so the crossing reads as continuing rather than as being moved.
/// </remarks>
public static class MapEdgeRules
{
    public const float BandSquares = 6f;

    /// <summary>
    /// How far in she appears on the new map. Larger than the band, so arriving
    /// does not put her straight back into a crossing and bounce her between
    /// two charts.
    /// </summary>
    public const float SpawnInsetSquares = 8f;

    public static MapEdge EdgeAt(float x, float y)
    {
        var toNorth = y - WorldRules.MapMin;
        var toSouth = WorldRules.MapMax - y;
        var toWest = x - WorldRules.MapMin;
        var toEast = WorldRules.MapMax - x;
        var nearest = MathF.Min(MathF.Min(toNorth, toSouth), MathF.Min(toWest, toEast));
        if (nearest >= BandSquares)
        {
            return MapEdge.None;
        }

        if (nearest == toNorth)
        {
            return MapEdge.North;
        }

        if (nearest == toWest)
        {
            return MapEdge.West;
        }

        return nearest == toSouth ? MapEdge.South : MapEdge.East;
    }

    /// <summary>
    /// Where she appears on the map she has sailed onto. Crossing north puts her
    /// near the southern edge of the map above, at the same distance along it.
    /// </summary>
    public static (float X, float Y) ArrivalPoint(MapEdge crossed, float alongAxis)
    {
        var inset = SpawnInsetSquares;
        return crossed switch
        {
            MapEdge.North => (alongAxis, WorldRules.MapMax - inset),
            MapEdge.South => (alongAxis, WorldRules.MapMin + inset),
            MapEdge.West => (WorldRules.MapMax - inset, alongAxis),
            MapEdge.East => (WorldRules.MapMin + inset, alongAxis),
            _ => (alongAxis, alongAxis),
        };
    }

    /// <summary>
    /// Where a hull is put when she reaches a border that leads nowhere: just
    /// inside the band, not on the line. Stopping her dead on the edge would let
    /// her sit in a crossing that never fires; this reads as a coast.
    /// </summary>
    public static (float X, float Y) HoldInside(float x, float y) =>
        (Math.Clamp(x, WorldRules.MapMin + BandSquares, WorldRules.MapMax - BandSquares),
         Math.Clamp(y, WorldRules.MapMin + BandSquares, WorldRules.MapMax - BandSquares));
}
```

- [ ] **Step 4: Run the test and watch it pass**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~MapEdgeRulesTests"
```

Expected: `Passed! - Failed: 0, Passed: 10`.

- [ ] **Step 5: Commit**

```bash
git add server/spacetimedb/spacetimedb/Domain/MapEdgeRules.cs server/spacetimedb/tests/MapEdgeRulesTests.cs
git commit -m "feat(domain): decide where a chart ends and the next begins

SEA_5 §10.2. Six squares of band and eight squares of inset, so a crossing
cannot bounce a hull between two charts, and she arrives at the same point
along the far edge she left at."
```

### Task 9.2: Offer the crossing, and carry her across when she accepts

SEA_5 §10.2 does not teleport a ship at the border. Reaching the band raises a
"Change map" prompt; the crossing happens when the captain confirms it. Until
she does she is held inside the border like any other coast, so a captain who
sailed too far east never loses her map without asking for it.

**Files:**
- Create: `server/spacetimedb/spacetimedb/Simulation/MapCrossingSystem.cs`
- Modify: `server/spacetimedb/spacetimedb/Schema/Tables.cs`, `server/spacetimedb/spacetimedb/Simulation/CommandReducers.cs`
- Test: `server/spacetimedb/tests/integration/MapCrossingTests.cs`

- [ ] **Step 1: Write the failing integration test**

```csharp
[Fact]
public void SailingIntoTheEastBandOffersGullRocksAndHoldsHerInside()
{
    using var world = TestWorld.Start();
    var ship = world.SpawnPlayerShip(mapId: 1, x: 380f, y: 200f);

    world.IssueCourse(ship, x: 399f, y: 200f);
    world.RunTicks(80);

    var held = world.Ship(ship);
    Assert.Equal(1, held.MapId);
    Assert.Equal(WorldRules.MapMax - MapEdgeRules.BandSquares, held.PositionX, 1);

    var offer = world.CrossingOffer(ship);
    Assert.NotNull(offer);
    Assert.Equal(2, offer!.Value.ToMapId);
}

[Fact]
public void ConfirmingTheOfferPutsHerEightSquaresInsideGullRocks()
{
    using var world = TestWorld.Start();
    var ship = world.SpawnPlayerShip(mapId: 1, x: 380f, y: 200f);
    world.IssueCourse(ship, 399f, 200f);
    world.RunTicks(80);

    var result = world.IssueChangeMap(ship);

    var moved = world.Ship(ship);
    Assert.Equal(CommandRejectionCode.None, result);
    Assert.Equal(2, moved.MapId);
    Assert.Equal(WorldRules.MapMin + MapEdgeRules.SpawnInsetSquares, moved.PositionX, 1);
    Assert.Equal(200f, moved.PositionY, 1);
    Assert.False(moved.HasRoute);
    Assert.Null(world.RouteOf(ship));
}

[Fact]
public void SheArrivesOnTheSameHeadingSheLeftOn()
{
    using var world = TestWorld.Start();
    var ship = world.SpawnPlayerShip(mapId: 1, x: 380f, y: 200f);
    world.IssueCourse(ship, 399f, 200f);
    world.RunTicks(80);
    var headingBefore = world.Ship(ship).HeadingDegrees;

    world.IssueChangeMap(ship);

    Assert.Equal(headingBefore, world.Ship(ship).HeadingDegrees, 3);
}

[Fact]
public void SailingBackOutOfTheBandWithdrawsTheOffer()
{
    using var world = TestWorld.Start();
    var ship = world.SpawnPlayerShip(mapId: 1, x: 380f, y: 200f);
    world.IssueCourse(ship, 399f, 200f);
    world.RunTicks(80);
    Assert.NotNull(world.CrossingOffer(ship));

    world.IssueCourse(ship, 300f, 200f);
    world.RunTicks(250);

    Assert.Null(world.CrossingOffer(ship));
}

[Fact]
public void ChangingMapWithNoOfferStandingIsRefused()
{
    using var world = TestWorld.Start();
    var ship = world.SpawnPlayerShip(mapId: 1, x: 200f, y: 200f);

    Assert.Equal(CommandRejectionCode.NoCrossingOffered, world.IssueChangeMap(ship));
}

[Fact]
public void AnEdgeWithNoNeighbourNeverOffersAnything()
{
    using var world = TestWorld.Start();
    var ship = world.SpawnPlayerShip(mapId: 1, x: 200f, y: 20f);

    world.IssueCourse(ship, 200f, 1f);
    world.RunTicks(80);

    Assert.Null(world.CrossingOffer(ship));
    Assert.Equal(MapEdgeRules.BandSquares, world.Ship(ship).PositionY, 1);
}
```

- [ ] **Step 2: Run it and watch it fail**

Run: `pnpm server:test`
Expected: FAIL — there is no `MapCrossingOffer` table and no `ChangeMap`
command, so the test does not compile.

- [ ] **Step 3: Add the two content lookups the system needs**

The generated catalogue holds maps in a list. Add these to
`Content/ContentCatalogMasks.cs`, the hand-written partial beside the generated
file, so the generator stays untouched:

```csharp
public static partial class ContentCatalog
{
    private static readonly Dictionary<byte, MapContent> MapsById =
        Content.Maps.ToDictionary(map => map.Id);

    /// <summary>One map by its id, without walking the list on a hot path.</summary>
    public static MapContent MapById(byte mapId) => MapsById[mapId];
}

public readonly record struct MapExit(MapEdge Edge, byte ToMapId);
```

and this to `Content/ContentDefinitions.cs`, on `MapContent`:

```csharp
    /// <summary>The exit on one edge, or null where that edge leads nowhere.</summary>
    public MapExit? ExitFor(MapEdge edge)
    {
        foreach (var exit in Exits)
        {
            if (exit.Edge == edge)
            {
                return exit;
            }
        }

        return null;
    }
```

The generator already emits `Exits` from the `exits` array authored in Phase 8;
Task 2.2 added the masks to the same partial, so this is the same pattern.

- [ ] **Step 4: Add the offer table and the rejection code**

In `Schema/Tables.cs`:

```csharp
/// <summary>
/// A standing "Change map" prompt. One row per ship at most, and only while she
/// is inside a border band that leads somewhere. The row is the prompt: the
/// client draws it when the row appears and takes it down when the row goes,
/// so a captain and the server never disagree about whether she was asked.
/// </summary>
[Table(Name = "MapCrossingOffer", Public = true)]
public partial struct MapCrossingOffer
{
    [PrimaryKey]
    public ulong EntityId;
    public byte ToMapId;
    public byte Edge;
    public float SpawnX;
    public float SpawnY;
    public ulong OfferedTick;
}
```

Add `NoCrossingOffered = 29` to `CommandRejectionCode`.

- [ ] **Step 5: Write the system**

```csharp
using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    /// <summary>
    /// The border, once a tick. A hull inside a band that leads somewhere is held
    /// inside it and offered the crossing; a hull inside a band that leads nowhere
    /// is only held. Nothing here moves a ship between maps: that is
    /// <see cref="ChangeMap"/>, which a captain has to ask for.
    /// </summary>
    private static void ProcessBorderBands(ReducerContext ctx, TickWorld world)
    {
        foreach (var (entityId, edge) in world.PendingCrossings)
        {
            if (ctx.Db.Ship.EntityId.Find(entityId) is not Ship ship)
            {
                continue;
            }

            var (heldX, heldY) = MapEdgeRules.HoldInside(ship.PositionX, ship.PositionY);
            ship.PositionX = heldX;
            ship.PositionY = heldY;
            ship.ChunkX = SpatialRules.ChunkCoordinate(heldX);
            ship.ChunkY = SpatialRules.ChunkCoordinate(heldY);
            ctx.Db.Ship.EntityId.Update(ship);

            // An NPC leashes home long before the border and has no one to ask,
            // so only a captain is ever offered a crossing.
            if (ship.OwnerIdentity is null ||
                ContentCatalog.MapById(ship.MapId).ExitFor(edge) is not MapExit exit)
            {
                continue;
            }

            // Where she will appear is worked out now rather than authored, so a
            // crossing lands her at the point along the far edge she left at.
            var alongAxis = edge is MapEdge.North or MapEdge.South ? heldX : heldY;
            var (spawnX, spawnY) = MapEdgeRules.ArrivalPoint(edge, alongAxis);
            var offer = new MapCrossingOffer
            {
                EntityId = entityId,
                ToMapId = exit.ToMapId,
                Edge = (byte)edge,
                SpawnX = spawnX,
                SpawnY = spawnY,
                OfferedTick = world.Tick,
            };
            if (ctx.Db.MapCrossingOffer.EntityId.Find(entityId) is null)
            {
                ctx.Db.MapCrossingOffer.Insert(offer);
            }
        }

        world.PendingCrossings.Clear();
        WithdrawStaleOffers(ctx, world);
    }

    /// <summary>
    /// A prompt no longer answerable: she sailed back out of the band, sank, or
    /// crossed. Walking the offers is cheap because there is at most one row per
    /// ship standing in a band, which is a handful on a busy map.
    /// </summary>
    private static void WithdrawStaleOffers(ReducerContext ctx, TickWorld world)
    {
        foreach (var offer in ctx.Db.MapCrossingOffer.Iter())
        {
            if (offer.OfferedTick == world.Tick)
            {
                continue;
            }

            ctx.Db.MapCrossingOffer.EntityId.Delete(offer.EntityId);
        }
    }
}
```

`ProcessMovingShip` records a band rather than acting on it, so a hull is
touched once per tick and outside the movement loop:

```csharp
        var edge = MapEdgeRules.EdgeAt(ship.PositionX, ship.PositionY);
        if (edge != MapEdge.None)
        {
            world.PendingCrossings.Add((ship.EntityId, edge));
        }
```

- [ ] **Step 6: Write the command handler**

In `Simulation/CommandReducers.cs`:

```csharp
    /// <summary>
    /// Confirming the prompt from SEA_5 §10.2. Her course is dropped: it was
    /// plotted against a land mask that no longer applies, and following it here
    /// would sail her through an island she cannot see. Her heading is kept, so
    /// she puts out of the new chart pointing the way she came in.
    /// </summary>
    private static CommandRejectionCode ChangeMap(ReducerContext ctx, ref Ship ship, ulong tick)
    {
        if (ctx.Db.MapCrossingOffer.EntityId.Find(ship.EntityId) is not MapCrossingOffer offer)
        {
            return CommandRejectionCode.NoCrossingOffered;
        }

        ClearRoute(ctx, ref ship);
        ClearEffects(ctx, ship.EntityId);
        ship.MapId = offer.ToMapId;
        ship.PositionX = offer.SpawnX;
        ship.PositionY = offer.SpawnY;
        ship.ChunkX = SpatialRules.ChunkCoordinate(offer.SpawnX);
        ship.ChunkY = SpatialRules.ChunkCoordinate(offer.SpawnY);
        ship.TargetEntityId = 0;
        ctx.Db.Ship.EntityId.Update(ship);
        ctx.Db.MapCrossingOffer.EntityId.Delete(ship.EntityId);
        return CommandRejectionCode.None;
    }
```

Pending volleys are cleared by `ClearEffects`, which already empties the
magazine's in-flight state when a ship is teleported on respawn.

SEA_5 §10.2 sends a crossing into a harbour map through "the countdown in
SEA_3". SEA_3_MECHANICS defines no such countdown — only the duel countdown and
the cast-off channel — so the confirmation is instant on every map, and Task 0.3
writes that down as the open question it is.

- [ ] **Step 7: Answer the prompt on the client**

`SeaWorldView` subscribes to `MapCrossingOffer` for the owned ship. A row
appearing raises the prompt; a row going takes it down; confirming sends
`ChangeMap`. This is one more row-driven panel of the kind the HUD already has
for the repair channel.

- [ ] **Step 8: Call it from the tick**

In the world tick, after `AdvanceMovingShips` and before replication.

- [ ] **Step 9: Run**

```sh
pnpm content:generate
pnpm server:test
pnpm unity:test
```

Expected: PASS.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "feat(server): ask before sailing off the chart

SEA_5 §10.2. Reaching a border holds a hull inside it and raises a prompt; the
crossing happens when the captain confirms. Confirming drops the course, which
was plotted against a land mask that does not apply on the new map, and clears
effects and the target for the same reason, but keeps her heading so she puts
out of the new chart pointing the way she came in. A border with nothing beyond
it only holds her, and never asks."
```

### Task 9.3: Harbour safe water and shoals

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Domain/PortRules.cs`
- Test: `server/spacetimedb/tests/PortRulesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void SafeWaterReachesThirtySquaresFromTheHarbour()
{
    Assert.True(PortRules.IsSafeWater(70f, 300f, harborX: 70f, harborY: 330f));
    Assert.False(PortRules.IsSafeWater(70f, 299f, 70f, 330f));
}

[Fact]
public void AShoalSlowsATierThreeHullAndStopsATierFive()
{
    Assert.True(PortRules.CanCrossShoal(tier: 1));
    Assert.True(PortRules.CanCrossShoal(3));
    Assert.False(PortRules.CanCrossShoal(4));
    Assert.False(PortRules.CanCrossShoal(5));
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: FAIL, the radius is in world units and `CanCrossShoal` does not exist.

- [ ] **Step 3: Write it**

```csharp
    /// <summary>
    /// SEA_5 §10.3: no fire either way inside thirty squares of a harbour. Wide
    /// enough that a hull leaving port is not chased out of it, narrow enough
    /// that it is not somewhere to hide from a fight.
    /// </summary>
    public static bool IsSafeWater(float x, float y, float harborX, float harborY) =>
        WorldRules.IsInRange(x, y, harborX, harborY, WorldRules.HarborSafeRadiusSquares);

    /// <summary>
    /// Shallow water. A small hull crosses it slowly (TacticalRules.ShoalMultiplier);
    /// a fourth or fifth rate draws too much and is turned back.
    /// </summary>
    public const byte DeepestShoalCrossingTier = 3;

    public static bool CanCrossShoal(byte tier) => tier <= DeepestShoalCrossingTier;
```

- [ ] **Step 4: Add shoals to the pathfinder for the hulls that cannot cross**

`ContentCatalog.LandMaskFor(mapId)` returns the mask of islands and reefs. Add
`ContentCatalog.DeepDraftMaskFor(mapId)`, which is the same mask with the shoals
rasterised in as well, generated in the same pass in `scripts/rasterize-maps.mjs`.
`SetCourse` picks the mask by the hull's tier:

```csharp
        var mask = PortRules.CanCrossShoal(ship.Tier)
            ? ContentCatalog.LandMaskFor(ship.MapId)
            : ContentCatalog.DeepDraftMaskFor(ship.MapId);
```

- [ ] **Step 5: Run**

```sh
pnpm content:generate
pnpm server:test
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(server): keep the harbour safe and the shallows shallow

SEA_5 §10.1 and §10.3. Thirty squares of safe water round a harbour, and a
fourth-rate now has her course plotted round a shoal she cannot cross rather
than through one she will be stopped by."
```

---

# Phase 10 — NPC movement

SEA_5 §11. NPCs already decide at 2 Hz and already have wander, chase and leash
behaviour; what changes is that they steer with routes like everyone else, and
the four distances become the ones SEA_5 names.

### Task 10.1: `NpcMovementRules`

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Domain/NpcRules.cs`
- Test: `server/spacetimedb/tests/NpcRulesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void TheFourDistancesAreTheOnesSeaFiveNames()
{
    Assert.Equal(25f, NpcMovementRules.WanderRadiusSquares, 4);
    Assert.Equal(20f, NpcMovementRules.AggroRadiusSquares, 4);
    Assert.Equal(60f, NpcMovementRules.LeashRadiusSquares, 4);
    Assert.Equal(0.8f, NpcMovementRules.HoldDistanceFraction, 4);
}

[Fact]
public void ANpcChasesAShipInsideAggro()
{
    Assert.Equal(
        NpcIntent.Chase,
        NpcMovementRules.Decide(distanceToTargetSquares: 15f, distanceFromHomeSquares: 10f));
}

[Fact]
public void ANpcTooFarFromHomeGoesHomeWhateverIsChasingHer()
{
    Assert.Equal(
        NpcIntent.Leash,
        NpcMovementRules.Decide(distanceToTargetSquares: 5f, distanceFromHomeSquares: 61f));
}

[Fact]
public void ANpcHoldsAtEightyPerCentOfHerRangeRatherThanClosingToTouch()
{
    var hold = NpcMovementRules.HoldDistanceSquares(effectiveRangeSquares: 20f);

    Assert.Equal(16f, hold, 4);
    Assert.Equal(NpcIntent.Hold, NpcMovementRules.Decide(16f, 10f, holdDistanceSquares: 16f));
}

[Fact]
public void ANpcReplansTwiceASecondAtMost()
{
    Assert.Equal(5UL, NpcMovementRules.ReplanIntervalTicks);
}

[Fact]
public void AnIdleNpcPicksANewSpotEveryEightToTwentySeconds()
{
    for (var entityId = 1UL; entityId <= 200UL; entityId++)
    {
        var wait = NpcMovementRules.WanderWaitTicks(entityId, wanderIndex: 3UL);

        Assert.InRange(wait, 80UL, 200UL);
    }
}

[Fact]
public void TwoNpcsDoNotAllPickTheirNextSpotOnTheSameTick()
{
    var waits = new HashSet<ulong>();
    for (var entityId = 1UL; entityId <= 50UL; entityId++)
    {
        waits.Add(NpcMovementRules.WanderWaitTicks(entityId, 0UL));
    }

    Assert.True(waits.Count > 10);
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: build error, `NpcMovementRules` does not exist.

- [ ] **Step 3: Write it**

```csharp
namespace Sea.Server;

public enum NpcIntent : byte
{
    Wander = 0,
    Chase = 1,
    Hold = 2,
    Leash = 3,
}

/// <summary>How an enemy decides where to sail (SEA_5 §11).</summary>
/// <remarks>
/// The four numbers matter to each other more than they matter on their own.
/// Aggro is inside a gun's range so an enemy is shot at before she notices;
/// leash is well past sight so a chase is a chase and not a leash; hold at
/// eighty per cent of range keeps her shooting without drifting into ramming
/// distance every time the target turns.
/// </remarks>
public static class NpcMovementRules
{
    public const float WanderRadiusSquares = 25f;
    public const float AggroRadiusSquares = 20f;
    public const float LeashRadiusSquares = 60f;
    public const float HoldDistanceFraction = 0.8f;

    /// <summary>
    /// Half a second. A course is only replotted this often however fast the
    /// target moves, because A* on a four-hundred-square grid is the most
    /// expensive thing an NPC can ask for and twice a second is enough to
    /// follow anything on the map.
    /// </summary>
    public const ulong ReplanIntervalTicks = 5UL;

    /// <summary>The shortest and longest an idle enemy loiters (SEA_5 §11.2).</summary>
    public const ulong MinimumWanderWaitTicks = 80UL;
    public const ulong MaximumWanderWaitTicks = 200UL;

    public static float HoldDistanceSquares(float effectiveRangeSquares) =>
        effectiveRangeSquares * HoldDistanceFraction;

    /// <summary>
    /// How long an idle enemy sits before picking her next spot: eight to twenty
    /// seconds, derived from her id and how many times she has already moved.
    /// Derived rather than rolled, so a replay of the same log wanders the same
    /// way, and spread rather than fixed, so fifteen hostiles on one map do not
    /// all ask for a route on the same tick.
    /// </summary>
    public static ulong WanderWaitTicks(ulong entityId, ulong wanderIndex)
    {
        var span = MaximumWanderWaitTicks - MinimumWanderWaitTicks;
        return MinimumWanderWaitTicks + (Mix(entityId * 0x9E3779B97F4A7C15UL + wanderIndex) % (span + 1UL));
    }

    private static ulong Mix(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    public static NpcIntent Decide(
        float distanceToTargetSquares,
        float distanceFromHomeSquares,
        float holdDistanceSquares = 0f)
    {
        if (distanceFromHomeSquares > LeashRadiusSquares)
        {
            return NpcIntent.Leash;
        }

        if (distanceToTargetSquares > AggroRadiusSquares)
        {
            return NpcIntent.Wander;
        }

        return distanceToTargetSquares <= holdDistanceSquares ? NpcIntent.Hold : NpcIntent.Chase;
    }
}
```

- [ ] **Step 4: Run the test and watch it pass**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~NpcRulesTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add server/spacetimedb/spacetimedb/Domain/NpcRules.cs server/spacetimedb/tests/NpcRulesTests.cs
git commit -m "feat(domain): give an enemy the distances and the clock she steers by

SEA_5 §11, in squares. Aggro sits inside a gun's range so an enemy is fired on
before she notices, and a course is replanned twice a second at most because A*
is the most expensive thing an NPC can ask for. An idle enemy loiters eight to
twenty seconds, derived from her id so a replay wanders the same way and so a
map full of hostiles does not ask for fifteen routes on one tick."
```

### Task 10.2: Steer NPCs with routes

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Simulation/NpcSystem.cs`
- Test: `server/spacetimedb/tests/integration/NpcMovementTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void AnEnemyChasingAcrossAnIslandGoesRoundIt()
{
    using var world = TestWorld.Start();
    var prey = world.SpawnPlayerShip(mapId: 1, x: 190f, y: 250f);
    var hunter = world.SpawnNpc(mapId: 1, x: 110f, y: 250f);

    world.RunTicks(600);

    // Carrick is centred at (150, 250) with a radius of 26; she cannot have
    // crossed it, so her track must have gone round.
    Assert.All(
        world.TrackOf(hunter),
        point => Assert.False(ContentCatalog.LandMaskFor(1).IsLand(point.X, point.Y)));
    Assert.True(world.DistanceBetween(hunter, prey) < NpcMovementRules.AggroRadiusSquares);
}

[Fact]
public void AnEnemyDoesNotReplanMoreThanTwiceASecond()
{
    using var world = TestWorld.Start();
    var prey = world.SpawnPlayerShip(1, 200f, 250f);
    var hunter = world.SpawnNpc(1, 195f, 250f);

    world.RunTicks(100);

    Assert.True(world.RouteVersionOf(hunter) <= 20);
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: FAIL — NPCs set `DestinationX/Y` directly and sail through Carrick.

- [ ] **Step 3: Route them through `SetCourse`**

Replace every direct write to `DestinationX`/`DestinationY` in `NpcSystem.cs`
with a call to the same `SetCourse` a player's click goes through, gated on
`tick % NpcMovementRules.ReplanIntervalTicks == entityId % ReplanIntervalTicks`
so the replans of a hundred enemies are spread across the five ticks rather
than landing on one.

An NPC whose `SetCourse` returns `NoPath` holds station rather than being
refused, because there is nobody to tell.

An idle enemy is the other half of this. She keeps `NextWanderTick` and
`WanderIndex` on her NPC row; when the tick reaches `NextWanderTick` she picks a
random water point within `WanderRadiusSquares` of her spawn, sets a course to
it, increments `WanderIndex` and sets
`NextWanderTick = tick + NpcMovementRules.WanderWaitTicks(entityId, WanderIndex)`.
That is SEA_5 §11.2's eight to twenty seconds, and it means an idle map costs
one route every twelve seconds per hostile rather than one every tick.

- [ ] **Step 4: Run**

```sh
pnpm server:test
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(server): let enemies steer round islands like everyone else

An NPC set her destination directly and sailed through whatever was between, so
a chase across Carrick was a chase through it. Replans are staggered across
five ticks so a hundred enemies never plot on the same one."
```

---

# Phase 11 — Boarding

SEA_5 §9 for the trigger and the cooldowns, SEA_2_MATH §5.7 for the scores,
SEA_3_MECHANICS §4.3 for what happens after. This is the feature the roadmap
put in Milestone 3; it is here because the user asked for it.

### Task 11.1: `BoardingRules`

**Files:**
- Create: `server/spacetimedb/spacetimedb/Domain/BoardingRules.cs`
- Test: `server/spacetimedb/tests/BoardingRulesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class BoardingRulesTests
{
    [Fact]
    public void GrapplingReachesFourSquares()
    {
        Assert.True(BoardingRules.IsInReach(3.9f));
        Assert.False(BoardingRules.IsInReach(4.1f));
    }

    [Fact]
    public void AShipAboveHalfHealthCannotBeBoarded()
    {
        Assert.False(BoardingRules.CanBoard(defenderHull: 51, defenderMaxHull: 100));
        Assert.True(BoardingRules.CanBoard(50, 100));
    }

    [Fact]
    public void ACaptainWithHalfHerHandsGoneCannotBoard()
    {
        Assert.False(BoardingRules.HasHandsToBoard(hands: 4, maxHands: 10));
        Assert.True(BoardingRules.HasHandsToBoard(5, 10));
    }

    [Fact]
    public void TheStrongerCrewWins()
    {
        var attacker = new BoardingParty(hands: 20, moraleFraction: 1.0f, tier: 3);
        var defender = new BoardingParty(hands: 12, moraleFraction: 1.0f, tier: 3);

        Assert.True(BoardingRules.Score(attacker) > BoardingRules.Score(defender));
    }

    [Fact]
    public void ABiggerHullFightsBetterWithTheSameHands()
    {
        var small = new BoardingParty(20, 1f, 2);
        var large = new BoardingParty(20, 1f, 5);

        Assert.True(BoardingRules.Score(large) > BoardingRules.Score(small));
    }

    [Fact]
    public void APlayerWaitsAMinuteAndAnEnemyFifteenSeconds()
    {
        Assert.Equal(600UL, BoardingRules.PlayerCooldownTicks);
        Assert.Equal(150UL, BoardingRules.NpcCooldownTicks);
    }

    [Fact]
    public void TheSameVictimCannotBeBoardedTwiceInFiveMinutes()
    {
        Assert.Equal(3000UL, BoardingRules.VictimImmunityTicks);
    }

    [Fact]
    public void AWinTakesATenthOfHerHullAndThreeSecondsOfHerGuns()
    {
        var outcome = BoardingRules.Resolve(
            new BoardingParty(20, 1f, 3), new BoardingParty(10, 1f, 3), defenderMaxHull: 1000);

        Assert.True(outcome.AttackerWon);
        Assert.Equal(100u, outcome.HullDamage);
        Assert.Equal(30UL, outcome.SilenceTicks);
    }

    [Fact]
    public void ALossCostsTheAttackerHerHandsAndNothingElse()
    {
        var outcome = BoardingRules.Resolve(
            new BoardingParty(5, 1f, 2), new BoardingParty(25, 1f, 5), 1000);

        Assert.False(outcome.AttackerWon);
        Assert.Equal(0u, outcome.HullDamage);
        Assert.True(outcome.AttackerHandsLost > 0);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: build error, `BoardingRules` does not exist.

- [ ] **Step 3: Write it**

```csharp
namespace Sea.Server;

/// <summary>One side of a boarding action.</summary>
public readonly record struct BoardingParty(uint Hands, float MoraleFraction, byte Tier);

/// <summary>What a boarding did.</summary>
public readonly record struct BoardingOutcome(
    bool AttackerWon,
    uint HullDamage,
    uint AttackerHandsLost,
    uint DefenderHandsLost,
    ulong SilenceTicks);

/// <summary>
/// Grappling and taking a ship. SEA_5 §9 sets the reach and the cooldowns,
/// SEA_2_MATH §5.7 the scores, SEA_3_MECHANICS §4.3 the outcome.
/// </summary>
/// <remarks>
/// Boarding is deliberately hard to start and cheap to lose: four squares is
/// almost touching, the target has to be at half health, and the attacker needs
/// half her hands. Everything about it is a finisher, not an opener.
/// </remarks>
public static class BoardingRules
{
    public const float ReachSquares = 4f;

    /// <summary>A hull is boardable at or below half her health (SEA_5 §9.1).</summary>
    public const float BoardableHullFraction = 0.50f;

    /// <summary>An attacker needs at least half her hands (SEA_3 §4.3).</summary>
    public const float RequiredHandsFraction = 0.50f;

    public const ulong PlayerCooldownTicks = 600UL;
    public const ulong NpcCooldownTicks = 150UL;

    /// <summary>One victim cannot be boarded again for five minutes (SEA_3 §4.3).</summary>
    public const ulong VictimImmunityTicks = 3000UL;

    /// <summary>A win takes a tenth of the loser's maximum hull (SEA_3 §4.3).</summary>
    public const float WinHullDamageFraction = 0.10f;

    /// <summary>Her guns are silent for three seconds afterwards.</summary>
    public const ulong SilenceTicks = 30UL;

    /// <summary>How much a bigger hull is worth in a melee, per tier above the first.</summary>
    public const float TierWeight = 0.15f;

    public static bool IsInReach(float distanceSquares) => distanceSquares <= ReachSquares;

    public static bool CanBoard(uint defenderHull, uint defenderMaxHull) =>
        defenderMaxHull > 0 && (float)defenderHull / defenderMaxHull <= BoardableHullFraction;

    public static bool HasHandsToBoard(uint hands, uint maxHands) =>
        maxHands > 0 && (float)hands / maxHands >= RequiredHandsFraction;

    public static float Score(in BoardingParty party) =>
        party.Hands *
        Math.Clamp(party.MoraleFraction, 0f, 1f) *
        (1f + (TierWeight * (party.Tier - 1)));

    public static BoardingOutcome Resolve(
        in BoardingParty attacker,
        in BoardingParty defender,
        uint defenderMaxHull)
    {
        var attackerScore = Score(attacker);
        var defenderScore = Score(defender);
        var total = attackerScore + defenderScore;
        if (total <= 0f)
        {
            return new BoardingOutcome(false, 0u, 0u, 0u, 0UL);
        }

        // Losses are proportional to how one-sided it was: an even fight costs
        // both crews dearly, a rout costs the winner almost nothing.
        var attackerWon = attackerScore > defenderScore;
        var margin = MathF.Abs(attackerScore - defenderScore) / total;
        var attackerLoss = (uint)MathF.Round(attacker.Hands * 0.5f * (attackerWon ? 1f - margin : 1f));
        var defenderLoss = (uint)MathF.Round(defender.Hands * 0.5f * (attackerWon ? 1f : 1f - margin));

        return new BoardingOutcome(
            attackerWon,
            attackerWon ? (uint)MathF.Round(defenderMaxHull * WinHullDamageFraction) : 0u,
            attackerLoss,
            attackerWon ? defenderLoss : 0u,
            attackerWon ? SilenceTicks : 0UL);
    }
}
```

- [ ] **Step 4: Run the test and watch it pass**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~BoardingRulesTests"
```

Expected: `Passed! - Failed: 0, Passed: 9`.

- [ ] **Step 5: Commit**

```bash
git add server/spacetimedb/spacetimedb/Domain/BoardingRules.cs server/spacetimedb/tests/BoardingRulesTests.cs
git commit -m "feat(domain): decide a boarding action

Reach and cooldowns from SEA_5 §9, scores from SEA_2_MATH §5.7, outcome from
SEA_3_MECHANICS §4.3. Losses scale with how one-sided the fight was, so an even
boarding is expensive for both crews and a rout is not."
```

### Task 11.2: The boarding command

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Simulation/CommandReducers.cs`, `Schema/Tables.cs`
- Test: `server/spacetimedb/tests/integration/BoardingTests.cs`

- [ ] **Step 1: Write the failing integration test**

```csharp
[Fact]
public void BoardingAHealthyShipIsRefused()
{
    using var world = TestWorld.Start();
    var attacker = world.SpawnPlayerShip(1, 200f, 200f);
    var defender = world.SpawnNpc(1, 202f, 200f);

    var result = world.IssueBoard(attacker, defender);

    Assert.Equal(CommandRejectionCode.TargetNotBoardable, result);
}

[Fact]
public void BoardingAHalfSunkShipInReachSucceeds()
{
    using var world = TestWorld.Start();
    var attacker = world.SpawnPlayerShip(1, 200f, 200f);
    var defender = world.SpawnNpc(1, 202f, 200f);
    world.SetHull(defender, fraction: 0.4f);

    var result = world.IssueBoard(attacker, defender);

    Assert.Equal(CommandRejectionCode.None, result);
    Assert.True(world.Ship(defender).WeaponSilencedUntilTick > 0);
}

[Fact]
public void TheSameVictimCannotBeBoardedAgainForFiveMinutes()
{
    using var world = TestWorld.Start();
    var first = world.SpawnPlayerShip(1, 200f, 200f);
    var second = world.SpawnPlayerShip(1, 201f, 200f);
    var defender = world.SpawnNpc(1, 202f, 200f);
    world.SetHull(defender, 0.4f);

    world.IssueBoard(first, defender);
    world.RunTicks(300);

    Assert.Equal(CommandRejectionCode.TargetRecentlyBoarded, world.IssueBoard(second, defender));
}

[Fact]
public void ABoardingInSafeWaterIsRefused()
{
    using var world = TestWorld.Start();
    var attacker = world.SpawnPlayerShip(1, 70f, 320f);
    var defender = world.SpawnNpc(1, 71f, 320f);
    world.SetHull(defender, 0.4f);

    Assert.Equal(CommandRejectionCode.InPort, world.IssueBoard(attacker, defender));
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: FAIL, boarding answers `NotAvailable`.

- [ ] **Step 3: Add the columns and the handler**

On `Ship`: `public ulong BoardCooldownUntilTick;`, `public ulong BoardImmuneUntilTick;`,
`public ulong WeaponSilencedUntilTick;`, `public uint Hands;`, `public uint MaxHands;`.
Add `TargetNotBoardable = 27` and `TargetRecentlyBoarded = 28` to
`CommandRejectionCode`.

The handler checks, in this order and refusing at the first failure: both ships
alive; not in safe water; `IsInReach`; `CanBoard`; `HasHandsToBoard`;
`tick >= attacker.BoardCooldownUntilTick`; `tick >= defender.BoardImmuneUntilTick`.
Then it calls `BoardingRules.Resolve`, applies the outcome, sets
`BoardCooldownUntilTick` from `PlayerCooldownTicks` or `NpcCooldownTicks`, sets
`BoardImmuneUntilTick` from `VictimImmunityTicks`, and awards the haul through
the reward path that already exists for a kill.

- [ ] **Step 4: Answer the bound key**

`SeaCombatInput` already binds boarding and shows `NotAvailable`. Send the
command instead.

- [ ] **Step 5: Run**

```sh
pnpm server:test
pnpm unity:test
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(server): board a ship that is half sunk and in reach

The key has been bound and answering NotAvailable since Milestone 1. Order of
checks matters: a captain is told the nearest reason she cannot board, not the
last one, so 'too healthy' beats 'on cooldown' when both are true."
```

---

# Phase 12 — Trust score

SEA_5 §12. The rate limit from Task 4.4 already counts drops. This phase turns
the count into a score and gives the other three signals somewhere to go.

### Task 12.1: `TrustScoreRules`

**Files:**
- Create: `server/spacetimedb/spacetimedb/Domain/TrustScoreRules.cs`
- Test: `server/spacetimedb/tests/TrustScoreRulesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class TrustScoreRulesTests
{
    [Fact]
    public void EveryoneStartsFullyTrusted()
    {
        Assert.Equal(100, TrustScoreRules.StartingScore);
    }

    [Fact]
    public void ADroppedCommandCostsALittle()
    {
        Assert.Equal(99, TrustScoreRules.Apply(100, TrustSignal.DroppedCommand));
    }

    [Fact]
    public void ImpossibleMovementCostsALot()
    {
        Assert.Equal(90, TrustScoreRules.Apply(100, TrustSignal.ImpossibleMovement));
    }

    [Fact]
    public void TheScoreNeverGoesBelowZeroOrAboveAHundred()
    {
        Assert.Equal(0, TrustScoreRules.Apply(3, TrustSignal.ImpossibleMovement));
        Assert.Equal(100, TrustScoreRules.Recover(100, elapsedTicks: 36_000UL));
    }

    [Fact]
    public void AnHourOfGoodBehaviourRecoversTenPoints()
    {
        Assert.Equal(60, TrustScoreRules.Recover(50, elapsedTicks: 36_000UL));
    }

    [Theory]
    [InlineData(100, TrustBand.Trusted)]
    [InlineData(70, TrustBand.Trusted)]
    [InlineData(69, TrustBand.Watched)]
    [InlineData(40, TrustBand.Watched)]
    [InlineData(39, TrustBand.Flagged)]
    [InlineData(0, TrustBand.Flagged)]
    public void TheScoreFallsIntoThreeBands(int score, TrustBand band)
    {
        Assert.Equal(band, TrustScoreRules.BandFor(score));
    }

    [Fact]
    public void AMetronomeIsNotAHand()
    {
        // Twenty courses exactly 1.2 s apart. No hand does this.
        var ticks = new ulong[20];
        for (var index = 0; index < ticks.Length; index++)
        {
            ticks[index] = (ulong)index * 12UL;
        }

        Assert.True(TrustScoreRules.IsMetronomic(ticks));
    }

    [Fact]
    public void AHandIsNotAMetronome()
    {
        ulong[] ticks =
        {
            0, 14, 25, 41, 50, 67, 74, 91, 103, 112,
            129, 138, 155, 161, 180, 188, 203, 219, 226, 244,
        };

        Assert.False(TrustScoreRules.IsMetronomic(ticks));
    }

    [Fact]
    public void ATargetHeldAtExactlyTheEdgeOfRangeIsASignal()
    {
        // Sitting on range minus the grace, volley after volley, is a number a
        // client computed, not a distance a captain sailed to.
        Assert.True(TrustScoreRules.IsEdgeOfRange(distanceSquares: 23.5f, effectiveRangeSquares: 24f));
    }

    [Fact]
    public void ATargetHeldAnywhereElseIsNot()
    {
        Assert.False(TrustScoreRules.IsEdgeOfRange(21.0f, 24f));
        Assert.False(TrustScoreRules.IsEdgeOfRange(23.9f, 24f));
    }

    [Fact]
    public void NothingHereBansAnybody()
    {
        // SEA_5 §12: the score is evidence for a person to read, not an action.
        Assert.Equal(TrustBand.Flagged, TrustScoreRules.BandFor(0));
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: build error, `TrustScoreRules` does not exist.

- [ ] **Step 3: Write it**

```csharp
namespace Sea.Server;

public enum TrustSignal : byte
{
    DroppedCommand = 0,
    RejectedCommand = 1,
    ImpossibleMovement = 2,
    ImpossibleFire = 3,
    MetronomicCommands = 4,
    EdgeOfRangeTargeting = 5,
}

public enum TrustBand : byte
{
    Trusted = 0,
    Watched = 1,
    Flagged = 2,
}

/// <summary>
/// A number that says how much a captain's client has been arguing with the
/// server (SEA_5 §12).
/// </summary>
/// <remarks>
/// Nothing here punishes anybody. A low score is a reason for a person to look,
/// and the bands exist so that looking can be sorted. Every penalty is small
/// enough that a bad connection cannot flag an honest player inside an hour,
/// and the recovery is fast enough that one bad session does not follow them.
/// </remarks>
public static class TrustScoreRules
{
    public const int StartingScore = 100;
    public const int MinimumScore = 0;
    public const int MaximumScore = 100;

    public const int DroppedMovePenalty = 1;

    public const int TrustedFloor = 70;
    public const int WatchedFloor = 40;

    /// <summary>Ten points an hour, which at 10 Hz is one point every six minutes.</summary>
    public const ulong RecoveryIntervalTicks = 3_600UL;

    public static int PenaltyFor(TrustSignal signal) => signal switch
    {
        TrustSignal.DroppedCommand => 1,
        TrustSignal.RejectedCommand => 2,
        TrustSignal.ImpossibleMovement => 10,
        TrustSignal.ImpossibleFire => 10,
        TrustSignal.MetronomicCommands => 5,
        TrustSignal.EdgeOfRangeTargeting => 5,
        _ => 0,
    };

    /// <summary>
    /// How many courses in a row are looked at before calling a client a
    /// metronome, and how much they may vary and still count as one.
    /// </summary>
    public const int MetronomeSampleCount = 20;
    public const ulong MetronomeToleranceTicks = 1UL;

    /// <summary>
    /// Every event is stamped by the server, so the gaps between a captain's
    /// courses are a measurement rather than something a client reports. Twenty
    /// gaps that all match to within a tick is not a hand (SEA_5 §12.4).
    /// </summary>
    public static bool IsMetronomic(ReadOnlySpan<ulong> commandTicks)
    {
        if (commandTicks.Length < MetronomeSampleCount)
        {
            return false;
        }

        var first = commandTicks[1] - commandTicks[0];
        for (var index = 2; index < commandTicks.Length; index++)
        {
            var gap = commandTicks[index] - commandTicks[index - 1];
            var difference = gap > first ? gap - first : first - gap;
            if (difference > MetronomeToleranceTicks)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A tenth of a square either side of the grace line (SEA_5 §12.4).</summary>
    public const float EdgeOfRangeToleranceSquares = 0.1f;

    /// <summary>
    /// Holding station at exactly range minus the grace is a number a client
    /// worked out, not a distance a captain sailed to. It is only a signal: a
    /// good player kiting at the edge trips it occasionally, which is why the
    /// penalty is five points and not a ban.
    /// </summary>
    public static bool IsEdgeOfRange(float distanceSquares, float effectiveRangeSquares) =>
        MathF.Abs(distanceSquares - (effectiveRangeSquares - RangeRules.GraceSquares)) <=
        EdgeOfRangeToleranceSquares;

    public static int Apply(int score, TrustSignal signal) =>
        Math.Clamp(score - PenaltyFor(signal), MinimumScore, MaximumScore);

    public static int Recover(int score, ulong elapsedTicks) =>
        Math.Clamp(
            score + (int)(elapsedTicks / RecoveryIntervalTicks), MinimumScore, MaximumScore);

    public static TrustBand BandFor(int score)
    {
        if (score >= TrustedFloor)
        {
            return TrustBand.Trusted;
        }

        return score >= WatchedFloor ? TrustBand.Watched : TrustBand.Flagged;
    }
}
```

- [ ] **Step 4: Run the test and watch it pass**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~TrustScoreRulesTests"
```

Expected: `Passed! - Failed: 0, Passed: 16`.

- [ ] **Step 5: Commit**

```bash
git add server/spacetimedb/spacetimedb/Domain/TrustScoreRules.cs server/spacetimedb/tests/TrustScoreRulesTests.cs
git commit -m "feat(domain): score how much a client argues with the server

SEA_5 §12. Nothing here bans anyone: it sorts a list for a person to read.
Penalties are small enough that a bad connection cannot flag an honest captain
in an hour, and recovery is fast enough that one bad session does not follow
her. The two signals SEA_5 §12.4 names by hand -- courses spaced like a
metronome, and a target held at exactly range minus the grace -- are measurable
because the server stamps every event itself, so neither can be faked away by a
client."
```

### Task 12.2: The `PlayerTrust` table

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Schema/Tables.cs`, `Simulation/RouteSystem.cs`
- Test: `server/spacetimedb/tests/integration/TrustScoreTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void TwelveCoursesInOneSecondCostFourPoints()
{
    using var world = TestWorld.Start();
    var ship = world.SpawnPlayerShip(1, 200f, 200f);

    for (var attempt = 0; attempt < 12; attempt++)
    {
        world.IssueCourse(ship, 210f, 200f);
    }

    Assert.Equal(96, world.TrustOf(ship).Score);
    Assert.Equal(4u, world.TrustOf(ship).DroppedCommands);
}

[Fact]
public void AnHourOfSailingQuietlyGivesThePointsBack()
{
    using var world = TestWorld.Start();
    var ship = world.SpawnPlayerShip(1, 200f, 200f);
    world.SetTrust(ship, 50);

    world.RunTicks(36_000);

    Assert.Equal(60, world.TrustOf(ship).Score);
}

[Fact]
public void TrustIsPrivateToTheServer()
{
    Assert.False(typeof(PlayerTrust).GetCustomAttribute<TableAttribute>()!.Public);
}

[Fact]
public void TwentyCoursesOnAPerfectBeatCostFivePoints()
{
    using var world = TestWorld.Start();
    var ship = world.SpawnPlayerShip(1, 200f, 200f);

    // Twelve ticks apart is well inside the eight-a-second limit, so none of
    // these is dropped. What is wrong with them is that they are perfect.
    for (var attempt = 0; attempt < 20; attempt++)
    {
        world.IssueCourse(ship, 210f + attempt, 200f);
        world.RunTicks(12);
    }

    Assert.Equal(95, world.TrustOf(ship).Score);
    Assert.Equal(1u, world.TrustOf(ship).MetronomicRuns);
}

[Fact]
public void ACaptainWhoClicksLikeAPersonKeepsHerScore()
{
    using var world = TestWorld.Start();
    var ship = world.SpawnPlayerShip(1, 200f, 200f);
    int[] gaps = { 14, 11, 16, 9, 17, 7, 17, 12, 9, 17, 9, 17, 6, 19, 8, 15, 16, 7, 18, 13 };

    foreach (var gap in gaps)
    {
        world.IssueCourse(ship, 210f, 200f);
        world.RunTicks(gap);
    }

    Assert.Equal(100, world.TrustOf(ship).Score);
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: build error, `PlayerTrust` does not exist.

- [ ] **Step 3: Add the table**

```csharp
/// <summary>
/// How much a captain's client has argued with the server. Private: a captain
/// who can see her own score can tune a cheat against it.
/// </summary>
[Table(Name = "PlayerTrust", Public = false)]
public partial struct PlayerTrust
{
    [PrimaryKey]
    public Identity PlayerIdentity;

    public int Score;
    public uint DroppedCommands;
    public uint RejectedCommands;
    public uint ImpossibleMovements;
    public uint ImpossibleFires;
    public uint MetronomicRuns;
    public uint EdgeOfRangeVolleys;
    public ulong LastPenaltyTick;

    /// <summary>
    /// When her last twenty courses were ordered, as a ring. Twenty ulongs a
    /// captain is 160 bytes, which is why this is a fixed window and not a log.
    /// </summary>
    public List<ulong> RecentCourseTicks;
    public byte RecentCourseWriteIndex;
}
```

- [ ] **Step 4: Feed it**

Replace the `DroppedCommandCount` stand-in from Task 4.4 with a real
`RecordTrustSignal(ctx, identity, signal, tick)` that reads the row, calls
`TrustScoreRules.Recover` for the ticks since `LastPenaltyTick`, then
`TrustScoreRules.Apply`, then writes it back. Call it from four places: the
rate limiter, the command-rejection path, the position-validation path, and the
range check when a client fires at something out of range.

Recovery happens on the next penalty rather than on a timer, so a quiet captain
costs the tick nothing at all.

- [ ] **Step 5: Measure the two signals SEA_5 §12.4 names**

Every accepted course writes its tick into `RecentCourseTicks` at
`RecentCourseWriteIndex`, which wraps at `TrustScoreRules.MetronomeSampleCount`.
Once the ring is full, `SetCourse` unrolls it into a stack span in order and
asks `TrustScoreRules.IsMetronomic`; a true answer raises
`TrustSignal.MetronomicCommands` and empties the ring, so one bot run costs five
points rather than five points a course.

The volley path already has the distance and the effective range from Task 6.2.
When `TrustScoreRules.IsEdgeOfRange` is true it increments `EdgeOfRangeVolleys`
and raises `TrustSignal.EdgeOfRangeTargeting` on every tenth one, for the same
reason: a good player kiting at the edge should cost a few points over a long
fight, not a hundred.

Both are pure reads of numbers the server already has. Neither adds a row write
or an allocation to the tick: the ring lives on a row that is written anyway
when a course is accepted, and the span is on the stack.

- [ ] **Step 6: Run**

```sh
pnpm server:build
pnpm server:generate:csharp
pnpm server:generate:typescript
pnpm server:test
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(server): keep a trust score per captain

Six signals feed it and nothing reads it but an operator. Recovery is worked
out on the next penalty rather than on a timer, so a captain who never trips it
costs the tick nothing, and the two bot signals SEA_5 §12.4 names are read off
numbers the tick already has rather than measured with new work."
```

---

> ## Review gate after Phase 12
>
> The server is now complete against SEA_5. Ask the user to run
> `/thermo-nuclear-code-quality-review` and `/improve-codebase-architecture`
> over Phases 8 to 12 before the client work starts.

---

# Phase 13 — The client agrees with the server

SEA_5 §12.2 and §12.3. The client's copy of the movement rules is a mirror of a file that
no longer exists. It has to be replaced, not repaired.

### Task 13.1: `SeaRouteRules`

**Files:**
- Delete: `apps/game-unity/Assets/Domain/SeaSailingRules.cs`
- Create: `apps/game-unity/Assets/Domain/SeaRouteRules.cs`
- Test: `apps/game-unity/Assets/Tests/EditMode/SeaRouteRulesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void SheWalksHerRouteAtConstantSpeed()
{
    var route = new[] { new Vector2(0f, 0f), new Vector2(100f, 0f) };

    var step = SeaRouteRules.Advance(route, 0, new Vector2(0f, 0f), 90f, 5f);

    Assert.AreEqual(5f, step.Position.x, 0.0001f);
    Assert.AreEqual(0f, step.Position.y, 0.0001f);
    Assert.AreEqual(90f, step.HeadingDegrees, 0.0001f);
}

[Test]
public void ACornerIsTurnedInsideOneStepWithNoDistanceLost()
{
    var route = new[]
    {
        new Vector2(0f, 0f), new Vector2(3f, 0f), new Vector2(3f, 4f),
    };

    var step = SeaRouteRules.Advance(route, 0, new Vector2(0f, 0f), 90f, 5f);

    Assert.AreEqual(3f, step.Position.x, 0.0001f);
    Assert.AreEqual(2f, step.Position.y, 0.0001f);
    Assert.AreEqual(1, step.WaypointIndex);
}

[Test]
public void SheStopsOnTheLastWaypointAndNotPastIt()
{
    var route = new[] { new Vector2(0f, 0f), new Vector2(2f, 0f) };

    var step = SeaRouteRules.Advance(route, 0, new Vector2(0f, 0f), 90f, 5f);

    Assert.AreEqual(2f, step.Position.x, 0.0001f);
    Assert.IsTrue(step.Arrived);
}

[Test]
public void TheClientAndTheServerAgreeOnTheSameRoute()
{
    // The numbers here are SEA_5 §13 test 1, the same assertion the server
    // makes in RouteRulesTests. If these two ever disagree the local ship is
    // drawn where the server will not agree she is.
    var route = new[] { new Vector2(50f, 50f), new Vector2(250f, 50f) };
    var position = new Vector2(50f, 50f);
    var index = 0;

    for (var tick = 0; tick < 100; tick++)
    {
        var step = SeaRouteRules.Advance(route, index, position, 90f, 5.0f * 0.1f);
        position = step.Position;
        index = step.WaypointIndex;
    }

    Assert.AreEqual(100f, position.x, 0.01f);
}
```

- [ ] **Step 2: Run it and watch it fail**

```sh
pnpm unity:test
```

Expected: FAIL, `SeaRouteRules` does not exist.

- [ ] **Step 3: Write it**

```csharp
using UnityEngine;

namespace Sea.Client
{
    public readonly struct SeaRouteStep
    {
        public SeaRouteStep(Vector2 position, float headingDegrees, int waypointIndex, bool arrived)
        {
            Position = position;
            HeadingDegrees = headingDegrees;
            WaypointIndex = waypointIndex;
            Arrived = arrived;
        }

        public Vector2 Position { get; }

        public float HeadingDegrees { get; }

        public int WaypointIndex { get; }

        public bool Arrived { get; }
    }

    /// <summary>
    /// Walking a route, in the client's own terms. This is a deliberate mirror
    /// of the server's RouteRules.Advance and must stay identical to it: any
    /// place the two disagree, the local ship is drawn somewhere the server will
    /// not agree with, and the correction a captain sees is what reads as the
    /// ship behaving oddly.
    /// </summary>
    /// <remarks>
    /// SEA_5 §4.2 says there is no inertia, which is why this is fifteen lines
    /// where the old mirror was two hundred. There is no acceleration to match,
    /// no braking curve to match and no turning circle to match, so there is
    /// almost nothing left to get wrong.
    /// </remarks>
    public static class SeaRouteRules
    {
        public static SeaRouteStep Advance(
            Vector2[] route,
            int waypointIndex,
            Vector2 position,
            float headingDegrees,
            float travelDistance)
        {
            if (route == null || waypointIndex >= route.Length - 1 || travelDistance <= 0f)
            {
                return new SeaRouteStep(position, headingDegrees, waypointIndex, route == null ||
                    waypointIndex >= route.Length - 1);
            }

            var index = waypointIndex;
            var remaining = travelDistance;
            var heading = headingDegrees;
            while (remaining > 0f && index < route.Length - 1)
            {
                var target = route[index + 1];
                var toTarget = target - position;
                var distance = toTarget.magnitude;
                if (distance <= 0.000001f)
                {
                    index++;
                    continue;
                }

                heading = SeaGeometry.HeadingTo(position, target);
                if (distance > remaining)
                {
                    position += toTarget * (remaining / distance);
                    return new SeaRouteStep(position, heading, index, false);
                }

                position = target;
                remaining -= distance;
                index++;
            }

            return new SeaRouteStep(position, heading, index, index >= route.Length - 1);
        }
    }
}
```

Add the bearing helper to `apps/game-unity/Assets/Domain/SeaGeometry.cs`:

```csharp
using UnityEngine;

namespace Sea.Client
{
    /// <summary>
    /// The client's copy of the compass convention: 0 is north, 90 is east, and
    /// north is up the screen, which on a y-down chart is -y. This mirrors
    /// GeometryRules.HeadingTo on the server and must not drift from it.
    /// </summary>
    public static class SeaGeometry
    {
        public static float HeadingTo(Vector2 from, Vector2 to)
        {
            var delta = to - from;
            if (delta.sqrMagnitude <= 0.000001f)
            {
                return 0f;
            }

            return NormalizeAngle(Mathf.Atan2(delta.x, -delta.y) * Mathf.Rad2Deg);
        }

        public static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            return angle < 0f ? angle + 360f : angle;
        }
    }
}
```

with its own test:

```csharp
[Test]
public void ZeroIsNorthAndNinetyIsEast()
{
    Assert.AreEqual(0f, SeaGeometry.HeadingTo(Vector2.zero, new Vector2(0f, -1f)), 0.001f);
    Assert.AreEqual(90f, SeaGeometry.HeadingTo(Vector2.zero, new Vector2(1f, 0f)), 0.001f);
    Assert.AreEqual(180f, SeaGeometry.HeadingTo(Vector2.zero, new Vector2(0f, 1f)), 0.001f);
    Assert.AreEqual(270f, SeaGeometry.HeadingTo(Vector2.zero, new Vector2(-1f, 0f)), 0.001f);
}
```

- [ ] **Step 4: Delete the old mirror**

```sh
git rm apps/game-unity/Assets/Domain/SeaSailingRules.cs apps/game-unity/Assets/Domain/SeaSailingRules.cs.meta
grep -rn "SeaSailingRules\|SeaSailingParameters\|SeaSailingState\|SeaSailingStep" apps --include='*.cs'
```

Every hit is a call site to move onto `SeaRouteRules`.

- [ ] **Step 5: Run**

```sh
pnpm unity:test
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(client): walk the route the server walks

The client held a two-hundred-line copy of an inertia model the server no
longer has. What replaces it is fifteen lines, and its last test asserts the
same numbers the server's own test does, so the two cannot drift without one of
them going red."
```

### Task 13.2: Predict with the effective speed

**Files:**
- Modify: `apps/game-unity/Assets/Presentation/SeaLocalShipPrediction.cs`
- Test: `apps/game-unity/Assets/Tests/EditMode/SeaLocalShipPredictionTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void PredictionUsesTheSpeedTheServerSentNotTheHullsRatedSpeed()
{
    // The server says she is doing 4.25 in a storm; her rated speed is 5.0.
    var prediction = new SeaLocalShipPrediction();
    prediction.OnServerUpdate(
        position: new Vector2(50f, 50f),
        headingDegrees: 90f,
        effectiveSpeed: 4.25f,
        route: new[] { new Vector2(50f, 50f), new Vector2(250f, 50f) },
        routeVersion: 1);

    prediction.Advance(1.0f);

    Assert.AreEqual(54.25f, prediction.Position.x, 0.01f);
}

[Test]
public void ANewRouteVersionReplacesTheOldOneWithoutASnap()
{
    var prediction = new SeaLocalShipPrediction();
    prediction.OnServerUpdate(new Vector2(50f, 50f), 90f, 5f,
        new[] { new Vector2(50f, 50f), new Vector2(250f, 50f) }, 1);
    prediction.Advance(1f);

    prediction.OnServerUpdate(new Vector2(55f, 50f), 90f, 5f,
        new[] { new Vector2(55f, 50f), new Vector2(55f, 250f) }, 2);

    Assert.AreEqual(55f, prediction.Position.x, 0.01f);
    Assert.AreEqual(50f, prediction.Position.y, 0.01f);
}

[Test]
public void HeadingCatchesUpOverFourHundredMillisecondsRatherThanSnapping()
{
    var prediction = new SeaLocalShipPrediction();
    prediction.OnServerUpdate(new Vector2(0f, 0f), 0f, 5f,
        new[] { new Vector2(0f, 0f), new Vector2(0f, 100f) }, 1);

    prediction.OnServerUpdate(new Vector2(0f, 0f), 180f, 5f,
        new[] { new Vector2(0f, 0f), new Vector2(0f, -100f) }, 2);
    prediction.Advance(0.2f);

    Assert.AreEqual(90f, prediction.DrawnHeadingDegrees, 5f);
}

[Test]
public void SmallDisagreementsAreEasedAwayRatherThanSnapped()
{
    var prediction = new SeaLocalShipPrediction();
    prediction.OnServerUpdate(new Vector2(50f, 50f), 90f, 5f,
        new[] { new Vector2(50f, 50f), new Vector2(250f, 50f) }, 1);
    prediction.Advance(1f);

    // The server says 54.4 where we drew 55.0: six tenths of a square, which
    // is inside the tolerance, so the drawn hull stays put and closes the gap
    // over the next few frames.
    prediction.OnServerUpdate(new Vector2(54.4f, 50f), 90f, 5f,
        new[] { new Vector2(54.4f, 50f), new Vector2(250f, 50f) }, 1);

    Assert.AreEqual(55f, prediction.Position.x, 0.01f);
}

[Test]
public void AnErrorOverOneSquareSnapsToTheServer()
{
    var prediction = new SeaLocalShipPrediction();
    prediction.OnServerUpdate(new Vector2(50f, 50f), 90f, 5f,
        new[] { new Vector2(50f, 50f), new Vector2(250f, 50f) }, 1);
    prediction.Advance(1f);

    // Two squares out. Easing that away would leave the hull wrong for most of
    // a second, so she is put where the server says she is (SEA_5 §12.3).
    prediction.OnServerUpdate(new Vector2(53f, 50f), 90f, 5f,
        new[] { new Vector2(53f, 50f), new Vector2(250f, 50f) }, 1);

    Assert.AreEqual(53f, prediction.Position.x, 0.01f);
}
```

- [ ] **Step 2: Run it and watch it fail**

```sh
pnpm unity:test
```

Expected: FAIL — prediction reads the hull's rated speed from the content
catalog, so a ship in a storm is drawn ahead of where she is and snapped back
every time the server speaks.

- [ ] **Step 3: Fix the prediction**

Four changes:

1. Take `EffectiveSpeedSquaresPerSecond` off the ship row rather than looking
   up the hull, so wind, storms, damage and slows are all already in it.
2. Follow the `ShipRoute` row with `SeaRouteRules.Advance`, and reset to the
   server's position whenever `RouteVersion` changes.
3. Turn the drawn heading toward the server's heading over
   `HeadingCatchUpSeconds = 0.4f` rather than setting it, so the 90-degree
   snap a route corner produces is smoothed for the eye without the position
   ever lying (SEA_5 §6.2).
4. Compare the drawn position against the server's on every update. Under
   `SnapToleranceSquares` the drawn hull keeps her place and the difference is
   folded into the next few frames; over it she is put where the server says
   (SEA_5 §12.3). Straight-line movement makes the second case rare: only a
   lost packet or a course change we have not heard about yet can open a gap
   that wide.

```csharp
        /// <summary>
        /// A route turns a corner instantly, which is correct and looks wrong.
        /// The drawn heading catches up over four hundred milliseconds; the
        /// position is never smoothed, because that is what the server is
        /// authoritative about.
        /// </summary>
        public const float HeadingCatchUpSeconds = 0.4f;

        /// <summary>
        /// How far the drawn hull may be from the server's before she is moved
        /// rather than eased. SEA_5 §12.3 sets this at one square: below it a
        /// captain cannot see the difference, above it she can, and easing a
        /// two-square error would leave the hull wrong for most of a second.
        /// </summary>
        public const float SnapToleranceSquares = 1.0f;

        /// <summary>
        /// How long an error under the tolerance takes to close. Short enough
        /// that it is gone before the next server tick, slow enough that it
        /// reads as the ship settling rather than as a jump.
        /// </summary>
        public const float ErrorEaseSeconds = 0.2f;
```

- [ ] **Step 4: Run**

```sh
pnpm unity:test
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "fix(client): predict with the speed the server is actually using

Prediction read the hull's rated speed, so a ship in a storm or under a slow
was drawn ahead of herself and pulled back on every update. The drawn heading
now catches up over four hundred milliseconds so a route corner does not snap,
while the position stays exactly what the route says. Disagreements under one
square are eased away over two hundred milliseconds and anything larger snaps,
which is the tolerance SEA_5 §12.3 asks for."
```

### Task 13.3: Draw the route and the new ruler

**Files:**
- Modify: `apps/game-unity/Assets/Presentation/SeaWorldView.Chart.cs`, `SeaHudView.cs`
- Test: `apps/game-unity/Assets/Tests/EditMode/SeaChartCoordinatesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void TheRulerRunsAToAnAndOneToForty()
{
    Assert.AreEqual("A1", SeaChartCoordinates.LabelAt(0f, 0f));
    Assert.AreEqual("AN40", SeaChartCoordinates.LabelAt(399f, 399f));
}

[Test]
public void ARouteIsDrawnAsOneLineThroughEveryWaypoint()
{
    var points = SeaRouteView.BuildLine(
        new[] { new Vector2(0f, 0f), new Vector2(10f, 0f), new Vector2(10f, 10f) });

    Assert.AreEqual(3, points.Length);
}

[Test]
public void NoRouteDrawsNothing()
{
    Assert.AreEqual(0, SeaRouteView.BuildLine(System.Array.Empty<Vector2>()).Length);
}
```

- [ ] **Step 2: Run it and watch it fail**

```sh
pnpm unity:test
```

Expected: FAIL, the ruler is still 20 squares and `SeaRouteView` does not exist.

- [ ] **Step 3: Draw them**

The ruler already reads `SeaChartCoordinates`, which Task 1.5 changed to 40×40,
so the HUD change is to widen the label field to three characters.

`SeaRouteView` is a pooled `LineRenderer` per visible ship. The pure part, which
is what the test above exercises, is the point list:

```csharp
using UnityEngine;

namespace Sea.Client
{
    /// <summary>Turning a route into a line to draw (SEA_5 §4.3).</summary>
    /// <remarks>
    /// A route is already the polyline: there is nothing to smooth, because the
    /// ship really does turn each corner instantly. Drawing a curve here would
    /// show a captain a course her ship is not following.
    /// </remarks>
    public static class SeaRouteView
    {
        private static readonly Vector3[] Empty = new Vector3[0];

        public static Vector3[] BuildLine(Vector2[] route)
        {
            if (route == null || route.Length == 0)
            {
                return Empty;
            }

            var points = new Vector3[route.Length];
            for (var index = 0; index < route.Length; index++)
            {
                points[index] = new Vector3(route[index].x, 0f, route[index].y);
            }

            return points;
        }
    }
}
```

The `MonoBehaviour` around it draws the local ship's route always and another
ship's only while she is selected, so a crowded chart is not a cobweb.

- [ ] **Step 4: Run**

```sh
pnpm unity:test
pnpm check
```

Expected: PASS, exit 0.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(client): draw the course a ship is actually following

SEA_5 §4.3 makes the route public, so a captain can see the line she ordered
rather than inferring it from where the bow points. Other ships' courses are
drawn only while selected, which keeps a crowded chart readable."
```

---

# Phase 14 — Replicate a chunk, not a ship

This is the rewrite `docs/STATUS.md` §4 names as the known fix for both missed
gates, and it is the largest single change in the plan. It is here, after the
physics, because it changes how a position reaches a client and nothing about
what the position is.

The measured problem: a movement shard carries every hull it sails in one blob,
so moving one hull rewrites the whole blob. p95 is 37.7 ms against a 10 ms
gate, and only 1,357 of 5,000 ships kept sailing.

### Task 14.1: The `ChunkMovement` table

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Schema/Tables.cs`
- Create: `server/spacetimedb/spacetimedb/Domain/ChunkBlobRules.cs`
- Test: `server/spacetimedb/tests/ChunkBlobRulesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class ChunkBlobRulesTests
{
    [Fact]
    public void AShipPacksIntoSixteenBytes()
    {
        Assert.Equal(16, ChunkBlobRules.BytesPerShip);
    }

    [Fact]
    public void WhatIsPackedComesBackOut()
    {
        var buffer = new byte[ChunkBlobRules.BytesPerShip];
        ChunkBlobRules.Pack(buffer, 0, entityId: 4242UL, x: 123.5f, y: 76.25f, headingDegrees: 47f);

        ChunkBlobRules.Unpack(buffer, 0, out var entityId, out var x, out var y, out var heading);

        Assert.Equal(4242UL, entityId);
        Assert.Equal(123.5f, x, 2);
        Assert.Equal(76.25f, y, 2);
        Assert.Equal(47f, heading, 1);
    }

    [Fact]
    public void PositionKeepsAHundredthOfASquare()
    {
        var buffer = new byte[ChunkBlobRules.BytesPerShip];
        ChunkBlobRules.Pack(buffer, 0, 1UL, 399.99f, 0.01f, 359.9f);
        ChunkBlobRules.Unpack(buffer, 0, out _, out var x, out var y, out _);

        Assert.Equal(399.99f, x, 2);
        Assert.Equal(0.01f, y, 2);
    }

    [Fact]
    public void AFullChunkOfShipsPacksAndUnpacksWithNoAllocation()
    {
        var buffer = new byte[64 * ChunkBlobRules.BytesPerShip];
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 64; index++)
        {
            ChunkBlobRules.Pack(buffer, index, (ulong)index, index, index, index);
            ChunkBlobRules.Unpack(buffer, index, out _, out _, out _, out _);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: build error, `ChunkBlobRules` does not exist.

- [ ] **Step 3: Write it**

```csharp
using System.Buffers.Binary;

namespace Sea.Server;

/// <summary>
/// Packing the ships in one chunk into a single row (SEA_5 §12.1).
/// </summary>
/// <remarks>
/// Sixteen bytes a hull: eight for her id, two each for x and y as hundredths
/// of a square, two for her heading in tenths of a degree, and two spare for a
/// status word. A hundredth of a square is a tenth of a metre at any scale a
/// captain can see, and a tenth of a degree is far finer than a sprite can be
/// drawn, so nothing is lost that anyone can look at.
/// </remarks>
public static class ChunkBlobRules
{
    public const int BytesPerShip = 16;

    private const float PositionScale = 100f;
    private const float HeadingScale = 10f;

    public static void Pack(
        Span<byte> buffer, int index, ulong entityId, float x, float y, float headingDegrees)
    {
        var slot = buffer.Slice(index * BytesPerShip, BytesPerShip);
        BinaryPrimitives.WriteUInt64LittleEndian(slot, entityId);
        BinaryPrimitives.WriteUInt16LittleEndian(
            slot[8..], (ushort)MathF.Round(Math.Clamp(x, 0f, 400f) * PositionScale / 10f * 10f));
        BinaryPrimitives.WriteUInt16LittleEndian(
            slot[10..], (ushort)MathF.Round(Math.Clamp(y, 0f, 400f) * PositionScale / 10f * 10f));
        BinaryPrimitives.WriteUInt16LittleEndian(
            slot[12..],
            (ushort)MathF.Round(GeometryRules.NormalizeAngle(headingDegrees) * HeadingScale));
        BinaryPrimitives.WriteUInt16LittleEndian(slot[14..], 0);
    }

    public static void Unpack(
        ReadOnlySpan<byte> buffer,
        int index,
        out ulong entityId,
        out float x,
        out float y,
        out float headingDegrees)
    {
        var slot = buffer.Slice(index * BytesPerShip, BytesPerShip);
        entityId = BinaryPrimitives.ReadUInt64LittleEndian(slot);
        x = BinaryPrimitives.ReadUInt16LittleEndian(slot[8..]) / PositionScale;
        y = BinaryPrimitives.ReadUInt16LittleEndian(slot[10..]) / PositionScale;
        headingDegrees = BinaryPrimitives.ReadUInt16LittleEndian(slot[12..]) / HeadingScale;
    }
}
```

Note the position range: 400 squares at a hundredth is 40,000, which fits a
`ushort` with room to spare. That is why the map size and the packing are
allowed to be this simple, and why this task could not have come before Phase 1.

- [ ] **Step 4: Add the table**

```csharp
/// <summary>
/// Every ship in one chunk, packed. Public, and the only thing a client
/// subscribes to for other ships' positions.
/// </summary>
[Table(Name = "ChunkMovement", Public = true)]
[Index.BTree(Name = "ByMapAndChunk", Columns = new[] { nameof(MapId), nameof(ChunkX), nameof(ChunkY) })]
public partial struct ChunkMovement
{
    [PrimaryKey]
    public uint Id;

    public byte MapId;
    public byte ChunkX;
    public byte ChunkY;
    public ushort ShipCount;
    public ulong Tick;
    public List<byte> Payload;
}
```

- [ ] **Step 5: Run**

```sh
pnpm server:build
pnpm server:generate:csharp
pnpm server:generate:typescript
pnpm server:test
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(schema): pack a chunk of ships into one row

Sixteen bytes a hull at a hundredth of a square and a tenth of a degree, which
is finer than anything a captain can see. A four-hundred-square map fits a
ushort at that precision, which is what makes the packing this simple."
```

### Task 14.2: Publish per chunk

**Files:**
- Modify: `server/spacetimedb/spacetimedb/Simulation/SailingSystem.cs`, `ReplicationRules.cs`
- Delete: the `ShipMovement` per-ship publication
- Test: `server/spacetimedb/tests/integration/ReplicationTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void AChunkWithNoMovementIsNotRewritten()
{
    using var world = TestWorld.Start();
    world.SpawnPlayerShip(1, 200f, 200f);
    world.RunTicks(10);
    var before = world.RowWritesInLastTick();

    world.RunTicks(1);

    Assert.Equal(0u, before);
}

[Fact]
public void TenShipsInOneChunkCostOneRowWrite()
{
    using var world = TestWorld.Start();
    for (var index = 0; index < 10; index++)
    {
        var ship = world.SpawnPlayerShip(1, 200f + index, 200f);
        world.IssueCourse(ship, 220f + index, 200f);
    }

    world.RunTicks(1);

    Assert.Equal(1u, world.ChunkRowWritesInLastTick());
}

[Fact]
public void AShipLeavingAChunkIsRemovedFromItsBlob()
{
    using var world = TestWorld.Start();
    var ship = world.SpawnPlayerShip(1, 249f, 200f);
    world.IssueCourse(ship, 260f, 200f);
    world.RunTicks(40);

    Assert.DoesNotContain(ship, world.ShipsInChunk(1, chunkX: 4, chunkY: 4));
    Assert.Contains(ship, world.ShipsInChunk(1, 5, 4));
}
```

- [ ] **Step 2: Run it and watch it fail**

Expected: FAIL, one row per moving ship per tick.

- [ ] **Step 3: Rewrite publication**

`ProcessMovementBatch` stops calling `PublishMovement` per ship. Instead it
marks the chunk dirty:

```csharp
            // A chunk is rewritten at most once a tick however many hulls in it
            // moved, which is the whole point of the blob.
            if (moved || changedChunk)
            {
                world.MarkChunkDirty(ship.MapId, chunkX, chunkY);
                if (changedChunk)
                {
                    world.MarkChunkDirty(ship.MapId, ship.ChunkX, ship.ChunkY);
                }
            }
```

and a new `PublishDirtyChunks(ctx, world)` runs once at the end of the tick,
packing each dirty chunk's ships from the shard arrays into its `Payload` and
writing one row.

`ReplicationRules.ShouldPublish` keeps its job — deciding whether a hull has
moved enough to be worth republishing — but now answers per chunk: a chunk is
dirty if any hull in it answers yes.

- [ ] **Step 4: Move the client onto it**

`SeaWorldView` reads `ChunkMovement` rows instead of `ShipMovement` rows,
unpacks with a client mirror of `ChunkBlobRules`, and interpolates the same way
it does today. The subscription query changes from a per-ship filter to
`SELECT * FROM ChunkMovement WHERE MapId = ? AND ChunkX BETWEEN ? AND ? AND ChunkY BETWEEN ? AND ?`,
which with `SubscriptionRadiusSquares = 65` and 50-square chunks is at most a
4×4 block of chunks.

- [ ] **Step 5: Run**

```sh
pnpm server:test
pnpm unity:test
pnpm verify
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "perf(server): replicate a chunk of ships instead of each ship

A movement shard rewrote its whole blob to move one hull, which is what put the
world tick at 37.7 ms against a 10 ms gate and stopped 3,600 of 5,000 ships
from sailing. A chunk is now written at most once a tick however many hulls in
it moved."
```

---

# Phase 15 — The twenty acceptance tests

SEA_5 §13 lists twenty numbered tests. Most are already covered by the unit
tests written along the way; this phase writes them out as one file, in order,
so that the document and the suite can be read side by side.

### Task 15.1: `SeaFiveAcceptanceTests`

**Files:**
- Create: `server/spacetimedb/tests/SeaFiveAcceptanceTests.cs`

- [ ] **Step 1: Write the whole file**

One `[Fact]` per numbered test, named for it, each with a comment giving the
section it comes from. The twenty:

| # | Test | Asserted by |
| --- | --- | --- |
| 1 | Straight course, 200 sq at 5.0 sq/s, x = 100 at 10 s | `RouteRules.Advance` over 100 ticks |
| 2 | Course into an island returns `NO_PATH` | `SetCourse` on a land-locked point |
| 3 | Course round an island has ≤32 waypoints and no land on any segment | `PathfindingRules` + `LandMask.SegmentIsClear` |
| 4 | Nine `MoveTo` in one second: eight accepted, one dropped | `MoveRateRules` |
| 5 | Shot at 24.4 sq with a 24 sq gun fires; at 24.6 sq it does not | `RangeRules.IsWithinRange` |
| 6 | Shot from dead ahead hits the bow | `CombatRules.FaceHit` |
| 7 | Downwind is 1.10×, upwind 0.90×, abeam 1.00× | `SpeedRules.WindMultiplier` |
| 8 | Storm and head wind together are 0.85 × 0.90 | `SpeedRules.Effective` |
| 9 | Hull at 40% is 0.92×, at 20% is 0.85× | `SpeedRules.HpStateMultiplier` |
| 10 | 35% of bonuses gives 1.25× | `SpeedRules.Effective` with the kept cap |
| 11 | Two slows multiply but stop at 0.50× | `SpeedRules.Effective` |
| 12 | A frozen ship makes no way and keeps her route | `SpeedRules.Effective` + `RouteRules.Advance` |
| 13 | A stopped ship in a current drifts, and stops at land | `ApplyCurrentDrift` |
| 14 | Sailing into the edge band moves her to the next map | `MapCrossingTests` |
| 15 | A crossing drops her route, effects and target | `MapCrossingTests` |
| 16 | Boarding a ship above 50% hull is refused | `BoardingTests` |
| 17 | A boarding win costs the loser 10% of max hull and 3 s of guns | `BoardingRules.Resolve` |
| 18 | One shot in ten is a critical for 1.5× | `CriticalHitRules` |
| 19 | The same seed and command log replay to the same state hash | the existing replay harness, extended to cover routes |
| 20 | An NPC chases inside 20 sq, holds at 0.8× range, leashes past 60 sq | `NpcMovementRules.Decide` |

Each test in the file calls the rule directly rather than through the module
where it can, so a failure names the rule that broke.

- [ ] **Step 2: Run**

```sh
./scripts/dotnet.sh test server/spacetimedb/tests/Sea.Server.Tests.csproj --filter "FullyQualifiedName~SeaFiveAcceptanceTests"
```

Expected: `Passed! - Failed: 0, Passed: 20`.

- [ ] **Step 3: Fix whatever fails**

A failure here is a real gap, not a test to adjust. Do not change an assertion
to match what the code prints; find which task left the gap and fix it there.

- [ ] **Step 4: Commit**

```bash
git add server/spacetimedb/tests/SeaFiveAcceptanceTests.cs
git commit -m "test(server): assert the twenty acceptance tests from SEA_5 §13

One fact per numbered test, in the document's own order, so the specification
and the suite can be read side by side. Each calls the rule directly where it
can, so a failure names what broke."
```

---

# Phase 16 — Measure it again

`tests/performance/Sea.PerformanceEvidence/PerformanceBudget.cs` still holds the
Milestone 1 gates and `pnpm runtime:test:scale-isolated` still exits non-zero on
purpose. Phase 14 was supposed to fix that. This phase finds out.

### Task 16.1: Re-baseline

- [ ] **Step 1: Run the scale test**

```sh
pnpm verify:full
```

Expected: the 100-client world tick under the 10 ms p95 / 20 ms p99 gates, and
5,000 ships sailing rather than 1,357.

- [ ] **Step 2: Record what was measured**

Write the numbers into `docs/performance/benchmarks.md` beside the old ones,
with the date and the commit. Do not lower a gate to make it pass. If a gate is
still missed, write down by how much and why, exactly as `docs/STATUS.md` §4
does today.

- [ ] **Step 3: Profile whatever is still over**

The three most likely remaining costs, in order:

1. **A\* on the NPC replan.** 100 enemies replanning twice a second is 200
   searches a second. If this shows up, raise `NpcMovementRules.ReplanIntervalTicks`
   and re-measure; the straight-line test answers most of them without a search
   at all, so measure before assuming.
2. **The land mask lookup per drift.** `ContentCatalog.LandMaskFor` should
   return a cached instance, not build one. Check it does.
3. **Packing a chunk that did not change.** Confirm `MarkChunkDirty` is not
   being called for a hull that moved less than `ReplicationRules` cares about.

- [ ] **Step 4: Commit**

```bash
git add docs/performance/benchmarks.md tests/performance
git commit -m "perf: re-baseline the tick after the chunk-blob rewrite

The numbers, the date and the commit they were taken at. No gate has been
lowered; anything still missed is written down as missed."
```

---

# Phase 17 — Close out

### Task 17.1: Final review gates

- [ ] **Step 1: Ask the user to run both reviews**

Neither can be started by an agent — both carry `disable-model-invocation: true`
and refuse unless a person invokes them. Ask for:

- `/thermo-nuclear-code-quality-review`
- `/improve-codebase-architecture`

over the whole range this plan produced.

- [ ] **Step 2: Fix what comes back**

One commit per finding, in the same style as the rest of the plan.

### Task 17.2: Update the documents

**Files:**
- Modify: `docs/STATUS.md`, `docs/PLAN.md`

- [ ] **Step 1: Rewrite `docs/STATUS.md` §1**

The world section is wrong in every particular: it says one map, twenty squares
by twenty, one square is ten world units, the world runs from -100 to +100. All
four are now false. The sailing section's "stops from full speed in 10 units and
turns in a circle 9 units wide" describes a model that no longer exists.

- [ ] **Step 2: Move the finished items out of §2 and §3**

Boarding, map edge exits, maps 1/2 and 1/3, and the trust score all move from
"partly done" or "not started" into "done and working". Say plainly that they
arrived ahead of their milestone.

- [ ] **Step 3: Rewrite §4 with the Phase 16 numbers**

If both gates now pass, §4 becomes short. If one still misses, it stays, with
the new number.

- [ ] **Step 4: Commit**

```bash
git add docs/STATUS.md docs/PLAN.md
git commit -m "docs: say what the world is now

Every number in the world section described a twenty-square map measured in
world units. Boarding, map crossings, two more maps and the trust score are
done and are recorded as done, ahead of the milestones that planned them."
```

---

## What this plan does not do

Written down so that nobody looks for them:

- **Ramming and ship-to-ship collision.** SEA_5 §4.1.7 says hulls pass through
  each other, so nothing here changes it. Ramming stays Milestone 3.
- **Abilities and the skill trees.** SEA_5 does not describe them, and the
  bonuses they would feed already have a capped input in `SpeedRules` and
  `RangeRules`.
- **The remaining three ammunition types.** Frost, Blessed and Heavy arrive with
  the maps that use them.
- **Island towers.** SEA_5 §10.4 gives a tower a circle of `TOWER_RANGE` and
  the same range check and fire timing as a ship, so it is a small piece of
  work — but a tower belongs to whoever owns the island, and island ownership
  is Milestone 4. Building the gun before there is anyone to own it would mean
  guessing who it shoots at. `RangeRules` and the volley path this plan leaves
  behind are already the two things a tower needs.
- **Anything in Milestones 4 and 5.** Guilds, ownership, accounts, payments.
- **The played session** that `docs/STATUS.md` §5 still owes. A person has to
  sail it.
