using SpacetimeDB;
using SpacetimeDB.Types;
using System.Collections.Concurrent;

namespace Sea.LoadTests;

internal sealed class SpacetimeConnectionPump
{
    private readonly PumpShard[] shards;
    private long nextRegistration;

    private SpacetimeConnectionPump()
    {
        shards = Enumerable.Range(0, ConnectionPumpPolicy.DefaultShardCount)
            .Select(_ => new PumpShard(ConnectionPumpPolicy.PumpInterval))
            .ToArray();
    }

    public static SpacetimeConnectionPump Shared { get; } = new();

    public ConnectionRegistration Register(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var registration = Interlocked.Increment(ref nextRegistration);
        var shardIndex = ConnectionPumpPolicy.ShardIndex(registration, shards.Length);
        return shards[shardIndex].Register(connection);
    }

    private sealed class PumpShard
    {
        private readonly ConcurrentDictionary<long, ConnectionEntry> connections = new();
        private readonly TimeSpan interval;
        private long nextId;

        public PumpShard(TimeSpan interval)
        {
            this.interval = interval;
            _ = Task.Run(PumpAsync);
        }

        public ConnectionRegistration Register(DbConnection connection)
        {
            var id = Interlocked.Increment(ref nextId);
            var entry = new ConnectionEntry(connection, new ConnectionAccessGate());
            if (!connections.TryAdd(id, entry))
            {
                throw new InvalidOperationException("Could not register a load-test connection.");
            }

            return new ConnectionRegistration(connections, id, entry);
        }

        private async Task PumpAsync()
        {
            while (true)
            {
                foreach (var pair in connections)
                {
                    try
                    {
                        pair.Value.Gate.Execute(pair.Value.Connection.FrameTick);
                    }
                    catch (Exception error)
                    {
                        connections.TryRemove(pair.Key, out _);
                        Console.Error.WriteLine(
                            $"Load connection pump removed a failed client: {error.GetType().Name}.");
                    }
                }

                await Task.Delay(interval).ConfigureAwait(false);
            }
        }
    }

    internal sealed record ConnectionEntry(
        DbConnection Connection,
        ConnectionAccessGate Gate);

    internal sealed class ConnectionRegistration(
        ConcurrentDictionary<long, ConnectionEntry> connections,
        long id,
        ConnectionEntry entry) : IDisposable
    {
        private int disposed;

        public void Execute(Action action) => entry.Gate.Execute(action);

        public T Execute<T>(Func<T> action) => entry.Gate.Execute(action);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                connections.TryRemove(id, out _);
            }
        }
    }
}
