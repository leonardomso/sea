using Sea.LoadTests;
using Xunit;

namespace Sea.LoadTests.Tests;

public sealed class LoadRunTrackerTests
{
    [Fact]
    public void SnapshotSeparatesAttemptsConnectionsRetentionAndFailures()
    {
        var tracker = new LoadRunTracker();
        tracker.RecordAttempt();
        tracker.RecordAttempt();
        tracker.RecordConnected(TimeSpan.FromMilliseconds(20));
        tracker.RecordAcknowledgement(TimeSpan.FromMilliseconds(12));
        tracker.RecordRetained();
        tracker.RecordFailure(new TimeoutException());

        var evidence = tracker.Snapshot(activeClients: 1, dormantClients: 1);

        Assert.Equal(2, evidence.AttemptedClients);
        Assert.Equal(1, evidence.ConnectedClients);
        Assert.Equal(1, evidence.RetainedClients);
        Assert.Equal(1, evidence.FailedClients);
        Assert.Equal(1, evidence.Failures[nameof(TimeoutException)]);
        Assert.Contains(
            "timed out",
            evidence.FailureSamples[nameof(TimeoutException)],
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(12, evidence.CommandAckP95Milliseconds);
    }

    [Fact]
    public void PhaseTimeoutsRetainTheirDiagnosticStage()
    {
        var tracker = new LoadRunTracker();
        tracker.RecordFailure(new LoadPhaseTimeoutException("ship subscription"));

        var evidence = tracker.Snapshot(activeClients: 0, dormantClients: 1);

        Assert.Equal(1, evidence.Failures[
            "LoadPhaseTimeoutException:ship subscription"]);
    }

    [Fact]
    public void PhaseInvariantsRetainTheirDiagnosticStage()
    {
        var tracker = new LoadRunTracker();
        tracker.RecordFailure(new LoadPhaseInvariantException(
            "ownership subscription",
            "missing row"));

        var evidence = tracker.Snapshot(activeClients: 0, dormantClients: 1);

        Assert.Equal(1, evidence.Failures[
            "LoadPhaseInvariantException:ownership subscription"]);
    }

    [Theory]
    [InlineData("load player reducer")]
    [InlineData("ship subscription cache")]
    [InlineData("command tracking")]
    public void InvariantStagesRemainDistinct(string phase)
    {
        var tracker = new LoadRunTracker();
        tracker.RecordFailure(new LoadPhaseInvariantException(phase, "failure"));

        var evidence = tracker.Snapshot(activeClients: 0, dormantClients: 1);

        Assert.Equal(1, evidence.Failures[$"LoadPhaseInvariantException:{phase}"]);
    }

    [Fact]
    public void ConcurrentUpdatesAreNotLost()
    {
        var tracker = new LoadRunTracker();

        Parallel.For(0, 1_000, _ =>
        {
            tracker.RecordAttempt();
            tracker.RecordConnected(TimeSpan.FromMilliseconds(1));
            tracker.RecordRetained();
        });

        var evidence = tracker.Snapshot(activeClients: 200, dormantClients: 800);
        Assert.Equal(1_000, evidence.AttemptedClients);
        Assert.Equal(1_000, evidence.ConnectedClients);
        Assert.Equal(1_000, evidence.RetainedClients);
    }
}
