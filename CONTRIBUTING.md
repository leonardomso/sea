# Contributing to Sea

Sea is currently a local-first game prototype. Read `PLAN.md` and `AGENTS.md`
before changing code. The plan defines the current phase and its acceptance
gate; repository instructions define the boundaries that every phase must keep.

## Prepare the workspace

Use the exact versions listed in `README.md`, then run:

```sh
corepack enable
corepack prepare pnpm@11.25.0 --activate
pnpm install --frozen-lockfile
pnpm infra:up
pnpm server:reset
```

Resetting the server deletes the local SpacetimeDB world. It does not delete
the other Compose volumes.

## Make a change

1. Add or update a regression test.
2. Confirm that the test fails for the intended reason.
3. Make the smallest coherent implementation.
4. Run the focused tests, `pnpm ci:fast`, and the phase gate from `PLAN.md`.
5. Regenerate and commit C# and TypeScript bindings when public server contracts
   change.
6. Review the diff for generated output, credentials, debug code, and asset
   licensing.

Use conventional commits such as `feat(combat): ...`, `fix(runtime): ...`, or
`docs(repo): ...`. Roadmap implementation stays in its owning phase commit.

## Pull requests

Describe the behavior and contracts that changed, list the exact commands that
passed, and state whether local SpacetimeDB data must be reset. Include an asset
source and license note for every new binary asset.

Routine GitHub CI is intentionally short. Docker runtime integration, Unity
player builds, soak tests, load tests, and manual gameplay validation run at
the local phase gates described in `PLAN.md`.
