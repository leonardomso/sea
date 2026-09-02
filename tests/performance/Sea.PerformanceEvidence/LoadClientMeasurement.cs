namespace Sea.Performance;

public sealed record LoadClientMeasurement(
    int SchemaVersion,
    int AttemptedClients,
    int ConnectedClients,
    int RetainedClients,
    int ActiveClients,
    int DormantClients,
    double CommandAckP95Milliseconds,
    double CommandAckP99Milliseconds,
    double LoadRunnerCpuPercent,
    int FailedClients);
