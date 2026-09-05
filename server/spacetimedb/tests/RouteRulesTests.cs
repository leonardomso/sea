using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

/// <summary>
/// Following a course. SEA_5 §4.1.3 makes position exact linear interpolation along a
/// waypoint list, so the whole of movement is here: no acceleration, no braking curve,
/// no turning circle, and a tick that reaches a corner spends the rest of itself on the
/// next leg rather than stopping on the corner.
/// </summary>
public sealed class RouteRulesTests
{
    private static readonly RouteWaypoint[] StraightEast =
    {
        new(250f, 50f),
    };

    private static readonly RouteWaypoint[] Dogleg =
    {
        new(60f, 50f),
        new(60f, 90f),
    };

    private static readonly RouteWaypoint[] NoRoute = Array.Empty<RouteWaypoint>();

    [Fact]
    public void OneTickWalksExactlyOneTicksWorthOfSea()
    {
        var step = RouteRules.Advance(StraightEast, 0, 50f, 50f, 0f, 0.5f);

        Assert.Equal(50.5f, step.PositionX, 4);
        Assert.Equal(50f, step.PositionY, 4);
        Assert.Equal(90f, step.HeadingDegrees, 3);
        Assert.False(step.Arrived);
        Assert.Equal(0, step.WaypointIndex);
    }

    [Fact]
    public void ATickThatOvershootsAWaypointCarriesOnDownTheNextLeg()
    {
        // Sitting 1 square short of the corner with 3 squares of travel: 1 east
        // to the corner, then 2 south.
        var step = RouteRules.Advance(Dogleg, 0, 59f, 50f, 90f, 3f);

        Assert.Equal(60f, step.PositionX, 4);
        Assert.Equal(52f, step.PositionY, 4);
        Assert.Equal(180f, step.HeadingDegrees, 3);
        Assert.Equal(1, step.WaypointIndex);
        Assert.False(step.Arrived);
    }

    [Fact]
    public void TheLastWaypointStopsTheShipExactlyOnIt()
    {
        var step = RouteRules.Advance(StraightEast, 0, 249f, 50f, 90f, 5f);

        Assert.Equal(250f, step.PositionX, 4);
        Assert.Equal(50f, step.PositionY, 4);
        Assert.True(step.Arrived);
        Assert.Equal(1, step.WaypointIndex);
    }

    [Fact]
    public void AShipWithNoTravelLeftKeepsHerPlaceAndHerBearing()
    {
        var step = RouteRules.Advance(StraightEast, 0, 100f, 50f, 90f, 0f);

        Assert.Equal(100f, step.PositionX, 4);
        Assert.Equal(90f, step.HeadingDegrees, 3);
        Assert.False(step.Arrived);
    }

    [Fact]
    public void AFinishedRouteReportsArrivedAndKeepsTheOldBearing()
    {
        var step = RouteRules.Advance(StraightEast, 1, 250f, 50f, 90f, 5f);

        Assert.True(step.Arrived);
        Assert.Equal(250f, step.PositionX, 4);
        Assert.Equal(90f, step.HeadingDegrees, 3);
    }

    /// <summary>A ship with no course at all is stopped, not underway.</summary>
    [Fact]
    public void AShipWithNoRouteIsAlreadyArrived()
    {
        var step = RouteRules.Advance(NoRoute, 0, 120f, 30f, 270f, 0.5f);

        Assert.True(step.Arrived);
        Assert.Equal(120f, step.PositionX, 4);
        Assert.Equal(30f, step.PositionY, 4);
        Assert.Equal(270f, step.HeadingDegrees, 3);
        Assert.Equal(0, step.WaypointIndex);
    }

    /// <summary>
    /// SEA_5 §13 test 2 and §4.1.7: reversing is instant. A hull steering east and given a
    /// mark astern of her makes way west on the very next tick, by exactly one tick's
    /// travel, with no overshoot and no turning circle.
    /// </summary>
    [Fact]
    public void ReversingCourseMakesWayTheOtherWayOnTheSameTick()
    {
        RouteWaypoint[] astern = { new(50f, 50f) };

        var step = RouteRules.Advance(astern, 0, 100f, 50f, 90f, 0.5f);

        Assert.Equal(99.5f, step.PositionX, 4);
        Assert.Equal(50f, step.PositionY, 4);
        Assert.Equal(270f, step.HeadingDegrees, 3);
        Assert.False(step.Arrived);
    }

