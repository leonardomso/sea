# Sea game project plan

## Project intent

Build an original browser game inspired by the broad gameplay loop of Seafight:

- Explore a top-down tropical sea.
- Select and engage computer-controlled enemies.
- Fire automatically after engagement when the target is in range and the cannon cooldown is ready.
- Earn resources.
- Improve one player ship.

The first objective is validation. We want to prove that the core loop is enjoyable, that the local architecture works, and that the project can grow without replacing its foundation.

The game may share genre mechanics with Seafight, but it must use original branding, maps, writing, artwork, audio, and other creative content.

## Current decisions

### Product and validation

- The eventual goal is a serious product that could launch publicly.
- The current work is a validation project because the team is new to game development.
- Development is AI-driven. AI may create code, infrastructure, tests, documentation, and commits.
- The user reviews each phase and runs the final manual playtest.
- Manual gameplay testing is the last step of the complete plan. Intermediate phase gates use automated checks.
- The validation build must prove:
  - The core loop is playable for 10–15 minutes.
  - The full local stack works.
  - WebGL and macOS builds work.
  - The architecture supports future multiplayer and progression.
- Final visual similarity to Seafight is not an early go/no-go requirement.

### Client

- Unity 6.3 LTS, using the newest compatible patch available when the project is created.
- Unity is the primary client for WebGL and macOS desktop builds.
- The world uses an orthographic 2.5D presentation with 3D ships and islands, 2D ocean effects, and a fixed top-down camera.
- Use standard Unity GameObjects and MonoBehaviours, organized behind separate domain and gameplay systems.
- Support WebGL and macOS first. Add Windows later.
- The Unity Editor runs on the host machine. Docker runs the backend and local web services.

### Backend

- SpacetimeDB is the authoritative backend and persistent game state store.
- The SpacetimeDB server module uses C#.
- Unity sends intent commands such as `move_to`, `select_target`, and `engage`.
- SpacetimeDB validates commands and owns movement, collision, targeting, combat, NPC behavior, damage, rewards, and progression.
- The world simulation uses a fixed server tick, initially targeting 20–30 updates per second.
- Unity interpolates received state for smooth rendering.
- Use anonymous local identities for the validation build.
- Do not run Nakama alongside SpacetimeDB. They would both compete to own the authoritative backend role.

### Data and content

- Version-controlled data files are the source of truth for map gameplay data, ship stats, NPC definitions, loot, and balance values.
- The server seeds this data into SpacetimeDB.
- Unity owns visual scene assets.
- Server-readable data defines walkable water, islands, reefs, ports, spawn points, and other gameplay geometry.
- Runtime player state lives in SpacetimeDB.
- Local data persists across restarts by default.
- An explicit reset command wipes and reseeds local data.

### Local infrastructure

Docker Compose starts the complete local environment using current image channels:

- SpacetimeDB.
- PostgreSQL, reserved for future reporting, analytics, or back-office data unless a concrete use is approved.
- Redis, available for future caching, queues, rate limiting, or coordination.
- MinIO, as the local S3-compatible object-storage replacement for Cloudflare R2.
- The TanStack Start admin panel.

Cloudflare, R2, and cloud servers are not part of the current deployment scope. Production configuration can be documented later without being activated now.

### Admin

- Use TanStack Start with `@tanstack/react-start`.
- Use TanStack Router with file-based routes under `src/routes`.
- The first admin panel is read-only.
- Initial views show service health, connected players, ships, positions, and recent events.
- The admin panel must use protected backend/admin operations. It must not write directly to database tables.

### Assets

- The validation build may use temporary licensed or free assets.
- Art must be replaceable without changing gameplay code.
- The final commercial art pipeline remains a later decision.

## Repository shape

```text
sea/
  apps/
    admin/                    # TanStack Start + TanStack Router
    game-unity/               # Unity project

  server/
    spacetimedb/              # C# server module and seed data

  packages/
    contracts/                # Shared names, schemas, and generated metadata
    tooling/                  # Repository scripts and verification helpers

  infra/
    docker-compose.yml
    minio/
    postgres/
    redis/

  docs/
  scripts/
  PLAN.md
  README.md
  .env.example
  .gitignore
  .gitattributes
```

