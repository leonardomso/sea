# Sea

Sea is an original browser game inspired by the broad systems and feel of classic pirate browser games.

The project is currently in the local validation phase. The repository is a monorepo containing a Unity client, a C# SpacetimeDB module, a TanStack Start admin panel, and Docker-managed local services.

## Local prerequisites

- Docker Desktop with Docker Compose.
- Current Node.js LTS or newer.
- pnpm 11 or newer.
- Unity 6.3 LTS with Web Build Support and macOS Build Support.
- Git LFS for large binary assets.

## Start the local environment

Copy `.env.example` to `.env`, then run:

```sh
pnpm install
pnpm infra:up
```

The local endpoints are:

- SpacetimeDB: `http://localhost:3000`
- Admin panel: `http://localhost:3001`
- PostgreSQL: `localhost:5432`
- Redis: `localhost:6379`
- MinIO API: `http://localhost:9000`
- MinIO console: `http://localhost:9001`

The Unity client is opened from `apps/game-unity` and is not run inside Docker.

## Useful commands

```sh
pnpm infra:config   # validate Compose configuration
pnpm infra:up       # start or rebuild local services
pnpm infra:down     # stop services and keep volumes
pnpm infra:logs     # follow service logs
pnpm infra:reset    # stop services and remove local volumes
pnpm admin:dev      # run the admin app on the host
pnpm admin:check    # format, lint, and typecheck the admin app
pnpm verify         # run the checks available at the current phase
```

## Repository layout

```text
apps/admin/              TanStack Start and TanStack Router admin panel
apps/game-unity/         Unity client
server/spacetimedb/      C# SpacetimeDB module
packages/contracts/      Shared contracts and generated metadata
packages/tooling/        Repository tooling
infra/                   Docker Compose and local service configuration
docs/                    Developer documentation
```

See [PLAN.md](./PLAN.md) for the phased build plan and commit policy.
