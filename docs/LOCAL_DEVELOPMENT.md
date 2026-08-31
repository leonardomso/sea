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

## SpacetimeDB module

```sh
pnpm server:build
pnpm server:test
pnpm server:publish
pnpm server:generate:csharp
pnpm server:generate:typescript
```

Use `pnpm server:reset` when you intentionally want to clear and republish the local module data. It removes only the SpacetimeDB container and named volume; PostgreSQL, Redis, MinIO, and admin state are preserved.

Local Compose services follow current image channels. This keeps validation close to the latest supported releases; before production work begins, we will replace these channels with tested image digests.
