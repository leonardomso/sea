using System.Diagnostics;
using SpacetimeDB;
using SpacetimeDB.Types;
using Xunit;

namespace Sea.Server.IntegrationTests;

public sealed class ReducerIntegrationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(45);

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

    [Fact]
    public void FourClientsShareBoundedWorldInterestWithoutPlayerCombat()
    {
        using var first = IntegrationClient.Connect();
        using var second = IntegrationClient.Connect();
        using var third = IntegrationClient.Connect();
        using var fourth = IntegrationClient.Connect();
        var clients = new[] { first, second, third, fourth };

        foreach (var client in clients)
        {
            client.LoadPlayer();
            Assert.True(client.IssueSetCourse(1, 0f, 0f).Accepted);
        }

        PumpAllUntil(clients, () => clients.All(client => client.IsNear(0f, 0f, 14f)));
        foreach (var client in clients)
        {
            client.SubscribeSpatial(chunkX: 4, chunkY: 4, radius: 1);
        }

        var expectedPlayerShips = clients
            .Select(client => client.OwnedShip().EntityId)
            .ToHashSet();
        PumpAllUntil(clients, () => clients.All(client =>
            expectedPlayerShips.IsSubsetOf(client.VisiblePlayerShipIds())));
        var target = second.OwnedShip().EntityId;
        Assert.True(first.IssueSelectTarget(2, target).Accepted);
        Assert.Equal(8, first.IssueBroadside(3).RejectionCode);
        Assert.Equal(8, first.IssueBoarding(4).RejectionCode);
        Assert.All(clients, client => Assert.True(client.HasOnlyBoundedSpatialRows(3, 5)));
    }

    private static void PumpAllUntil(
        IReadOnlyCollection<IntegrationClient> clients,
        Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            foreach (var client in clients)
            {
                client.PumpOnce();
            }

            if (stopwatch.Elapsed > Timeout)
            {
                throw new TimeoutException("Shared-world integration operation timed out.");
            }

            Thread.Sleep(5);
        }
    }

    private sealed class IntegrationClient : IDisposable
    {
        private readonly DbConnection connection;
        private readonly Identity identity;
        private readonly List<CommandResultEvent> commandResults = [];
        private bool subscribed;
        private bool playerSubscribed;
        private bool spatialSubscribed;
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
            var ownerLiteral = IdentitySqlLiteral(identity);
            connection.SubscriptionBuilder()
                .OnApplied(_ => subscribed = true)
                .OnError((_, error) => failure = error)
                .Subscribe([
                    $"SELECT * FROM player_ownership WHERE owner = {ownerLiteral}",
                    $"SELECT * FROM player_command_state WHERE owner = {ownerLiteral}",
                    $"SELECT * FROM command_result_event WHERE owner = {ownerLiteral}",
                ]);
            PumpUntil(connection, () => subscribed || failure is not null);
            ThrowIfFailed();

            connection.Reducers.LoadPlayer();
            PumpUntil(connection, () =>
                connection.Db.PlayerOwnership.Owner.Find(identity) is not null ||
                failure is not null);
            ThrowIfFailed();

            var ownership = connection.Db.PlayerOwnership.Owner.Find(identity)!;
            connection.SubscriptionBuilder()
                .OnApplied(_ => playerSubscribed = true)
                .OnError((_, error) => failure = error)
                .Subscribe([
                    $"SELECT * FROM ship WHERE entity_id = {ownership.ShipEntityId}",
                    $"SELECT * FROM inventory WHERE ship_entity_id = {ownership.ShipEntityId}",
                    $"SELECT * FROM ship_status WHERE ship_entity_id = {ownership.ShipEntityId}",
                    $"SELECT * FROM cooldown WHERE ship_entity_id = {ownership.ShipEntityId}",
                    $"SELECT * FROM ship_channel WHERE ship_entity_id = {ownership.ShipEntityId}",
                ]);
            PumpUntil(connection, () => playerSubscribed || failure is not null);
            ThrowIfFailed();
        }

        public void SubscribeSpatial(int chunkX, int chunkY, int radius)
        {
            var minimumX = chunkX - radius;
            var maximumX = chunkX + radius;
            var minimumY = chunkY - radius;
            var maximumY = chunkY + radius;
            var bounds = $"chunk_x >= {minimumX} AND chunk_x <= {maximumX} " +
                $"AND chunk_y >= {minimumY} AND chunk_y <= {maximumY}";
            spatialSubscribed = false;
            connection.SubscriptionBuilder()
                .OnApplied(_ => spatialSubscribed = true)
                .OnError((_, error) => failure = error)
                .Subscribe([
                    $"SELECT * FROM ship WHERE is_active = true AND {bounds}",
                    $"SELECT * FROM world_object WHERE is_active = true AND {bounds}",
                ]);
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

        public CommandResultEvent IssueSelectTarget(ulong commandId, ulong entityId) => Issue(
            commandId,
            new ShipCommand.SelectTarget(new SelectTargetCommand(entityId)));

        public CommandResultEvent IssueBoarding(ulong commandId) => Issue(
            commandId,
            new ShipCommand.StartBoarding(new StartBoardingCommand()));

        public bool IsNear(float x, float y, float radius)
        {
            var ship = OwnedShip();
            var deltaX = ship.PositionX - x;
            var deltaY = ship.PositionY - y;
            return deltaX * deltaX + deltaY * deltaY <= radius * radius;
        }

        public ulong[] VisiblePlayerShipIds() => connection.Db.Ship.Iter()
            .Where(ship => ship.FactionCode == 1)
            .Select(ship => ship.EntityId)
            .ToArray();

        public bool HasOnlyBoundedSpatialRows(int minimumChunk, int maximumChunk)
        {
            if (!spatialSubscribed)
            {
                return false;
            }

            return connection.Db.Ship.Iter().All(ship =>
                       ship.ChunkX >= minimumChunk && ship.ChunkX <= maximumChunk &&
                       ship.ChunkY >= minimumChunk && ship.ChunkY <= maximumChunk) &&
                   connection.Db.WorldObject.Iter().All(worldObject =>
                       worldObject.ChunkX >= minimumChunk && worldObject.ChunkX <= maximumChunk &&
                       worldObject.ChunkY >= minimumChunk && worldObject.ChunkY <= maximumChunk);
        }

        public void PumpOnce() => connection.FrameTick();

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

        private static string IdentitySqlLiteral(Identity value)
        {
            var text = value.ToString();
            return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? text
                : "0x" + text;
        }

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
