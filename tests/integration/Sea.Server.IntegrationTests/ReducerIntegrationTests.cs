using System.Diagnostics;
using SpacetimeDB;
using SpacetimeDB.Types;
using Xunit;

namespace Sea.Server.IntegrationTests;

public sealed class ReducerIntegrationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [Fact]
    public void RejectedReducerIsObservedAndDoesNotChangeShipState()
    {
        using var client = IntegrationClient.Connect();
        client.LoadPlayer();
        var before = client.OwnedShip();

        var error = client.InvokeRejectedBroadside();
        var after = client.OwnedShip();

        Assert.Contains("target", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before.EntityId, after.EntityId);
        Assert.Equal(before.Hull, after.Hull);
        Assert.Equal(before.TargetEntityId, after.TargetEntityId);
        Assert.Equal(before.NextPortFireTick, after.NextPortFireTick);
    }

    private sealed class IntegrationClient : IDisposable
    {
        private readonly DbConnection connection;
        private readonly Identity identity;
        private bool subscribed;
        private Exception? failure;

        private IntegrationClient(DbConnection connection, Identity identity)
        {
            this.connection = connection;
            this.identity = identity;
        }

        public static IntegrationClient Connect()
        {
            var database = RequiredEnvironment("SEA_TEST_DATABASE");
            var server = Environment.GetEnvironmentVariable("SEA_TEST_SERVER")
                ?? "http://host.docker.internal:3000";
            DbConnection? connectedClient = null;
            Identity connectedIdentityValue = default;
            Exception? connectionError = null;

            var pendingConnection = DbConnection.Builder()
                .OnConnect((value, connectedIdentity, _) =>
                {
                    connectedClient = value;
                    connectedIdentityValue = connectedIdentity;
                })
                .OnConnectError(error => connectionError = error)
                .WithUri(server)
                .WithDatabaseName(database)
                .Build();

            PumpUntil(pendingConnection, () =>
                connectedClient is not null || connectionError is not null);
            if (connectionError is not null)
            {
                pendingConnection.Disconnect();
                throw new InvalidOperationException("Could not connect to the test database.", connectionError);
            }

            return new IntegrationClient(connectedClient!, connectedIdentityValue);
        }

        public void LoadPlayer()
        {
            connection.SubscriptionBuilder()
                .OnApplied(_ => subscribed = true)
                .OnError((_, error) => failure = error)
                .Subscribe([
                    "SELECT * FROM player_ownership",
                    "SELECT * FROM ship",
                ]);
            PumpUntil(connection, () => subscribed || failure is not null);
            ThrowIfFailed();

            connection.Reducers.LoadPlayer();
            PumpUntil(connection, () =>
                connection.Db.PlayerOwnership.Owner.Find(identity) is not null ||
                failure is not null);
            ThrowIfFailed();
        }

        public Ship OwnedShip()
        {
            var ownership = connection.Db.PlayerOwnership.Owner.Find(identity)
                ?? throw new InvalidOperationException("The integration identity has no ownership row.");
            return connection.Db.Ship.EntityId.Find(ownership.ShipEntityId)
                ?? throw new InvalidOperationException("The integration identity has no ship row.");
        }

        public Exception InvokeRejectedBroadside()
        {
            Exception? reducerError = null;
            void HandleError(ReducerEventContext _, Exception error) => reducerError = error;

            connection.OnUnhandledReducerError += HandleError;
            try
            {
                connection.Reducers.FireBroadside("port", "hull");
                PumpUntil(connection, () => reducerError is not null);
                return reducerError!;
            }
            finally
            {
                connection.OnUnhandledReducerError -= HandleError;
            }
        }

        public void Dispose() => connection.Disconnect();

        private void ThrowIfFailed()
        {
            if (failure is not null)
            {
                throw new InvalidOperationException("The integration subscription failed.", failure);
            }
        }

        private static string RequiredEnvironment(string name) =>
            Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"Environment variable {name} is required.");

        private static void PumpUntil(DbConnection connection, Func<bool> condition)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!condition())
            {
                connection.FrameTick();
                if (stopwatch.Elapsed > Timeout)
                {
                    throw new TimeoutException("SpacetimeDB integration operation timed out.");
                }

                Thread.Sleep(5);
            }

            connection.FrameTick();
        }
    }
}