Unity is part of the monorepo, but it is not forced into the JavaScript package workspace. JavaScript and TypeScript workspaces use pnpm. Repository-wide commands can be exposed through a root task runner or Makefile.

## Phases

Each phase ends with automated verification, a diff review, and one conventional commit. No phase is considered complete because code exists. It is complete when its acceptance checks pass.

### Phase 0: plan and decisions

Deliverables:

- This `PLAN.md`.
- Confirmed stack and architecture decisions.
- Explicit list of deferred decisions.

Acceptance:

- The plan is reviewed and approved.
- No implementation is included in the phase commit.

Commit:

```text
docs(plan): define local game architecture and phases
```

### Phase 1: repository and local environment foundation

Deliverables:

- Monorepo directories.
- pnpm workspace for TypeScript applications and tools.
- Docker Compose with current service image channels, health checks, named volumes, and local configuration.
- Local SpacetimeDB service.
- PostgreSQL, Redis, and MinIO services.
- TanStack Start admin shell.
- Root commands for starting, stopping, resetting, checking, and inspecting the local environment.
- Git ignore and Git LFS policy for Unity and large binary assets.
- Environment examples with no committed secrets.

Automated acceptance:

- Compose configuration validates.
- All local services become healthy.
- Admin package installs, typechecks, and builds.
- Reset and seed commands are deterministic.

Commit example:

```text
build(infra): add reproducible local development stack
```

### Phase 2: SpacetimeDB module and contracts

Deliverables:

- C# SpacetimeDB module.
- Initial tables for identity, player ship, map entities, resources, and events.
- Reducers for connecting, loading a player, moving, selecting a target, and engaging.
- Fixed-tick simulation skeleton.
- Version-controlled seed data.
- Generated C# bindings for Unity.
- Generated TypeScript bindings for the admin panel.

Automated acceptance:

- Module builds and publishes locally.
- Reducer and validation tests pass.
- A clean reset recreates the same initial state.
- Generated bindings are reproducible.

Commit example:

```text
feat(server): add authoritative world contracts
```

### Phase 3: Unity project and connection

Deliverables:

- Unity 6.3 LTS project with the exact patch recorded by Unity’s `ProjectVersion.txt`.
- URP-based orthographic 2.5D setup.
- WebGL and macOS build profiles.
- SpacetimeDB C# SDK integration.
- Anonymous local identity and reconnect handling.
- Generated binding import workflow.
- Basic connection and subscription screen.

Automated acceptance:

- Unity imports the project in batch mode.
- Unity compiles scripts without errors.
- WebGL build completes.
- macOS build completes.
- Connection and binding smoke tests pass against the local server.

Commit example:

```text
feat(client): connect Unity builds to local SpacetimeDB
```

### Phase 4: map and sailing foundation

Deliverables:

- One tropical map.
- Harbor, open water, islands, reefs, and spawn points.
- One player ship.
- Click-to-move input.
- `move_to` intent command.
- Server-side movement, bounds, collision, and fixed-tick updates.
- Client interpolation and camera behavior.

Automated acceptance:

- Movement rules pass unit and integration tests.
- Invalid movement commands are rejected.
- The client renders server state rather than owning authoritative position.
- WebGL and macOS builds remain green.

Commit example:

```text
feat(world): add authoritative sailing on the first map
```

### Phase 5: targeting and combat

Deliverables:

- One computer-controlled enemy ship.
- Mouse and keyboard target selection.
- Explicit engage action.
- Automatic cannon fire after engagement.
- Range checks, cooldowns, damage, sinking, and combat events.
- Player and enemy health state.
- Combat feedback, projectiles, impact effects, and sound placeholders.

Automated acceptance:

