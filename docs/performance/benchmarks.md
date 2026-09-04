# Domain benchmarks

`tests/performance/Sea.Server.Benchmarks` holds BenchmarkDotNet microbenchmarks for the domain hot paths.
They run inside the pinned .NET 8 SDK container, in-process, one benchmark class at a time:

```sh
pnpm perf:domain                       # every benchmark, short job
pnpm perf:domain -- --filter '*ShipStat*'   # one class
```

Reports land in `BenchmarkDotNet.Artifacts/` (ignored by git).

## Baseline (2026-09-04, Apple Silicon host, Docker linux/arm64, short job)

| Benchmark | Mean | Allocated |
| --- | ---: | ---: |
| ShipStatBenchmark.ComputeBareHull | 107 ns | 0 |
| ShipStatBenchmark.ComputeFullKit (six sources) | 1,392 ns | 0 |
| ContentValidationBenchmark.ValidateDefaultCatalog | 3.83 µs | 6.5 KB |
| ContentIndexBenchmark.AmmunitionByCode | 206 ns | 2,264 B |
| ContentIndexBenchmark.HullsById | 73 ns | 216 B |
| VolleyBenchmark.ResolveFacing | 12.4 ns | 0 |
| VolleyBenchmark.ResolveVolley (facing, armour, damage) | 17.6 ns | 0 |
| MagazineBenchmark.AdvanceWhileReloading | 0.88 ns | 0 |
| MagazineBenchmark.AdvanceWhileFull | 0.67 ns | 0 |
| MagazineBenchmark.SpendVolley | 0.96 ns | 0 |
| CommandEvaluationBenchmark.EvaluateFire | 18.5 ns | 0 |
| MovementModifierBenchmark.CleanWater | 0.62 ns | 0 |
| MovementModifierBenchmark.SlowedInAShoalStorm | 3.26 ns | 0 |
| MovementModifierBenchmark.MoveStorm | 7.80 ns | 0 |
| NavigationBenchmark.FindDetour | 246 ns | 544 B |
| SpatialLookupBenchmark.ResolveChunks | 1.07 µs | 0 |
| RewardDistributionBenchmark.DistributeRewards | 13.6 µs | 14.21 KB |

The whole combat tick path -- advance the magazine, spend a volley, resolve the face, read the
armour behind it and take the damage off -- costs under 25 ns per ship per shot and allocates
nothing, so a hundred hulls in a fight cost the tick a few microseconds.

## What the numbers led to

- `ShipStatRules.Compute` sorted sources with LINQ (`OrderBy`/`ThenBy`/`Select`/`ToArray`) and built a
  second array for the prefix sums, so even a bare hull allocated. It now insertion-sorts into a
  stack buffer (kits of up to eight sources) and accumulates the prefix in a second stack buffer, so a
  stat recompute on login or refit allocates nothing. Re-measure with the filter above after touching it.
- The combat and movement benchmarks used to be written with their arguments as literals, which let
  the JIT constant-fold the whole rule away: `ResolveFacing` and both `MovementModifiers` cases
  reported 0.00 ns, which is the harness measuring an empty method. They now read their inputs from
  mutable fields. Write a new benchmark the same way, or it will report a number that is not there.
- Content validation and the code indexes run once at module load; their allocations do not matter.
- `DistributeRewards` is the next candidate worth a look (14 KB per settlement) when settlement fan-out
  lands in Milestone 3.

## The world tick under load (2026-09-04, 100 clients, 30 s)

The microbenchmarks above measure rules; they never touched the thing that actually costs a tick,
which is talking to the datastore. Every host call -- a `Find`, an `Update`, an index scan -- costs
somewhere between 50 and 150 µs no matter how little work the rule behind it does, so a tick's
price is very close to the number of rows it reads and writes.

Measure it by flipping `SimulationWorkRules.ProfileDispatchPhases` to true, publishing, running the
load driver, and feeding the module log to the profiler:

