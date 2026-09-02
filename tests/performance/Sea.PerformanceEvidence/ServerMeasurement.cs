namespace Sea.Performance;

public sealed record ServerMeasurement(
    int SchemaVersion,
    int ActiveShips,
    int DormantShips,
    double TickP95Milliseconds,
    double TickP99Milliseconds,
    double ServerCpuPercent,
    double MemoryGrowthPercent);
