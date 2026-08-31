# Sea core combat and performance roadmap

## Product goal

Build a fast, local-only PvE combat vertical slice using Seafight's navigation
and interaction language while keeping the code, balance, visual identity,
artwork, names, audio, and interface original.

The validation build must prove that sailing and combat feel responsive before
larger MMO systems are introduced. It uses one map, one player ship, twelve
roaming NPC ships, manual broadside combat, tactical recovery, environmental
hazards, loot, death and respawning, and XP progression.

Development remains AI-driven and local-only. Every phase ends with automated
verification, diff review, and one conventional commit. The user's manual
playtest is the final gate after every automated check passes.

## Locked decisions

### Stack and delivery

- Unity `6000.3.23f1` is the macOS and WebGL client.
- SpacetimeDB `2.8.3` is the authoritative backend and persistent game store.
- TanStack Start and TanStack Router provide the local read-only admin panel.
- Docker Compose runs SpacetimeDB, PostgreSQL 18, Redis, MinIO, and admin.
- PostgreSQL, Redis, and MinIO stay outside the authoritative combat path.
- There is no cloud deployment in this roadmap.
- Anonymous local identity remains acceptable.
- Schema replacement may reset local SpacetimeDB data.
- PvP, bosses, playable parties, group UI, quests, economy expansion, and
  additional maps are deferred.

### Navigation and chart controls

| Input | Behavior |
| --- | --- |
| Left-click water | Set or replace the ship's course |
| Right-click water | Stop the course and decelerate |
| Left-click NPC | Select that target |
| WASD | Pan the chart without steering the ship |
| Mouse wheel | Zoom the chart |
| Space | Recenter the chart on the player ship |
| N | Open coordinate navigation |
| Tab / Shift+Tab | Cycle targets forward or backward |
| T | Clear the selected target |
| Escape | Open the menu and disable gameplay input; the world continues |

The map uses columns `A` through `BZ` and rows `0` through `60`. Columns
increase west-to-east and rows increase south-to-north. Coordinate navigation
accepts values such as `AX 59` and sails to the selected cell center.

Ship motion is server-authoritative at 10 Hz and presented at 60 FPS. Ships
have heading, speed, acceleration, deceleration, turn rate, stopping distance,
collision-aware course following, wind response, and current response.

### Combat controls

| Input | Behavior |
| --- | --- |
| Q | Fire port broadside |
| E | Fire starboard broadside |
| 1 / 2 / 3 | Aim at Hull / Sails / Cannons |
| 4 / 5 / 6 / 7 | Select Round / Chain / Grapeshot / Incendiary ammunition |
| Z | Full Sail |
| X | Brace |
| C | Emergency Pump |
| V | Smoke Screen |
| R | Start or cancel repair |
| B | Start or cancel boarding |

All controls use Unity's Input System and are rebindable.

The player selects a target and weak point, then manually chooses the firing
side. Port and starboard have independent 100-degree firing arcs and reload
timers. A legal shot creates one server-authoritative aggregate volley. Damage
resolves only when the volley reaches the target, and a legally fired volley
cannot miss. It becomes harmless only if the target has already sunk.

The client presents each volley with pooled cannonballs, staggered muzzle
flashes, smoke, recoil, water trails, impacts, and spatial audio.

### Ammunition and weak points

- Round shot deals balanced hull damage and can cause flooding.
- Chain shot deals heavy sail damage and can slow a ship.
- Grapeshot deals short-range crew and boarding-protection damage.
- Incendiary shot deals lower impact damage and applies burning.
- Hull reaching zero sinks the ship.
- Sail damage reduces acceleration, maximum speed, and turn rate; zero sail
  health disables acceleration.
- Cannon damage increases reload time and reduces volley damage; zero cannon
  health disables firing until repaired.

### Tactical systems

- Full Sail increases speed and acceleration by 35% for five seconds.
- Brace reduces incoming damage by 40% for four seconds.
- Emergency Pump removes flooding and restores hull gradually.
- Smoke Screen prevents new long-range locks for four seconds; existing
  volleys still land.
- Burning, flooding, slowed, and disabled-sails effects have server-owned
  stacks, durations, expiry, and immunity windows.
- Repair consumes one kit, disables firing and boarding, caps movement at 50%,
  and restores hull and subsystems progressively for five seconds. Incoming
  damage interrupts it.
- Boarding requires the target below 25% hull, close range, and an
  uninterrupted three-second channel. Resolution compares boarding power with
  remaining crew protection.
- Successful boarding grants bonus loot. Failure starts a cooldown and
  temporarily reduces boarding power.

### World and progression

- Deterministic global wind modifies speed by heading.
- Current zones add directional velocity.
- Moving storms reduce turning and weapon effectiveness and periodically deal
  damage.
