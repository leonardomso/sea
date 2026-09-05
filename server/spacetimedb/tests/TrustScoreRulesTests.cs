using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class TrustScoreRulesTests
{
    [Fact]
    public void EveryoneStartsFullyTrusted()
    {
        Assert.Equal(100, TrustScoreRules.StartingScore);
    }

    [Fact]
    public void ADroppedCommandCostsALittle()
    {
        Assert.Equal(99, TrustScoreRules.Apply(100, TrustSignal.DroppedCommand));
    }

    [Fact]
    public void ImpossibleMovementCostsALot()
    {
        Assert.Equal(90, TrustScoreRules.Apply(100, TrustSignal.ImpossibleMovement));
    }

    [Fact]
    public void TheScoreNeverGoesBelowZeroOrAboveAHundred()
    {
        Assert.Equal(0, TrustScoreRules.Apply(3, TrustSignal.ImpossibleMovement));
        Assert.Equal(100, TrustScoreRules.Recover(100, elapsedTicks: 36_000UL));
    }

    [Fact]
    public void AnHourOfGoodBehaviourRecoversTenPoints()
    {
        Assert.Equal(60, TrustScoreRules.Recover(50, elapsedTicks: 36_000UL));
    }

    [Fact]
    public void NoAmountOfWaitingCanTurnRecoveryIntoAPenalty()
    {
        // A caller that subtracts the ticks the wrong way round hands this an
        // enormous elapsed count. Recovery may run out of points to give; it may
        // never take one away.
        Assert.Equal(100, TrustScoreRules.Recover(50, elapsedTicks: ulong.MaxValue));
    }

    [Theory]
    [InlineData(100, TrustBand.Trusted)]
    [InlineData(70, TrustBand.Trusted)]
    [InlineData(69, TrustBand.Watched)]
    [InlineData(40, TrustBand.Watched)]
    [InlineData(39, TrustBand.Flagged)]
    [InlineData(0, TrustBand.Flagged)]
    public void TheScoreFallsIntoThreeBands(int score, TrustBand band)
    {
        Assert.Equal(band, TrustScoreRules.BandFor(score));
    }

    [Fact]
    public void AMetronomeIsNotAHand()
    {
        // Twenty courses exactly 1.2 s apart. No hand does this.
        var ticks = new ulong[20];
        for (var index = 0; index < ticks.Length; index++)
        {
            ticks[index] = (ulong)index * 12UL;
        }

        Assert.True(TrustScoreRules.IsMetronomic(ticks));
    }

    [Fact]
    public void AHandIsNotAMetronome()
    {
        ulong[] ticks =
        [
            0, 14, 25, 41, 50, 67, 74, 91, 103, 112,
            129, 138, 155, 161, 180, 188, 203, 219, 226, 244,
        ];

        Assert.False(TrustScoreRules.IsMetronomic(ticks));
    }

    [Fact]
    public void AHandDrummingAtTheRateLimitIsNotJudgedAtAll()
    {
        // Courses one and two ticks apart: someone hammering the mouse at the
        // eight-a-second ceiling. Their own jitter is smaller than the tick the
        // server stamps them with, so the gaps read as regular whatever the hand
        // actually did. There is no evidence here either way and the server says
        // nothing rather than inventing some.
        var ticks = new ulong[20];
        for (var index = 1; index < ticks.Length; index++)
        {
            ticks[index] = ticks[index - 1] + ((index % 2 == 0) ? 1UL : 2UL);
        }

        Assert.False(TrustScoreRules.IsMetronomic(ticks));
    }

    [Fact]
    public void TooFewCoursesToJudgeIsNotAnAccusation()
    {
        var ticks = new ulong[TrustScoreRules.MetronomeSampleCount - 1];
        for (var index = 0; index < ticks.Length; index++)
        {
            ticks[index] = (ulong)index * 12UL;
        }

        Assert.False(TrustScoreRules.IsMetronomic(ticks));
    }

    [Fact]
    public void ATargetHeldAtExactlyTheEdgeOfRangeIsASignal()
    {
        // Sitting on range minus the grace, volley after volley, is a number a
        // client computed, not a distance a captain sailed to.
        Assert.True(TrustScoreRules.IsEdgeOfRange(distanceSquares: 23.5f, effectiveRangeSquares: 24f));
    }

    [Fact]
    public void ATargetHeldAnywhereElseIsNot()
    {
        Assert.False(TrustScoreRules.IsEdgeOfRange(21.0f, 24f));
        Assert.False(TrustScoreRules.IsEdgeOfRange(23.9f, 24f));
    }

    [Fact]
    public void TheEdgeBandIsWiderThanTheTrigonometryTableIsWrong()
    {
        // GeometryRules.Direction reads a quarter-degree table, so a hull can sit
        // up to 0.125 degrees off the bearing it was given. At the longest range
        // on the map that moves the distance to a target by well under a
        // thousandth of a square, so the band is noise-proof; it is still under
        // the half-square a ship covers in one tick, so an honest captain sailing
        // through it is inside it for one volley and not a series.
        const float longestRangeSquares = 30f;
        var radialNoiseSquares =
            longestRangeSquares * (1f - MathF.Cos(0.125f * MathF.PI / 180f));

        Assert.True(TrustScoreRules.EdgeOfRangeToleranceSquares > radialNoiseSquares * 100f);
        Assert.True(TrustScoreRules.EdgeOfRangeToleranceSquares * 2f < 0.5f);
    }

    [Fact]
    public void NothingHereBansAnybody()
    {
        // SEA_5 §12: the score is evidence for a person to read, not an action.
        Assert.Equal(TrustBand.Flagged, TrustScoreRules.BandFor(0));
    }
    /// <summary>
    /// Drives the window the way the reducer does: one course in, the count back out.
    /// </summary>
    private static int Feed(Span<ulong> window, int count, ulong tick, out bool metronomic) =>
        TrustScoreRules.RecordCourse(window, count, tick, out metronomic);

    [Fact]
    public void ARunOfTwentyPerfectCoursesIsReportedOnceAndOnceOnly()
    {
        Span<ulong> window = stackalloc ulong[TrustScoreRules.MetronomeSampleCount];
        var count = 0;
        var reports = 0;

        // Forty courses exactly twelve ticks apart. That is two full windows' worth, but a
        // reported run empties the window, so it is one accusation and not twenty-one.
        for (var course = 0; course < 40; course++)
        {
            count = Feed(window, count, (ulong)(course * 12), out var metronomic);
            if (metronomic)
            {
                reports++;
                Assert.Equal(0, count);
            }
        }

        Assert.Equal(2, reports);
    }

    [Fact]
    public void TheWindowSaysNothingUntilItIsFull()
    {
        Span<ulong> window = stackalloc ulong[TrustScoreRules.MetronomeSampleCount];
        var count = 0;

        for (var course = 0; course < TrustScoreRules.MetronomeSampleCount - 1; course++)
        {
            count = Feed(window, count, (ulong)(course * 12), out var metronomic);
            Assert.False(metronomic);
        }

        Assert.Equal(TrustScoreRules.MetronomeSampleCount - 1, count);
    }

    [Fact]
    public void TheWindowSlidesRatherThanStartingOverSoAScriptCannotOutwaitIt()
    {
        Span<ulong> window = stackalloc ulong[TrustScoreRules.MetronomeSampleCount];
        var count = 0;
        var tick = 0UL;

        // Five courses by a hand first, so the window does not start on a boundary a bot
        // could have counted to. The run that follows is still caught.
        foreach (var gap in new ulong[] { 14, 11, 16, 9, 17 })
        {
            tick += gap;
            count = Feed(window, count, tick, out _);
        }

        var caught = false;
        for (var course = 0; course < TrustScoreRules.MetronomeSampleCount; course++)
        {
            tick += 12;
            count = Feed(window, count, tick, out var metronomic);
            caught |= metronomic;
        }

        Assert.True(caught);
    }

    [Fact]
    public void ACaptainWhoClicksLikeAPersonIsNeverAccused()
    {
        Span<ulong> window = stackalloc ulong[TrustScoreRules.MetronomeSampleCount];
        var count = 0;
        var tick = 0UL;
        ulong[] gaps =
        {
            14, 11, 16, 9, 17, 7, 17, 12, 9, 17,
            9, 17, 6, 19, 8, 15, 16, 7, 18, 13,
            14, 11, 16, 9, 17, 7, 17, 12, 9, 17,
        };

        foreach (var gap in gaps)
        {
            tick += gap;
            count = Feed(window, count, tick, out var metronomic);
            Assert.False(metronomic);
        }
    }

    [Theory]
    [InlineData(CommandRejectionCode.None, null)]
    [InlineData(CommandRejectionCode.RateLimited, TrustSignal.DroppedCommand)]
    [InlineData(CommandRejectionCode.InvalidCourse, TrustSignal.ImpossibleMovement)]
    [InlineData(CommandRejectionCode.OutOfRange, TrustSignal.ImpossibleFire)]
    [InlineData(CommandRejectionCode.Reloading, TrustSignal.RejectedCommand)]
    [InlineData(CommandRejectionCode.InPort, TrustSignal.RejectedCommand)]
    [InlineData(CommandRejectionCode.NoPath, TrustSignal.RejectedCommand)]
    [InlineData(CommandRejectionCode.TargetNotBoardable, TrustSignal.RejectedCommand)]
    public void EachRefusalIsEvidenceOfExactlyOneThing(
        CommandRejectionCode rejection,
        TrustSignal? expected)
    {
        Assert.Equal(expected, TrustScoreRules.SignalFor(rejection));
    }

    /// <summary>
    /// A course to a square that is not on the chart is not a captain being refused, it is a
    /// client asking for a place no hull can be. SEA_5 12.4 prices those far higher, so the
    /// two must not be answered with the same signal.
    /// </summary>
    [Fact]
    public void AskingForSomewhereNoShipCouldBeCostsFiveTimesBeingRefused()
    {
        Assert.Equal(
            TrustScoreRules.PenaltyFor(TrustSignal.RejectedCommand) * 5,
            TrustScoreRules.PenaltyFor(TrustSignal.ImpossibleMovement));
    }

    [Fact]
    public void EveryTenthVolleyOnTheGraceLineIsWorthReporting()
    {
        Assert.False(TrustScoreRules.ShouldReportEdgeOfRange(1u));
        Assert.False(TrustScoreRules.ShouldReportEdgeOfRange(9u));
        Assert.True(TrustScoreRules.ShouldReportEdgeOfRange(10u));
        Assert.False(TrustScoreRules.ShouldReportEdgeOfRange(11u));
        Assert.True(TrustScoreRules.ShouldReportEdgeOfRange(20u));
        Assert.False(TrustScoreRules.ShouldReportEdgeOfRange(0u));
    }

}
