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

        var mark = client.OpenWater();
        var course = client.IssueSetCourse(1, mark.X, mark.Y);
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

        // Only the first course has to be sailable. The duplicate is answered by its command
        // id and the stale one by the sequence, both before the water is ever looked at, so the
        // marks they carry are deliberately somewhere a course would never be accepted.
        var mark = client.OpenWater();
        var first = client.IssueSetCourse(commandId: 1, x: mark.X, y: mark.Y);
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

        Assert.All(clients, client => client.LoadPlayer());
        var rendezvous = first.OpenWater();
        foreach (var client in clients)
        {
            var course = client.IssueSetCourse(1, rendezvous.X, rendezvous.Y);
            Assert.True(
                course.Accepted,
                $"SetCourse was rejected with code {course.RejectionCode}.");
        }

        // Four hulls that stop on the same mark are four hulls in the same chunk, which is the
        // whole point: the window each of them then asks for is one chunk and its neighbours.
        PumpAllUntil(
            clients,
            () => clients.All(client => client.IsNear(rendezvous.X, rendezvous.Y, 14f)));
        foreach (var client in clients)
        {
            var berth = client.OwnedShip();
            client.SubscribeSpatial(berth.ChunkX, berth.ChunkY, radius: 1);
        }

        var expectedPlayerShips = clients
            .Select(client => client.OwnedShip().EntityId)
            .ToHashSet();
        PumpAllUntil(clients, () => clients.All(client =>
            expectedPlayerShips.IsSubsetOf(client.VisiblePlayerShipIds())));
        var target = second.OwnedShip().EntityId;
        Assert.True(first.IssueSelectTarget(2, target).Accepted);
        Assert.Equal(8, first.IssueFire(3).RejectionCode);

        // Hooks are turned down for the same reason the guns are: the other hull belongs to a
        // player, and SEA_5 9.1 keeps boarding to hostiles.
        Assert.Equal(8, first.IssueBoarding(4).RejectionCode);
        Assert.All(clients, client => Assert.True(client.HasOnlyBoundedSpatialRows()));
    }

    [Fact]
    public void TheHostileRosterSeedsAndBeginsDeterministicRoaming()
    {
        using var client = IntegrationClient.Connect();

        // An empty world skips its dispatch, so the NPCs only roam once a player is
        // loaded. Connecting a socket is not enough on its own.
        client.LoadPlayer();
        client.SubscribeNpcWorld();
        var initial = client.NpcPositions();

        // Twelve patrol slots, every fifth of them a veteran, plus the named captain and the
        // two escorts moored beside her: five skiffs, five reef crabs, four fancies, one Red Mary.
        Assert.Equal(15, initial.Count);
        Assert.Equal(5, client.NpcCount(1));
        Assert.Equal(5, client.NpcCount(2));
        Assert.Equal(4, client.NpcCount(3));
        Assert.Equal(1, client.NpcCount(4));

        PumpAllUntil([client], () =>
        {
            var current = client.NpcPositions();
            return current.Any(pair =>
                initial.TryGetValue(pair.Key, out var position) &&
                (MathF.Abs(pair.Value.X - position.X) > 0.01f ||
                 MathF.Abs(pair.Value.Y - position.Y) > 0.01f));
        });

        Assert.Equal(15, client.NpcAiCount());
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
