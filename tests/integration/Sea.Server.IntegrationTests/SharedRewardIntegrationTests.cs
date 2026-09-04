using System.Diagnostics;
using SpacetimeDB.Types;
using Xunit;

namespace Sea.Server.IntegrationTests;

public sealed class SharedRewardIntegrationTests
{
    private const byte NoTargetRejection = 11;
    private const byte TargetSunkRejection = 12;
    private const byte ReloadingRejection = 13;
    private const byte FiringTooFastRejection = 14;
    private const byte OutOfRangeRejection = 15;

    /// <summary>A hull that has just put to sea keeps its shield until the tenth second.</summary>
    private const byte SpawnShieldedRejection = 23;
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

        // Port Lowell answers every fire command with InPort, so the fight is picked with a
        // hostile clear of the harbour and every participant is at sea before it starts.
        var targetId = first.ClosestUntouchedNpcClearOfPort(3).EntityId;
        foreach (var client in clients)
        {
            var hostile = client.NpcPosition(targetId);
            client.PutToSea(hostile.X, hostile.Y);
        }

        MoveIntoRange(clients, targetId);

        foreach (var client in clients)
        {
            var selection = client.SelectTarget(targetId);
            Assert.True(
                selection.Accepted,
                $"Target selection rejected with {selection.RejectionCode}; the target is " +
                $"hull {client.Npc(targetId).Hull}/{client.Npc(targetId).MaxHull}, " +
                $"alive {client.Npc(targetId).IsAlive}.");
            FireWhenLegal(client, clients, targetId);
        }

        var disconnectedToken = fourth.Token;
        var disconnectedShipId = fourth.OwnedShip().EntityId;
        fourth.Dispose();
        clients = [first, second, third];
        FinishOff(first, clients, targetId);
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

    /// <summary>
    /// Keeps the last shooter firing until the target is gone. The fat <c>ship</c> row the test
    /// reads only republishes on a chunk change or a stop, so "still alive" can be a stale
    /// answer; the module's own rejection is the authority on a target that has already sunk.
    /// </summary>
    private static void FinishOff(
        IntegrationClient client,
        IReadOnlyCollection<IntegrationClient> clients,
        ulong targetId)
    {
        var stopwatch = Stopwatch.StartNew();
        while (client.Npc(targetId).IsAlive)
        {
            var fire = client.Fire();
            if (fire.RejectionCode is NoTargetRejection or TargetSunkRejection)
            {
                return;
            }

            if (!fire.Accepted)
            {
                Assert.True(
                    fire.RejectionCode is ReloadingRejection or FiringTooFastRejection
                        or OutOfRangeRejection or SpawnShieldedRejection,
                    $"Unexpected fire rejection {fire.RejectionCode}.");
                if (fire.RejectionCode == OutOfRangeRejection)
                {
                    Approach(client, client.Npc(targetId));
                }
            }

            PumpFor(clients, TimeSpan.FromMilliseconds(150));
            ThrowIfTimedOut(stopwatch);
        }
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

            if (fire.RejectionCode is ReloadingRejection or FiringTooFastRejection
                or SpawnShieldedRejection)
            {
                PumpFor(clients, TimeSpan.FromMilliseconds(100));
                ThrowIfTimedOut(stopwatch);
                continue;
            }

            // Range is the only geometry left: the magazine bears in every direction, so a
            // rejection that is not the reload is the target sitting too far away.
            Assert.True(
                fire.RejectionCode == OutOfRangeRejection,
                $"Unexpected fire rejection {fire.RejectionCode}; shooter hull " +
                $"{client.OwnedShip().Hull}/{client.OwnedShip().MaxHull} mode " +
                $"{client.OwnedShip().ModeCode} target {client.OwnedShip().TargetEntityId}; " +
                $"target hull {client.Npc(targetId).Hull}/{client.Npc(targetId).MaxHull} " +
                $"alive {client.Npc(targetId).IsAlive}.");
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
