# Repository instructions

These instructions apply to the entire repository. `PLAN.md` is the source of
truth for scope, milestone order, acceptance gates, and commit boundaries.
`docs/SEA_1_KNOWLEDGE.md`, `docs/SEA_2_MATH.md`, `docs/SEA_3_MECHANICS.md`,
and `docs/SEA_4_TECHNICAL.md` are the design of record: where they and the
code disagree, the docs win, and `docs/SEA_5_GAP_ANALYSIS.md` records how each
difference is resolved.

## Project boundaries

- Development and validation are local-only until the roadmap says otherwise.
- SpacetimeDB is the authoritative game server. Unity and the admin app are
  clients; they must not calculate authoritative movement, combat, rewards, or
  progression.
- Players and NPCs use the same command policy and effect executor.
- Expected gameplay rejection returns a typed command result. It must not throw
  an unhandled reducer exception.
- Ships may pass through one another. Islands and reefs remain blocked.
- One map is playable: Havenmere (1/1), twenty squares by twenty. Content is
  authored in squares of ten world units; the server stores world units.
- Combat is the design's: a selected target, one magazine of volleys, guns that
  bear in every direction, and armour read from the face a shot lands on. Do
  not reintroduce broadsides, aim points, or the four damage pools.
- Boarding, ramming, abilities, and the PvP flag stay bound to their keys and
  are rejected with `NotAvailable` until their roadmap phase.
- PostgreSQL, Redis, and MinIO are supporting local services and are not part of
  the combat path.
- Do not add PvP, parties, chat, cloud deployment, bosses, quests, or economy
  expansion before their roadmap phase.

## Repository map

- `server/spacetimedb/spacetimedb`: schema, reducers, command handling,
  simulation systems, content, and rewards.
- `server/spacetimedb/tests`: pure server unit, property, and replay tests.
- `apps/game-unity`: Unity client, presentation, input, UI, and Unity tests.
- `apps/admin`: local TanStack Start and TanStack Router admin panel.
- `packages/contracts`: generated TypeScript SpacetimeDB bindings.
- `apps/game-unity/Assets/Generated/SpacetimeDB`: generated C# bindings.
- `tests/integration`: tests against a published real module.
- `tests/performance` and `tests/load`: capacity and performance evidence
  tooling used by milestone gates.
- `scripts`: canonical local commands. Extend these instead of documenting
  one-off shell procedures.
- `.claude/skills`: SpacetimeDB reference skills (tables, indexes, reducers,
  subscriptions, SQL, migrations, clients). Consult the `spacetimedb-tables`,
  `spacetimedb-sql`, and `spacetimedb-clients` performance references before
  changing schema, subscriptions, or hot reducer paths.

## Implementation rules

- Add or update a regression test first and confirm that it fails for the
  intended reason before changing behavior.
- Keep pure domain rules independent of Unity and SpacetimeDB runtime types.
- Process server work through due-tick, active-state, and spatial indexes. Do
  not add full-table or full-world scans to a hot simulation path.
- Aggregate changes and write a ship row once per simulation tick.
- Unity row callbacks maintain client state. Do not scan subscribed tables from
  `Update`.
- Per-frame client work is limited to input sampling, camera movement,
  interpolation, visibility, and active presentation effects.
- Pool repeated presentation objects and use shared materials with
  `MaterialPropertyBlock`.
- Do not use runtime scene searches such as `FindFirstObjectByType`; use
  dependency injection or explicit serialized adapters.
- Handwritten production and test C# files must stay at or below 500 lines.
- Treat generated C# and TypeScript bindings as generated files. Never edit
  them by hand; regenerate and commit both sides with schema changes.
- Game content lives in `server/spacetimedb/spacetimedb/Content/Data/*.json`.
  `server/spacetimedb/spacetimedb/Generated/ContentCatalog.g.cs` is generated
  from it by `pnpm content:generate`; never edit it by hand, and run
  `pnpm quality:content` before committing content changes.
- Preserve deterministic behavior. Fixed seeds and command logs must replay to
  the same state hash.

## Validation and commits

Use the smallest relevant test while developing, then run:

```sh
pnpm ci:fast
pnpm server:test
pnpm verify
```

`pnpm verify` is the normal phase gate and requires Docker and Unity. Run
`pnpm verify:full` only for the roadmap sub-phases that own load, soak, mutation,
and production performance proof.

Every roadmap sub-phase receives one conventional commit. Before committing,
review the diff for unrelated changes, secrets, debug code, generated drift,
and unlicensed assets. Do not rewrite or discard user changes.

Commit messages follow the Conventional Commits guideline:
`type(scope): imperative summary` under 72 characters, with `feat`, `fix`,
`refactor`, `perf`, `test`, `docs`, `chore`, `build`, or `ci` as the type and
the area touched as the scope (`content`, `combat`, `world`, `client`,
`server`, `domain`, `infra`, `tooling`, ...). The body explains why, wrapped
at 72 columns. Never add AI attribution of any kind to a commit, pull request,
or file: no "Generated with", no `Co-Authored-By` for an assistant, no session
links, no tool badges. The same applies to pull request titles and bodies.

## Local state and assets

`pnpm server:reset` intentionally clears the disposable local SpacetimeDB
world. Use it only when a schema migration or explicit test boundary requires
it. It must not remove PostgreSQL, Redis, MinIO, or admin data.

Use only owned or clearly licensed art, audio, fonts, and source material.
Apricum and other user-provided models are allowed. Do not import Seafight
SWFs, copied interface assets, names, audio, or balancing data. Never commit
credentials, tokens, local identities, build output, caches, or raw reports.
