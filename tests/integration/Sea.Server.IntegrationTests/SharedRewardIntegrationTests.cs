using System.Diagnostics;
using SpacetimeDB.Types;
using Xunit;

namespace Sea.Server.IntegrationTests;

public sealed class SharedRewardIntegrationTests
{
    private const byte ReloadingRejection = 13;
    private const byte FiringTooFastRejection = 14;
    private const byte OutOfRangeRejection = 15;
    private static readonly TimeSpan ScenarioTimeout = TimeSpan.FromSeconds(90);

    [Fact]
    public void FourClientsReceiveOneConservedRewardFromTheSameNpc()
    {
        using var first = IntegrationClient.Connect();
        using var second = IntegrationClient.Connect();
        using var third = IntegrationClient.Connect();
        using var fourth = IntegrationClient.Connect();
        IntegrationClient[] clients = [first, second, third, fourth];
        foreach (var client in clients)
        {
            client.LoadPlayer();
            client.SubscribeNpcWorld();
        }

        var centerX = clients.Average(client => client.OwnedShip().PositionX);
        var centerY = clients.Average(client => client.OwnedShip().PositionY);
        var targetId = first.ClosestNpcTo(3, centerX, centerY).EntityId;
        MoveIntoRange(clients, targetId);

        foreach (var client in clients)
        {
            Assert.True(client.SelectTarget(targetId).Accepted);
            FireWhenLegal(client, clients, targetId);
        }

        var disconnectedToken = fourth.Token;
        var disconnectedShipId = fourth.OwnedShip().EntityId;
        fourth.Dispose();
        clients = [first, second, third];
        if (first.Npc(targetId).IsAlive)
        {
            FireWhenLegal(first, clients, targetId);
        }
        PumpUntil(clients, () => !first.Npc(targetId).IsAlive);
        using var reconnectedFourth = IntegrationClient.Connect(disconnectedToken);
        reconnectedFourth.LoadPlayer();
        reconnectedFourth.SubscribeNpcWorld();
        Assert.Equal(disconnectedShipId, reconnectedFourth.OwnedShip().EntityId);
        clients = [first, second, third, reconnectedFourth];
        PumpUntil(clients, () => clients.All(client => client.EncounterRewards().Length > 0));

        AssertSettlementAndRespawn(clients, first, targetId);
    }

    private static void AssertSettlementAndRespawn(
        IReadOnlyCollection<IntegrationClient> clients,
        IntegrationClient observer,
        ulong targetId)
    {
        var rewards = clients.SelectMany(client => client.EncounterRewards()).ToArray();
        Assert.All(clients, client => Assert.Single(client.EncounterRewards()));
        Assert.Single(rewards.Select(reward => reward.EncounterId).Distinct());
        Assert.Equal(140u, rewards.Aggregate(0u, (total, reward) => total + reward.Gold));
        Assert.Equal(175ul, rewards.Aggregate(0ul, (total, reward) => total + reward.Experience));
        Assert.Equal(4, rewards.Select(reward => reward.ContributorEntityId).Distinct().Count());
        Assert.All(clients, client => Assert.Null(client.UnhandledReducerError));

        var rewardIds = rewards.Select(reward => reward.RewardId).Order().ToArray();
        PumpFor(clients, TimeSpan.FromSeconds(1));
        Assert.Equal(
            rewardIds,
            clients.SelectMany(client => client.EncounterRewards())
                .Select(reward => reward.RewardId)
                .Order()
                .ToArray());

        var settledEncounterId = rewards[0].EncounterId;
        PumpUntil(clients, () =>
        {
            var respawned = observer.Npc(targetId);
            return respawned.IsAlive && respawned.EncounterId != settledEncounterId;
        });
        Assert.Equal(
            rewardIds,
            clients.SelectMany(client => client.EncounterRewards())
                .Select(reward => reward.RewardId)
                .Order()
                .ToArray());
    }

