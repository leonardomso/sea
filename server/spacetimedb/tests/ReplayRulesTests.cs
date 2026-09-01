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

    private static ReplayCommand[] Commands() =>
    [
        new(0, ReplayCommandKind.SetCourse, 80f, 20f),
        new(35, ReplayCommandKind.SetCourse, -20f, 70f),
        new(80, ReplayCommandKind.StopCourse, 0f, 0f),
    ];
}
