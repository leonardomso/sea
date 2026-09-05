using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

/// <summary>
/// The twenty tests SEA_5_PHYSICS §13 says must pass before deployment, written out in the
/// document's own order so the specification and the suite can be read side by side.
/// </summary>
/// <remarks>
/// Each fact calls the rule it is about directly rather than going through the module, so a
/// failure names what broke. Where §13 describes a whole ship over several ticks, the fact
/// sails the rule for those ticks; there is no live module in this suite and nothing here
/// needs one, because every number §13 quotes is decided in the domain layer.
/// </remarks>
public sealed class SeaFiveAcceptanceTests
{
    private const int MaskSize = 64;
    private static readonly GameContent Catalog = ContentCatalog.CreateDefault();

    // §13 test 1. A Brig from (50,50) to (250,50) on open water: x = 100 after 10 s, and
    // she is stopped on the mark after 40 s. Her rating is read off the catalogue rather
    // than written here, so a content change that moves her speed fails this.
    [Fact]
    public void AStraightCourseIsWalkedAtHerRating()
    {
        var brig = Catalog.Hulls.Single(hull => hull.Tier == 3);
        Assert.Equal(5.0f, brig.SpeedSquaresPerSecond, 4);
        var travel = brig.SpeedSquaresPerSecond * WorldRules.SecondsPerTick;
        var route = new[] { new RouteWaypoint(250f, 50f) };

        var atTenSeconds = SailFor(route, 10 * (int)WorldRules.TickRateHz, travel, 50f, 50f, 90f);
        Assert.Equal(100f, atTenSeconds.PositionX, 0.05);
        Assert.Equal(50f, atTenSeconds.PositionY, 4);
        Assert.False(atTenSeconds.Arrived);

        var atFortySeconds = SailFor(route, 40 * (int)WorldRules.TickRateHz, travel, 50f, 50f, 90f);
        Assert.True(atFortySeconds.Arrived);
        Assert.Equal(250f, atFortySeconds.PositionX, 0.05);
        Assert.Equal(50f, atFortySeconds.PositionY, 4);
    }

    // §13 test 4 (§4.1.5). A mark inside a land-locked lake is refused with NO_PATH, and so
    // is a mark on the beach itself: there is nowhere to put her either way.
    [Fact]
    public void ACourseWithNoWayThroughIsRefused()
    {
        Span<RouteWaypoint> route = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];
        var scratch = new PathfindingScratch(MaskSize);

        var intoTheLake = PathfindingRules.TryBuildRoute(
            WalledLake(), scratch, 4f, 4f, 45.5f, 45.5f, route, out var lakeCount);
        Assert.Equal(PathOutcome.NoPath, intoTheLake);
        Assert.Equal(0, lakeCount);