- Reefs block courses; shoals slow ships and can cause flooding.
- NPC deaths create floating crates collected by sailing through their pickup
  radius. Each crate can be claimed exactly once.
- A sunk player respawns after five seconds at a random safe navigable
  coordinate with 50% hull and five seconds of invulnerability.
- The HUD shows player HP and XP, target hull/sails/cannons, statuses, reloads,
  ammunition, abilities, coordinates, and repair or boarding progress.
- XP comes from combat contribution, kills, boarding, and configured loot.
- Four patrol ships are neutral until attacked.
- Four raiders close distance and attack sails and crew.
- Four gunships maintain broadside range and use incendiary ammunition.
- NPC decisions run at 2 Hz while movement runs at 10 Hz.
- Sunk NPCs respawn after 30 seconds at deterministic valid positions.

## Architecture contracts

- Use one indexed authoritative ship model for players and NPCs, supported by
  focused tables for ownership, AI, inventories, effects, volleys, loot,
  cooldowns, and contributions.
- Index active, moving, engaged, chunk, owner, and target state. Scheduled
  reducers process active rows only and never scan persisted offline players.
- `ClientConnected` may update an existing player but must never create one.
  Only `LoadPlayer` creates game state.
- Clients subscribe only to their player state, nearby chunks, nearby ships,
  active volleys, nearby loot, and HUD data.
- Transient combat events expire by tick and cannot grow without bounds.
- Balance, NPC, ammunition, ability, and map definitions are validated,
  version-controlled content.
- The server alone owns movement, collision, firing validation, ammunition,
  impacts, damage, effects, repairs, boarding, loot, XP, death, respawning, and
  rewards.

Public intent reducers:

- `LoadPlayer`
- `SetCourse` and `StopCourse`
- `SelectTarget` and `ClearTarget`
- `SetAmmo`
- `FireBroadside`
- `StartRepair` and `CancelRepair`
- `StartBoarding` and `CancelBoarding`
- `ActivateAbility`

Contribution records support damage, boarding, and future support credit.
Shared rewards reserve 30% for equal eligible participation and distribute 70%
by contribution, with a 5% eligibility threshold. Group management and
playable multiplayer remain deferred.

## Phases

### Phase 0: replace the project plan

- Replace the previous passive-combat plan with this roadmap.
- Review scope, exclusions, performance gates, and commit boundaries.

Commit: `docs(plan): define core combat roadmap`

### Phase 1: eliminate the runtime resource failure

- Prevent anonymous admin and SQL connections from creating players.
- Add a lightweight admin health endpoint that never loads dashboard data.
- Run Docker admin as a production build; keep Vite development host-only.
- Reset and reseed contaminated local SpacetimeDB data.
- Make macOS development builds windowed, resizable, 1280x720 by default,
  foreground-capped at 60 FPS, and background-capped at 15 FPS.
- Add identity-leak, idle-resource, connection, and window regression tests.

Acceptance:

- Repeated health checks create zero identities or ships.
- Idle scheduled reducers finish inside their tick interval.
- The complete local stack does not saturate Docker CPU.
- The visible game cannot open as forced borderless fullscreen.

Commit: `fix(runtime): eliminate local resource runaway`

### Phase 2: establish scalable world and combat state

- Introduce unified ship contracts, indexed active access, bounded events,
  spatial chunks, and content definitions.
- Move simulation to 10 Hz with 60 FPS client presentation.
- Replace full-table scans and `SubscribeToAllTables`.
- Regenerate Unity and TypeScript bindings.
- Reset local data as the explicit development migration.

Commit: `refactor(world): establish scalable combat state`

### Phase 3: build chart navigation and sailing physics

- Add A-BZ/0-60 coordinate conversion and navigation.
- Implement course replacement, stopping, acceleration, turning, heading,
  collision avoidance, and deterministic safe spawning.
- Add WASD camera panning, constrained zoom, recentering, and coordinate HUD.
- Add wind and current foundations.

Commit: `feat(sailing): add chart navigation and ship handling`

### Phase 4: replace prototype input and HUD

- Add Unity Input System Gameplay and Menu action maps.
- Replace immediate-mode GUI with the combat HUD and pause/settings menu.
- Add HP/XP bars, subsystem bars, hotbar, ammunition, cooldowns, coordinates,
  and progress channels.
- Disable gameplay actions while menus are open without pausing the world.

Commit: `feat(client): add combat controls and HUD`

### Phase 5: implement manual broadside combat

- Add target selection, weak-point choice, firing arcs, side-specific reloads,
  ammunition inventory, and guaranteed-hit traveling volleys.
- Add all four ammunition types and authoritative impact resolution.
- Add pooled broadside, projectile, splash, impact, recoil, and audio feedback.
- Remove automatic engagement and automatic fire.

Commit: `feat(combat): add manual broadside volleys`

### Phase 6: add tactical damage and recovery

