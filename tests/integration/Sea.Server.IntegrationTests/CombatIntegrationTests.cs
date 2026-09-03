using System.Diagnostics;
using SpacetimeDB.Types;
using Xunit;

namespace Sea.Server.IntegrationTests;

/// <summary>
/// End-to-end coverage of the 1b combat reducers against a live module: the magazine the fire
/// command spends, the reload that refills it, and the retired commands the module still answers.
/// </summary>
public sealed class CombatIntegrationTests
{
    private const byte ReloadingRejection = 13;
    private const byte FiringTooFastRejection = 14;
    private const byte OutOfRangeRejection = 15;
    private const byte NotAvailableRejection = 21;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    [Fact]
    public void FiringSpendsAVolleyAndBitesTheTargetHull()
    {
        using var client = Engage(out var targetId);
        var beforeHull = client.Npc(targetId).Hull;

        var fire = FireWhenLegal(client, targetId);

        Assert.True(fire.Accepted, $"Fire was rejected with code {fire.RejectionCode}.");
        Assert.Null(client.UnhandledReducerError);

        // The ship spawns with a full magazine and this is its first shot, so the racks read one
        // short of the sheet. Comparing against MagazineSize rather than a pre-approach snapshot
        // keeps the assertion honest: the fat ship row is only republished on a change, so a
        // reading taken before the run-in can be several ticks stale.
        var after = client.OwnedShip();
        Assert.Equal(after.MagazineSize - 1u, after.ReadyVolleys);
        Assert.True(after.HasFired);

        // The volley row is the shot's only public record, and the hull bite is the damage that
        // same shot resolved on the tick it was fired.
        PumpUntil(client, () => client.Volleys().Length > 0);
        var volley = Assert.Single(client.Volleys(), row => row.TargetEntityId == targetId);
        Assert.Equal(after.EntityId, volley.SourceEntityId);
        Assert.Equal(volley.FiredAtTick + 10ul, volley.ExpiresAtTick);
        PumpUntil(client, () => client.Npc(targetId).Hull < beforeHull);
    }

    [Fact]
    public void AnEmptyMagazineRejectsUntilTheReloadPutsAVolleyBack()
    {
        using var client = Engage(out var targetId);

        // Bounded by the sheet rather than by patience: if the racks are still not empty after
        // one magazine's worth of shots, the reload is outrunning the shot interval and that is
        // the bug worth failing on.
        for (var shots = 0u; shots <= client.OwnedShip().MagazineSize; shots++)
        {
            if (client.OwnedShip().ReadyVolleys == 0)
            {
                break;
            }

            var shot = FireWhenLegal(client, targetId);
            Assert.True(shot.Accepted, $"Fire was rejected with code {shot.RejectionCode}.");
        }

        Assert.Equal(0u, client.OwnedShip().ReadyVolleys);

        // An empty magazine answers Reloading rather than the one-second shot interval or the
        // range, because the module checks the racks before it checks the clock or the distance.
        Assert.Equal(ReloadingRejection, client.Fire().RejectionCode);

        PumpUntil(client, () => client.OwnedShip().ReadyVolleys > 0);
        Assert.True(FireWhenLegal(client, targetId).Accepted);
        Assert.Null(client.UnhandledReducerError);
    }

    [Fact]
    public void RetiredCommandsAnswerNotAvailableWithoutTouchingTheShip()
    {
        using var client = IntegrationClient.Connect();
        client.LoadPlayer();
        var before = client.OwnedShip();

        // Abilities and boarding left the game with 1b but stay on the wire, so a stale client
        // gets a stable answer instead of a command the module silently reinterprets.
        Assert.Equal(NotAvailableRejection, client.IssueAbility(1, "full_sail").RejectionCode);
        Assert.Equal(NotAvailableRejection, client.IssueBoarding(2).RejectionCode);

        var after = client.OwnedShip();
        Assert.Null(client.UnhandledReducerError);
        Assert.Equal(before.Hull, after.Hull);
        Assert.Equal(before.ReadyVolleys, after.ReadyVolleys);
        Assert.Equal(before.ModeCode, after.ModeCode);
        Assert.Empty(client.Effects());
    }

    /// <summary>
    /// Connects a client, subscribes it to the world it is about to shoot at, and locks it onto
    /// the nearest NPC. Closing the distance is left to <see cref="FireWhenLegal"/>.
    /// </summary>
    private static IntegrationClient Engage(out ulong targetId)
    {
        var client = IntegrationClient.Connect();
        try
        {
            client.LoadPlayer();
            client.SubscribeNpcWorld();
            client.SubscribeVolleys();
            var own = client.OwnedShip();
            targetId = client.ClosestNpcTo(3, own.PositionX, own.PositionY).EntityId;
            Assert.True(client.SelectTarget(targetId).Accepted);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Fires as soon as the shot interval and the range both allow it, sailing onto the target
    /// while they do not. The fire command itself is the range probe, because a rejected command
    /// spends nothing: only an accepted one takes a volley out of the racks. Every other
    /// rejection is a real failure and is handed straight back to the caller to assert on.
    /// </summary>
    private static CommandResultEvent FireWhenLegal(IntegrationClient client, ulong targetId)
    {
        var stopwatch = Stopwatch.StartNew();
        var nextCourseAt = TimeSpan.Zero;
        while (true)
        {
            var fire = client.Fire();
            if (fire.Accepted ||
                fire.RejectionCode is not (FiringTooFastRejection or OutOfRangeRejection))
            {
                return fire;
            }

            if (fire.RejectionCode == OutOfRangeRejection && stopwatch.Elapsed >= nextCourseAt)
            {
                var target = client.Npc(targetId);
                Assert.True(client.SetCourse(target.PositionX, target.PositionY).Accepted);
                nextCourseAt = stopwatch.Elapsed + TimeSpan.FromSeconds(1);
            }

            Assert.True(client.Npc(targetId).IsAlive, "The target sank before the shot landed.");
            client.PumpOnce();
            ThrowIfTimedOut(stopwatch);
        }
    }

    private static void PumpUntil(IntegrationClient client, Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            client.PumpOnce();
            ThrowIfTimedOut(stopwatch);
        }
    }

    private static void ThrowIfTimedOut(Stopwatch stopwatch)
    {
        if (stopwatch.Elapsed > Timeout)
        {
            throw new TimeoutException("Combat integration operation timed out.");
        }
    }
}
