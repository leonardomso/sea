using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class ReplayRulesTests
{
    private static readonly SailingParameters Parameters = new(12f, 3f, 4f, 360f);

    [Fact]
    public void Same_seed_and_command_log_produce_the_same_state_hash()
    {
        var commands = Commands();

        var first = ReplayRules.Run(100, new SailingState(0f, 0f, 0f, 0f), commands, Parameters, 0.1f);
        var second = ReplayRules.Run(100, new SailingState(0f, 0f, 0f, 0f), commands, Parameters, 0.1f);

        Assert.Equal(first.StateHash, second.StateHash);
        Assert.Equal(first.State, second.State);
    }

    [Fact]
    public void Replay_hash_detects_a_changed_command_log()
    {
        var baseline = ReplayRules.Run(
            100, new SailingState(0f, 0f, 0f, 0f), Commands(), Parameters, 0.1f);
        var changed = ReplayRules.Run(
            100,
            new SailingState(0f, 0f, 0f, 0f),
            new[] { new ReplayCommand(0, ReplayCommandKind.SetCourse, -80f, 20f) },
            Parameters,
            0.1f);

        Assert.NotEqual(baseline.StateHash, changed.StateHash);
    }

    [Fact]
    public void No_command_run_keeps_the_initial_state_and_a_stable_hash()
    {
        var initial = new SailingState(0f, 0f, 0f, 0f);

        var result = ReplayRules.Run(100, initial, [], Parameters, 0.1f);

        Assert.Equal(initial, result.State);
        // Recorded from master before Milestone 1a; a mismatch means the sailing replay changed and must be investigated, not re-baselined.
        Assert.Equal(9594698449054650917UL, result.StateHash);
    }

    /// <summary>
    /// The whole log, replayed. The hash is the cheap half of this test and the assertions
    /// under it are the half that knows what the answer means: a hash can tell you the replay
    /// moved, never whether it moved the right way, and re-baselining one on its own is how a
    /// migration quietly writes down whatever it happens to do.
    /// </summary>
    [Fact]
    public void Recorded_command_log_replays_to_a_pinned_hash()
    {
        var result = ReplayRules.Run(100, new SailingState(0f, 0f, 0f, 0f), Commands(), Parameters, 0.1f);

        // Re-baselined once, in the top-left-origin migration, after checking the track leg by
        // leg rather than accepting the number the run produced. The old figure was recorded
        // when heading 0 sailed south: SailingRules propelled a hull by an unnegated cosine and
        // steered by its matching inverse, so the two agreed with each other and disagreed with
        // every current and storm on the same tick. The commands are unchanged; the compass
        // under them is. Investigate a mismatch here, do not re-baseline it -- and if it does
        // turn out to be a deliberate change, replace this note rather than adding to it.
        Assert.Equal(2197665753278242264UL, result.StateHash);

        // What the hash is a shorthand for. Her last order is a stop at tick 80, so by 100 she
        // is still carrying way toward the second mark at (-20, 70): west of where she started
        // and well south of it, on a bearing that points at it.
        Assert.Equal(211.13f, result.State.HeadingDegrees, 2);
        Assert.True(
            result.State.PositionX < 0f && result.State.PositionY > 40f,
            $"expected her south-west of the start, found ({result.State.PositionX}, {result.State.PositionY})");
        Assert.True(
            result.State.Speed is > 0f and < 12f,
            $"expected her still shedding way from 12, found {result.State.Speed}");
    }

    private static ReplayCommand[] Commands() =>
    [
        new(0, ReplayCommandKind.SetCourse, 80f, 20f),
        new(35, ReplayCommandKind.SetCourse, -20f, 70f),
        new(80, ReplayCommandKind.StopCourse, 0f, 0f),
    ];
}
