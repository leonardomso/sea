namespace Sea.LoadTests;

public sealed class LoadClientPopulation : IAsyncDisposable
{
    private readonly LoadClientSession[] sessions;

    private LoadClientPopulation(LoadClientSession[] sessions)
    {
        this.sessions = sessions;
    }

    public IReadOnlyList<LoadClientSession> Sessions => sessions;

    public static async Task<LoadClientPopulation> ConnectAsync(
        LoadRunOptions options,
        LoadRunTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tracker);
        var startedAt = DateTimeOffset.UtcNow;
        var tasks = Enumerable.Range(0, options.TotalClients)
            .Select(index => ConnectOneAsync(index, startedAt, options, tracker))
            .ToArray();
        var connectedSessions = await Task.WhenAll(tasks).ConfigureAwait(false);
        return new LoadClientPopulation(connectedSessions);
    }

    public void RecordRetention(LoadRunTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        foreach (var session in sessions)
        {
            if (session.Client.IsConnected)
            {
                tracker.RecordRetained();
            }
            else
            {
                tracker.RecordFailure(new LoadPhaseInvariantException(
                    "connection retention",
                    $"Client {session.Plan.ClientIndex} disconnected before cleanup."));
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Task.WhenAll(sessions.Select(session =>
                session.Client.DisposeAsync().AsTask()))
            .ConfigureAwait(false);
    }

    private static async Task<LoadClientSession> ConnectOneAsync(
        int clientIndex,
        DateTimeOffset startedAt,
        LoadRunOptions options,
        LoadRunTracker tracker)
    {
        var dueAt = startedAt + LoadRampPolicy.DelayFor(
            clientIndex,
            options.TotalClients,
            options.RampDuration);
        var delay = dueAt - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay).ConfigureAwait(false);
        }

        tracker.RecordAttempt();
        SpacetimeLoadClient? client = null;
        var plan = LoadWorkloadPlan.Create(
            clientIndex,
            options.TotalClients,
            options.ActiveClients);
        try
        {
            client = await SpacetimeLoadClient.ConnectAsync(options.Server, options.Database)
                .ConfigureAwait(false);
            tracker.RecordConnected(client.ConnectionDuration);
            await client.LoadPlayerAsync(plan.Mode == LoadClientMode.Sailing)
                .ConfigureAwait(false);
            if (plan.Mode == LoadClientMode.Sailing &&
                await client.TryStartSailingAsync(plan).ConfigureAwait(false) is null)
            {
                throw new LoadPhaseInvariantException(
                    "initial sailing command",
                    $"Client {clientIndex} could not start a valid course.");
            }
            return new LoadClientSession(
                client,
                plan,
                options.ActiveClients);
        }
        catch (Exception error)
        {
            tracker.RecordFailure(error);
            if (client is not null)
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }
}
