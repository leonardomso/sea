using Xunit;

namespace Sea.Server.IntegrationTests;

/// <summary>
/// Havenmere's one named captain, fought the way four captains in the same world would fight her.
/// The unit tests own the threshold arithmetic; what only a live world can show is that four
/// separate connections drive one hull down together, that her two escorts lie at their moorings
/// until she signals, and that the signal is given once however long the fight runs on.
/// </summary>
public sealed class RedMaryIntegrationTests
{
    /// <summary>ShipArchetypeCode.RedMary.</summary>
    private const byte RedMaryArchetype = 4;

    /// <summary>NpcRules.CallHelpHullRatio: she signals at half her hull.</summary>
    private const float CallHelpHullRatio = 0.5f;

    /// <summary>NpcRules.CallHelpCount.</summary>
    private const int EscortCount = 2;

    // A Named hull is five times a player's staying power, so four captains need most of a
    // minute of sustained fire on top of the sail out to reach her half-hull mark.
    private static readonly TimeSpan ScenarioTimeout = TimeSpan.FromSeconds(240);

    [Fact]
    public void FourCaptainsBreakingRedMaryToHalfHullBringHerEscortsOffTheirMoorings()
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

        // Her beat can take her through the harbour's sheltered water, where nobody's guns
        // answer; the fight waits for her to come back out of it.
        FightScenario.PumpUntil(
            clients,
            () => first.TryClosestNpcClearOfPort(RedMaryArchetype) is not null,
            ScenarioTimeout);
        var maryId = first.ClosestNpcClearOfPort(RedMaryArchetype).EntityId;

        // Before a shot is fired she is silent, and the escorts are hers alone.
        var escorts = first.EscortsOf(maryId);
        Assert.Equal(EscortCount, escorts.Length);
        Assert.False(first.NpcBrain(maryId).HasCalledHelp);
        Assert.All(escorts, escort => Assert.Equal(maryId, escort.LeaderEntityId));
        var moorings = escorts.ToDictionary(
            escort => escort.ShipEntityId,
            escort => first.NpcPosition(escort.ShipEntityId));

        SailOntoHer(clients, maryId);

        // The sail out took real time and they have not moved an inch of it: an escort answers to
        // nobody but its captain, so until she calls it does not even pick its own fight.
        Assert.All(
            moorings,
            mooring => Assert.Equal(mooring.Value, first.NpcPosition(mooring.Key)));

        BreakToHalfHull(clients, first, maryId);
        AssertEscortsAnswerOnce(clients, first, maryId, moorings);
    }

    /// <summary>
    /// Puts all four to sea and closes on her. Port Lowell answers every fire command with InPort,
    /// so the fight cannot start until every participant is clear of the harbour.
    /// </summary>
    private static void SailOntoHer(IReadOnlyCollection<IntegrationClient> clients, ulong maryId)
    {
        foreach (var client in clients)
        {
            var position = client.NpcPosition(maryId);
            client.PutToSea(position.X, position.Y);
        }

        FightScenario.MoveIntoRange(clients, maryId, ScenarioTimeout);
        foreach (var client in clients)
        {
            var selection = client.SelectTarget(maryId);
            Assert.True(
                selection.Accepted,
                $"Target selection rejected with {selection.RejectionCode}.");
        }
    }

    private static void BreakToHalfHull(
        IReadOnlyCollection<IntegrationClient> clients,
        IntegrationClient observer,
        ulong maryId)
    {
        var callThreshold = (uint)(observer.Npc(maryId).MaxHull * CallHelpHullRatio);
        FightScenario.KeepFiring(
            clients,
            maryId,
            () => observer.Npc(maryId).Hull <= callThreshold || !observer.Npc(maryId).IsAlive,
            ScenarioTimeout);
        Assert.True(
            observer.Npc(maryId).IsAlive,
            "Red Mary sank before she reached the hull she signals at.");
    }

    private static void AssertEscortsAnswerOnce(
        IReadOnlyCollection<IntegrationClient> clients,
        IntegrationClient observer,
        ulong maryId,
        IReadOnlyDictionary<ulong, (float X, float Y)> moorings)
    {
        // The signal is a decision, so it lands on the AI beat rather than on the volley, and it
        // is world state rather than a private event: every client reads the same flag.
        FightScenario.PumpUntil(
            clients,
            () => observer.NpcBrain(maryId).HasCalledHelp,
            ScenarioTimeout);
        Assert.All(clients, client => Assert.True(client.NpcBrain(maryId).HasCalledHelp));

        // Both hulls come off their moorings, and only hers do: the roster she was seeded with is
        // the roster that answers, however long the fight runs on after the call.
        FightScenario.PumpUntil(
            clients,
            () => moorings.All(mooring => observer.NpcPosition(mooring.Key) != mooring.Value),
            ScenarioTimeout);
        var answered = observer.EscortsOf(maryId);
        Assert.Equal(EscortCount, answered.Length);
        Assert.Equal(
            moorings.Keys.Order().ToArray(),
            answered.Select(escort => escort.ShipEntityId).Order().ToArray());
        Assert.True(observer.NpcBrain(maryId).HasCalledHelp);
        Assert.All(clients, client => Assert.Null(client.UnhandledReducerError));
    }
}
