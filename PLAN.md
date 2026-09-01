# Sea scalability and PvE completion plan

## Product goal

Build a fast, local-only PvE combat vertical slice with shared multiplayer,
original presentation, and enough measured capacity to support the next MMO
features without rewriting the core game.

The finished build has one SpacetimeDB world, four real local clients, twelve
roaming NPCs, manual broadside combat, tactical recovery, hazards, loot, death,
respawning, XP, and shared rewards. It must also prove 5,000 connected players
and 1,000 active ships distributed across one map.

Development stays local. Every phase ends with automated verification, diff
review, and one conventional commit. The user's manual playtest remains the
last gate.

## Completed foundation

Phases 0 through 6 are complete at base commit `a5e1670`.

| Phase | Result | Commit |
| --- | --- | --- |
| 0 | Core combat roadmap | `e2fd19b` |
| 1 | Local resource runaway fixes | `697f18a` |
| 2 | Unified world and combat state | `704ba22` |
| 3 | Chart navigation and ship handling | `3d92909` |
| 3 follow-up | Apricum sailing and chart polish | `0097b3b` |
| 4 | Combat controls and HUD | `a156db5` |
| 5 | Manual broadside volleys | `c6dcbe1` |
| 6 | Tactical damage and recovery | `a5e1670` |

## Locked decisions

### Scope

- Unity `6000.3.23f1` remains the macOS and WebGL client.
- SpacetimeDB remains the authoritative backend and game store.
- TanStack Start and TanStack Router provide the local read-only admin panel.
- Docker Compose runs SpacetimeDB, PostgreSQL 18, Redis, MinIO, and admin.
- PostgreSQL, Redis, and MinIO stay outside the authoritative combat path.
- There is no cloud deployment in this roadmap.
- Anonymous local identity remains acceptable.
- Schema replacement may reset local SpacetimeDB data.
- PvP, parties, party UI, chat, accounts, bosses, quests, guilds, additional
  maps, and economy expansion remain deferred.
- Four real local clients share PvE. Synthetic clients prove larger capacity.
- Players can inspect other player ships but cannot attack or board them.
- Ships do not collide or block one another. Islands and reefs still block
  movement.
- Apricum and other user-provided models are the only imported game art.
- Original procedural materials and effects may fill missing presentation
  roles.

### Capacity and presentation

- The final local load test connects 5,000 loaded players.
- It keeps 4,000 ships dormant and distributes 1,000 active ships across the
  map.
- The macOS client holds 60 FPS at 1920x1080 with 250 visible ships.
- The WebGL client holds 60 FPS at 1920x1080 with 100 visible ships.
- Long load, mutation, and 15-minute soak tests run in Phase 18 and final
  verification. Earlier phases run normal verification and short performance
  smoke tests.

### Existing gameplay

- Left-click water sets or replaces the ship's course.
- Right-click water stops the course and decelerates.
- Left-click NPC selects that target.
- WASD pans the chart without steering the ship.
- Mouse wheel zooms the chart. Space recenters on the player.
- Minimap clicks move only the chart camera.
- The map uses Y-axis bands `AA` through `CZ` and X-axis numbers `0` through
  `60`.
- Ship motion is authoritative at 10 Hz and presented at 60 FPS.
- Q and E fire manual port and starboard broadsides.
- Ammunition, weak points, abilities, repair, boarding, statuses, hazards,
  fog, coordinates, and the combat HUD keep their current rules unless a phase
  below changes them explicitly.

## Public contracts and architecture

### Command model

Keep `LoadPlayer` and progression reducers separate. Replace individual
gameplay reducers with:

```csharp
IssueShipCommand(CommandEnvelope envelope)
```

`CommandEnvelope` contains a monotonically increasing `CommandId` scoped to the
player identity and one `ShipCommand` SpacetimeDB tagged union.

`ShipCommand` variants:

- `SetCourse`
- `StopCourse`
- `SelectTarget`
- `ClearTarget`
- `SetAmmo`
- `FireBroadside`
- `ActivateAbility`
- `StartRepair`
- `StartBoarding`
- `CancelChannel`

Remove old public gameplay reducers after Unity and generated TypeScript code
move to `IssueShipCommand`. Remove the legacy `MoveTo` alias.

