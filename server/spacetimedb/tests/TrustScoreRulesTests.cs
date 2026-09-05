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
}
