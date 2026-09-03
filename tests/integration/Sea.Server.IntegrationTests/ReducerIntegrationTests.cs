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

        var result = client.IssueFire(commandId: 1);
        var after = client.OwnedShip();

        Assert.False(result.Accepted);
        Assert.Equal(11, result.RejectionCode);
        Assert.False(result.IsDuplicate);
        Assert.Null(client.UnhandledReducerError);
        Assert.Equal(before.EntityId, after.EntityId);
        Assert.Equal(before.Hull, after.Hull);
        Assert.Equal(before.TargetEntityId, after.TargetEntityId);
        Assert.Equal(before.ReadyVolleys, after.ReadyVolleys);
        Assert.Equal(before.ReloadProgressTicks, after.ReloadProgressTicks);
    }

    [Fact]
    public void PlayerClockAnchorsOnceWhileThePrivateSimulationContinues()
    {
        using var client = IntegrationClient.Connect();
        client.LoadPlayer();
        var clock = client.OwnedClock();
        var start = client.OwnedShip();

        var destinationX = MathF.Abs(start.PositionX) > 1f || MathF.Abs(start.PositionY) > 1f
            ? 0f
            : 20f;
        var course = client.IssueSetCourse(1, destinationX, 0f);
        Assert.True(
            course.Accepted,
            $"SetCourse was rejected with code {course.RejectionCode}.");
        PumpAllUntil([client], () =>
        {
            var moved = client.OwnedShip();
            return MathF.Abs(moved.PositionX - start.PositionX) > 0.1f ||
                MathF.Abs(moved.PositionY - start.PositionY) > 0.1f;
        });

        Assert.Equal(clock.Tick, client.OwnedClock().Tick);
        Assert.Equal(10u, clock.TickRateHz);
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
            var course = client.IssueSetCourse(1, 0f, 0f);
            Assert.True(
                course.Accepted,
                $"SetCourse was rejected with code {course.RejectionCode}.");
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
        Assert.Equal(8, first.IssueFire(3).RejectionCode);

        // Boarding retired with the magazine rework but stays on the wire, so a stale client
        // gets a stable "not available" rather than a silently reinterpreted command.
        Assert.Equal(21, first.IssueBoarding(4).RejectionCode);
        Assert.All(clients, client => Assert.True(client.HasOnlyBoundedSpatialRows(3, 5)));
    }

    [Fact]
    public void TwelveNpcShipsSeedAndBeginDeterministicRoaming()
    {
        using var client = IntegrationClient.Connect();

        // An empty world skips its dispatch, so the NPCs only roam once a player is
        // loaded. Connecting a socket is not enough on its own.
        client.LoadPlayer();
        client.SubscribeNpcWorld();
        var initial = client.NpcPositions();

        Assert.Equal(12, initial.Count);
        Assert.Equal(4, client.NpcCount(1));
        Assert.Equal(4, client.NpcCount(2));
        Assert.Equal(4, client.NpcCount(3));

        PumpAllUntil([client], () =>
        {
            var current = client.NpcPositions();
            return current.Any(pair =>
                initial.TryGetValue(pair.Key, out var position) &&
                (MathF.Abs(pair.Value.X - position.X) > 0.01f ||
                 MathF.Abs(pair.Value.Y - position.Y) > 0.01f));
        });

        Assert.Equal(12, client.NpcAiCount());
        Assert.Null(client.UnhandledReducerError);
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

}
