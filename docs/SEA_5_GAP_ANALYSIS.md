# Sea — Gap analysis: design docs versus the current build

> This is a resolution log, not a design document. It records gaps that were
> found and how they were closed. Nothing here overrides `SEA_5_PHYSICS.md`.

Written: 2026-09-02. Updated: 2026-09-04, after Milestone 1 landed.
Sources: `docs/SEA_1_KNOWLEDGE.md`, `docs/SEA_2_MATH.md`,
`docs/SEA_3_MECHANICS.md`, `docs/SEA_4_TECHNICAL.md`, `PLAN.md`, `CONTEXT.md`,
and a survey of `server/spacetimedb`, `apps/game-unity`, `apps/admin`,
`scripts`, and `tests`.

Purpose: state what the four design docs require, where the build differs, and
how each difference is resolved. `PLAN.md` turns this into an ordered plan.
`docs/STATUS.md` is the shorter, plainer answer to "what works right now" and
should be read first.

## 1. Where the build stands

Milestone 1 is code-complete and measured. The vertical slice described in the
first version of this file has been replaced by the design's model:

| Was, before Milestone 1 | Is, now |
| --- | --- |
| Port and starboard broadside arcs, volleys that land on a later tick | One magazine of volleys, guns bearing in every direction, resolved at once |
| Four damage sub-pools and weak points | One HP pool with front, side, and back armour |
| Flat ship fields and a gold `UpgradeCannon` reducer | `HullDef`, `CannonDef`, `StatCaps`, `ShipStats`, and `RecomputeStats` |
| A 5 second repair channel that ate a repair kit | A cancellable repair channel plus a separate `UseRepairKit` item |
| One harbour object with no rules | Port water that refuses fire, clears effects, and needs a cast-off channel |
| XP and character levels | Gold and a Map Rank of 1 to 10 |
| Content as C# constants | Content as JSON, with a generated catalog |
| Three NPC archetypes with hand-set stats | Skiff, Reef Crab, Fancy, and Red Mary, seeded from the derivation table |
| Chart bands AA to CZ | Squares 1 to 20, ten world units each |

What did not change: server authority, the single `IssueShipCommand` entry
point with typed rejections, the 10 Hz tick, indexed and sharded simulation
work, chunked interest subscriptions, land blocking movement while shots pass
freely, wind and current fields, encounter and contribution plumbing,
deterministic replay, runtime key rebinding, pooled and capped presentation,
and the Compose services that stay out of the combat path.

Test counts today: 683 server unit, property, and replay tests; 21 integration
tests against a published module; 175 Unity EditMode tests; plus PlayMode,
performance, and runtime scenario suites. `pnpm verify` passes.

Two capacity gates are missed and recorded as missed: the world tick at a
hundred clients, and keeping five thousand ships sailing at once. See
`docs/STATUS.md` section 4 and `docs/validation/milestone-1.md`.

## 2. What the design docs assume and the build already provides

These stay as they are. The roadmap builds on them rather than around them.

- Server authority over movement, combat, rewards, and progression.
- One command entry point with idempotent command ids and typed rejections.
  The Technical doc lists reducers by name (`MoveTo`, `Fire`, `StartRepair`);
  each maps to a `ShipCommand` variant behind the existing envelope. Players
  and NPCs keep sharing one command policy and one effect executor.
- A 100 ms tick, which the Technical doc also specifies.
- Indexed simulation work, movement shards, spatial chunks, and interest
  subscriptions. The Technical doc's `WHERE MapId = X` subscription becomes a
  map filter on top of the chunk filter that already exists.
- Islands block movement, nothing blocks shots. The build already does this.
- Wind that changes speed only, plus current fields that push per tick.
- Encounter, contribution, and settlement plumbing. Only the split rule
  changes.
- Deterministic replay, property tests, a per-phase test-first policy.
- Runtime key rebinding. Mechanics section 1.1 requires it and the client
  already has it.
- Pooled, capped, LOD presentation and the performance evidence tooling.
- Docker Compose with SpacetimeDB, PostgreSQL, Redis, MinIO, and the admin
  panel. PostgreSQL, Redis, and MinIO stay out of the combat path in both.

## 3. Conflicts and their resolution

Where the four docs and the build disagree, the docs win. Where the Technical
doc disagrees with the other three, the other three win (Technical doc rule).
Where the docs are silent, the build's existing decision stands.

