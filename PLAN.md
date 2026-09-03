# Sea roadmap

Date: 2026-09-02
Status: adopted 2026-09-02. This file is the source of truth for scope,
milestone order, acceptance gates, and commit boundaries. It supersedes the
"Sea scalability and PvE completion plan", whose completed phases are listed
in section 6.

Read `docs/SEA_5_GAP_ANALYSIS.md` first. It explains every difference between
the design docs and the build and how each one is resolved. This file only
orders the work. The four design docs `docs/SEA_1_KNOWLEDGE.md` through
`docs/SEA_4_TECHNICAL.md` are the design of record.

## 1. Why the previous roadmap stopped at Phase 17

The previous plan's Phase 18 (capacity proof) and Phase 19 (playtest) were not
run. Milestone 1 starts instead.

Reasons:

- Phase 19 would playtest broadside arcs, weak points, sub-pools, and XP. The
  design docs remove all four. The findings would be discarded.
- Phase 18 would prove tick budgets for a combat model that is about to be
  replaced. The tooling from `644e94c` stays and runs again on the new model,
  where the numbers matter.
- Everything the design docs assume about the platform already exists:
  authoritative module, command envelope, 10 Hz sharded tick, spatial
  interest, encounters, replay, rebinding, pooled presentation, evidence
  tooling. Milestone 1 is a combat rules rewrite on a finished platform, not
  a restart.

What is kept from the previous Phase 18: the scale smoke (`pnpm runtime:test:scale-isolated`)
becomes a gate on every milestone, and the full 5,000 client proof runs at the
end of Milestone 1 against the real combat model.

## 2. Invariants that do not change

These are the `AGENTS.md` rules restated so nothing in the milestones can be
read as loosening them.

- The server computes movement, combat, rewards, and progression. Clients
  send intent and render state.
- One `IssueShipCommand(CommandEnvelope)` entry point. Design-doc reducer
  names (`MoveTo`, `Fire`, `StartRepair`, `UseKit`, `Ram`, `Board`) are
  `ShipCommand` variants. Expected rejections return typed results.
- Players and NPCs share one command policy and one effect executor.
- Islands and land block movement. Nothing blocks shots.
- Indexed due-tick and spatial work only. No full-table scans in hot paths.
- Same seed and command log produce the same state hash.
- Regression test first, then the change. One conventional commit per
  sub-phase. Handwritten C# files stay at or under 500 lines. Generated
  bindings are regenerated on both sides, never edited.
- PostgreSQL, Redis, and MinIO stay out of the combat path.
- Only owned or licensed assets. No Seafight assets, names, audio, or numbers.
- Gates: `pnpm ci:fast` and `pnpm server:test` before every commit,
  `pnpm verify` before every milestone commit, `pnpm verify:full` only where a
  sub-phase names it.

## 3. Units and constants shared by every milestone

- One design square is 10 world units. Map 1/1 is 20 x 20 squares, which is
  the existing 200 x 200 unit world. Chunk size stays 25 units.
- Tick is 100 ms. Seconds in the design docs convert to ticks by x10.
- Cannon range 8 squares is 80 units. Speed 2.4 squares per second is 2.4
  units per tick.
- All balance constants live in one `StatCaps` row seeded from JSON. Code
  never hardcodes a cap the docs list in Math section 13.2.

## 4. Milestones

Each milestone ends with `pnpm verify`, a diff review, and a playtest note.
Sub-phases end with one commit each.

### Milestone 1: one map, one fight (Havenmere 1/1)

Goal: two base Tier 1 ships fight on Havenmere by the design rules, and a
player can sink every 1/1 enemy. Base versus base lasts 33 plus or minus 4 s.

Out of scope: Map Rank progression, missions, shops, skills, crew, PvP flag,
duels, arena, boarding, ram, abilities, guilds, accounts, cloud.

Sub-phases:

**1a. Content and ship stats.** Commit `feat(content): add Havenmere content and ship stats`

