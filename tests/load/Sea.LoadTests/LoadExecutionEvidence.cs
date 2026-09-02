namespace Sea.LoadTests;

public sealed record LoadExecutionEvidence(
    int SchemaVersion,
    DateTimeOffset StartedAtUtc,
    double ElapsedSeconds,
    int AttemptedClients,
    int ConnectedClients,
    int RetainedClients,
    int ActiveClients,
    int DormantClients,
    double ConnectionP95Milliseconds,
    double ConnectionP99Milliseconds,
    double CommandAckP95Milliseconds,
    double CommandAckP99Milliseconds,
    double LoadRunnerCpuPercent,
    int FailedClients,
    IReadOnlyDictionary<string, int> Failures,
    IReadOnlyDictionary<string, string> FailureSamples);
