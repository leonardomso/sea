# Local development

Phase 1 keeps the entire backend environment local.

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

Local Compose services follow current image channels. This keeps validation close to the latest supported releases; before production work begins, we will replace these channels with tested image digests.