        var ontoTheBeach = PathfindingRules.TryBuildRoute(
            WalledLake(), scratch, 4f, 4f, 41.5f, 41.5f, route, out var beachCount);
        Assert.Equal(PathOutcome.NoPath, ontoTheBeach);
        Assert.Equal(0, beachCount);
    }

    // §13 test 3 (§4.1.5). A course round land has at least two legs and at most thirty-two,
    // and not one of them crosses a square the mask calls land.
    [Fact]
    public void ACourseRoundLandBendsAndStaysWet()
    {
        Span<RouteWaypoint> route = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];
        var mask = WallWithAGap();

        var outcome = PathfindingRules.TryBuildRoute(
            mask, new PathfindingScratch(MaskSize), 4f, 4f, 60f, 4f, route, out var count);

        Assert.Equal(PathOutcome.Routed, outcome);
        Assert.InRange(count, 2, RouteRules.MaximumWaypoints);

        var fromX = 4f;
        var fromY = 4f;
        for (var leg = 0; leg < count; leg++)
        {
            Assert.True(
                mask.SegmentIsClear(fromX, fromY, route[leg].X, route[leg].Y),
                "a leg of the course crosses land");
            fromX = route[leg].X;
            fromY = route[leg].Y;
        }
    }

    // §13 test 18 (§4.1.8). Nine MoveTo inside one second: eight are answered and the ninth
    // is dropped, never queued. The window opens again a second after it started.
    [Fact]
    public void TheNinthMoveInASecondIsDropped()
    {
        var windowStart = 0UL;
        var used = 0u;
        var accepted = 0;

        for (var order = 0; order < 9; order++)
        {
            if (MoveRateRules.Allow(ref windowStart, ref used, tick: 0UL))
            {
                accepted++;
            }
        }

        Assert.Equal((int)MoveRateRules.MaximumPerSecond, accepted);
        Assert.True(MoveRateRules.Allow(ref windowStart, ref used, MoveRateRules.WindowTicks));
    }

    // §13 test 11 (§7.2). Half a square of grace, checked when the trigger is pulled: a
    // twenty-four square gun fires at 24.4 and does not at 24.6.
    [Fact]
    public void AGunFiresIntoItsGraceAndNoFurther()
    {
        Assert.True(RangeRules.IsWithinRange(24.4f, RangeRules.BaseRangeSquares(3)));
        Assert.False(RangeRules.IsWithinRange(24.6f, RangeRules.BaseRangeSquares(3)));
    }

    // §13 test 13 (§8.4). Dead ahead is the bow; the arcs either side of it are 45° and 135°.
    [Fact]
    public void AShotFromDeadAheadHitsTheBow()
    {
        Assert.Equal(ArmorFace.Front, CombatRules.FaceHit(defenderHeadingDegrees: 90f, bearingToAttackerDegrees: 90f));
        Assert.Equal(ArmorFace.Front, CombatRules.FaceHit(90f, 134f));
        Assert.Equal(ArmorFace.Sides, CombatRules.FaceHit(90f, 136f));
        Assert.Equal(ArmorFace.Back, CombatRules.FaceHit(90f, 270f));
    }

    // §13 test 7 (§5.1). The wind is worth a tenth either way: with it 1.10, into it 0.90,
    // across it nothing at all.
    [Fact]
    public void TheWindIsWorthATenthEitherWay()
    {
        Assert.Equal(1.10f, SpeedRules.WindMultiplier(headingDegrees: 45f, windDirectionDegrees: 45f), 4);
        Assert.Equal(0.90f, SpeedRules.WindMultiplier(45f, 225f), 4);
        Assert.Equal(1.00f, SpeedRules.WindMultiplier(45f, 135f), 4);
    }

    // §13 test 8 (§5.1). Inside a storm and sailing upwind, the two land together and neither
    // one swallows the other: 0.85 x 0.90.
    [Fact]
    public void AStormAndAHeadWindBothCount()
    {
        var speed = SpeedRules.Effective(Sailing(heading: 0f, wind: 180f, storm: true));

        Assert.Equal(5f * SpeedRules.StormMultiplier * 0.90f, speed, 3);
    }

    // §13 test 9 (§5.2). Damaged at or under half hull, Burning at or under a quarter.
    [Fact]
    public void AHurtHullSailsSlower()
    {
        Assert.Equal(SpeedRules.NormalHpMultiplier, SpeedRules.HpStateMultiplier(60, 100), 4);
        Assert.Equal(SpeedRules.DamagedHpMultiplier, SpeedRules.HpStateMultiplier(40, 100), 4);
        Assert.Equal(SpeedRules.BurningHpMultiplier, SpeedRules.HpStateMultiplier(20, 100), 4);
    }

    // §13 test 10 (§5.1). Thirty-five per cent of fitted bonuses is worth twenty-five: the cap
    // is on the sum, and it is kept.
    [Fact]
    public void BonusesAddThenStopAtTheCap()
    {
        Assert.Equal(5f * 1.25f, SpeedRules.Effective(Sailing(bonus: 0.35f)), 3);
        Assert.Equal(5f * 1.25f, SpeedRules.Effective(Sailing(bonus: SpeedRules.BonusCap)), 3);
        Assert.Equal(5f * 1.20f, SpeedRules.Effective(Sailing(bonus: 0.20f)), 3);
    }

    // §13 test 8's other half (§5.1). Two slows multiply, and the product -- not each term --
    // is what stops at half. Floored term by term, a chained and grapeshotted hull crawled.
    [Fact]
    public void TwoSlowsMultiplyDownToAFloorOfAHalf()
    {
        Assert.Equal(5f * 0.64f, SpeedRules.Effective(Sailing(debuff: 0.8f * 0.8f)), 3);
        Assert.Equal(5f * SpeedRules.DebuffFloor, SpeedRules.Effective(Sailing(debuff: 0.6f * 0.6f)), 3);
    }

    // §13 test 14 (§5.2). A freeze is not a slow: she stops dead, and the course is still
    // hers when it lifts -- same leg, same mark, not arrived.
    [Fact]
    public void AFrozenShipMakesNoWayAndKeepsHerCourse()
    {
        Assert.Equal(0f, SpeedRules.Effective(Sailing(frozen: true)), 4);

        var route = new[] { new RouteWaypoint(10f, 0f), new RouteWaypoint(20f, 0f) };
        var held = SailFor(route, ticks: 30, travelPerTick: 0f, x: 5f, y: 0f, headingDegrees: 90f);

        Assert.Equal(5f, held.PositionX, 4);
        Assert.Equal(0, held.WaypointIndex);
        Assert.False(held.Arrived);
    }

    // §13 test 6 (§5.2). A ship at anchor is carried by the current, and the carry stops at
    // the shore: she fetches up on the last water she was on and never enters land.
    [Fact]
    public void AShipAtAnchorDriftsAndFetchesUpOnTheShore()
    {
        var mask = CoastAt(cellX: 40);

        var carried = DriftRules.Drift(35f, 10f, velocityX: 1f, velocityY: 0f, deltaSeconds: 1f, mask);
        Assert.Equal(36f, carried.X, 4);
        Assert.Equal(10f, carried.Y, 4);

        var againstTheBeach = DriftRules.Drift(39.6f, 10f, 1f, 0f, 1f, mask);
        Assert.Equal(39.6f, againstTheBeach.X, 4);
        Assert.False(mask.IsLand(againstTheBeach.X, againstTheBeach.Y));
    }

    // §13 test 16, first half (§10.2). Sailing into the edge band raises the crossing prompt,
    // and answering it puts her eight squares inside the far side of the next chart.
    [Fact]
    public void TheEdgeBandOffersTheNextChart()
    {
        var edge = MapEdgeRules.EdgeAt(x: 396f, y: 200f);
        Assert.Equal(MapEdge.East, edge);

        var offer = MapCrossingRules.Offer(mapId: 1, edge, heldX: 394f, heldY: 200f);

        Assert.NotNull(offer);
        Assert.Equal(2, offer!.Value.ToMapId);
        Assert.Equal(WorldRules.MapMin + MapEdgeRules.SpawnInsetSquares, offer.Value.SpawnX, 4);
        Assert.Equal(200f, offer.Value.SpawnY, 4);
    }

    // §13 test 16, second half (§10.2). She arrives with no course, no target and nothing
    // stuck to her: all three were facts about the chart she left.
    [Fact]
    public void ACrossingCostsHerCourseTargetAndEffects()
    {
        var offer = MapCrossingRules.Offer(1, MapEdge.East, 394f, 200f)!.Value;

        var arrival = MapCrossingRules.Arrive(offer);

        Assert.Equal(offer.ToMapId, arrival.MapId);
        Assert.Equal(offer.SpawnX, arrival.PositionX, 4);
        Assert.Equal(SpatialRules.ChunkCoordinate(offer.SpawnX), arrival.ChunkX);
        Assert.False(arrival.HasRoute);
        Assert.Equal(0UL, arrival.TargetEntityId);
        Assert.False(arrival.IsEngaged);
        Assert.Equal(0, (int)arrival.MovementStatusMask);
        Assert.Equal(0f, arrival.MovementSlowMagnitude, 4);
        Assert.Equal(0, (int)arrival.EnvironmentExposureCode);
    }

    // §13 test 17 (§9.1). A ship above half hull cannot be grappled, and being too healthy is
    // the reason she is given -- ahead of a cooldown that is also standing.
    [Fact]
    public void AHealthyShipCannotBeBoarded()
    {
        Assert.False(BoardingRules.CanBoard(defenderHull: 60, defenderMaxHull: 100));
        Assert.True(BoardingRules.CanBoard(50, 100));

        Assert.Equal(BoardingRejection.TargetNotBoardable, BoardingRules.Validate(Grapple(defenderHull: 60)));
        Assert.Equal(BoardingRejection.None, BoardingRules.Validate(Grapple(defenderHull: 50)));
    }

    // §13's boarding outcome (§9.2, SEA_3 §4.3). A win takes a tenth of the loser's maximum
    // hull off her and silences her guns for three seconds.
    [Fact]
    public void ABoardingWinCostsTheLoserATenthAndThreeSeconds()
    {
        var brig = Catalog.Hulls.Single(hull => hull.Tier == 3);
        var attacker = new BoardingParty(Hands: 40, MoraleFraction: 1f, Tier: 3);
        var defender = new BoardingParty(Hands: 10, MoraleFraction: 0.5f, Tier: 2);

        var outcome = BoardingRules.Resolve(attacker, defender, brig.HitPoints, roll: 0f);

        Assert.True(outcome.AttackerWon);
        Assert.Equal((uint)MathF.Round(brig.HitPoints * 0.10f), outcome.HullDamage);
        Assert.Equal(3UL * WorldRules.TickRateHz, outcome.SilenceTicks);
    }

    // §13's critical (§8.5). One shot in ten, for one and a half times the damage that came
    // through armour, and the same four inputs always give the same answer.
    [Fact]
    public void OneShotInTenIsACriticalForHalfAgain()
    {
        Assert.Equal(150u, CriticalHitRules.Apply(100u, isCritical: true));
        Assert.Equal(100u, CriticalHitRules.Apply(100u, isCritical: false));

        var criticals = 0;
        const int Shots = 20000;
        for (var shot = 0; shot < Shots; shot++)
        {
            if (CriticalHitRules.IsCritical(seed: 7UL, (ulong)shot, attackerId: 11UL, defenderId: 12UL))
            {
                criticals++;
            }
        }

        Assert.InRange(criticals / (double)Shots, 0.09, 0.11);
    }

    // §13 test 20 (§12.5). The same log sailed twice gives the same hash for every tick, and a
    // log that differs by one mark does not.
    [Fact]
    public void TheSameLogReplaysToTheSameHash()
    {
        var log = new[]
        {
            new ReplayCommand(2u, ReplayCommandKind.SetCourse, 0f, 0f)
            {
                Corners = new[] { new RouteWaypoint(60f, 50f), new RouteWaypoint(60f, 90f) },
            },
            new ReplayCommand(120u, ReplayCommandKind.StopCourse, 0f, 0f),
            new ReplayCommand(140u, ReplayCommandKind.SetCourse, 20f, 90f),
        };
        var start = new ReplayState(50f, 50f, 90f, 0, false);

        var first = ReplayRules.Run(400u, start, log, travelPerTick: 0.5f);
        var second = ReplayRules.Run(400u, start, log, 0.5f);

        Assert.Equal(first.StateHash, second.StateHash);
        Assert.Equal(first.State, second.State);

        var moved = log.ToArray();
        moved[2] = new ReplayCommand(140u, ReplayCommandKind.SetCourse, 20.5f, 90f);
        Assert.NotEqual(first.StateHash, ReplayRules.Run(400u, start, moved, 0.5f).StateHash);
    }

    // §13 test 19 (§11). An enemy takes an interest inside twenty squares, holds at eight
    // tenths of her gun's reach, and turns for home past sixty from where she spawned.
    [Fact]
    public void AnEnemyChasesHoldsAndLeashes()
    {
        var hold = NpcMovementRules.HoldDistanceSquares(RangeRules.BaseRangeSquares(1));
        Assert.Equal(18f * NpcMovementRules.HoldDistanceFraction, hold, 4);

        Assert.Equal(NpcIntent.Wander, NpcMovementRules.Decide(20.5f, 10f, hold));
        Assert.Equal(NpcIntent.Chase, NpcMovementRules.Decide(19.5f, 10f, hold));
        Assert.Equal(NpcIntent.Hold, NpcMovementRules.Decide(hold - 0.5f, 10f, hold));
        Assert.Equal(NpcIntent.Leash, NpcMovementRules.Decide(5f, 60.5f, hold));
    }

    private static RouteStep SailFor(
        RouteWaypoint[] route,
        int ticks,
        float travelPerTick,
        float x,
        float y,
        float headingDegrees)
    {
        var step = new RouteStep(x, y, headingDegrees, 0, false);
        for (var tick = 0; tick < ticks; tick++)
        {
            step = RouteRules.Advance(
                route,
                step.WaypointIndex,
                step.PositionX,
                step.PositionY,
                step.HeadingDegrees,
                travelPerTick);
        }

        return step;
    }

    // A Brig's rating with the wind on the beam, so a fact that says nothing about the weather
    // is not quietly given a tenth of a knot by it.
    private static SpeedInputs Sailing(
        float baseSpeed = 5f,
        float bonus = 0f,
        uint hull = 100,
        uint maxHull = 100,
        float heading = 0f,
        float wind = 90f,
        bool storm = false,
        float debuff = 1f,
        bool frozen = false) =>
        new(baseSpeed, bonus, hull, maxHull, heading, wind, storm, debuff, frozen);

    private static BoardingRequest Grapple(uint defenderHull) => new()
    {
        SourceAlive = true,
        TargetSelected = true,
        TargetAlive = true,
        InPort = false,
        DistanceSquares = 3.9f,
        DefenderHull = defenderHull,
        DefenderMaxHull = 100,
        AttackerHands = 40,
        AttackerMaxHands = 40,
        CurrentTick = 1000UL,
        AttackerCooldownUntilTick = 0UL,
        DefenderImmuneUntilTick = 0UL,
    };

    private static LandMask CoastAt(int cellX)
    {
        var bits = new ulong[LandMask.WordCount(MaskSize)];
        for (var y = 0; y < MaskSize; y++)
        {
            for (var x = cellX; x < MaskSize; x++)
            {
                var index = (y * MaskSize) + x;
                bits[index >> 6] |= 1UL << (index & 63);
            }
        }

        return new LandMask(MaskSize, bits);
    }

    private static LandMask WallWithAGap()
    {
        var bits = new ulong[LandMask.WordCount(MaskSize)];
        for (var y = 0; y < MaskSize; y++)
        {
            if (y >= 30 && y < 34)
            {
                continue;
            }

            var index = (y * MaskSize) + 32;
            bits[index >> 6] |= 1UL << (index & 63);
        }

        return new LandMask(MaskSize, bits);
    }

    private static LandMask WalledLake()
    {
        var bits = new ulong[LandMask.WordCount(MaskSize)];
        for (var y = 40; y <= 50; y++)
        {
            for (var x = 40; x <= 50; x++)
            {
                var index = (y * MaskSize) + x;
                var inTheLake = x >= 44 && x <= 46 && y >= 44 && y <= 46;
                if (!inTheLake)
                {
                    bits[index >> 6] |= 1UL << (index & 63);
                }
            }
        }

        return new LandMask(MaskSize, bits);
    }
}
