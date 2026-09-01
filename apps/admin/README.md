# Local admin panel

The admin panel uses TanStack Start and TanStack Router. It is a local
operational view, not a gameplay client and not part of the authoritative
combat path.

The root Docker Compose stack runs its production build. For host-side UI
iteration:

```sh
pnpm install --frozen-lockfile
pnpm admin:dev
```

Validate it with:

```sh
pnpm admin:check
pnpm admin:build
pnpm admin:test:health
```

Routes live under `src/routes`. TanStack Router generates `src/routeTree.gen.ts`;
do not edit that file manually.

The lightweight health route must never load dashboard data, connect as a game
client, or create a SpacetimeDB identity or ship. Cloud deployment is outside
the current roadmap.
