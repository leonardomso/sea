# Local development

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

- `pnpm ci:fast`: static checks, admin/type builds, and repository invariants.
- `pnpm server:test`: fast pure domain and command tests in pinned .NET.
- `pnpm verify`: normal phase gate, including the real module, local services,
  Unity tests, runtime scenarios, and macOS/WebGL builds.
- `pnpm verify:full`: Phase 18 and final load, soak, mutation, and performance
  proof. It is not a routine pull-request check.

`pnpm unity:test:runtime` publishes against `sea-local`, launches the built
macOS client, and verifies connection, sailing, combat, NPC sinking, loot, XP,
respawn, hazards, abilities, and repair. The script restores the original local
identity preference when it exits.

Compose images use immutable digests. Update a stable tag and its digest
together, then run the complete local gate before committing the upgrade.
