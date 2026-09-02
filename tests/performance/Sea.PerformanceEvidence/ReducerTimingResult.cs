namespace Sea.Performance;

public sealed record ReducerTimingResult(
    string Name,
    int SampleCount,
    double P95Microseconds,
    double P99Microseconds);
