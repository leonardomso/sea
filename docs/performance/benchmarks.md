# Domain benchmarks

`tests/performance/Sea.Server.Benchmarks` holds BenchmarkDotNet microbenchmarks for the domain hot paths.
They run inside the pinned .NET 8 SDK container, in-process, one benchmark class at a time:

```sh
pnpm perf:domain                       # every benchmark, short job
pnpm perf:domain -- --filter '*ShipStat*'   # one class
```

Reports land in `BenchmarkDotNet.Artifacts/` (ignored by git).

## Baseline (2026-09-03, Apple Silicon host, Docker linux/arm64, short job)

| Benchmark | Mean | Allocated |
| --- | ---: | ---: |
| ShipStatBenchmark.ComputeBareHull | 171 ns | 280 B |
| ShipStatBenchmark.ComputeFullKit (six sources) | 1,196 ns | 1,688 B |
| ContentValidationBenchmark.ValidateDefaultCatalog | 4.24 µs | 6.91 KB |
| ContentIndexBenchmark.AmmunitionByCode | 286 ns | 2,264 B |
| ContentIndexBenchmark.HullsById | 60 ns | 216 B |
| VolleyBenchmark.DamageProfile | 8.5 ns | 0 |
| VolleyBenchmark.StatusRoll | 2.3 ns | 0 |
| CommandEvaluationBenchmark.EvaluateFire | 52 ns | 0 |
| NavigationBenchmark.FindDetour | 273 ns | 544 B |
| SpatialLookupBenchmark.ResolveChunks | 1.21 µs | 0 |
| StatusProcessingBenchmark.ApplyStatus | 2.0 ns | 0 |
| RewardDistributionBenchmark.DistributeRewards | 15.6 µs | 14.21 KB |

## What the numbers led to

- `ShipStatRules.Compute` sorted sources with LINQ (`OrderBy`/`ThenBy`/`Select`/`ToArray`) and built a
  second array for the prefix sums, so even a bare hull allocated. It now insertion-sorts into a
  stack buffer (kits of up to eight sources) and accumulates the prefix in a second stack buffer, so a
  stat recompute on login or refit allocates nothing. Re-measure with the filter above after touching it.
- Content validation and the code indexes run once at module load; their allocations do not matter.
- `DistributeRewards` is the next candidate worth a look (14 KB per settlement) when settlement fan-out
  lands in Milestone 3.