- Add subsystem damage and all four status effects.
- Add Full Sail, Brace, Emergency Pump, and Smoke Screen.
- Add channelled repair and boarding.
- Activate storms, currents, reefs, and shoals as gameplay hazards.

Commit: `feat(combat): add tactical damage and recovery`

### Phase 7: add roaming NPC combat, loot, death, and XP

- Seed four patrols, four raiders, and four gunships.
- Add deterministic roaming, aggro, broadside positioning, weak-point choice,
  retreat, repair, and NPC respawning.
- Add sail-over loot, exactly-once claims, XP, sinking, safe random respawn,
  and temporary protection.
- NPCs obey the same movement, arcs, ammunition, effects, and hazards as the
  player.

Commit: `feat(world): add roaming combat and progression`

### Phase 8: lock group reward contracts

- Add contribution accounting and shared reward calculations without playable
  groups.
- Cover disconnects, late joins, eligibility, boarding credit, duplicates, and
  rounding.
- Preserve interfaces for future party membership and multiplayer clients.

Commit: `feat(combat): add shared reward contracts`

### Phase 9: performance hardening and soak testing

- Pool ships, volleys, impacts, health bars, statuses, and loot visuals.
- Remove steady-state per-frame allocations.
- Add interest-based subscriptions and chunk transitions.
- Profile server reducers, Unity frame time, rendering, memory, garbage
  collection, and Docker idle load.
- Add automated 15-minute sailing, combat, and respawn soak scenarios.

Performance gates:

- Stable 60 FPS at 1920x1080 with 100 visible ships and combat effects on the
  M1 Pro.
- Frame-time p95 is at most 16.7 ms.
- No sustained gameplay allocations after warm-up.
- Server tick p95 is below 10 ms with 1,000 dormant and 100 active ships.
- Identity count stays constant through health checks and admin refreshes.
- Aggregate local container CPU averages below 25% after warm-up.
- Runtime memory grows by less than 5% during the soak test.
- Local firing feedback and authoritative acknowledgement arrive within 150
  ms.

Commit: `perf(runtime): enforce combat performance budgets`

### Phase 10: complete verification and manual validation

Automated gates run first:

- Server unit, property, reducer integration, deterministic AI, and reward
  tests.
- Unity EditMode and PlayMode tests.
- macOS runtime scenarios.
- WebGL browser smoke scenarios.
- Full-stack health and identity-leak checks.
- Performance and soak suites.
- WebGL and macOS production builds.
- Canonical `pnpm verify` from a clean state.

Only after every automated gate passes:

- Launch the complete local stack.
- Launch the macOS player in a safe window.
- The user performs a 10-15 minute playtest covering sailing, broadside
  positioning, ammunition, abilities, repairs, boarding, effects, hazards,
  loot, death, respawn, and XP.
- Record the go/no-go result.

Commit: `docs(validation): record core combat playtest`

## Test policy

Tests exercise public rules, reducers, generated contracts, Unity inputs and
presentation, local health endpoints, and built-player behavior. They do not
assert private implementation details.

Required scenarios include:

- Coordinate boundaries, invalid labels, and cell-center conversion.
- Safe spawn positions outside islands, storms, ships, and hazards.
- Course changes, stops, collision, acceleration, turning, and camera
  independence.
- Arc boundaries, wrong-side fire, reloads, empty ammunition, disabled
  cannons, and dead targets.
- Guaranteed impacts and a target sinking before impact.
- Every ammunition and weak-point combination.
- Effect stacking, expiry, immunity, repair, and ability interactions.
- Interrupted repairs and boarding, range loss, consumption, success, and
  failure.
- Loot contention and exactly-once rewards.
- Death during channels or volleys, safe respawn, and invulnerability expiry.
- Deterministic NPC behavior under fixed seeds.
- Menu input blocking while the authoritative world advances.
- Contribution eligibility, reward splits, duplicates, and rounding.
- No player creation from SQL, admin, health, or non-game connections.

Before each phase commit:

1. Demonstrate the phase's red regression gate.
2. Implement vertically until the gate is green.
3. Run phase-specific and repository-wide checks.
4. Review the diff for unrelated changes, generated drift, secrets, and debug
   instrumentation.
5. Commit only the completed phase with a conventional commit message.

## Reference research

- [Seafight controls](https://board-en.seafight.com/threads/options-overview.1858/)
- [Seafight sea chart and HP/EP](https://board-en.seafight.com/threads/sea-chart-overview.172255/)
- [Seafight boarding](https://board-en.seafight.com/threads/boarding.1044/)
- [Seafight ammunition](https://board-en.seafight.com/threads/ammunition.9856/)
- [Seafight sail-over loot](https://board-en.seafight.com/threads/glitters.1969/)
- [Unity Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest)
- [SpacetimeDB documentation](https://spacetimedb.com/docs/)
