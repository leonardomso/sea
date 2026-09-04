# SpacetimeDB server

This directory contains Sea's authoritative C# game module. It targets .NET 8
for the pinned macOS-compatible SpacetimeDB WASI toolchain.

## Ownership

The module owns identity, ships, movement, targeting, combat, effects,
repairs, port rules, hazards, NPC decisions, loot, progression, death,
respawn, and rewards. The `ActivateAbility`, `StartBoarding`, and ramming
paths exist and answer with a typed `NotAvailable` rejection until their
milestone; see `docs/STATUS.md`.

Clients issue `IssueShipCommand` envelopes with a monotonic player-scoped
command ID. Expected rejection produces a typed command result instead of an
unhandled reducer error.

The world simulation runs at 10 Hz and NPC decisions at 2 Hz. Hot systems
process active and due rows through indexes and spatial chunks. Dormant ships
must not create movement or AI work.

## Layout

- `spacetimedb`: schema, reducers, command policy and execution, simulation
  systems, deterministic content, events, and rewards.
- `tests`: pure unit, property, command-matrix, and replay tests.
- `spacetimedb/Content/Data/*.json`: the embedded content (maps and sectors,
  hulls, cannons, ammo, NPCs, stat caps); `pnpm content:generate` turns it
  into `Generated/ContentCatalog.g.cs`. Never edit the generated file, and run
  `pnpm quality:content` after changing the JSON.

Pure domain files are linked into tests and benchmarks without depending on
SpacetimeDB runtime types.

## Commands

Run all tooling through the pinned repository scripts:

```sh
pnpm server:build
pnpm server:test
pnpm server:test:integration
pnpm server:publish
pnpm server:reset
pnpm server:generate:csharp
pnpm server:generate:typescript
```

`server:reset` destroys only disposable local SpacetimeDB state. Schema or
public reducer changes require regenerated C# and TypeScript bindings in the
same commit.

Do not use floating SpacetimeDB images, SDK packages, or host CLI versions.
The server image, CLI, runtime package, Unity SDK, and TypeScript SDK must stay
on the same pinned stable release.
