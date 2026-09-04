using BenchmarkDotNet.Attributes;
using Sea.Server;

namespace Sea.Server.Benchmarks;

public static class ServerBenchmarks
{
    [MemoryDiagnoser]
    public class NavigationBenchmark
    {
        private readonly NavigationBlocker[] blockers =
        [
            new(50f, 50f, 8f),
            new(80f, 70f, 6f),
            new(120f, 90f, 10f),
        ];

        [Benchmark]
        public bool FindDetour() => NavigationRules.TryFindDetour(
            10f, 10f, 160f, 140f, blockers, out _);
    }

    [MemoryDiagnoser]
    public class CommandEvaluationBenchmark
    {
        // A shot that passes every gate: the validator does all of its work before it can say yes,
        // so the accepted case is the one worth measuring.
        private readonly FireRequest request = new()
        {
            SourceAlive = true,
            TargetSelected = true,
            TargetAlive = true,
            InPort = false,
            SpawnShielded = false,
            IsChanneling = false,
            ReadyVolleys = 2,
            CurrentTick = 100,
            HasFired = true,
            LastShotTick = 40,
            SourceX = 50f,
            SourceY = 50f,
            TargetX = 20f,
            TargetY = 50f,
            RangeUnits = 40f,
        };

        [Benchmark]
        public FireRejection EvaluateFire() => CombatRules.ValidateFire(request);
    }

    [MemoryDiagnoser]
    public class SpatialLookupBenchmark
    {
        private readonly float[] positions = Enumerable.Range(0, 1_000)
            .Select(index => WorldRules.MapMin + index % 200)
            .ToArray();

        [Benchmark]
        public int ResolveChunks()
        {
            var sum = 0;
            foreach (var position in positions)
            {
                sum += SpatialRules.ChunkCoordinate(position);
            }

            return sum;
        }
    }

    /// <summary>
    /// Handling is recomputed for every moving ship on every tick, and the storm walks the map on
    /// the same tick, so both sit directly on the movement hot path.
    /// </summary>
    [MemoryDiagnoser]
    public class MovementModifierBenchmark
    {
        // The water a ship is in, held in mutable fields rather than written into the call: with
        // constant arguments the JIT folds the whole rule away and the benchmark measures nothing.
        private (bool Slowed, float Magnitude, bool Shoal, bool Storm, bool Repairing) openWater =
            (false, 0f, false, false, false);

        private (bool Slowed, float Magnitude, bool Shoal, bool Storm, bool Repairing) shoalStorm =
            (true, 0.35f, true, true, false);

        private (float X, float Y, float Heading) storm = (40f, -60f, 135f);

        [Benchmark(Baseline = true)]
        public TacticalModifiers CleanWater() => TacticalRules.MovementModifiers(
            openWater.Slowed,
            openWater.Magnitude,
            openWater.Shoal,
            openWater.Storm,
            openWater.Repairing);

        [Benchmark]
        public TacticalModifiers SlowedInAShoalStorm() => TacticalRules.MovementModifiers(
            shoalStorm.Slowed,
            shoalStorm.Magnitude,
            shoalStorm.Shoal,
            shoalStorm.Storm,
            shoalStorm.Repairing);

        [Benchmark]
        public HazardPosition MoveStorm() =>
            TacticalRules.MoveStorm(storm.X, storm.Y, storm.Heading, 3f, 1f / 20f);
    }

    [MemoryDiagnoser]
    public class RewardDistributionBenchmark
    {
        private readonly RewardContribution[] contributions = Enumerable.Range(1, 100)
            .Select(value => new RewardContribution(
                (ulong)value,
                (ulong)value * 100,
                (ulong)(value % 5) * 20,
                (ulong)(value % 3) * 10))
            .ToArray();

        [Benchmark]
        public IReadOnlyList<RewardShare> DistributeRewards() =>
            SharedRewardRules.Distribute(100_000, contributions);
    }
}