```sh
./scripts/spacetime.sh logs sea-local -n 200000 \
  --server http://host.docker.internal:43000 | node scripts/profile-dispatch.mjs
```

Milliseconds spent inside `run_simulation_dispatch`, 350 sampled ticks per run:

| Stage | tick avg | tick p95 | movement avg | movement p95 | npc avg | npc p95 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Before the pass | 31.40 | 49.42 | 18.78 | 24.89 | 9.11 | 27.56 |
| + one read per tick | 29.00 | 41.30 | 17.47 | 22.92 | 7.34 | 21.78 |
| + half the fleet per tick | 22.58 | 33.93 | 13.28 | 19.43 | 5.41 | 9.14 |
| + publish only on divergence | 20.13 | 29.25 | 10.31 | 14.16 | 5.96 | 10.28 |

Three changes, in the order they were measured:

- **One read per tick.** `TickWorld` now memoises the movement shard rows and the roster of
  connected players for the length of a dispatch. The NPC phase used to re-read every player's
  `ShipMovement` row for every hostile deciding what to chase; it now reads the roster once and
  probes at most `NpcRules.MaximumTargetProbes` candidates per hostile.
- **Half the fleet per tick.** `MovementShardStride = 2` means a shard sails on every second tick
  and integrates both ticks when its turn comes round, so the water is the same and the row count
  halves. The client's `SeaSnapshotClock.RenderDelayTicks` is 2 to match: a remote ship now
  publishes every other tick, and rendering has to trail her by the stride or it runs past her
  newest sample and guesses. A course set between a shard's turns still bites within a tick of
  the ack.
- **Publish only on divergence.** A ship holding her course is already drawn where she is, so
  `ReplicationRules` only spends a row when the reckoning has drifted a unit, the heading two
  degrees, the hull has crossed into another chunk or come to rest -- and on a ten tick heartbeat
  regardless, so nothing goes stale. The client's `SeaMotionTimeline.MaximumExtrapolationTicks` is
  10 to match the heartbeat; past it a ship holds her last reckoning instead of sailing off.

`pnpm runtime:test:scale-smoke` measures the same tick from outside, over a window rather than a
log line, and agrees:

| Measure | Before | After | Gate |
| --- | ---: | ---: | ---: |
| dispatch p95 | 36.02 ms | 21.02 ms | 10 ms |
| dispatch p99 | 44.41 ms | 26.60 ms | 20 ms |
| command ack p95 | 44.18 ms | 22.65 ms | 150 ms |
| command ack p99 | 65.01 ms | 31.87 ms | -- |
| server CPU | 2.01 % | 1.41 % | -- |

### What is still missed

The Phase 18 tick gates are p95 ≤ 10 ms and p99 ≤ 20 ms at 100 clients. This pass took roughly 36 %
off the average and 41 % off p95 -- 42 % by the scale smoke's own measure -- and still does not
meet them. What is left is not rule cost -- the
rules above are nanoseconds -- it is the per-host-call floor multiplied by the rows a tick has to
touch, and the only way further down is to touch fewer rows still: wider shard strides (at the cost
of command latency), or moving the whole moving fleet into one blob per chunk rather than one row
per ship.

This is a budget miss, not a playability one. The tick fires at 10 Hz, so 20 ms of work sits inside
a 100 ms budget with room to spare, the host is at 1.4 % CPU, and a command is acknowledged in
22.65 ms p95 against a 150 ms gate. Record the number honestly and revisit the gate when the row
layout changes.

### Re-measured in isolation (2026-09-04)

`pnpm runtime:test:scale-isolated` publishes its own database into a container of its own rather
than sharing the development stack, so it is the number to quote. Two runs at 100 clients, the
second sampling only after a ten second warm-up:

