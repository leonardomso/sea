# Local development

Read `docs/STATUS.md` first if you want to know what the game currently does.
This file is only about running and testing it.

Sea is developed and validated locally. Docker Compose runs SpacetimeDB,
PostgreSQL, Redis, MinIO, and the production admin build. Unity runs on the
macOS host.

## Required versions

- Node.js `24.19.0`
- pnpm `11.25.0`
- Unity `6000.3.23f1` with macOS and WebGL build support
- Docker Desktop with Docker Compose
- Git LFS

The repository scripts run the pinned .NET and SpacetimeDB toolchains in
containers. Do not replace them with an unpinned host installation.

## Install and start

```sh
cp .env.example .env
corepack enable
corepack prepare pnpm@11.25.0 --activate
pnpm install --frozen-lockfile
pnpm infra:up
pnpm server:reset
```

`server:reset` clears and republishes only the disposable SpacetimeDB world.
PostgreSQL, Redis, MinIO, and admin data are preserved.

Stop containers without deleting data:

```sh
pnpm infra:down
```

Use `pnpm infra:reset` only when all local Compose volumes should be removed.

## Run the clients

The admin already runs as a production build in Compose. Run its development
server on the host only when changing the interface:

```sh
pnpm admin:dev
```

Build and open the macOS game:

```sh
pnpm unity:build:macos
open apps/game-unity/Build/Sea.app
```

Create a WebGL build with `pnpm unity:build:webgl`. Build output and Unity's
regenerable folders are ignored by Git.

## Server development

```sh
pnpm server:build
pnpm server:test
pnpm server:test:integration
pnpm server:publish
```

After changing the schema, reducers, events, or tagged unions:

```sh
pnpm server:generate:csharp
pnpm server:generate:typescript
pnpm quality:bindings
pnpm server:reset
```

Commit the schema and both generated binding sets together.

## Validation levels

- `pnpm ci:fast`: static checks, admin and type builds, and repository
  invariants. No Docker, no Unity. This is what pull-request CI runs.
- `pnpm server:test`: the pure domain, command, and replay tests in pinned
  .NET. 683 tests, and fast.
- `pnpm server:test:integration`: 21 tests against a real published module.
- `pnpm verify`: the normal phase gate. It builds the module, runs every test
  suite, builds both Unity players, and runs the runtime and presentation
  probes against real local services. It passes.
- `pnpm verify:full`: `pnpm verify` plus the two proofs that need more than one
  client, `pnpm runtime:test:shared-world` (four clients in one world) and
  `pnpm runtime:test:scale-isolated` (a hundred clients against a private
  database). It currently exits non-zero on the second, because the world tick
  misses its gate and the gate has not been lowered. See `docs/STATUS.md`
  section 4.
- `pnpm server:test:mutation Domain/ShipStatRules.cs`: Stryker on one domain
  file at a time; a whole-domain run exhausts memory. Not part of any routine
  gate. Use it when you want evidence about one specific rule file.

`pnpm unity:test:runtime` publishes against `sea-local`, launches the built
macOS client, and drives a real session: connect, set a course, stop, select a
target, choose ammunition, fire, sink an NPC, take loot, start and finish a
repair, sink, and choose a berth. It also checks that a retired command is
answered with the right rejection code. The script restores the original local
identity preference when it exits.

`pnpm unity:verify` additionally builds the WebGL player and runs the
presentation benchmark on both platforms: 250 ships on macOS and 100 in
headless Chrome, each measured for 300 frames after a 180 frame warm-up. The
benchmark sails alone, without a live world, because on WebGL the required
ship count is exactly the platform's ship budget.

Compose images use immutable digests. Update a stable tag and its digest
together, then run the complete local gate before committing the upgrade.