Authoritative ship modes:

- `Operational`
- `Repairing`
- `Boarding`
- `Sunk`

Movement, targeting, ammunition, cooldowns, subsystem health, and statuses stay
separate from ship mode. Firing is a command, not a mode.

Rules by mode:

- Operational ships may use all valid commands.
- Repairing ships may move at reduced speed, stop, select or clear a target,
  and cancel the channel. They cannot fire, board, or activate abilities.
- Boarding ships may move, stop, select or clear a target, and cancel the
  channel. They cannot fire, repair, or activate abilities.
- Sunk ships cannot issue gameplay commands.
- Damage interrupts repair.
- Boarding failure, range loss, cancellation, or completion returns the ship
  to `Operational`.
- Respawning returns a sunk ship to `Operational` and adds temporary
  invulnerability.
- NPC AI uses the same command policy and effect executor as players.

The pure command policy returns a `CommandDecision` with acceptance, a stable
rejection code, the next ship mode, and reducer effects. Expected gameplay
rejection records a command result without changing gameplay state. Only
corrupt state and broken invariants throw exceptions.

Add:

- `PlayerCommandState` with the last processed command ID and result.
- `CommandResultEvent` as a SpacetimeDB event table.
- Owner-filtered access to command results.
- Duplicate handling that returns the stored result without applying effects
  twice.
- Stale command rejection for IDs older than the stored ID.

### Server simulation

Keep the 10 Hz simulation tick. Each system processes only indexed work that is
active and due.

Add indexes for:

- Active and moving ships by chunk.
- Status by ship and type.
- Status work by active state and next process tick.
- Volleys by active state and impact tick.
- Channels by active state and next process tick.
- Loot by active state and expiry tick.
- NPC decisions by active state and next decision tick.
- Respawns by state and respawn tick.
- Cooldowns by ship and type.

Use integer codes for hot-path status, ability, ammunition, weak-point, faction,
and mode values. Keep display names and balance values in content definition
tables.

Remove:

- Ship-to-ship movement blocking.
- Full active-ship scans from movement.
- Full world-object scans from each ship update.
- Full active-row scans for work that is not due.
- Persisted transient combat events and their cleanup scan.
- String comparisons inside movement, damage, status, and cooldown loops.

Query islands, reefs, storms, shoals, currents, ships, volleys, and loot by
chunk. Calculate island detours when a course changes. Apply all systems to one
in-memory ship result and write the ship row once per tick.

Use SpacetimeDB event tables for short-lived command and presentation events.
Use indexed regular tables for state that must survive reconnects.

### Unity client

Split Unity into assembly definitions for client models, networking, input,
presentation, UI, Editor tools, EditMode tests, PlayMode tests, and performance
tests.

Use VContainer with one application lifetime scope. Plain C# services use
constructor injection. MonoBehaviours remain thin scene and rendering adapters.
Runtime code must not use `FindFirstObjectByType`.

SpacetimeDB insert, update, delete, and event callbacks maintain client state.
Per-frame work is limited to visible transform interpolation, active effects,
camera movement, and input sampling. Runtime `Update` methods must not scan
subscribed tables.

Use:

- Direct package references for Burst, Collections, Mathematics, Performance
  Testing, Addressables, Memory Profiler, Profile Analyzer, and Code Coverage.
- Burst jobs for plain-data interpolation and visibility calculations.
- Pools for ships, health bars, target rings, cannonballs, smoke, impacts,
  status icons, and loot.
- Shared materials and `MaterialPropertyBlock`.
- LOD and distance culling.
- Dirty-state HUD updates.
- Profiler markers around networking, interpolation, presentation, HUD,
  minimap, and effects.

The macOS client renders at most 250 ship presentations. WebGL renders at most
100. Targeted ships and active volley endpoints remain visible while relevant.

### Version policy

At the start of Phase 8:

- Resolve the newest stable versions compatible with Unity `6000.3.23f1` and
  the local macOS SpacetimeDB toolchain.
- Do not use preview packages when a compatible stable release exists.
- Pin exact NuGet, npm, Unity, Docker, and tool versions.
- Pin Docker images by immutable digest after selecting a stable tag.
- Use one SpacetimeDB release for the image, CLI, C# runtime, Unity SDK, and
  TypeScript SDK.