    private static void MoveIntoRange(
        IReadOnlyCollection<IntegrationClient> clients,
        ulong targetId)
    {
        var nextCourseAt = TimeSpan.Zero;
        var stopwatch = Stopwatch.StartNew();
        while (!clients.All(client => Distance(client.OwnedShip(), client.Npc(targetId)) <= 24f))
        {
            if (stopwatch.Elapsed >= nextCourseAt)
            {
                var target = clients.First().Npc(targetId);
                foreach (var client in clients.Where(client =>
                             Distance(client.OwnedShip(), target) > 24f))
                {
                    Assert.True(TrySetApproachCourse(client, target));
                }

                nextCourseAt = stopwatch.Elapsed + TimeSpan.FromSeconds(1);
            }

            PumpOnce(clients);
            ThrowIfTimedOut(stopwatch);
        }
    }

    private static bool TrySetApproachCourse(IntegrationClient client, Ship target)
    {
        var source = client.OwnedShip();
        var sourceAngle = MathF.Atan2(
            source.PositionX - target.PositionX,
            source.PositionY - target.PositionY);
        for (var index = 0; index < 8; index++)
        {
            var angle = sourceAngle + index * MathF.PI / 4f;
            var result = client.SetCourse(
                target.PositionX + MathF.Sin(angle) * 22f,
                target.PositionY + MathF.Cos(angle) * 22f);
            if (result.Accepted)
            {
                return true;
            }
        }

        return false;
    }

    private static void FireWhenLegal(
        IntegrationClient client,
        IReadOnlyCollection<IntegrationClient> clients,
        ulong targetId)
    {
        var stopwatch = Stopwatch.StartNew();
        while (client.Npc(targetId).IsAlive)
        {
            var fire = client.Fire();
            if (fire.Accepted)
            {
                return;
            }

            if (fire.RejectionCode is ReloadingRejection or FiringTooFastRejection)
            {
                PumpFor(clients, TimeSpan.FromMilliseconds(100));
                ThrowIfTimedOut(stopwatch);
                continue;
            }

            // Range is the only geometry left: the magazine bears in every direction, so a
            // rejection that is not the reload is the target sitting too far away.
            Assert.True(
                fire.RejectionCode == OutOfRangeRejection,
                $"Unexpected fire rejection {fire.RejectionCode}.");
            Approach(client, client.Npc(targetId));
            PumpFor(clients, TimeSpan.FromMilliseconds(150));
            ThrowIfTimedOut(stopwatch);
        }

        throw new InvalidOperationException("Target sank before every participant fired.");
    }

    private static void Approach(IntegrationClient client, Ship target)
    {
        var source = client.OwnedShip();
        var radians = Bearing(source, target) * MathF.PI / 180f;
        var distance = MathF.Max(8f, Distance(source, target) - 20f);
        Assert.True(client.SetCourse(
            source.PositionX + MathF.Sin(radians) * distance,
            source.PositionY + MathF.Cos(radians) * distance).Accepted);
    }

    private static float Bearing(Ship source, Ship target) =>
        MathF.Atan2(target.PositionX - source.PositionX, target.PositionY - source.PositionY) *
        (180f / MathF.PI);

    private static float Distance(Ship source, Ship target)
    {
        var deltaX = target.PositionX - source.PositionX;
        var deltaY = target.PositionY - source.PositionY;
        return MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    private static void PumpUntil(
        IReadOnlyCollection<IntegrationClient> clients,
        Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            PumpOnce(clients);
            ThrowIfTimedOut(stopwatch);
        }
    }

    private static void PumpFor(
        IReadOnlyCollection<IntegrationClient> clients,
        TimeSpan duration)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < duration)
        {
            PumpOnce(clients);
        }
    }

    private static void PumpOnce(IEnumerable<IntegrationClient> clients)
    {
        foreach (var client in clients)
        {
            client.PumpOnce();
        }

        Thread.Sleep(5);
    }

    private static void ThrowIfTimedOut(Stopwatch stopwatch)
    {
        if (stopwatch.Elapsed > ScenarioTimeout)
        {
            throw new TimeoutException("Shared reward integration scenario timed out.");
        }
    }
}
