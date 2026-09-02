namespace Sea.Performance;

public static class PerformanceEvidenceAssembler
{
    public static PerformanceRunEvidence Assemble(
        string machine,
        DateTimeOffset recordedAtUtc,
        LoadClientMeasurement loadClient,
        ServerMeasurement server,
        ClientEvidence macOS,
        ClientEvidence webGL,
        CorrectnessEvidence correctness,
        QualityEvidence quality)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machine);
        ArgumentNullException.ThrowIfNull(loadClient);
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(macOS);
        ArgumentNullException.ThrowIfNull(webGL);
        ArgumentNullException.ThrowIfNull(correctness);
        ArgumentNullException.ThrowIfNull(quality);
        if (loadClient.SchemaVersion != 1 || server.SchemaVersion != 1)
        {
            throw new InvalidDataException("Performance fragment schema version is unsupported.");
        }

        if (loadClient.ActiveClients != server.ActiveShips ||
            loadClient.DormantClients != server.DormantShips)
        {
            throw new InvalidDataException(
                "Load-client and server population measurements do not match.");
        }

        return new PerformanceRunEvidence(
            1,
            machine,
            recordedAtUtc,
            new LoadEvidence(
                loadClient.AttemptedClients,
                loadClient.ConnectedClients,
                loadClient.RetainedClients,
                server.ActiveShips,
                server.DormantShips,
                server.TickP95Milliseconds,
                server.TickP99Milliseconds,
                loadClient.CommandAckP95Milliseconds,
                loadClient.CommandAckP99Milliseconds,
                server.ServerCpuPercent,
                loadClient.LoadRunnerCpuPercent,
                server.MemoryGrowthPercent,
                loadClient.FailedClients),
            macOS,
            webGL,
            correctness,
            quality);
    }
}
