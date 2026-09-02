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

    [Fact]
    public void Recorded_command_log_replays_to_a_pinned_hash()
    {
        var result = ReplayRules.Run(100, new SailingState(0f, 0f, 0f, 0f), Commands(), Parameters, 0.1f);

        Assert.Equal(3073545830116257169UL, result.StateHash);
    }

    private static ReplayCommand[] Commands() =>
    [
        new(0, ReplayCommandKind.SetCourse, 80f, 20f),
        new(35, ReplayCommandKind.SetCourse, -20f, 70f),
        new(80, ReplayCommandKind.StopCourse, 0f, 0f),
    ];
}