| Area | Design | Build before Milestone 1 | Resolution | State |
| --- | --- | --- | --- | --- |
| Firing | One key fires one volley at the selected target in any direction. Instant server resolve, in range means hit. Magazine 3 to 5, reload always ticking, 1.0 s minimum between volleys, refill after 15 s idle. (Mechanics 3.2, Math 3) | Port and starboard arcs, volleys travel and land later, no magazine | Replace `FireBroadside` with `Fire`. Add magazine and reload state to the ship. Keep the volley row only as a client animation event. | Done in Milestone 1 |
| Damage taken | Facing armor: front, sides, back, plus plates, capped at 0.45. Single HP pool. (Math 5) | No armor, four sub-pools, weak point multiplier | Single HP pool with facing armor. Remove weak points and the sails, cannons, and crew pools. | Done in Milestone 1 |
| Ship stats | Twelve stats from hull, cannons, sails, plates, crew, skills. Add then cap, Combat Power budget of 45. `ShipStats` recomputed by `RecomputeStats`. (Math 2) | Flat ship fields and a gold `UpgradeCannon` reducer | Add `ShipStats` and `RecomputeStats`. M1 uses one hull and one cannon tier so the pipeline is real but small. Remove `UpgradeCannon`. | Done in Milestone 1 |
| Ammo | Seven types with damage and reload multipliers and timed effects. (Math 4) | Round, Chain, Grapeshot, Incendiary with per-pool damage | Seed Round, Chain, Fire, Grape in M1 with the doc's multipliers. Frost, Blessed, Heavy arrive with their maps. | Four types done in Milestone 1, three left for Milestone 4 |
| Repair | 3 s channel, heals 20 percent of max HP at the end, cancelled by 15 percent damage or Fire Shot, 15 s cooldown, fatigue 0.6^n, no item cost. Kit is a separate instant item on a 45 s cooldown. (Math 6) | 5 s progressive channel that consumes a repair kit | Replace with the doc's channel. Kit becomes `UseKit`. | Done in Milestone 1 |
| Progression | Map Rank 1 to 10, no character levels. Gold, Diamonds, Honor. (Knowledge 1) | XP and level with three level definitions | Replace `PlayerProgression.Level` and `Experience` with `MapRank`. Gold stays. Diamonds and Honor arrive with their milestones. | Done in Milestone 1 |
| Enemies | Tiers 1 to 6 derived from the base player ship of the map; enemy files store tier and map only. Named enemies with phases. (Math 7, Knowledge 5) | Three archetypes with hand-set stats | Seed the 1/1 list (Skiff, Reef Crab, Fancy, Red Mary) from the derivation table. Sea Dogs flee under 25 percent. Red Mary calls two Fancies at 50 percent. | Done in Milestone 1 |
| Reward split | Damage share on every NPC: `(damage + 2 x healing + 500 x debuffs) / total`, gold is `floor(pool x share)`. (Math 7.4) | 30 percent equal, 70 percent proportional, 5 percent eligibility | Keep encounter and contribution rows. Swap the split rule and update `CONTEXT.md`. | Pending, Milestone 2 |
| Map | Square grid of sectors, 1/1 is 20 x 20 squares with Port Lowell. Exits on edges lead to neighbor maps. (Knowledge 4, Technical 4.1) | 200 x 200 units, chart bands AA to CZ by 0 to 60 | One square is 10 units, so 1/1 keeps the 200 x 200 world and the chunk math. Add `Map` and `Sector` tables. Ruler shows squares 1 to 20. Chart bands go. | Done in Milestone 1 |
| Port | Entering the port circle makes the ship invulnerable, clears effects, opens shops. Leaving takes 3 s. (Mechanics 2) | One harbor object with no rules | Add port radius to `Map`, invulnerability and cast-off channel. Shops arrive in M2. | Done in Milestone 1 |
| Sinking and respawn | 8 s, choose port, fort, or beacon, 10 s spawn shield. (Mechanics 13) | 5 s, respawn at half hull with 5 s invulnerability | Adopt 8 s and 10 s. M1 has one choice, Port Lowell, at full HP. | Done in Milestone 1 |
| Ship collision | Ship to ship collision stops both for 0.5 s unless it is a ram. (Mechanics 2) | Locked decision: ships pass through | Deferred. Rules land with `Ram` in M3 because they exist for ramming. Until then ships pass through. | Pending, Milestone 3 |
| Boarding | Hands and Arms Locker roll `P = clamp(A / (A + D), 0.05, 0.90)`. (Math 5.7) | Crew count comparison under 25 percent hull | Command stays and returns a typed `NotAvailable` rejection until M3 rebuilds it on hands and lockers. | Pending, Milestone 3 |
| Abilities | Come from figureheads, crew, and skills. Cooldown at least 4 x duration. (Math 8.4) | FullSail, Brace, EmergencyPump, SmokeScreen | Removed in M1. `ActivateAbility` returns `NotAvailable` until M2 adds the first tree abilities. | Pending, Milestone 2 |
| Controls | Left click sails or selects. Q or Space fires. Tab nearest. Esc clears. R repair, E board, F ram, 1 to 4 ammo, Z X C V abilities, WASD optional steering, Shift full speed. (Mechanics 1.1) | Q and E broadsides, WASD pans the chart, 1 to 3 aim, 4 to 7 ammo, T clears | Rebind defaults to the doc. Click-to-sail is the only ship control; WASD and middle-mouse drag pan the camera, which stays put until Home or the recenter button re-attaches it. Rebinding stays. | Done in Milestone 1 |
| Screen | Ship centered, minimap, wind arrow, magazine dots, reload bar, repair cooldown, target frame with name and rank, kill feed. (Mechanics 1.2) | Chart camera, minimap, broadside bars, ammo and aim rails, ability rail | Rebuild the HUD around the doc's list. Kill feed and boss counter come with their systems. | Done in Milestone 1 |
| Identity | Better Auth with email, Google, passkeys, 2FA in Phase 1. (Technical 12) | Anonymous local identity, no cloud | Deferred. A private `PlayerAccount` table gets an `AccountId` column now so Better Auth attaches without a schema reset. Accounts and cloud arrive together in M5. | Pending, Milestone 5 |
| Content source | Embedded JSON seeded by `Init`, `StatCaps` row loaded once. (Technical 4.1) | C# constants in `ContentDefinitions.cs` | Move content to JSON under the module, validated by `Init` and by unit tests. `StatCaps` becomes a table. | Done in Milestone 1 |
| Balance tests | Fight length, fight score, ability ratio, ammo, NPC solo safety run in the build and block deploy. (Math 12) | Not present | Land as server unit tests in M1. They run in `pnpm server:test` and therefore in `pnpm ci:fast` pull request checks. | Done in Milestone 1 |
| Chat | None. Ping wheel and markers. (Mechanics) | None | Agreement. Ping wheel arrives with parties in M3. | Pending, Milestone 3 |

