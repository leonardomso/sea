namespace Sea.Performance;

public sealed record LoadEvidence(
    int AttemptedClients,
    int ConnectedClients,
    int RetainedClients,
    int ActiveShips,
    int DormantShips,
    double TickP95Milliseconds,
    double TickP99Milliseconds,
    double CommandAckP95Milliseconds,
    double CommandAckP99Milliseconds,
    double ServerCpuPercent,
    double LoadRunnerCpuPercent,
    double MemoryGrowthPercent,
    int FailedClients);
