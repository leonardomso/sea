namespace Sea.Performance;

public static class PerformanceBudget
{
    public static PerformanceVerdict Evaluate(PerformanceRunEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var load = evidence.Load;
        var correctness = evidence.Correctness;
        var quality = evidence.Quality;
        var retention = load.AttemptedClients <= 0
            ? 0
            : load.RetainedClients * 100d / load.AttemptedClients;
        var checks = new List<PerformanceCheck>
        {
            Equal("schema-version", evidence.SchemaVersion, 1),
            AtLeast("clients-attempted", load.AttemptedClients, 5_000),
            AtLeast("clients-connected", load.ConnectedClients, 5_000),
            AtLeast("connections-retained", retention, 99.9, "%"),
            AtLeast("active-ships", load.ActiveShips, 1_000),
            AtLeast("dormant-ships", load.DormantShips, 4_000),
        };
        checks.AddRange(EvaluateLoadPerformance(load).Checks);
        checks.AddRange(
        [
            AtLeast("macos-visible-ships", evidence.MacOS.VisibleShips, 250),
            AtMost("macos-frame-p95", evidence.MacOS.FrameP95Milliseconds, 16.7, "ms"),
            AtLeast("webgl-visible-ships", evidence.WebGL.VisibleShips, 100),
            AtMost("webgl-frame-p95", evidence.WebGL.FrameP95Milliseconds, 16.7, "ms"),
            AtMost(
                "client-frame-p99",
                Math.Max(evidence.MacOS.FrameP99Milliseconds, evidence.WebGL.FrameP99Milliseconds),
                25,
                "ms"),
            Equal(
                "idle-allocations",
                Math.Max(evidence.MacOS.IdleBytesPerFrame, evidence.WebGL.IdleBytesPerFrame),
                0,
                "bytes/frame"),
            Equal(
                "pool-growth",
                evidence.MacOS.PoolsStable && evidence.WebGL.PoolsStable ? 0 : 1,
                0),
            Equal("identity-delta", correctness.IdentityDelta, 0),
            Equal("runtime-errors", correctness.RuntimeErrors, 0),
            Equal("unhandled-reducer-errors", correctness.UnhandledReducerErrors, 0),
            Equal("missing-assets", correctness.MissingAssets, 0),
            Equal("duplicate-rewards", correctness.DuplicateRewards, 0),
            Equal("dormant-movement-work", correctness.DormantMovementWork, 0),
            Equal("dormant-ai-work", correctness.DormantAiWork, 0),
            AtLeast("line-coverage", quality.LineCoveragePercent, 95, "%"),
            AtLeast("branch-coverage", quality.BranchCoveragePercent, 90, "%"),
            AtLeast("mutation-score", quality.MutationScorePercent, 90, "%"),
            Equal("critical-surviving-mutations", quality.CriticalSurvivingMutations, 0),
        ]);
        return new PerformanceVerdict(checks.All(check => check.Passed), checks);
    }

    public static PerformanceVerdict EvaluateLoadPerformance(LoadEvidence load)
    {
        ArgumentNullException.ThrowIfNull(load);
        var checks = new[]
        {
            AtMost("server-tick-p95", load.TickP95Milliseconds, 10, "ms"),
            AtMost("server-tick-p99", load.TickP99Milliseconds, 20, "ms"),
            AtMost("command-ack-p95", load.CommandAckP95Milliseconds, 150, "ms"),
            AtMost("command-ack-p99", load.CommandAckP99Milliseconds, 250, "ms"),
            AtMost("server-cpu", load.ServerCpuPercent, 85, "%"),
            AtMost("load-runner-cpu", load.LoadRunnerCpuPercent, 85, "%"),
            AtMost("memory-growth", load.MemoryGrowthPercent, 5, "%"),
            Equal("failed-load-clients", load.FailedClients, 0),
        };
        return new PerformanceVerdict(checks.All(check => check.Passed), checks);
    }

    private static PerformanceCheck AtMost(
        string name,
        double measured,
        double maximum,
        string unit = "") => new(
            name,
            measured,
            $"<= {maximum:0.###}{unit}",
            double.IsFinite(measured) && measured >= 0 && measured <= maximum);

    private static PerformanceCheck AtLeast(
        string name,
        double measured,
        double minimum,
        string unit = "") => new(
            name,
            measured,
            $">= {minimum:0.###}{unit}",
            double.IsFinite(measured) && measured >= minimum);

    private static PerformanceCheck Equal(
        string name,
        double measured,
        double expected,
        string unit = "") => new(
            name,
            measured,
            $"= {expected:0.###}{unit}",
            double.IsFinite(measured) && measured == expected);
}
