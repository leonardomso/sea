using SpacetimeDB;
using SpacetimeDB.Types;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Sea.LoadTests;

public sealed class SpacetimeLoadClient : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);
    private readonly DbConnection connection;
    private readonly SpacetimeConnectionPump.ConnectionRegistration pumpRegistration;
    private readonly Identity identity;
    private readonly ConcurrentDictionary<ulong, TaskCompletionSource<CommandResultEvent>>
        pendingCommands = new();
    private ulong nextCommandId;
    private ulong ownedShipEntityId;
    private uint courseCycle;

    private SpacetimeLoadClient(
        DbConnection connection,
        Identity identity,
        TimeSpan connectionDuration,
        SpacetimeConnectionPump.ConnectionRegistration pumpRegistration)
    {
        this.connection = connection;
        this.identity = identity;
        ConnectionDuration = connectionDuration;
        this.pumpRegistration = pumpRegistration;
        pumpRegistration.Execute(() =>
            connection.Db.CommandResultEvent.OnInsert += OnCommandResult);
    }

    public TimeSpan ConnectionDuration { get; }

    public bool IsConnected => pumpRegistration.Execute(() => connection.IsActive);

    public static async Task<SpacetimeLoadClient> ConnectAsync(string server, string database)
    {
        var connected = new TaskCompletionSource<(DbConnection, Identity)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stopwatch = Stopwatch.StartNew();
        var pending = DbConnection.Builder()
            .OnConnect((value, identity, _) => connected.TrySetResult((value, identity)))
            .OnConnectError(error => connected.TrySetException(error))
            .WithUri(server)
            .WithDatabaseName(database)
            .WithConfirmedReads(false)
            .Build();
        var pendingPumpRegistration = SpacetimeConnectionPump.Shared.Register(pending);

        using var timeout = new CancellationTokenSource(Timeout);
        try
        {
            var result = await WaitForPhase(
                    connected.Task,
                    "connection",
                    timeout.Token)
                .ConfigureAwait(false);
            return new SpacetimeLoadClient(
                result.Item1,
                result.Item2,
                stopwatch.Elapsed,
                pendingPumpRegistration);
        }
        catch
        {
            pendingPumpRegistration.Dispose();
            pending.Disconnect();
            throw;
        }
    }

    public async Task LoadPlayerAsync(bool subscribeActiveShip)
    {
        await RequestPlayerLoadAsync().ConfigureAwait(false);
        if (subscribeActiveShip)
        {
            await SubscribeActiveShipAsync().ConfigureAwait(false);
        }
    }

    private async Task RequestPlayerLoadAsync()
    {
        var reducerCompleted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var failed = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        pumpRegistration.Execute(() =>
        {
            connection.Reducers.OnLoadPlayer += OnLoadPlayer;
            connection.Reducers.LoadPlayer();
        });

        using var timeout = new CancellationTokenSource(Timeout);
        try
        {
            await WaitForOutcome(
                    reducerCompleted.Task,
                    failed.Task,
                    "load player reducer",
                    timeout.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            pumpRegistration.Execute(() =>
                connection.Reducers.OnLoadPlayer -= OnLoadPlayer);
        }

        return;

        void OnLoadPlayer(ReducerEventContext context)
        {
            switch (context.Event.Status)
            {
                case Status.Committed:
                    reducerCompleted.TrySetResult();
                    break;
                case Status.Failed(var reason):
                    failed.TrySetResult(new LoadPhaseInvariantException(
                        "load player reducer",
                        reason));
                    break;
                case Status.OutOfEnergy:
                    failed.TrySetResult(new LoadPhaseInvariantException(
                        "load player reducer energy",
                        "The load-player reducer ran out of energy."));
                    break;
            }
        }
    }

    private async Task SubscribeActiveShipAsync()
    {
        var ownerLiteral = ToIdentitySqlLiteral(identity);
        await SubscribeAsync(
                LoadSubscriptionPlan.Ownership(ownerLiteral),
                "ownership subscription")
            .ConfigureAwait(false);
        ownedShipEntityId = pumpRegistration.Execute(() =>
            connection.Db.PlayerOwnership.Owner.Find(identity)?.ShipEntityId ?? 0);
        if (ownedShipEntityId == 0)
        {
            throw new LoadPhaseInvariantException(
                "ownership subscription",
                "The active load client did not receive its ownership row.");
        }

        await SubscribeAsync(
                LoadSubscriptionPlan.ActiveShip(ownedShipEntityId, ownerLiteral),
                "active ship subscription")
            .ConfigureAwait(false);
    }

    private async Task SubscribeAsync(string[] queries, string phase)
    {
        var subscribed = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var failed = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        pumpRegistration.Execute(() => connection.SubscriptionBuilder()
            .OnApplied(_ => subscribed.TrySetResult())
            .OnError((_, error) => failed.TrySetResult(error))
            .Subscribe(queries));

        using var timeout = new CancellationTokenSource(Timeout);
        await WaitForOutcome(
                subscribed.Task,
                failed.Task,
                phase,
                timeout.Token)
            .ConfigureAwait(false);
    }

    private void OnCommandResult(EventContext _, CommandResultEvent result)
    {
        if (result.Owner == identity && pendingCommands.TryRemove(
                result.CommandId,
                out var completion))
        {
            completion.TrySetResult(result);
        }
    }

    private static string ToIdentitySqlLiteral(Identity value)
    {
        var text = value.ToString();
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? text
            : "0x" + text;
    }

    public async Task<TimeSpan?> TryStartSailingAsync(LoadWorkloadPlan plan)
    {
        var movement = pumpRegistration.Execute(() =>
            connection.Db.ShipMovement.EntityId.Find(ownedShipEntityId));
        if (movement is null)
        {
            throw new LoadPhaseInvariantException(
                "active ship subscription",
                "The active load client lost its movement row.");
        }

        var destinations = plan.CourseAttempts(
            movement.PositionX,
            movement.PositionY,
            courseCycle++);
        for (var index = 0; index < destinations.Count; index++)
        {
            var commandId = ++nextCommandId;
            var destination = destinations[index];
            var completion = new TaskCompletionSource<CommandResultEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            if (!pendingCommands.TryAdd(commandId, completion))
            {
                throw new LoadPhaseInvariantException(
                    "command tracking",
                    "A command ID was reused before acknowledgement.");
            }

            var stopwatch = Stopwatch.StartNew();
            pumpRegistration.Execute(() =>
                connection.Reducers.IssueShipCommand(new CommandEnvelope(
                    commandId,
                    new ShipCommand.SetCourse(
                        new SetCourseCommand(destination.X, destination.Y)))));

            using var timeout = new CancellationTokenSource(Timeout);
            CommandResultEvent result;
            try
            {
                result = await WaitForPhase(
                        completion.Task,
                        "command acknowledgement",
                        timeout.Token)
                    .ConfigureAwait(false);
            }
            catch
            {
                pendingCommands.TryRemove(commandId, out _);
                throw;
            }

            if (result.Accepted)
            {
                return stopwatch.Elapsed;
            }
        }

        return null;
    }

    public ValueTask DisposeAsync()
    {
        pumpRegistration.Execute(() =>
        {
            connection.Db.CommandResultEvent.OnInsert -= OnCommandResult;
            connection.Disconnect();
        });
        foreach (var completion in pendingCommands.Values)
        {
            completion.TrySetCanceled();
        }

        pendingCommands.Clear();
        pumpRegistration.Dispose();
        return ValueTask.CompletedTask;
    }

    private static async Task<T> WaitForPhase<T>(
        Task<T> task,
        string phase,
        CancellationToken cancellationToken)
    {
        try
        {
            return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new LoadPhaseTimeoutException(phase);
        }
    }

    private static async Task WaitForOutcome(
        Task success,
        Task<Exception> failure,
        string phase,
        CancellationToken cancellationToken)
    {
        var completed = await WaitForPhase(
                Task.WhenAny(success, failure),
                phase,
                cancellationToken)
            .ConfigureAwait(false);
        if (completed == failure)
        {
            throw await failure.ConfigureAwait(false);
        }

        await success.ConfigureAwait(false);
    }
}