- Move content from `ContentDefinitions.cs` to embedded JSON under the
  module: `Map`, `Sector`, `HullDef`, `CannonDef`, `AmmoDef`, `NpcDef`,
  `StatCaps`. `Init` validates and throws on bad content.
- Seed one hull (Tier 1, 1,600 HP, armor 0.15 front, 0.08 sides, 0.03 back,
  8 slots, speed 2.4, turn 60), one cannon (Tier 1, 20 damage, 3.0 reload,
  range 8), four ammo (Round, Chain, Fire, Grape with the Math section 4
  multipliers and effects), Havenmere with Port Lowell and its sector grid.
- Add `Hull`, `ShipStats`, and `RecomputeStats`. Add then cap, per-stat caps,
  Combat Power budget. With one hull and one cannon every stat equals base,
  which is the point: the pipeline is real before gear exists.
- `PlayerProgression` gains `MapRank`; a private `PlayerAccount` row holds
  `AccountId` so it never reaches other clients. `Level` and `Experience` go
  (done in 1a; encounter XP pools are removed in 1b). `LevelDefinition` goes.
- Tests: property tests that add-then-cap never exceeds a cap; Math section
  12.1, 12.2, 12.4, and 12.5 as unit tests over the content tables; content
  validation tests for every JSON file; replay hash unchanged for a
  no-command run.

**1b. Magazine firing and facing armor.** Commit `feat(combat): add magazine firing and facing armor`

- `Fire` replaces `FireBroadside`. Rules: selected target, within range, at
  least one ready volley, 1.0 s since the last volley, not in port. Resolve
  on the same tick. Damage is `floor(VolleyDamage x (1 - armor_face))` with
  facing from the angle between the target heading and the shooter.
- Ship gains `Magazine`, `ReadyVolleys`, `ReloadProgress`, `LastShotTick`,
  `LastCombatTick`. Reload ticks every tick, firing or not. Full refill after
  15 s with no shot fired or taken.
- Single HP pool. Remove sails, cannons, and crew pools, weak points, aim
  commands, and the four abilities. `ActivateAbility` and `StartBoarding`
  return `NotAvailable`.
- Ammo effects as `Effect` rows: Chain slows 30 percent for 4 s, Fire burns
  0.006 max HP per second for 5 s and halves healing, Grape adds 50 percent to
  reload for 3 s and only within 4 squares. Same effect refreshes, different
  effects stack.
- Volley rows stay as a public event the client animates. They carry no
  damage state.
- Tests: arc-free firing from every bearing; facing boundaries at 45 and 135
  degrees; magazine burst of 3 over 2 s then one per reload; 1.0 s minimum;
  idle refill; each ammo effect and its refresh rule; integration test that
  two base ships trading Round Shot on the sides sink in 32 to 38 s at 10 Hz.

**1c. Repair, port, sinking.** Commit `feat(combat): add channelled repair and port rules`

- `StartRepair`: 3 s channel, heal `floor(MaxHP x 0.20 x 0.6^n x burn)` at
  the end, cancelled by 15 percent of max HP taken during the channel or any
  Fire Shot hit, 15 s cooldown from the end or the cancel, fatigue window
  60 s. `UseKit`: instant 25 percent, 45 s cooldown, counts for fatigue,
  consumes one kit item.
- Port Lowell: inside the port circle the ship is invulnerable, effects are
  cleared, firing is rejected. Leaving is a 3 s cast-off channel.
- Sinking at 0 HP. Respawn after 8 s at Port Lowell at full HP with a 10 s
  spawn shield that blocks attacking and being attacked. The respawn choice
  command exists with one option.
- Tests: every repair rule including cancel-then-cooldown; fatigue after
  four heals; kit and repair on separate cooldowns; port invulnerability and
  cast-off; sinking, shield, and shield expiry.

**1d. Havenmere enemies.** Commit `feat(world): add Havenmere enemies`