    [Fact]
    public void ABrigSailsTwoHundredSquaresInFortySecondsAtFiveASecond()
    {
        var positionX = 50f;
        var positionY = 50f;
        var heading = 0f;
        var index = 0;
        var arrived = false;
        var ticks = 0;
        for (var tick = 0; tick < 400 && !arrived; tick++)
        {
            var step = RouteRules.Advance(
                StraightEast, index, positionX, positionY, heading, 5f * WorldRules.SecondsPerTick);
            positionX = step.PositionX;
            positionY = step.PositionY;
            heading = step.HeadingDegrees;
            index = step.WaypointIndex;
            arrived = step.Arrived;
            ticks = tick + 1;

            if (tick == 99)
            {
                // SEA_5 §13 test 1: 10.0 s in, x = 100 within 0.05.
                Assert.Equal(100f, positionX, 2);
            }
        }

        Assert.True(arrived);
        Assert.Equal(400, ticks);
        Assert.Equal(250f, positionX, 3);
        Assert.Equal(50f, positionY, 3);
    }

    /// <summary>
    /// A becalmed hull standing inside <see cref="RouteRules.ArrivalRadius"/> of her last
    /// mark is standing on it. SEA_5 §4.1.3 stops a ship exactly on the last waypoint, and
    /// §13 test 5 wants two ships sent to one point to hold that same exact point; without
    /// this a hull with no way on her would report "still sailing" for the rest of the match
    /// over a tenth of a square she can never cross.
    /// </summary>
    [Fact]
    public void AHullInsideTheArrivalRadiusOfHerLastMarkIsStandingOnIt()
    {
        var gap = RouteRules.ArrivalRadius * 0.5f;

        var step = RouteRules.Advance(StraightEast, 0, 250f - gap, 50f, 90f, 0f);

        Assert.Equal(250f, step.PositionX, 4);
        Assert.Equal(50f, step.PositionY, 4);
        Assert.True(step.Arrived);
        Assert.Equal(1, step.WaypointIndex);
    }

    /// <summary>
    /// The arrival radius belongs to the last mark alone. Letting it round off the corners
    /// in between would cut a straightened A* path back across the land it was built to
    /// avoid (SEA_5 §4.1.5).
    /// </summary>
    [Fact]
    public void TheArrivalRadiusDoesNotCutTheCornersInBetween()
    {
        var gap = RouteRules.ArrivalRadius * 0.5f;

        var step = RouteRules.Advance(Dogleg, 0, 60f - gap, 50f, 90f, 0f);

        Assert.Equal(60f - gap, step.PositionX, 4);
        Assert.Equal(50f, step.PositionY, 4);
        Assert.False(step.Arrived);
        Assert.Equal(0, step.WaypointIndex);
    }

    /// <summary>
    /// How much sea is left is measured along the course, not across it. A* hands back a
    /// dogleg and the time it takes is its own length over her speed, whatever the straight
    /// line between the ends says.
    /// </summary>
    [Fact]
    public void TheSeaLeftIsMeasuredAlongTheCourseNotAcrossIt()
    {
        Assert.Equal(50f, RouteRules.RemainingDistance(Dogleg, 0, 50f, 50f), 3);
        Assert.Equal(40f, RouteRules.RemainingDistance(Dogleg, 1, 60f, 50f), 3);
    }

    [Fact]
    public void AFinishedCourseHasNoSeaLeft()
    {
        Assert.Equal(0f, RouteRules.RemainingDistance(StraightEast, 1, 250f, 50f), 3);
        Assert.Equal(0f, RouteRules.RemainingDistance(NoRoute, 0, 120f, 30f), 3);
    }

    [Fact]
    public void TravelCannotBeNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RouteRules.Advance(StraightEast, 0, 50f, 50f, 0f, -1f));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void TravelHasToBeAFiniteDistance(float travel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RouteRules.Advance(StraightEast, 0, 50f, 50f, 0f, travel));
    }
}
