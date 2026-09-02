using Sea.Performance;

namespace Sea.PerformanceEvidence.Tests;

internal static class EvidenceFactory
{
    public static PerformanceRunEvidence Passing() => new(
        SchemaVersion: 1,
        Machine: "M1 Pro local",
        RecordedAtUtc: new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero),
        Load: new LoadEvidence(
            AttemptedClients: 5_000,
            ConnectedClients: 5_000,
            RetainedClients: 4_995,
            ActiveShips: 1_000,
            DormantShips: 4_000,
            TickP95Milliseconds: 10,
            TickP99Milliseconds: 20,
            CommandAckP95Milliseconds: 150,
            CommandAckP99Milliseconds: 250,
            ServerCpuPercent: 85,
            LoadRunnerCpuPercent: 85,
            MemoryGrowthPercent: 5,
            FailedClients: 0),
        MacOS: new ClientEvidence(250, 16.7, 25, 0, true),
        WebGL: new ClientEvidence(100, 16.7, 25, 0, true),
        Correctness: new CorrectnessEvidence(0, 0, 0, 0, 0, 0, 0),
        Quality: new QualityEvidence(95, 90, 90, 0));

    public static PerformanceRunEvidence WithFailure(string check) => check switch
    {
        "connections-retained" or "server-tick-p99" or "command-ack-p95" or
            "server-cpu" or "load-runner-cpu" or "memory-growth" or
            "failed-load-clients" => WithLoadFailure(check),
        "macos-frame-p95" => Passing() with
        {
            MacOS = Passing().MacOS with { FrameP95Milliseconds = 16.71 },
        },
        "webgl-frame-p95" => Passing() with
        {
            WebGL = Passing().WebGL with { FrameP95Milliseconds = 16.71 },
        },
        "client-frame-p99" => Passing() with
        {
            MacOS = Passing().MacOS with { FrameP99Milliseconds = 25.01 },
        },
        "idle-allocations" => Passing() with
        {
            MacOS = Passing().MacOS with { IdleBytesPerFrame = 1 },
        },
        "runtime-errors" => Passing() with
        {
            Correctness = Passing().Correctness with { RuntimeErrors = 1 },
        },
        "line-coverage" => Passing() with
        {
            Quality = Passing().Quality with { LineCoveragePercent = 94.99 },
        },
        "branch-coverage" => Passing() with
        {
            Quality = Passing().Quality with { BranchCoveragePercent = 89.99 },
        },
        "mutation-score" => Passing() with
        {
            Quality = Passing().Quality with { MutationScorePercent = 89.99 },
        },
        _ => throw new ArgumentOutOfRangeException(nameof(check)),
    };

    private static PerformanceRunEvidence WithLoadFailure(string check)
    {
        var load = check switch
        {
            "connections-retained" => Passing().Load with { RetainedClients = 4_994 },
            "server-tick-p99" => Passing().Load with { TickP99Milliseconds = 20.01 },
            "command-ack-p95" => Passing().Load with
            {
                CommandAckP95Milliseconds = 150.01,
            },
            "server-cpu" => Passing().Load with { ServerCpuPercent = 85.01 },
            "load-runner-cpu" => Passing().Load with { LoadRunnerCpuPercent = 85.01 },
            "memory-growth" => Passing().Load with { MemoryGrowthPercent = 5.01 },
            "failed-load-clients" => Passing().Load with { FailedClients = 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(check)),
        };
        return Passing() with { Load = load };
    }
}