| Measure | Run A | Run B (10 s warm-up) | Gate |
| --- | ---: | ---: | ---: |
| dispatch p95 | 27.68 ms | 25.64 ms | 10 ms |
| dispatch p99 | 58.10 ms | 40.48 ms | 20 ms |
| command ack p95 | 35.21 ms | 27.50 ms | 150 ms |
| command ack p99 | 43.06 ms | 40.12 ms | -- |
| server CPU | 1.07 % | 0.58 % | 85 % |
| memory growth | 0 % | 0 % | 5 % |
| failed clients | 0 | 0 | 0 |

A cold module is worth about two milliseconds of p95 and nearly twenty of p99, which is why the
warm-up is worth setting; past that the two runs agree with the shared-stack number above to within
the run-to-run spread. The conclusion does not move: the tick is over its gate, everything else in
the budget passes with room, and the remaining distance is rows touched per tick rather than rule
cost.

The harness itself learned one thing from the five thousand client attempt. Its control plane reads
-- the metrics scrape and the SQL counts that decide readiness -- had two and five second budgets,
and a `SELECT` that returns a row per client is slower the more clients there are, so the run died
on `curl` exit 28 with no diagnostics rather than on a verdict about the server. They now wait
`SEA_CONTROL_PLANE_TIMEOUT` seconds (30 by default) and treat a timeout as a missing sample.

## Re-measured after the sailing pass (2026-09-04, 100 clients)

The sailing pass gave the ships back their fresh positions: `MovementShardStride` went from 2 to 1
so every hull is integrated on the tick it moves rather than up to 200 ms later. That undid the
"half the fleet per tick" saving above, and the tick is dearer for it. `pnpm
runtime:test:scale-isolated`, same harness as the section before:

| Measure | Stride 2 (previous) | Stride 1 (now) | Gate |
| --- | ---: | ---: | ---: |
| dispatch p95 | 25.64 ms | 37.66 ms | 10 ms |
| dispatch p99 | 40.48 ms | 56.80 ms | 20 ms |
| command ack p95 | 27.50 ms | 39.64 ms | 150 ms |
| command ack p99 | 40.12 ms | 136.60 ms | -- |
| server CPU | 0.58 % | 1.44 % | 85 % |
| memory growth | 0 % | 0 % | 5 % |
| failed clients | 0 | 0 | 0 |

Everything but the tick still passes with room. The tick was already over its gate at stride 2 and
is further over now, so this is a worse miss of a gate that was never met, not a newly broken one.

### Where the tick actually goes

Measured with `ProfileDispatchPhases` on, 100 clients, 496 sampled ticks, milliseconds per tick.
The sub-lines are the sum across every hull the phase handled in that tick, so they add up to the
phase above them:

| Phase | mean | p95 |
| --- | ---: | ---: |
| everything before the fleet | 5.15 | -- |
| npc, total | 10.30 | 13.59 |
| -- hydrate the deciding hull | 1.85 | 4.42 |
| -- read her target | 1.11 | 2.75 |
| -- hunt for a new one | 1.30 | 5.10 |
| -- carry out the decision | 0.94 | 3.69 |
| -- loop and index overhead | 5.10 | -- |
| movement, total | 15.30 | 18.40 |
| -- read the shard row | 0.58 | 1.45 |
| -- apply pending commands | 0.67 | 2.07 |
| -- sail and publish | 3.23 | 8.66 |
| -- write the shard row back | 3.56 | 8.50 |
| whole tick | 30.75 | 41.64 |

Two things stand out. Writing the eight movement shard rows back costs 3.56 ms a tick on its own,
and that is the price of stride 1 outright: at stride 2 only four of them were written. And the NPC
phase costs 10 ms to make roughly seven decisions, which is 1.4 ms a hostile against a fleet of
fifteen -- the hostiles are dearer per hull than the hundred players are.

Neither is rule cost. A shard row carries every hull it is sailing in one blob, so a tick reads and
rewrites the whole blob to move anything in it, and a hostile pays a fat `Ship` row read plus a
linear scan of that blob before it can decide anything. Closing the remaining distance means
changing that layout, not tuning constants, which is a job for the architecture pass rather than
this one.
