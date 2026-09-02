namespace Sea.Performance;

public sealed record ReducerTimingSeries(
    string Name,
    IReadOnlyList<double> Microseconds,
    int MinimumSamples);
