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
        private readonly FireRequest request = new()
        {
            SourceAlive = true,
            TargetSelected = true,
            TargetAlive = true,
            Cannons = 100,
            Ammunition = 200,
            CurrentTick = 100,
            ReadyAtTick = 90,
            SourceX = 50f,
            SourceY = 50f,
            SourceHeadingDegrees = 0f,
            TargetX = 20f,
            TargetY = 50f,
            MaximumRange = 40f,
            RangeMultiplier = 1f,
            Side = BroadsideSide.Port,
            IsChanneling = false,
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

    [MemoryDiagnoser]
    public class StatusProcessingBenchmark
    {
        private readonly TacticalStatusState status = new(true, 2, 200, 0);

        [Benchmark]
        public StatusApplication ApplyStatus() =>
            TacticalRules.ApplyStatus(status, 100, 50, 3);
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