- Keep the SpacetimeDB module on .NET 8 for this roadmap.
- Use .NET 10 inside Docker for load tests.
- Remove `latest`, `2.*`, and other floating production references.
- Commit generated bindings and lockfile changes with their owning phase.

Phase 8 dependency note: current TanStack Start Node hosting requires Nitro 3,
which has no stable release. Nitro `3.0.260610-beta` is the only preview
exception. It is pinned exactly and covered by the production-build exit test.
Addressables and Scriptable Build Pipeline are pinned to immutable commits from
their package mirrors because unattended Unity registry downloads required an
interactive Hub sign-in on this machine.

## Implementation phases

### Phase 7: replace the roadmap

Status: complete.

- Replace the previous unfinished roadmap with this plan.
- Record completed Phases 0 through 6 and their commits.
- Record scale, rendering, multiplayer, collision, test, dependency, and asset
  decisions.
- Review only. Do not change gameplay code.

Acceptance:

- `PLAN.md` matches the repository and commit history.
- Every unfinished phase has one commit and acceptance gate.
- The worktree contains only the plan change.

Commit: `docs(plan): define scalable combat completion roadmap`

### Phase 8: pin the toolchain and add quality tools

Status: complete.

- Align all SpacetimeDB components to one stable release.
- Pin PostgreSQL 18, Redis, MinIO, Node, pnpm, .NET SDK, and Docker images.
- Add direct Unity references for Burst, Collections, Mathematics, Performance
  Testing, Addressables, Memory Profiler, Profile Analyzer, and Code Coverage.
- Add VContainer with an exact package tag.
- Add FsCheck.Xunit and Coverlet to server tests.
- Add separate BenchmarkDotNet and NBomber projects under test tooling.
- Add Stryker.NET and ReportGenerator through a local .NET tool manifest.
- Add analyzers, nullable checks, warnings-as-errors, format verification,
  binding drift checks, and handwritten file-size checks.
- Reject handwritten production and test C# files above 500 lines. Exclude
  generated and imported package code.
- Define `pnpm verify` as the normal phase gate.
- Define `pnpm verify:full` as the final heavy gate.

Acceptance:

- No floating dependency or Docker image remains.
- A clean checkout resolves the same versions.
- Existing gameplay and tests stay green.
- `pnpm verify` does not rewrite tracked files.

Commit: `build(tooling): pin performance and test dependencies`

### Phase 9: split server and Unity responsibilities

Status: complete.

- Split the SpacetimeDB partial module into schema, reducers, simulation,
  content, commands, rewards, and event files.
- Keep pure rules in the module source tree. Link them into test and benchmark
  projects without SpacetimeDB runtime types.
- Split sailing, navigation, spawn, tactical, combat, and content rules into
  focused files.
- Split the large Unity Editor test file by subsystem.
- Add Unity assembly definitions and dependency rules.
- Add a VContainer application lifetime scope.
- Replace scene searches with injected services or explicit scene adapters.
- Keep behavior unchanged.

Acceptance:

- No handwritten C# file exceeds 500 lines.
- Pure rules compile without SpacetimeDB or Unity dependencies.
- Unity assemblies have no circular references.
- Current tests, builds, and runtime smoke checks pass unchanged.

Commit: `refactor(architecture): split game runtime boundaries`

### Phase 10: build the full test foundation

Status: complete.

- Add an isolated SpacetimeDB integration runner that publishes the real
  module, creates identities, invokes reducers, and reads committed rows.
- Give every integration test its own database or reset boundary.
- Add FsCheck generators for ships, commands, targets, statuses, ammunition,
  coordinates, and tick sequences.
- Add deterministic replay. A seed and command log must produce the same state
  hash.
- Add Unity PlayMode setup for the real scene, input, camera, UI, object
  creation, and teardown.
- Add Unity performance fixtures with warm-up, measurement, profiler markers,
  and GC collection.
- Add BenchmarkDotNet baselines for navigation, commands, spatial lookup,
  statuses, and rewards.
- Add an NBomber adapter that connects through the real SpacetimeDB C# SDK.
- Add coverage reports that exclude generated bindings, imported SDK code, and
  generated scenes.

