using SpacetimeDB;
using SpacetimeDB.Types;

namespace Sea.LoadTests;

public sealed class SpacetimeLoadClient : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    private readonly DbConnection connection;
    private readonly CancellationTokenSource stop = new();
    private readonly Task pumpTask;
    private readonly Identity identity;
    private readonly List<CommandResultEvent> commandResults = [];
    private PlayerOwnership? ownership;

    private SpacetimeLoadClient(DbConnection connection, Identity identity)
    {
        this.connection = connection;
        this.identity = identity;
        pumpTask = Task.Run(PumpAsync);
    }

    public static async Task<SpacetimeLoadClient> ConnectAsync(string server, string database)
    {
        var connected = new TaskCompletionSource<(DbConnection, Identity)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = DbConnection.Builder()
            .OnConnect((value, identity, _) => connected.TrySetResult((value, identity)))
            .OnConnectError(error => connected.TrySetException(error))
            .WithUri(server)
            .WithDatabaseName(database)
            .Build();

        using var timeout = new CancellationTokenSource(Timeout);
        try
        {
            while (!connected.Task.IsCompleted)
            {
                pending.FrameTick();
                await Task.Delay(2, timeout.Token).ConfigureAwait(false);
            }

            var result = await connected.Task.ConfigureAwait(false);
            return new SpacetimeLoadClient(result.Item1, result.Item2);
        }
        catch
        {
            pending.Disconnect();
            throw;
        }
    }

    public async Task LoadPlayerAsync()
    {
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var shipSubscribed = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var failed = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var ownerLiteral = ToIdentitySqlLiteral(identity);

        connection.Db.PlayerOwnership.OnInsert += OnOwnership;
        connection.Db.CommandResultEvent.OnInsert += OnCommandResult;
        connection.SubscriptionBuilder()
            .OnApplied(_ => connection.Reducers.LoadPlayer())
            .OnError((_, error) => failed.TrySetResult(error))
            .Subscribe([
                $"SELECT * FROM player_ownership WHERE owner = {ownerLiteral}",
                $"SELECT * FROM command_result_event WHERE owner = {ownerLiteral}",
            ]);

        using var timeout = new CancellationTokenSource(Timeout);
        var completed = await Task.WhenAny(loaded.Task, failed.Task)
            .WaitAsync(timeout.Token)
            .ConfigureAwait(false);
        connection.Db.PlayerOwnership.OnInsert -= OnOwnership;
        if (completed == failed.Task)
        {
            throw await failed.Task.ConfigureAwait(false);
        }

        connection.SubscriptionBuilder()
            .OnApplied(_ => shipSubscribed.TrySetResult())
            .OnError((_, error) => failed.TrySetResult(error))
            .Subscribe([
                $"SELECT * FROM ship WHERE entity_id = {ownership!.ShipEntityId}",
            ]);
        completed = await Task.WhenAny(shipSubscribed.Task, failed.Task)
            .WaitAsync(timeout.Token)
            .ConfigureAwait(false);
        if (completed == failed.Task)
        {
            throw await failed.Task.ConfigureAwait(false);
        }

        return;

        void OnOwnership(EventContext _, PlayerOwnership ownership)
        {
            if (ownership.Owner == identity)
            {
                this.ownership = ownership;
                loaded.TrySetResult();
            }
        }

        void OnCommandResult(EventContext _, CommandResultEvent result)
        {
            if (result.Owner == identity)
            {
                commandResults.Add(result);
            }
        }
    }

    private static string ToIdentitySqlLiteral(Identity value)
    {
        var text = value.ToString();
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? text
            : "0x" + text;
    }

    public async Task StartSailingAsync()
    {
        var ownedShip = ownership is null
            ? null
            : connection.Db.Ship.EntityId.Find(ownership.ShipEntityId);
        if (ownedShip is null)
        {
            throw new InvalidOperationException("The load client has no subscribed ship.");
        }

        var destinations = new[]
        {
            (ownedShip.PositionX >= 0f ? -90f : 90f, ownedShip.PositionY),
            (ownedShip.PositionX, ownedShip.PositionY >= 0f ? -90f : 90f),
            (ownedShip.PositionX >= 0f ? -90f : 90f, 90f),
            (ownedShip.PositionX >= 0f ? -90f : 90f, -90f),
        };
        for (var index = 0; index < destinations.Length; index++)
        {
            var commandId = (ulong)index + 1;
            var resultCount = commandResults.Count;
            var destination = destinations[index];
            connection.Reducers.IssueShipCommand(new CommandEnvelope(
                commandId,
                new ShipCommand.SetCourse(new SetCourseCommand(destination.Item1, destination.Item2))));

            using var timeout = new CancellationTokenSource(Timeout);
            while (commandResults.Count == resultCount)
            {
                await Task.Delay(2, timeout.Token).ConfigureAwait(false);
            }

            if (commandResults[^1].Accepted)
            {
                return;
            }
        }

        throw new InvalidOperationException("No deterministic load-test course was accepted.");
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        await pumpTask.ConfigureAwait(false);
        connection.Disconnect();
        stop.Dispose();
    }

    private async Task PumpAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            connection.FrameTick();
            try
            {
                await Task.Delay(2, stop.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
