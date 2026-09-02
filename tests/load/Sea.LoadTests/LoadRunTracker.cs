using System.Collections.Concurrent;
using System.Diagnostics;

namespace Sea.LoadTests;

public sealed class LoadRunTracker
{
    private readonly ConcurrentBag<double> connectionLatencies = [];
    private readonly ConcurrentBag<double> acknowledgementLatencies = [];
    private readonly ConcurrentDictionary<string, int> failures = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> failureSamples =
        new(StringComparer.Ordinal);
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private readonly DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
    private int attempted;
    private int connected;
    private int retained;

    public void RecordAttempt() => Interlocked.Increment(ref attempted);

    public void RecordConnected(TimeSpan latency)
    {
        Interlocked.Increment(ref connected);
        connectionLatencies.Add(latency.TotalMilliseconds);
    }

    public void RecordAcknowledgement(TimeSpan latency) =>
        acknowledgementLatencies.Add(latency.TotalMilliseconds);

    public void RecordRetained() => Interlocked.Increment(ref retained);

    public void RecordFailure(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var key = error switch
        {
            LoadPhaseTimeoutException timeout =>
                $"{error.GetType().Name}:{timeout.Phase}",
            LoadPhaseInvariantException invariant =>
                $"{error.GetType().Name}:{invariant.Phase}",
            _ => error.GetType().Name,
        };
        failures.AddOrUpdate(key, 1, static (_, count) => count + 1);
        failureSamples.TryAdd(key, error.Message);
    }

    public LoadExecutionEvidence Snapshot(
        int activeClients,
        int dormantClients,
        double loadRunnerCpuPercent = 0)
    {
        var connections = LatencyPercentiles.Calculate(connectionLatencies);
        var acknowledgements = LatencyPercentiles.Calculate(acknowledgementLatencies);
        return new LoadExecutionEvidence(
            1,
            startedAtUtc,
            elapsed.Elapsed.TotalSeconds,
            Volatile.Read(ref attempted),
            Volatile.Read(ref connected),
            Volatile.Read(ref retained),
            activeClients,
            dormantClients,
            connections.P95Milliseconds,
            connections.P99Milliseconds,
            acknowledgements.P95Milliseconds,
            acknowledgements.P99Milliseconds,
            loadRunnerCpuPercent,
            failures.Values.Sum(),
            failures.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            failureSamples.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }
}