Acceptance:

- Integration tests catch a deliberately rejected reducer call.
- Property failures report the seed and smallest failing input.
- Replay tests detect state differences.
- PlayMode and performance tests run from scripts without manual Unity work.
- Existing test cases remain present after file splitting.

Commit: `test(runtime): add reducer and property harnesses`

### Phase 11: centralize authoritative ship commands

Status: complete.

- Add `CommandEnvelope`, `ShipCommand`, `ShipMode`, `CommandDecision`, and typed
  rejection codes.
- Implement one allocation-free command transition table.
- Add one executor for accepted command effects.
- Send player and NPC actions through the same executor.
- Add `PlayerCommandState` and `CommandResultEvent`.
- Add duplicate and stale command handling.
- Replace expected reducer exceptions with command rejections.
- Move Unity to monotonic command IDs and authoritative acknowledgements.
- Remove optimistic success messages.
- Regenerate Unity and TypeScript bindings.
- Reset local SpacetimeDB data.

Required tests:

- Every command against every ship mode.
- Duplicate and stale command IDs.
- Repair and boarding cancellation.
- Damage interruption and sunk-ship rejection.
- Invalid targets, blocked courses, reloads, empty ammo, disabled cannons,
  cooldowns, and missing resources.
- No state change after rejection.
- Exactly one application after a retry.

Acceptance:

- Gameplay reducers do not duplicate mode validation.
- Expected rejection never reaches the unhandled reducer error callback.
- The client reports success only after server acceptance.
- NPC and player commands return the same decision from the same snapshot.

Commit: `refactor(combat): centralize authoritative ship commands`

### Phase 12: make the server simulation scale by indexed work

- Remove all ship-to-ship collision checks.
- Add due-tick and spatial indexes.
- Convert transient combat messages to event tables.
- Process only moving ships and due statuses, channels, volleys, NPC decisions,
  respawns, and loot expiry.
- Query hazards and currents from nearby chunks.
- Calculate navigation blockers when a course changes.
- Aggregate ship changes before one row update.
- Remove string comparisons from hot simulation code.
- Keep dormant ships out of movement, AI, hazard, and combat work.
- Add sampled timing output for the load runner.

Required tests:

- Ships pass through each other without blocking.
- Islands and reefs still block movement.
- Due rows run on the exact tick and non-due rows stay untouched.
- Status, channel, volley, loot, and respawn work runs once.
- Dormant and offline ships are not processed by active systems.
- Fixed-seed output remains deterministic.

Acceptance:

- A short 100-client and 100-active-ship run has no tick overruns.
- Server tick p95 stays below 10 ms in that run.
- Movement has no full active-ship scan inside its per-ship work.
- Regular transient event cleanup no longer exists.

Commit: `perf(server): index active simulation work`

### Phase 13: add stable shared-world subscriptions

- Replace broad subscriptions with owner, spatial, target, volley, loot,
  world-object, and HUD queries.
- Move world objects into spatial subscriptions.
- Add subscription generation IDs so old callbacks cannot replace new state.
- Add chunk hysteresis and a short debounce.
- Keep selected targets and active volley endpoints subscribed outside the
  nearby area.
- Remove stale Unity views through delete callbacks.
- Add four local profiles and scripts for one macOS client plus three WebGL or
  headless clients.
- Show player ships to each other.
- Exclude players from hostile target cycling.
- Reject firing and boarding against players.
- Keep health and admin checks unable to create identities or ships.

Required tests:

- Fast chunk crossings and out-of-order subscription callbacks.
- A target crossing an area boundary during a volley.
- Disconnect and reconnect during repair, boarding, death, and respawn.
- Four clients seeing the same ships and world objects.
- No duplicate view after subscription replacement.
- No rows outside declared interest queries.

Acceptance:

- Four real local clients connect and move in one world.
- Clients see each other inside interest range.
- Clients do not receive all ships or all world objects.
- Subscription churn does not duplicate or lose the player ship.

Commit: `feat(networking): add stable shared-world interest`

### Phase 14: make Unity presentation event-driven

- Replace per-frame table scans with row callback registries.
- Keep transform interpolation, active effects, camera, and input in per-frame
  loops.
