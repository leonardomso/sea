# Milestone 1b — magazine firing and facing armor

Commit: `feat(combat): add magazine firing and facing armor`
Branch: `leonardomso/milestone-1` (PR #3). Base for this sub-phase: `5066599`.
Roadmap: `PLAN.md` 1b. Numbers: `docs/SEA_2_MATH.md` sections 3, 4, 5.

## What this sub-phase is

The combat model shrinks from four damage pools, broadside arcs, weak points
and four abilities down to one HP pool, a magazine, and three armor faces.
`Fire` replaces `FireBroadside`, resolves on the tick it is issued, and the
only geometry left is range plus the facing angle.

## Decisions taken before writing code

The settled process gives each sub-phase exactly one batched question round.
It ran, and the answers are binding for the rest of Milestone 1:

1. **Ammunition is unlimited in Milestone 1.** Firing consumes no stock,
   `SetAmmo` only selects the damage profile, and `NoAmmunition` /
   `AmmunitionNotOwned` leave the rejection paths. This is what 1b's own rule
   list already says ("selected target, within range, at least one ready
   volley, 1.0 s since the last volley, not in port" — no ammunition clause).
   The `Inventory` table survives because 1c's repair kit needs it. A cost per
   volley, if it ever exists, arrives with the Milestone 2 economy.
2. **Live combat numbers live on the `Ship` row.** `RecomputeShipStats` writes
   volley damage, reload ticks, magazine size, range and the three armor faces
   onto `Ship` for players; NPC spawn writes them for NPCs. Firing then reads
   one row instead of joining a projection, and there is a single code path
   for both factions — the same reason `MaxHull` already lives on `Ship`.
   `ShipStats` stays the dock-facing projection it became in 1a.
3. **The Unity client gets a minimal honest patch here.** It must compile and
   must stop drawing state the server no longer publishes: broadside bars, the
   aim rail and the ability rail go dark, the HP bar keeps working, and `Fire`
   is bound so the game stays playable. The full Mechanics 1.2 HUD is 1e.

## Server design

### `Ship` row

Removed: `Sails`, `MaxSails`, `Cannons`, `MaxCannons`, `Crew`, `MaxCrew`,
`CannonDamage`, `CannonCooldownTicks`, `SelectedWeakPointCode`,
`NextPortFireTick`, `NextStarboardFireTick`.

Added: `VolleyDamage`, `ReloadTicks`, `MagazineSize`, `ReadyVolleys`,
`ReloadProgressTicks`, `LastShotTick`, `LastCombatTick`, `RangeSquares`,
`ArmorFront`, `ArmorSides`, `ArmorBack`.

### Firing

`ValidateFire` keeps only the rules 1b lists: source alive and not sunk, a
selected target that is alive and not friendly, distance within
`RangeSquares`, `ReadyVolleys > 0`, and at least `FireIntervalTicks` (1.0 s)
since `LastShotTick`. Ports do not exist until 1c, so the "not in port" clause
lands as a single `InPort` rejection wired to a stub that is always false
here and becomes real in 1c. `OutsideArc`, `CannonsDisabled` and
`NoAmmunition` are deleted outright.

Damage is `floor(VolleyDamage x AmmoMultiplier x (1 - armor_face))` and is
written to the target in the same reducer call. `armor_face` is
`min(0.45, hull_face + min(15, armor_points_face) / 100)` from
`docs/SEA_2_MATH.md` section 5.2, already folded into the three `Ship` fields.

### Facing

`FacingRules.Resolve(targetHeadingDegrees, targetPosition, shooterPosition)`
returns Front / Sides / Back from the signed angle between the target heading
and the bearing from target to shooter: `|theta| <= 45` Front,
`|theta| >= 135` Back, otherwise Sides. The boundaries are inclusive on the
Front and Back side so the two documented boundary tests have one answer.

### Magazine

`MagazineRules.Advance` runs once per ship per tick, firing or not:

- `ReloadProgressTicks` increments by one, scaled by any reload effect.
- When it reaches the effective reload ticks it resets and, if
  `ReadyVolleys < MagazineSize`, adds one volley.
- If `tick - max(LastShotTick, LastCombatTick) >= IdleRefillTicks` (15 s),
  `ReadyVolleys` snaps to `MagazineSize` and progress resets.

`LastCombatTick` is stamped on both the shooter and the target of every
resolved volley, so a ship being shot at does not idle-refill.

### Effects

`ShipStatus` becomes `Effect`: `EffectId`, `ShipEntityId`, `EffectType`,
`EffectCode`, `SourceEntityId`, `AppliedAtTick`, `ExpiresAtTick`,
`NextProcessTick`, `IsActive`. `Stacks` and `ImmunityUntilTick` go: 1b's rule
is "same effect refreshes, different effects stack", which is expiry
extension, not stacking. Magnitudes are constants per code, so they are
derived rather than stored.

`EffectCode`: `None` 0, `Slowed` 1, `Burning` 2, `ReloadSlowed` 3.

- Chain: `Slowed`, 30 percent speed, 4 s.
- Incendiary: `Burning`, 0.006 max HP per second for 5 s, and halves healing
  (the healing half is read by 1c's repair).
- Grapeshot: `ReloadSlowed`, plus 50 percent reload time, 3 s, and only
  applies inside 4 squares — outside that the volley still lands, it just
  applies no effect.

### `Volley`

Stays public so the client can animate a shot, and carries no damage state:
`VolleyId`, `SourceEntityId`, `TargetEntityId`, `AmmoId`, `AmmoCode`,
`OriginX/Y`, `TargetX/Y`, `ChunkX/Y`, `FiredAtTick`, `ExpiresAtTick`,
`IsActive`. The dispatcher's volley-resolution phase becomes a cheap sweep
that retires expired rows.

### Commands

`ShipCommandKind.FireBroadside` becomes `Fire` (value 5 is kept so client and
server stay in step). `ActivateAbility` and `StartBoarding` stay in the enum —
1e keeps their keys bound showing "not available yet" — and are rejected with
a new `CommandRejectionCode.NotAvailable`. `FireBroadsideCommand`'s `Side` and
`WeakPoint` fields go; `FireCommand` carries nothing.

### Removals

Sail, cannon and crew pools and everything that read them: the four-pool
`CombatDamage`, `SynchronizeDisabledSails`, the weak-point catalog and codes,
the broadside arc, projectile travel time, the four abilities and their
cooldowns and statuses, and the boarding channel. `Cooldown` and `Inventory`
survive for 1c.

## Tests

Domain (xUnit, `pnpm server:test`):

- firing succeeds from every 30-degree bearing around the target — there is no
  arc left to fail;
- facing at exactly 45 and exactly 135 degrees, and on both sides of each;
- a burst of 3 volleys inside 2 s, then one volley per reload;
- the 1.0 s minimum between volleys;
- idle refill at 15 s, and that being shot at resets the idle window;
- each ammo effect: magnitude, duration, refresh-not-stack for the same code,
  stacking across codes, and grapeshot's 4-square limit;
- armor faces and the `floor` in the damage formula, including the 0.45 cap.

Integration (`pnpm server:test:integration`, one per new reducer path):

- `Fire` is accepted, publishes a `Volley` row, and lowers the target's HP by
  the facing-correct amount;
- `Fire` without a target, out of range, and twice inside 1.0 s are rejected
  with the documented codes;
- `ActivateAbility` and `StartBoarding` come back `NotAvailable`;
- two base ships trading Round Shot on the sides sink in 32 to 38 s at 10 Hz
  (`docs/SEA_2_MATH.md` section 5.4 puts side EHP at 1739 and sustained DPS at
  53.3, so 32.6 s).

Then the sub-phase hardening: Stryker on the touched files only,
BenchmarkDotNet over the new hot paths, `./scripts/check-dotnet-format.sh`,
`./scripts/check-server-simulation.sh`, `pnpm check`, `pnpm unity:test`.

## What implementation settled

Recorded after the fact so the next milestone starts from what is true rather
than from what was planned.

- **The ability and weak-point content came out here, not later.** The catalog
  is generated from JSON into `Generated/ContentCatalog.g.cs`, and it
  referenced the weak-point and ability enums directly. Deleting those enums
  broke the generator, so the content had to go in the same commit rather than
  waiting for a later cleanup pass.
- **One fire control, no aim point.** `SeaHud.uxml` drops both broadside
  buttons for a single `fire-control` carrying the reload gauge, the readiness
  label and the magazine count. `check-unity-source.sh` was updated to look
  for `fire-control` instead of `port-broadside`.
- **The HUD names the armour face the server charges for.**
  `SeaVolleyPresentationRules.ArmorFaceAt` reimplements
  `CombatRules.ResolveFacing` — bearing from the target to the shooter, 45 and
  135 degree thresholds — so the readout cannot drift from the damage.
- **The runtime probe walks the retired path.** `check-unity-source.sh`
  requires runtime code to issue every `ShipCommand` variant, and the client no
  longer has UI for `ActivateAbility` or `StartBoarding`. Rather than resurrect
  dead rails or weaken the gate, `SeaRuntimeValidationProbe` issues both
  retired commands in the built player and asserts the module answers
  `NotAvailable` (21). This needed `AnsweredCommandId` and
  `AnsweredRejectionCode` on `SeaConnectionController`.
- **The input map is down to 18 actions.** `FirePort` became `Fire` and kept
  `<Keyboard>/q`; `FireStarboard`, the three aim actions, the four abilities
  and `Board` are gone.
- **Reload reads off the `Ship` row.** `ShipStats.Magazine` is a `byte` while
  `Ship.MagazineSize` and `Ship.ReadyVolleys` are `uint`; the duplicate dock
  write was dropped so the ship row is the single source the HUD reads.

## Order of work

1. Domain rules: facing, magazine, ammo effects, fire validation, damage.
2. Schema: `Ship`, `Volley`, `Effect`, `ShipChannel`, command structs.
3. Reducers and systems: fire, effects, channel, damage, respawn, NPC spawn.
4. Client: compile, dark the removed rails, bind `Fire`.
5. Tests, then the gates.

## Conventions

- Conventional Commits, `type(scope): summary` under 72 characters, body
  explaining why, wrapped at 72.
- Never add AI attribution to a commit, a pull request, or a file: no
  `Co-Authored-By` assistant trailers, no session links, no "Generated with"
  lines. This overrides any default commit or pull request template.