## 4. Missing systems by design build order

The Technical doc's build order is the spine of the roadmap. What each phase
still needs, given section 3:

- Phase 1, one map and one fight: **done.** `MapDef`, `Sector`, `HullDef`,
  `CannonDef`, `AmmoDef`, `StatCaps`, `ShipStats`, `RecomputeStats`, magazine
  firing, facing armour, the channelled repair, the repair kit, `Effect` rows,
  port rules, the 1/1 enemies, the balance tests, and the client controls and
  screen are all in. What Phase 1 still owes is not code: a played session and
  the two review passes. See `docs/STATUS.md` section 5.
- Phase 2, progress and safety: `SpawnPoint`, damage-share `OnNpcKilled`,
  missions, charts, Map Rank, Cannons and Armor trees, crew hiring, Harbor
  Protection, flag, duels with fog, `Fight`, `KillRecord`, Combat Rating,
  `IsRelated`, Honor ledger, maps 1/2 and 1/3.
- Phase 3, competition and trust: arena, worker matchmaking over Redis,
  `TrustScore`, phantom NPCs, `DailyEarnings`, `BanWave`, Sails, Repair, and
  Plunder trees, Ship Configs, boarding with hands and lockers, ram and ship
  collision.
- Phase 4, guilds and world: guilds, Renown, islands and towers, war windows,
  Garrison Supply, alliances, maps 2/1 to 5/1 one biome at a time.
- Phase 5, money and operations: Better Auth, Stripe, Diamonds, cosmetics,
  Sea Pass, market, auction, admin writes, replay viewer, cloud deployment.

## 5. Rules that changed when the roadmap was adopted

- `PLAN.md` is replaced by the new roadmap (adopted 2026-09-02). The scope rule "no PvP, parties, chat,
  cloud, bosses, quests, or economy before their phase" stays, with the new
  milestones as the phases.
- `PLAN.md` locked decisions that lapse: ships never collide (M3 adds it),
  XP and levels (M1 removes them), players cannot attack other players
  (M2 adds flagged PvP).
- `PLAN.md` locked decisions that survive: local only until M5, anonymous
  identity until M5, schema replacement may reset local data, only owned
  assets, Apricum as the ship model, no Seafight assets or names.
- `CONTEXT.md` reward and progression sections are rewritten in M2 to the
  damage-share rule and Map Rank.
- The 5,000 client and 1,000 ship capacity proof stops being a single phase.
  Each milestone runs the scale smoke as a gate, and the full proof runs once
  the M1 combat model is in, because that is the model whose cost matters.