- Pool ships, health bars, target rings, status icons, loot, cannonballs,
  smoke, splashes, and impacts.
- Use shared materials and `MaterialPropertyBlock`.
- Cache pooled component references.
- Update the HUD only when source rows or command results change.
- Run interpolation and visibility through Burst-compatible plain data.
- Cap visible ships at 250 on macOS and 100 on WebGL.
- Add near, medium, distant, and hidden presentation levels.
- Add profiler markers and allocation checks.

Required tests:

- Insert, update, delete, unsubscribe, and resubscribe lifecycle.
- Pool exhaustion and growth limits.
- Reuse without stale health, target, status, or material state.
- Dirty-state HUD updates.
- No table iteration from `Update`.
- Stable interpolation across uneven 10 Hz updates.
- Fog and camera movement do not reveal unsubscribed entities.

Acceptance:

- Owned client code allocates zero bytes per idle frame after warm-up.
- Pooled combat presentation allocates nothing after warm-up.
- A 100-ship macOS smoke test stays at 60 FPS.
- Draw calls and material instances remain bounded.

Commit: `perf(client): make Unity presentation event driven`

### Phase 15: finish roaming combat and progression

- Seed four patrols, four raiders, and four gunships.
- Run NPC decisions at 2 Hz through indexed due work.
- Add deterministic roaming, aggro, range control, weak-point and ammunition
  choice, retreat, repair, and respawn behavior.
- Send NPC actions through the player command policy.
- Add sail-over loot with atomic exactly-once claims.
- Add player and NPC sinking.
- Respawn players after five seconds at a safe position with 50 percent hull
  and five seconds of invulnerability.
- Respawn NPCs after 30 seconds.
- Add XP from contribution, kills, boarding, and configured loot.
- Add data-driven level thresholds and HUD updates.

Required tests:

- Fixed-seed decisions and movement.
- Patrol, raider, and gunship behavior.
- NPC arcs, reloads, ammunition, statuses, repairs, and hazards.
- Loot contention between clients.
- Death during channels and in-flight volleys.
- Safe respawn and invulnerability expiry.
- XP boundaries, level changes, and duplicate prevention.

Acceptance:

- Twelve NPCs roam and fight without manual setup.
- NPC behavior replays from the same seed.
- Four clients can damage and loot one NPC without duplicate rewards.
- Gameplay remains server-owned.

Commit: `feat(world): add roaming combat and progression`

### Phase 16: add shared PvE rewards

- Track damage, boarding, and future support contribution per encounter.
- Mark players eligible at 5 percent contribution.
- Reserve 30 percent for equal distribution among eligible players.
- Distribute 70 percent by contribution.
- Use deterministic integer rounding. Assign any remainder by contribution
  rank, then entity ID.
- Close encounters once and make settlement idempotent.
- Support disconnects, reconnects, late joins, death, boarding, and NPC
  respawn.
- Add reward feedback to each eligible client's HUD.
- Do not add parties, party UI, chat, or PvP.

Required tests:

- One, two, four, and many contributors.
- Exact and below-threshold contribution.
- Equal contribution and rounding ties.
- Disconnected, dead, and late players.
- Boarding credit and duplicate settlement.
- NPC respawn starting a new encounter.

Acceptance:

- Four real clients receive the expected split.
- Reward totals match the configured pool.
- Replaying settlement cannot grant a second reward.

Commit: `feat(combat): add shared reward contracts`

### Phase 17: finish the owned-asset presentation

- Put Apricum and other user-provided ships behind Addressables.
- Validate FBX scale, forward axis, pivot, texture import, materials, and bounds.
- Use Apricum for the player and owned material variants for NPCs until more
  models arrive.
- Add near and medium LODs plus a distant silhouette.
- Add replaceable Addressable slots for ships, islands, reefs, harbors, loot,
  projectiles, impacts, UI icons, and audio.
- Improve water, shoreline depth, island visibility, wakes, contact shadows,
  broadsides, impacts, storms, and fog with original materials and effects.
- Clean HUD spacing, readability, reload feedback, command results, target
  state, and shared rewards.
- Fail automated checks for missing textures, pink materials, wrong
  orientation, and broken Addressables.
- Do not import Seafight SWFs, copied assets, or unlicensed content.

