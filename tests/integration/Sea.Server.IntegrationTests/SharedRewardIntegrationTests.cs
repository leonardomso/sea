using System.Diagnostics;
using SpacetimeDB.Types;
using Xunit;

namespace Sea.Server.IntegrationTests;

public sealed class SharedRewardIntegrationTests
{
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

        FightScenario.MoveIntoRange(clients, targetId, ScenarioTimeout);

        foreach (var client in clients)
        {
            var selection = client.SelectTarget(targetId);
            Assert.True(
                selection.Accepted,
                $"Target selection rejected with {selection.RejectionCode}; the target is " +
                $"hull {client.Npc(targetId).Hull}/{client.Npc(targetId).MaxHull}, " +
                $"alive {client.Npc(targetId).IsAlive}.");
            FightScenario.FireWhenLegal(client, clients, targetId, ScenarioTimeout);
        }

        var disconnectedToken = fourth.Token;
        var disconnectedShipId = fourth.OwnedShip().EntityId;
        fourth.Dispose();
        clients = [first, second, third];
        FinishOff(first, clients, targetId);
        FightScenario.PumpUntil(clients, () => !first.Npc(targetId).IsAlive, ScenarioTimeout);
        using var reconnectedFourth = IntegrationClient.Connect(disconnectedToken);
        reconnectedFourth.LoadPlayer();
        reconnectedFourth.SubscribeNpcWorld();
        Assert.Equal(disconnectedShipId, reconnectedFourth.OwnedShip().EntityId);
        clients = [first, second, third, reconnectedFourth];
        FightScenario.PumpUntil(
            clients,
            () => clients.All(client => client.EncounterRewards().Length > 0),
            ScenarioTimeout);

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
        // A fancy is the map's veteran: 75 gold and 60 experience, split whole across the
        // four contributors and never rounded into or out of existence.
        Assert.Equal(75u, rewards.Aggregate(0u, (total, reward) => total + reward.Gold));
        Assert.Equal(60ul, rewards.Aggregate(0ul, (total, reward) => total + reward.Experience));
        Assert.Equal(4, rewards.Select(reward => reward.ContributorEntityId).Distinct().Count());
        Assert.All(clients, client => Assert.Null(client.UnhandledReducerError));

        var rewardIds = rewards.Select(reward => reward.RewardId).Order().ToArray();
        FightScenario.PumpFor(clients, TimeSpan.FromSeconds(1));
        Assert.Equal(
            rewardIds,
            clients.SelectMany(client => client.EncounterRewards())
                .Select(reward => reward.RewardId)
                .Order()
                .ToArray());

        var settledEncounterId = rewards[0].EncounterId;
        FightScenario.PumpUntil(
            clients,
            () =>
            {
                var respawned = observer.Npc(targetId);
                return respawned.IsAlive && respawned.EncounterId != settledEncounterId;
            },
            ScenarioTimeout);
        Assert.Equal(
            rewardIds,
            clients.SelectMany(client => client.EncounterRewards())
                .Select(reward => reward.RewardId)
                .Order()
                .ToArray());
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
            if (fire.RejectionCode is FightScenario.NoTargetRejection
                or FightScenario.TargetSunkRejection)
            {
                return;
            }

            if (!fire.Accepted)
            {
                Assert.True(
                    fire.RejectionCode is FightScenario.ReloadingRejection
                        or FightScenario.FiringTooFastRejection
                        or FightScenario.OutOfRangeRejection
                        or FightScenario.SpawnShieldedRejection,
                    $"Unexpected fire rejection {fire.RejectionCode}.");
                if (fire.RejectionCode == FightScenario.OutOfRangeRejection)
                {
                    FightScenario.Approach(client, client.Npc(targetId));
                }
            }

            FightScenario.PumpFor(clients, TimeSpan.FromMilliseconds(150));
            FightScenario.ThrowIfTimedOut(stopwatch, ScenarioTimeout);
        }
    }
}