- `NpcDef` stores tier, map, family, behavior. HP and DPS come from the
  derivation table at spawn: Common 0.50 EHP and 0.25 DPS, Veteran 1.00 and
  0.40, Named 5.00 and 0.90, armor 0.10 on every face for tiers 1 and 2, 0.20
  for tier 4. Gold is `G(1) = 30` times the tier multiplier.
- Seed Skiff (Common ship), Reef Crab (Common monster), Fancy (Veteran, one in
  five spawns), Red Mary (Named, one per map, 45 min, calls two Fancies at
  50 percent HP). Sea Dogs flee under 25 percent HP. Common respawn 30 s.
- NPC AI uses the same `Fire` policy as players: no arcs, hold range, fire
  when a volley is ready.
- Tests: derivation table values for map 1; solo kill of a Common in at most
  20 s; a base player survives a Common for 60 s while repairing on cooldown;
  Red Mary summons exactly once; flee threshold.

**1e. Client controls and screen.** Commit `feat(client): add Havenmere combat presentation`

- Default bindings from Mechanics section 1.1: left click sails or selects, Q
  and Space fire, holding fires when ready, Tab nearest enemy, Esc clears, R
  repair, 1 to 4 ammo. WASD and middle-mouse drag pan the camera; there is no
  manual steering and no full-speed key. E, F, Z X C V,
  P stay bound and show "not available yet". Rebinding stays.
- Camera follows the ship. WASD or middle mouse drag pans and the camera stays
  where it was pushed so the player can scout ahead; Space or the HUD recenter
  button re-attaches it to the ship. Wheel zooms.
- HUD from Mechanics section 1.2: HP bar, magazine dots, reload bar, repair
  cooldown, wind arrow, target frame with name and HP, cast-off bar, respawn
  countdown. Broadside bars, aim rail, and ability rail go. Ruler shows
  squares 1 to 20.
- Tests: EditMode tests for binding defaults, HUD state mapping, camera
  follow; existing pooling, interpolation, and performance tests keep
  passing.

**1f. Validation.** Commit `docs(validation): record Havenmere playtest`

- `pnpm verify`, then `pnpm verify:full` with the scale smoke and the 5,000
  client proof from the previous Phase 18 against the new model. Keep the
  Phase 18 numeric gates: tick p95 at most 10 ms, ack p95 at most 150 ms,
  99.9 percent connected, 250 ships at 60 FPS on macOS, 100 on WebGL.
- Four local clients share one Red Mary fight.
- The user plays 10 to 15 minutes covering sailing, steering, selecting,
  firing at every bearing, each ammo, repair and its cancel, port rules,
  sinking, respawn, every 1/1 enemy. Each finding is pass, fail, or deferred.
- Update `README.md`, `CONTEXT.md`, and `AGENTS.md` so they describe the
  Milestone 1 build.

### Milestone 2: progress and safety (Gull Rocks 1/2, Brine Fields 1/3)

- `SpawnPoint` table, damage-share `OnNpcKilled` replacing the 30/70 split,
  `DailyEarnings` cap.
- Map Rank, charts, edge exits, `EnterMap`, maps 1/2 and 1/3 with their
  enemies (Gull with Chain Shot, Brine Fields fort).
- Missions from Knowledge Appendix A for maps 1 to 3, Mission Board, daily
  and weekly.
- Port shops: Shipwright, Gunsmith with Tier 1 and 2 cannons, plates, ammo,
  kits. `BuyItem`, `SellItem`, `Equip`, `Unequip`. `RecomputeStats` now has
  something to compute.
- Cannons and Armor skill trees, `SetSkillLevel`, `ResetTree`, first
  abilities with the 4 x duration rule enforced at `Init`.
- Crew Hall with the first crew member.
- Harbor Protection, the flag, first-attack confirmation, duels with fog,
  `Fight`, `KillRecord`, Combat Rating, `IsRelated`, Honor ledger.
- Gates: fight-length and fight-score tests over every purchasable
  allocation, scale smoke, replay.

### Milestone 3: competition and trust

