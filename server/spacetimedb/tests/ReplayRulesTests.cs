using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class ReplayRulesTests
{
    /// <summary>Twelve squares a second at ten ticks a second.</summary>
    private const float TravelPerTick = 1.2f;

    private static readonly ReplayState Origin = new(0f, 0f, 0f, 0, false);

    [Fact]
    public void Same_seed_and_command_log_produce_the_same_state_hash()
    {
        var commands = Commands();

        var first = ReplayRules.Run(100, Origin, commands, TravelPerTick);
        var second = ReplayRules.Run(100, Origin, commands, TravelPerTick);

        Assert.Equal(first.StateHash, second.StateHash);
        Assert.Equal(first.State, second.State);
    }

    [Fact]
    public void Replay_hash_detects_a_changed_command_log()
    {
        var baseline = ReplayRules.Run(100, Origin, Commands(), TravelPerTick);
        var changed = ReplayRules.Run(
            100,
            Origin,
            new[] { new ReplayCommand(0, ReplayCommandKind.SetCourse, -80f, 20f) },
            TravelPerTick);

        Assert.NotEqual(baseline.StateHash, changed.StateHash);
    }

    [Fact]
    public void No_command_run_keeps_the_initial_state_and_a_stable_hash()
    {
        var result = ReplayRules.Run(100, Origin, [], TravelPerTick);

        Assert.Equal(Origin, result.State);
        // Recorded from master before Milestone 1a and unchanged by the move onto routes:
        // a ship who is given no order never leaves the origin, and the fourth field the
        // tick hashes went from a speed of zero to a route index of zero. A mismatch means
        // the replay changed and must be investigated, not re-baselined.
        Assert.Equal(9594698449054650917UL, result.StateHash);
    }

    /// <summary>
    /// A course bent round a corner, replayed. The hash is the cheap half of this test and
    /// the assertions under it are the half that knows what the answer means: a hash can
    /// tell you the replay moved, never whether it moved the right way, and re-baselining
    /// one on its own is how a migration quietly writes down whatever it happens to do.
    /// </summary>
    [Fact]
    public void Recorded_command_log_replays_to_a_pinned_hash()
    {
        var result = ReplayRules.Run(100, Origin, Commands(), TravelPerTick);

        // Baselined fresh in the SEA_5 physics migration, after checking the track leg by
        // leg rather than accepting the number the run produced. The old figure belonged to
        // the inertia model: a hull accelerated, braked and turned through a circle, and
        // none of those exist any more. Investigate a mismatch here, do not re-baseline it
        // -- and if it does turn out to be a deliberate change, replace this note rather
        // than adding to it.
        Assert.Equal(7012585430772866436UL, result.StateHash);

        // What the hash is a shorthand for. She makes 42 squares up the first leg before
        // the second order turns her south-west, then 54 more toward (-20, 70) before the
        // stop at tick 80 leaves her where she stands, still pointing at the mark she was
        // sent to and 31 squares short of it.
        Assert.Equal(2.27f, result.State.PositionX, 2);
        Assert.Equal(48.07f, result.State.PositionY, 2);
        Assert.Equal(225.44f, result.State.HeadingDegrees, 2);
        Assert.False(result.State.HasRoute, "the stop at tick 80 should have taken it away");
    }

    /// <summary>
    /// A dogleg is one course, not two. The corner costs nothing to round (SEA_5 4.1.7),
    /// so a bent course takes exactly its own length over her speed.
    /// </summary>
    [Fact]
    public void A_course_round_a_corner_takes_its_own_length_to_sail()
    {
        var corners = new[] { new RouteWaypoint(0f, -30f), new RouteWaypoint(40f, -30f) };
        var command = new ReplayCommand(0, ReplayCommandKind.SetCourse, 40f, -30f)
        {
            Corners = corners,
        };

        // 70 squares of course at 1.2 a tick is 58 and a third; 58 ticks leave her short.
        var beforeTheEnd = ReplayRules.Run(58, Origin, [command], TravelPerTick);
        var atTheEnd = ReplayRules.Run(59, Origin, [command], TravelPerTick);

        Assert.True(beforeTheEnd.State.HasRoute, "58 ticks is not enough sea for 70 squares");
        Assert.False(atTheEnd.State.HasRoute, "59 ticks should have put her on the mark");
        Assert.Equal(40f, atTheEnd.State.PositionX, 3);
        Assert.Equal(-30f, atTheEnd.State.PositionY, 3);
    }

    private static ReplayCommand[] Commands() =>
    [
        new(0, ReplayCommandKind.SetCourse, 80f, 20f),
        new(35, ReplayCommandKind.SetCourse, -20f, 70f),
        new(80, ReplayCommandKind.StopCourse, 0f, 0f),
    ];
}
