using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    /// <summary>
    /// Finds a captain's trust row, opening one at full trust if this is the first thing she
    /// has ever done worth writing down.
    /// </summary>
    /// <remarks>
    /// A row is only opened when there is something to put in it, so a captain who sails and
    /// fights and never argues with the server has no row at all. That is the point: the
    /// table is a list of people to look at, and an empty table means nobody.
    /// </remarks>
    private static PlayerTrust TrustFor(ReducerContext ctx, Identity owner, ulong tick)
    {
        if (ctx.Db.PlayerTrust.Owner.Find(owner) is { } existing)
        {
            return existing;
        }

        return ctx.Db.PlayerTrust.Insert(new PlayerTrust
        {
            Owner = owner,
            Score = TrustScoreRules.StartingScore,
            LastPenaltyTick = tick,
            RecentCourseTicks = new List<ulong>(TrustScoreRules.MetronomeSampleCount),
        });
    }

    /// <summary>
    /// Takes the cost of one signal off a score, giving back whatever the quiet since the last
    /// penalty has earned first (SEA_5 12.4).
    /// </summary>
    /// <remarks>
    /// Recovery is worked out here rather than on a timer because a score nobody is reading is
    /// a score that does not need to be right yet. The only moment it has to be correct is the
    /// moment it changes, and that is this one.
    /// </remarks>
    private static void Penalise(ref PlayerTrust trust, TrustSignal signal, ulong tick)
    {
        var quiet = tick > trust.LastPenaltyTick ? tick - trust.LastPenaltyTick : 0UL;
        trust.Score = TrustScoreRules.Apply(
            TrustScoreRules.Recover(trust.Score, quiet),
            signal);
        trust.LastPenaltyTick = tick;
        CountSignal(ref trust, signal);
    }

    /// <summary>
    /// Adds one to the tally the signal belongs to. The score alone says a client is wrong;
    /// the tallies say how, which is what an operator needs to tell a bad connection from a bot.
    /// </summary>
    private static void CountSignal(ref PlayerTrust trust, TrustSignal signal)
    {
        switch (signal)
        {
            case TrustSignal.DroppedCommand:
                trust.DroppedCommands++;
                break;
            case TrustSignal.RejectedCommand:
                trust.RejectedCommands++;
                break;
            case TrustSignal.ImpossibleMovement:
                trust.ImpossibleMovements++;
                break;
            case TrustSignal.ImpossibleFire:
                trust.ImpossibleFires++;
                break;
            case TrustSignal.MetronomicCommands:
                trust.MetronomicRuns++;
                break;
            default:
                // EdgeOfRangeTargeting keeps its own tally: every volley on the line is counted,
                // not only the tenth one that is reported, so counting it here would count twice.
                break;
        }
    }

    /// <summary>
    /// Judges one command a player sent, once it has been either refused or carried out. This is
    /// the single place a player's orders are scored: every one of them comes through here, and
    /// nothing an NPC does ever does, which is what keeps the hostiles out of a table that is
    /// about people.
    /// </summary>
    /// <remarks>
    /// An acceptance is not evidence of anything and costs nothing, except that an accepted
    /// course is also a sample of how she clicks -- the only signal in SEA_5 12.4 that is read
    /// off good orders rather than bad ones.
    /// </remarks>
    private static void RecordTrust(
        ReducerContext ctx,
        ulong tick,
        ShipCommandKind kind,
        CommandDecision decision)
    {
        if (TrustScoreRules.SignalFor(decision.Rejection) is { } signal)
        {
            RecordTrustSignal(ctx, ctx.Sender, signal, tick);
            return;
        }

        if (kind == ShipCommandKind.SetCourse)
        {
            RecordCourseCadence(ctx, ctx.Sender, tick);
        }
    }

    /// <summary>
    /// Writes down one order the server had to argue with.
    /// </summary>
    private static void RecordTrustSignal(
        ReducerContext ctx,
        Identity owner,
        TrustSignal signal,
        ulong tick)
    {
        var trust = TrustFor(ctx, owner, tick);
        Penalise(ref trust, signal, tick);
        ctx.Db.PlayerTrust.Owner.Update(trust);
    }

    /// <summary>
    /// Files one accepted course under the captain who ordered it, and reports a script if the
    /// twenty in the window are one.
    /// </summary>
    /// <remarks>
    /// The window is copied to the stack, slid, and copied back rather than being walked in
    /// place: twenty ulongs is a hundred and sixty bytes, and it keeps the rule that decides
    /// this a pure function of a span. Eight courses a second is the ceiling a captain can
    /// reach, so this is at worst eight small writes a second for someone holding the mouse
    /// down, and none at all for someone sailing a long leg.
    /// </remarks>
    private static void RecordCourseCadence(ReducerContext ctx, Identity owner, ulong tick)
    {
        var trust = TrustFor(ctx, owner, tick);
        Span<ulong> window = stackalloc ulong[TrustScoreRules.MetronomeSampleCount];
        var samples = trust.RecentCourseTicks;
        var first = Math.Max(0, samples.Count - TrustScoreRules.MetronomeSampleCount);
        var count = samples.Count - first;
        for (var index = 0; index < count; index++)
        {
            window[index] = samples[first + index];
        }

        count = TrustScoreRules.RecordCourse(window, count, tick, out var metronomic);
        samples.Clear();
        for (var index = 0; index < count; index++)
        {
            samples.Add(window[index]);
        }

        if (metronomic)
        {
            Penalise(ref trust, TrustSignal.MetronomicCommands, tick);
        }

        ctx.Db.PlayerTrust.Owner.Update(trust);
    }

    /// <summary>
    /// Judges one volley by where it was fired from. A shot that lands inside the half-square of
    /// grace and nowhere else, over and over, is a client that has computed the line rather than
    /// a captain who has sailed to it (SEA_5 12.4).
    /// </summary>
    /// <remarks>
    /// The owner lookup sits behind the range test on purpose. Nearly every volley in the world
    /// is fired somewhere short of the line, and those cost this nothing but one comparison
    /// against a number the shot already needed.
    /// </remarks>
    private static void ScoreEdgeOfRangeVolley(
        ReducerContext ctx,
        TickWorld world,
        Ship source,
        AmmunitionContent ammunition,
        float distanceSquares)
    {
        if (!TrustScoreRules.IsEdgeOfRange(
                distanceSquares,
                RangeRules.DebuffedSquares(source.RangeSquares, ammunition.RangePenaltySquares)))
        {
            return;
        }

        if (ctx.Db.PlayerOwnership.ShipEntityId.Find(source.EntityId) is { } owner)
        {
            RecordEdgeOfRangeVolley(ctx, owner.Owner, world.Tick);
        }
    }

    /// <summary>
    /// Notes a volley that landed exactly on the grace line. One in ten is reported: a good
    /// player kiting at the edge of her guns should pay a few points over a long fight, and a
    /// client computing the line to four decimal places should pay for every ten it computes.
    /// </summary>
    private static void RecordEdgeOfRangeVolley(ReducerContext ctx, Identity owner, ulong tick)
    {
        var trust = TrustFor(ctx, owner, tick);
        trust.EdgeOfRangeVolleys++;
        if (TrustScoreRules.ShouldReportEdgeOfRange(trust.EdgeOfRangeVolleys))
        {
            Penalise(ref trust, TrustSignal.EdgeOfRangeTargeting, tick);
        }

        ctx.Db.PlayerTrust.Owner.Update(trust);
    }
}
