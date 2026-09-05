namespace Sea.Server;

/// <summary>One thing a client did that the server had to argue with (SEA_5 §12.4).</summary>
public enum TrustSignal : byte
{
    DroppedCommand = 0,
    RejectedCommand = 1,
    ImpossibleMovement = 2,
    ImpossibleFire = 3,
    MetronomicCommands = 4,
    EdgeOfRangeTargeting = 5,
}

/// <summary>How hard a person should look at a captain, and nothing more.</summary>
public enum TrustBand : byte
{
    Trusted = 0,
    Watched = 1,
    Flagged = 2,
}

/// <summary>
/// A number that says how much a captain's client has been arguing with the
/// server (SEA_5 §12).
/// </summary>
/// <remarks>
/// <para>
/// Nothing here punishes anybody. A low score is a reason for a person to look,
/// and the bands exist so that looking can be sorted. Every penalty is small
/// enough that a bad connection cannot flag an honest player inside an hour,
/// and the recovery is fast enough that one bad session does not follow them.
/// </para>
/// <para>
/// Everything here is a pure function of what the server already wrote down. By
/// SEA_5 §12.2 a client never sends a position, so there is no claimed movement
/// to check: what the server has is the tick it stamped on each order and the
/// positions it computed itself. Both signals SEA_5 §12.4 names by hand are read
/// off those, which is why neither can be faked away by a client.
/// </para>
/// <para>
/// Both tolerances below are noise budgets, not taste. A threshold tighter than
/// the simulation's own measurement error would flag honest captains, and a
/// flagged honest captain is worse than a missed bot: the score exists to
/// shorten a person's reading list, so filling it with noise makes it useless.
/// </para>
/// </remarks>
public static class TrustScoreRules
{
    public const int StartingScore = 100;
    public const int MinimumScore = 0;
    public const int MaximumScore = 100;

    public const int DroppedMovePenalty = 1;

    public const int TrustedFloor = 70;
    public const int WatchedFloor = 40;

    /// <summary>Ten points an hour, which at 10 Hz is one point every six minutes.</summary>
    public const ulong RecoveryIntervalTicks = 3_600UL;

    /// <summary>
    /// How many courses in a row are looked at before calling a client a
    /// metronome, and how much they may vary and still count as one.
    /// </summary>
    public const int MetronomeSampleCount = 20;

    /// <summary>
    /// One tick. Orders are stamped by the server at 10 Hz, so two intervals that
    /// were identical in the world can still read a tick apart purely from which
    /// side of a boundary each one landed on. Anything tighter than a tick would
    /// be measuring the clock rather than the client.
    /// </summary>
    public const ulong MetronomeToleranceTicks = 1UL;

    /// <summary>
    /// One second, under which the server declines to judge. A hand's own
    /// unsteadiness is a fraction of the interval it is keeping, so at a second
    /// or more it is worth several ticks and a metronome stands out; at the
    /// eight-orders-a-second ceiling it is worth less than the tick the order was
    /// stamped with, and a captain hammering the mouse reads exactly like a
    /// script. There is no evidence at that cadence, so this reports none.
    /// </summary>
    public const ulong MetronomeMinimumGapTicks = 10UL;

    /// <summary>
    /// A tenth of a square either side of the grace line (SEA_5 §12.4). Wide
    /// enough to swallow the simulation's own error -- a hull steers on
    /// <see cref="GeometryRules.Direction"/>, which reads a quarter-degree table
    /// and can sit 0.125 degrees off, worth under a ten-thousandth of a square of
    /// range at the longest gun on the map -- and still narrow enough that a ship
    /// under way crosses the whole band inside a single tick.
    /// </summary>
    public const float EdgeOfRangeToleranceSquares = 0.1f;

    /// <summary>
    /// The half-square of slack a shot at the edge is allowed (SEA_5 §15,
    /// RANGE_GRACE). This is the same number as the grace the firing check
    /// applies; it is written here because <c>RangeRules</c> does not carry it
    /// yet, and it belongs on <c>RangeRules.GraceSquares</c> the moment that
    /// constant exists.
    /// </summary>
    public const float RangeGraceSquares = 0.5f;

    public static int PenaltyFor(TrustSignal signal) => signal switch
    {
        TrustSignal.DroppedCommand => DroppedMovePenalty,
        TrustSignal.RejectedCommand => 2,
        TrustSignal.ImpossibleMovement => 10,
        TrustSignal.ImpossibleFire => 10,
        TrustSignal.MetronomicCommands => 5,
        TrustSignal.EdgeOfRangeTargeting => 5,
        _ => 0,
    };

    /// <summary>
    /// Every event is stamped by the server, so the gaps between a captain's
    /// courses are a measurement rather than something a client reports. Twenty
    /// gaps that all match to within a tick is not a hand (SEA_5 §12.4).
    /// </summary>
    /// <remarks>
    /// The gaps are compared against each other rather than against the first of
    /// them, so the verdict does not hang off whichever order happens to open the
    /// window. Ticks that run backwards are not a cadence at all and are answered
    /// <see langword="false"/>: a wrong-way-round subtraction should read as no
    /// evidence, never as an accusation.
    /// </remarks>
    public static bool IsMetronomic(ReadOnlySpan<ulong> commandTicks)
    {
        if (commandTicks.Length < MetronomeSampleCount)
        {
            return false;
        }

        var shortest = ulong.MaxValue;
        var longest = ulong.MinValue;
        for (var index = 1; index < commandTicks.Length; index++)
        {
            if (commandTicks[index] < commandTicks[index - 1])
            {
                return false;
            }

            var gap = commandTicks[index] - commandTicks[index - 1];
            shortest = Math.Min(shortest, gap);
            longest = Math.Max(longest, gap);
        }

        return shortest >= MetronomeMinimumGapTicks &&
            longest - shortest <= MetronomeToleranceTicks;
    }

    /// <summary>
    /// Holding station at exactly range minus the grace is a number a client
    /// worked out, not a distance a captain sailed to. It is only a signal: a
    /// good player kiting at the edge trips it occasionally, which is why the
    /// penalty is five points and not a ban.
    /// </summary>
    public static bool IsEdgeOfRange(float distanceSquares, float effectiveRangeSquares) =>
        MathF.Abs(distanceSquares - (effectiveRangeSquares - RangeGraceSquares)) <=
        EdgeOfRangeToleranceSquares;

    public static int Apply(int score, TrustSignal signal) =>
        Math.Clamp(score - PenaltyFor(signal), MinimumScore, MaximumScore);

    /// <summary>
    /// Good behaviour pays a point back every six minutes. The points earned are
    /// capped before they are added: an elapsed count wide enough to overflow the
    /// addition would otherwise wrap and turn a long quiet spell into a penalty.
    /// </summary>
    public static int Recover(int score, ulong elapsedTicks)
    {
        var earned = elapsedTicks / RecoveryIntervalTicks;
        var capped = earned >= (ulong)MaximumScore ? MaximumScore : (int)earned;
        return Math.Clamp(score + capped, MinimumScore, MaximumScore);
    }

    public static TrustBand BandFor(int score)
    {
        if (score >= TrustedFloor)
        {
            return TrustBand.Trusted;
        }

        return score >= WatchedFloor ? TrustBand.Watched : TrustBand.Flagged;
    }
}