- Targeting and engagement rules pass tests.
- Cooldowns, range, damage, and rewards pass tests.
- Clients cannot apply damage directly.
- NPC behavior is deterministic under a fixed test seed.
- WebGL and macOS builds remain green.

Commit example:

```text
feat(combat): add authoritative cannon combat
```

### Phase 6: rewards and one upgrade path

Deliverables:

- One resource currency.
- Enemy reward table.
- Player progression record.
- One ship upgrade, such as cannon damage, hull strength, or range.
- Persistence across local restarts.
- Explicit reset and reseed workflow.

Automated acceptance:

- Rewards are granted exactly once.
- Upgrade costs and effects pass tests.
- Persistence and reset integration tests pass.
- Unauthorized state changes are rejected.
- WebGL and macOS builds remain green.

Commit example:

```text
feat(progression): add persistent rewards and first ship upgrade
```

### Phase 7: read-only admin and full automated verification

Deliverables:

- Admin service-health view.
- Connected-player and ship views.
- Current positions and recent event views.
- Local logs and basic error reporting.
- One root verification command covering Compose, SpacetimeDB, admin, Unity scripts, WebGL, and macOS builds.

Automated acceptance:

- The full local stack starts from a clean checkout.
- The full verification command passes.
- Both Unity builds complete from the same source revision.
- Admin data matches server state.
- A reset returns the environment to the documented seed state.

Commit example:

```text
feat(admin): add read-only local operations dashboard
```

### Phase 8: final manual validation

Deliverables:

- Run the WebGL build locally.
- Run the macOS build locally.
- Play through the complete loop for 10–15 minutes.
- Confirm sailing, exploration, targeting, combat, rewards, and the upgrade work together.
- Record findings and decide whether to continue, revise, or stop.

Acceptance:

- The user completes the manual playtest.
- The result is recorded in a validation note.
- A go/no-go decision is made for the next development cycle.

Commit example:

```text
docs(validation): record first playable assessment
```

## Verification policy

Before every phase commit:

1. Run the phase-specific automated checks.
2. Run the repository-wide automated checks available at that phase.
3. Review the diff for unrelated changes, generated-file drift, and secrets.
4. Confirm the conventional commit message.
5. Commit only after all automated checks pass.

The final manual test is intentionally not used as an intermediate implementation gate. Automated tests must cover game rules and integration behavior as far as possible. Manual play is reserved for judging whether the finished loop is actually fun.

## Deferred decisions

These decisions do not block Phase 0, but must be resolved before the phase that needs them:

- Exact Unity 6.3 LTS patch and required WebGL/macOS modules. Resolved for Phase 3 as `6000.3.23f1` on Apple Silicon with WebGL and macOS IL2CPP support.
- Exact SpacetimeDB runtime and CLI version pairing. Resolved for Phase 3 as CLI/runtime `2.8.3` with the Unity SDK pinned to `v2.8.3`.
- When local image channels should be replaced with tested digests for production.
- Whether the SpacetimeDB CLI runs on the host or through a Docker wrapper.
- Exact Unity package list and asset import pipeline. Phase 3 uses the URP blank template package set, a pinned SpacetimeDB Git package, and generated bindings under `Assets/Generated/SpacetimeDB`.
- Final licensed art and audio sources.
- Long-term account providers beyond anonymous local identity.
- Production hosting provider and multi-server topology.
- When PostgreSQL and Redis gain their first real consumers.
- Commercial licensing review for every external dependency and asset.

## Reference research

- [Seafight](https://www.seafight.com/?aid=632)
- [Seafight DevBlog](https://us1.seafight.com/devBlog/)
- [Unity 6 releases](https://unity.com/releases/unity-6)
- [SpacetimeDB Unity tutorial](https://spacetimedb.com/docs/tutorials/unity/)
- [SpacetimeDB self-hosting and Docker](https://spacetimedb.com/docs/intro/faq/)
- [TanStack Start](https://tanstack.com/start/latest)
- [TanStack Router](https://tanstack.com/router/latest)
