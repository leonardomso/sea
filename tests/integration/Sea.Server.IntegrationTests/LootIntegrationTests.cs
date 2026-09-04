using System.Diagnostics;
using SpacetimeDB.Types;
using Xunit;

namespace Sea.Server.IntegrationTests;

public sealed class LootIntegrationTests
{
    private static readonly TimeSpan ScenarioTimeout = TimeSpan.FromSeconds(120);

    /// <summary>How far from the wreck a crate of its cargo can be floating.</summary>
    private const float WreckRadius = 20f;

    /// <summary>
    /// The wreck site. A captain who steers onto a floating crate and lets the way come off her
    /// there has collected it: the claim is checked while she sails and again on the tick she
    /// comes to rest, so coasting to a stop on top of the loot is not a way to miss it.
    /// </summary>
    [Fact]
    public void ACaptainWhoComesToRestOnTheWreckCollectsWhatIsFloatingThere()
    {
        using var client = IntegrationClient.Connect();
        IntegrationClient[] clients = [client];
        client.LoadPlayer();
        client.SubscribeNpcWorld();
        client.SubscribeLoot();

        var targetId = SinkAHostile(clients, client);
        var wreck = client.NpcPosition(targetId);
        var goldBefore = client.OwnedProgression().Gold;
        var crate = WaitForWreckLoot(clients, client, wreck);

        // Not "the loot went away" but "she stopped where it was and it is hers": the row is
        // gone from the water and the purse is heavier than it was before she sailed over.
        SailOver(clients, client, crate);
        Assert.DoesNotContain(client.ActiveLoot(), loot => loot.LootId == crate.LootId);
        Assert.True(
            client.OwnedProgression().Gold > goldBefore,
            $"The purse stayed at {goldBefore} after collecting loot {crate.LootId}.");
        Assert.Null(client.UnhandledReducerError);
    }

    private static ulong SinkAHostile(
        IReadOnlyCollection<IntegrationClient> clients,
        IntegrationClient client)
    {
        FightScenario.PumpUntil(
            clients,
            () => client.TryClosestNpcClearOfPort(1) is not null,
            ScenarioTimeout);
        var targetId = client.ClosestUntouchedNpcClearOfPort(1).EntityId;
        var hostile = client.NpcPosition(targetId);
        client.PutToSea(hostile.X, hostile.Y);
        FightScenario.MoveIntoRange(clients, targetId, ScenarioTimeout);
        Assert.True(client.SelectTarget(targetId).Accepted);
        FightScenario.KeepFiring(
            clients,
            targetId,
            () => !client.Npc(targetId).IsAlive,
            ScenarioTimeout);
        FightScenario.PumpUntil(clients, () => !client.Npc(targetId).IsAlive, ScenarioTimeout);
        return targetId;
    }

    /// <summary>
    /// What this wreck left, not whatever is floating on the map: the tests share one world, so
    /// another fight's crate is no evidence about this one.
    /// </summary>
    private static Loot WaitForWreckLoot(
        IReadOnlyCollection<IntegrationClient> clients,
        IntegrationClient client,
        (float X, float Y) wreck)
    {
        FightScenario.PumpUntil(clients, () => NearestTo(client, wreck) is not null, ScenarioTimeout);
        return NearestTo(client, wreck)!;
    }

    private static Loot? NearestTo(IntegrationClient client, (float X, float Y) wreck) =>
        client.ActiveLoot()
            .Select(loot => (Loot: loot, Range: Distance(loot, wreck)))
            .Where(candidate => candidate.Range <= WreckRadius)
            .OrderBy(candidate => candidate.Range)
            .Select(candidate => candidate.Loot)
            .FirstOrDefault();

    /// <summary>
    /// Sails onto the crate and stops there. The course is re-laid every second because a current
    /// sets the ship off the point she was steering for, and arriving beside the loot rather than
    /// on it is exactly the mistake this test is about.
    /// </summary>
    private static void SailOver(
        IReadOnlyCollection<IntegrationClient> clients,
        IntegrationClient client,
        Loot crate)
    {
        var stopwatch = Stopwatch.StartNew();
        var nextCourseAt = TimeSpan.Zero;
        while (client.ActiveLoot().Any(loot => loot.LootId == crate.LootId))
        {
            if (stopwatch.Elapsed >= nextCourseAt)
            {
                Assert.True(
                    client.SetCourse(crate.PositionX, crate.PositionY).Accepted,
                    $"A course onto loot {crate.LootId} at " +
                    $"({crate.PositionX:0.0}, {crate.PositionY:0.0}) was refused.");
                nextCourseAt = stopwatch.Elapsed + TimeSpan.FromSeconds(1);
            }

            FightScenario.PumpOnce(clients);
            ThrowIfStranded(stopwatch, client, crate);
        }
    }

    private static void ThrowIfStranded(
        Stopwatch stopwatch,
        IntegrationClient client,
        Loot crate)
    {
        if (stopwatch.Elapsed <= ScenarioTimeout)
        {
            return;
        }

        var movement = client.OwnedMovement();
        throw new TimeoutException(
            $"Loot {crate.LootId} at ({crate.PositionX:0.0}, {crate.PositionY:0.0}) was still " +
            $"floating after {ScenarioTimeout.TotalSeconds:0}s; the ship is at " +
            $"({movement.PositionX:0.0}, {movement.PositionY:0.0}), moving {movement.IsMoving}, " +
            $"speed {movement.Speed:0.00}.");
    }

    private static float Distance(Loot loot, (float X, float Y) point)
    {
        var deltaX = loot.PositionX - point.X;
        var deltaY = loot.PositionY - point.Y;
        return MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }
}
