namespace Sea.Performance;

public sealed record PerformanceVerdict(
    bool Passed,
    IReadOnlyList<PerformanceCheck> Checks);
