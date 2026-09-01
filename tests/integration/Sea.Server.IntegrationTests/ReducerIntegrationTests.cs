using System.Diagnostics;
using SpacetimeDB;
using SpacetimeDB.Types;
using Xunit;

namespace Sea.Server.IntegrationTests;

public sealed class ReducerIntegrationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [Fact]
    public void RejectedCommandIsAcknowledgedWithoutUnhandledErrorOrStateChange()
    {
        using var client = IntegrationClient.Connect();
        client.LoadPlayer();
        var before = client.OwnedShip();

        var result = client.IssueBroadside(commandId: 1);
        var after = client.OwnedShip();

        Assert.False(result.Accepted);
        Assert.Equal(12, result.RejectionCode);
        Assert.False(result.IsDuplicate);
        Assert.Null(client.UnhandledReducerError);
        Assert.Equal(before.EntityId, after.EntityId);
        Assert.Equal(before.Hull, after.Hull);
        Assert.Equal(before.TargetEntityId, after.TargetEntityId);
        Assert.Equal(before.NextPortFireTick, after.NextPortFireTick);
    }

    [Fact]
    public void DuplicateAndStaleCommandsNeverApplyAnEffectTwice()
    {
        using var client = IntegrationClient.Connect();
        client.LoadPlayer();

        var first = client.IssueSetCourse(commandId: 1, x: 10f, y: 10f);
        var afterFirst = client.OwnedShip();
        var duplicate = client.IssueSetCourse(commandId: 1, x: -10f, y: -10f);
        var afterDuplicate = client.OwnedShip();
        var stale = client.IssueSetCourse(commandId: 0, x: -20f, y: -20f);
        var afterStale = client.OwnedShip();

        Assert.True(first.Accepted);
        Assert.True(duplicate.Accepted);
        Assert.True(duplicate.IsDuplicate);
        Assert.False(stale.Accepted);
        Assert.Equal(1, stale.RejectionCode);
        Assert.Equal(afterFirst.DestinationX, afterDuplicate.DestinationX);
        Assert.Equal(afterFirst.DestinationY, afterDuplicate.DestinationY);
        Assert.Equal(afterFirst.DestinationX, afterStale.DestinationX);
        Assert.Equal(afterFirst.DestinationY, afterStale.DestinationY);
        Assert.Null(client.UnhandledReducerError);
    }

    private sealed class IntegrationClient : IDisposable
    {
        private readonly DbConnection connection;
        private readonly Identity identity;
        private readonly List<CommandResultEvent> commandResults = [];
        private bool subscribed;
        private Exception? failure;

        private IntegrationClient(DbConnection connection, Identity identity)
        {
            this.connection = connection;
            this.identity = identity;
            connection.OnUnhandledReducerError += (_, error) => UnhandledReducerError = error;
            connection.Db.CommandResultEvent.OnInsert += (_, result) =>
            {
                if (result.Owner == identity)
                {
                    commandResults.Add(result);
                }
            };
        }

        public Exception? UnhandledReducerError { get; private set; }

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
                    "SELECT * FROM player_command_state",
                    "SELECT * FROM command_result_event",
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

        public CommandResultEvent IssueBroadside(ulong commandId) => Issue(
            commandId,
            new ShipCommand.FireBroadside(new FireBroadsideCommand("port", "hull")));

        public CommandResultEvent IssueSetCourse(ulong commandId, float x, float y) => Issue(
            commandId,
            new ShipCommand.SetCourse(new SetCourseCommand(x, y)));

        private CommandResultEvent Issue(ulong commandId, ShipCommand command)
        {
            var resultCount = commandResults.Count;
            connection.Reducers.IssueShipCommand(new CommandEnvelope(commandId, command));
            PumpUntil(connection, () => commandResults.Count > resultCount || failure is not null);
            ThrowIfFailed();
            return commandResults[^1];
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