- Arena on own ships with free ammo, worker matchmaking over Redis.
- `TrustScore`, phantom NPCs, `BanWave`.
- Sails, Repair, and Plunder trees, Ship Configs, `SwitchConfig`.
- Boarding rebuilt on hands and Arms Locker. `Ram` with the ship collision
  rule.
- Parties, Rally Beacon, Harbor Jump, ping wheel, markers.

### Milestone 4: guilds and world

- Guilds, Renown, contribution payouts, Guild Arena league.
- Islands, towers, war windows, Garrison Supply, alliances.
- Maps 2/1 to 5/1 one biome at a time with their sea systems, enemies, and
  bosses from the derivation table. Frost, Blessed, and Heavy ammo.

### Milestone 5: money and operations

- Better Auth attached to `AccountId`, then cloud deployment.
- Stripe, Diamonds, cosmetics, Sea Pass, anonymous market, legendary auction.
- Admin writes, metrics history, approvals, replay viewer.

## 5. Recorded decisions

Approved 2026-09-02. Milestone 1 assumes all of them.

1. This roadmap replaces the previous plan. Its Phase 18 gates are folded
   into sub-phase 1f. Its Phase 19 is dropped.
2. Identity stays anonymous and local until Milestone 5. A private
   `PlayerAccount` table with `AccountId` is added in sub-phase 1a so Better
   Auth attaches later without a schema reset.
3. Click-to-sail is the only ship control. WASD and middle mouse drag pan the
   chart and the camera stays where it was pushed; Space or the HUD recenter
   button brings it back onto the ship. Q and E fire.
4. Ship to ship collision waits for `Ram` in Milestone 3. Ships pass through
   each other until then.
5. Content is embedded JSON validated by `Init` and by unit tests.
6. The four design docs are committed and are the design of record. Where a
   doc and the code disagree, the doc wins. Where `SEA_4_TECHNICAL.md`
   disagrees with the other three, the other three win.

Decisions from the previous plan that survive: local only until Milestone 5,
schema replacement may reset local SpacetimeDB data, four real local clients
share PvE while synthetic clients prove capacity, Apricum and other owned
models are the only imported art, procedural materials may fill gaps, no
Seafight assets, names, audio, or balance numbers.

## 6. Completed foundation (previous plan)

| Phase | Result | Commit |
| --- | --- | --- |
| 0 | Core combat roadmap | `e2fd19b` |
| 1 | Local resource runaway fixes | `697f18a` |
| 2 | Unified world and combat state | `704ba22` |
| 3 | Chart navigation and ship handling | `3d92909`, `0097b3b` |
| 4 | Combat controls and HUD | `a156db5` |
| 5 | Manual broadside volleys | `c6dcbe1` |
| 6 | Tactical damage and recovery | `a5e1670` |
| 7 | Scalable combat completion roadmap | `09b3374` |
| 8 | Pinned toolchain and quality tools | `377f477` |
| 9 | Server and Unity responsibility split | `33268a3` |
| 10 | Reducer and property test foundation | `20b7ddf` |
| 11 | Centralized authoritative ship commands | `11f142b` |
| 12 | Indexed server simulation work | `97988c8` |
| 13 | Stable shared-world subscriptions | `3803775` |
| 14 | Event-driven Unity presentation | `8922123` |
| 15 | Roaming combat and progression | `8aff429` |
| 16 | Shared PvE reward contracts | `43754fc` |
| 17 | Scalable owned-asset presentation | `715b92e` |
| 18 | Performance evidence tooling only; proof deferred to 1f | `644e94c` |

## 7. Test policy

Before every sub-phase commit:

1. Add or update the regression test first.
2. Confirm the new test fails for the expected reason.
3. Implement the sub-phase.
4. Run the sub-phase's own tests, then `pnpm ci:fast` and `pnpm server:test`.
5. Run `pnpm verify` before the last sub-phase of a milestone.
6. Review the diff for unrelated changes, generated drift, secrets, debug
   code, and asset licensing.
7. Commit only when the sub-phase is complete.