Acceptance:

- Apricum keeps its colors and textures in macOS and WebGL.
- Every ship faces its movement direction.
- LOD changes do not create orientation or scale jumps.
- Build logs contain no missing asset or shader errors.
- The scene uses owned assets only.

Commit: `feat(client): add scalable owned-asset presentation`

### Phase 18: run full performance hardening

- Run BenchmarkDotNet and optimize measured server algorithms.
- Run NBomber with 5,000 loaded and connected clients.
- Keep 4,000 ships dormant and distribute 1,000 active ships across the map.
- Run four real local clients in shared PvE.
- Run 15-minute server and client soaks.
- Run macOS and WebGL performance tests.
- Run coverage, mutation, replay, identity, Docker, production build, and
  binding drift checks.
- Fix failed gates inside this phase, then repeat the full suite.
- Keep raw reports in ignored build output. Commit stable summaries only.

Final gates:

- At least 99.9 percent of 5,000 clients remain connected.
- 1,000 active ships remain distributed and simulated at 10 Hz.
- Server tick p95 is at most 10 ms and p99 is at most 20 ms.
- Command acknowledgement p95 is at most 150 ms and p99 at most 250 ms.
- Server and load runner stay below 85 percent sustained CPU each.
- Memory grows by less than 5 percent after warm-up.
- Dormant ships create no movement or AI work.
- Health and admin refreshes create zero identities and ships.
- macOS frame-time p95 is at most 16.7 ms with 250 visible ships.
- WebGL frame-time p95 is at most 16.7 ms with 100 visible ships.
- Client frame-time p99 is at most 25 ms.
- Owned client code allocates zero bytes per idle frame after warm-up.
- Pools do not grow after warm-up capacity.
- Runtime errors, unhandled reducer errors, missing assets, and duplicate
  rewards remain zero.
- Pure domain line coverage is at least 95 percent and branch coverage at
  least 90 percent.
- Command policy mutation score is at least 90 percent. Authorization,
  resources, death, rewards, and idempotency have no surviving mutation.
- `pnpm verify:full` passes from a clean checkout.

Commit: `perf(runtime): prove local multiplayer budgets`

### Phase 19: complete final validation

Run automated gates first:

- `pnpm verify`
- `pnpm verify:full`
- Clean macOS and WebGL production builds.
- Four-client shared PvE smoke test.
- Final Docker health and identity audit.

Only after every automated gate passes:

- Launch the complete local Docker stack.
- Launch the macOS game in a safe window.
- Launch the other local clients needed for shared PvE.
- The user performs a 10 to 15 minute playtest.
- Cover sailing, camera, minimap, island navigation, targeting, broadsides,
  ammunition, abilities, repair, boarding, statuses, hazards, NPC roles, loot,
  death, respawn, XP, and shared rewards.
- Record each finding as pass, fail, or deferred.
- Fix failures in their owning code phase before recording a go result.

Commit: `docs(validation): record core combat playtest`

## Test policy

Before every phase commit:

1. Add or update the regression test first.
2. Confirm the new test fails for the expected reason.
3. Implement the phase.
4. Run phase-specific tests.
5. Run `pnpm verify`.
6. Review the diff for unrelated changes, generated drift, secrets, debug code,
   and asset licensing.
7. Commit only when the phase is complete.

The suite covers:

- Every ship mode and command combination.
- Command retries, stale IDs, rejection, acknowledgement, and reconnect.
- Coordinates, courses, island detours, acceleration, turning, stopping, wind,
  and currents.
- Arc boundaries, ammunition, weak points, reloads, volleys, and dead targets.
- Statuses, abilities, repair, boarding, interruption, and cancellation.
- NPC decisions, aggro, positioning, retreat, repair, sinking, and respawn.
- Loot, XP, contribution, settlement, and duplicate prevention.
- Multi-client subscriptions, chunk changes, stale callbacks, targets, and
  volley endpoints.
- Unity pooling, interpolation, HUD state, camera, fog, input, assets, and scene
  lifecycle.
- Server tick time, command latency, connections, frame time, memory,
  allocations, draw calls, and Docker resources.
- macOS and WebGL production builds.
- No player creation from health, admin, SQL, or non-game connections.
