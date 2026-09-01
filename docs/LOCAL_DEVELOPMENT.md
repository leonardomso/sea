# Local development

The entire backend environment is local. Docker Compose runs the services, while the SpacetimeDB CLI and .NET test SDK are invoked from current Docker images through repository scripts. The module targets .NET 8 for macOS-compatible WASI tooling.

## Start

```sh
cp .env.example .env
pnpm install
pnpm infra:up
```

## Stop

```sh
pnpm infra:down
```

This keeps named volumes. Use `pnpm infra:reset` only when you intentionally want to remove local service data.

## Run the admin on the host

The Compose stack includes the admin service. For faster frontend iteration, it can also run directly on the host:

```sh
pnpm admin:dev
```

## Service ownership

- SpacetimeDB is the game state authority.
- PostgreSQL and Redis are reserved for future consumers.
- MinIO provides local S3-compatible object storage.
- The Unity Editor and Unity builds run outside Docker.

## Unity client

The Unity project uses the exact editor patch recorded in `apps/game-unity/ProjectSettings/ProjectVersion.txt`. The client package manifest pins the SpacetimeDB Unity SDK to the same `v2.8.3` release used by the generated bindings.

```sh
pnpm unity:scene
pnpm unity:test
pnpm unity:test:runtime
pnpm unity:build:webgl
pnpm unity:build:macos
```

`pnpm unity:scene` regenerates the main scene and build settings through an editor method. WebGL output is written to `apps/game-unity/Build/WebGL`; the macOS player is written to `apps/game-unity/Build/Sea.app`. Build outputs and Unity's regenerable folders are ignored by Git.

`pnpm unity:test:runtime` requires the local SpacetimeDB service and a published `sea-local` database. It launches the built macOS player headlessly with a stale test identity and verifies that the client recovers, subscribes, reaches `Ready`, and starts without fatal shader errors. The original local identity preference is restored when the test exits.

## SpacetimeDB module

```sh
pnpm server:build
pnpm server:test
pnpm server:publish
pnpm server:generate:csharp
pnpm server:generate:typescript
```

Use `pnpm server:reset` when you intentionally want to clear and republish the local module data. It removes only the SpacetimeDB container and named volume; PostgreSQL, Redis, MinIO, and admin state are preserved.

Local Compose services use tested immutable image digests. Update the tag and digest together, then run the full local verification gate before committing an upgrade.
