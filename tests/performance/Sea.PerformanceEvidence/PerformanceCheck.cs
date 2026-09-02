namespace Sea.Performance;

public sealed record PerformanceCheck(
    string Name,
    double Measured,
    string Requirement,
    bool Passed);
