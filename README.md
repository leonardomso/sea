# Sea

Sea is an original multiplayer naval-combat game. It uses the navigation and
combat language of classic pirate browser games, with original code, art,
balance, names, audio, and interface design.

The project is local-first and currently implements one shared PvE world. The
server is authoritative: clients send typed ship commands, while SpacetimeDB
owns movement, combat, NPC decisions, loot, progression, death, and respawn.

## Current state

The scalable PvE vertical slice (previous plan, Phases 0 through 17) is
complete. It includes click-to-sail navigation with island and reef avoidance,
broadside combat, repairs, boarding, statuses, wind, currents, storms, twelve
roaming NPCs, loot, respawn, shared rewards, four-client shared-world support,
and a macOS and WebGL Unity client using the owned Apricum ship asset. The
camera follows the ship; WASD or middle-mouse drag pans it and it eases back
after a few seconds.

The game is now built to the design in `docs/SEA_1_KNOWLEDGE.md` through
`docs/SEA_4_TECHNICAL.md`. Milestone 1 of [PLAN.md](./PLAN.md) replaces the
slice's combat rules with the design's: one map (Havenmere), magazine firing
at a selected target, facing armor, channelled repair, port rules, and enemies
derived from the base player ship. `docs/SEA_5_GAP_ANALYSIS.md` lists every
difference between the slice and the design.

## Architecture

```text
Unity macOS/WebGL clients
        │ typed commands + scoped subscriptions
        ▼
SpacetimeDB C# module ─── authoritative 10 Hz world and combat simulation
        │
        ├── generated C# bindings ──► Unity
        └── generated TypeScript bindings ──► admin and future tools

Docker Compose also runs PostgreSQL, Redis, MinIO, and the admin panel.
Those services are not in the authoritative combat path.
```

The client renders at 60 FPS and interpolates the 10 Hz server state. NPC AI
decisions run at 2 Hz. Ships may pass through one another; islands and reefs
remain blocked.

## Repository map

| Path | Purpose |
|---|---|
| `apps/game-unity` | Unity 6 client, UI, input, presentation, tests, and owned assets |
| `apps/game-unity/Assets/Generated/SpacetimeDB` | Generated C# client bindings; do not hand-edit |
| `apps/admin` | TanStack Start and TanStack Router local admin panel |
| `server/spacetimedb/spacetimedb` | Authoritative C# schema, reducers, commands, simulation, content, and rewards |
| `server/spacetimedb/tests` | Pure server unit, property, and replay tests |
| `packages/contracts` | Generated TypeScript bindings; do not hand-edit generated files |
| `packages/spacetimedb-unity` | Pinned local SpacetimeDB Unity SDK package |
| `tests/integration` | Tests that publish and call the real SpacetimeDB module |
| `tests/performance` | BenchmarkDotNet microbenchmarks |
| `tests/load` | NBomber load-test client |
| `infra` | Pinned Docker Compose services |
| `scripts` | Build, generation, verification, runtime, and launch entry points |
| `docs` | Focused development documentation |
| `PLAN.md` | Source of truth for scope, milestones, acceptance gates, and commit boundaries |
| `AGENTS.md` | Repository rules for AI and human contributors |

## Prerequisites

- macOS on Apple Silicon for the supported local game workflow.
- Docker Desktop with Docker Compose.
- Node.js `24.19.0`.
- pnpm `11.25.0` through Corepack.
- Unity `6000.3.23f1` with macOS and WebGL build support.
- Git LFS for binary game assets.

.NET and the SpacetimeDB CLI run from pinned containers for the normal local
workflow. You do not need to install their SDKs globally.

## First local run

```sh
cp .env.example .env
corepack enable
corepack prepare pnpm@11.25.0 --activate
pnpm install --frozen-lockfile
pnpm infra:up
pnpm server:reset
pnpm unity:build:macos
open apps/game-unity/Build/Sea.app
```

`pnpm server:reset` removes and recreates only the disposable local
SpacetimeDB volume. It preserves PostgreSQL, Redis, MinIO, and admin data.

Local endpoints:

| Service | Address |
|---|---|
| Game server | `http://localhost:43000` |
| Admin panel | `http://localhost:43001` |
| PostgreSQL | `localhost:45432` |
| Redis | `localhost:46379` |
| MinIO API | `http://localhost:49000` |
| MinIO console | `http://localhost:49001` |

## Daily commands

```sh
pnpm infra:up                 # start or rebuild local services
pnpm infra:down               # stop services and preserve volumes
pnpm infra:logs               # follow service logs
pnpm server:publish           # update sea-local without clearing state
pnpm server:reset             # clear and republish disposable game state
pnpm admin:dev                # run the admin panel on the host
pnpm unity:test               # Unity EditMode tests
pnpm unity:test:runtime       # built macOS gameplay scenario
pnpm unity:build:macos        # macOS production player
pnpm unity:build:webgl        # WebGL production player
pnpm ci:fast                  # short pull-request checks
pnpm verify                   # complete normal local gate
```

`pnpm verify` is intentionally thorough. It builds both Unity players and runs
real local services. Pull-request CI uses `pnpm ci:fast` plus server unit tests
so routine reviews remain short. Load, soak, mutation, and 5,000-client checks
belong to the milestone validation sub-phases and `pnpm verify:full`.

## Changing the server schema

After changing SpacetimeDB tables, reducers, tagged unions, or events:

```sh
pnpm server:generate:csharp
pnpm server:generate:typescript
pnpm quality:bindings
pnpm server:reset
```

Commit schema changes and both generated binding sets together. Never edit the
generated C# or TypeScript files by hand.

## Contribution rules

- Add or update a regression test before changing behavior.
- Run the phase-specific checks and `pnpm verify` before a phase commit.
- Use conventional commits. Each roadmap phase has one owning commit.
- Keep handwritten production and test C# files at or below 500 lines.
- Do not commit generated build output, credentials, local tokens, or reports.
- Use only owned or clearly licensed assets. Do not import copied Seafight
  assets, SWFs, UI, names, audio, or balance data.

More detail is available in [local development](./docs/LOCAL_DEVELOPMENT.md),
the [Unity client guide](./apps/game-unity/README.md), and the
[server guide](./server/spacetimedb/README.md).

## License

This repository is licensed under [GPL-3.0](./LICENSE).
