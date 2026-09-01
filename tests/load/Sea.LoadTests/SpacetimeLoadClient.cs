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
        var failed = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.Db.PlayerOwnership.OnInsert += OnOwnership;
        connection.SubscriptionBuilder()
            .OnApplied(_ => connection.Reducers.LoadPlayer())
            .OnError((_, error) => failed.TrySetResult(error))
            .Subscribe(["SELECT * FROM player_ownership"]);

        using var timeout = new CancellationTokenSource(Timeout);
        var completed = await Task.WhenAny(loaded.Task, failed.Task)
            .WaitAsync(timeout.Token)
            .ConfigureAwait(false);
        connection.Db.PlayerOwnership.OnInsert -= OnOwnership;
        if (completed == failed.Task)
        {
            throw await failed.Task.ConfigureAwait(false);
        }

        return;

        void OnOwnership(EventContext _, PlayerOwnership ownership)
        {
            if (ownership.Owner == identity)
            {
                loaded.TrySetResult();
            }
        }
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
