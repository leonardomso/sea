using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

/// <summary>
/// The client reckons the player's own ship forward with its own copy of the movement rule,
/// because a captain cannot wait a round trip to see the bow come round. Two copies of a
/// rule drift, and when these two drift the hull is drawn somewhere the server will not
/// agree with and gets tugged back - which is what a player reads as the ship behaving
/// oddly rather than as lag.
///
/// These figures are the contract the client copy has to meet. It is still on the old
/// inertia rule (apps/game-unity/Assets/Domain/SeaSailingRules.cs); porting it, and
/// asserting these same numbers verbatim in
/// apps/game-unity/Assets/Tests/EditMode/SeaLocalShipPredictionTests.cs, is Phase 13.
/// </summary>
public sealed class ClientPredictionParityTests
{
    // A sloop rated the way the seed content rates one, sailed at the world tick: 24
    // squares a second is 2.4 squares of way in one tick, whatever else is going on.
    private const float Tick = 1f / WorldRules.TickRateHz;
    private const float SloopTravel = 24f * Tick;

    [Fact]
    public void A_ship_lying_still_makes_a_whole_tick_of_way_on_the_first_tick()
    {
        var step = RouteRules.Advance(
            [new RouteWaypoint(100f, 0f)], 0, 0f, 0f, 90f, SloopTravel);

        Assert.Equal(2.4f, step.PositionX, 3);
        Assert.Equal(0f, step.PositionY, 3);
        Assert.Equal(90f, step.HeadingDegrees, 3);
        Assert.False(step.Arrived);
    }

    [Fact]
    public void A_ship_already_under_way_makes_exactly_the_same_way()
    {
        var first = RouteRules.Advance(
            [new RouteWaypoint(500f, 0f)], 0, 0f, 0f, 90f, SloopTravel);
        var second = RouteRules.Advance(
            [new RouteWaypoint(500f, 0f)], 0, first.PositionX, first.PositionY, 90f, SloopTravel);

        Assert.Equal(4.8f, second.PositionX, 3);
    }

    /// <summary>
    /// SEA_5 4.1.7: reversing is instant. A mark astern is made for on the same tick and
    /// costs her nothing, which is the whole of what replaced the turning circle.
    /// </summary>
    [Fact]
    public void A_mark_astern_is_made_for_on_the_same_tick()
    {
        var step = RouteRules.Advance(
            [new RouteWaypoint(-100f, 0f)], 0, 0f, 0f, 90f, SloopTravel);

        Assert.Equal(-2.4f, step.PositionX, 3);
        Assert.Equal(270f, step.HeadingDegrees, 3);
    }

    [Fact]
    public void A_ship_with_no_course_left_stops_dead_where_she_stands()
    {
        var step = RouteRules.Advance([], 0, 7f, 11f, 90f, SloopTravel);

        Assert.Equal(7f, step.PositionX, 3);
        Assert.Equal(11f, step.PositionY, 3);
        Assert.Equal(90f, step.HeadingDegrees, 3);
        Assert.True(step.Arrived);
    }

    [Fact]
    public void Rounding_a_corner_costs_her_none_of_the_tick()
    {
        var step = RouteRules.Advance(
            [new RouteWaypoint(1f, 0f), new RouteWaypoint(1f, -10f)],
            0,
            0f,
            0f,
            90f,
            SloopTravel);

        // One square east onto the corner, then the remaining 1.4 north up the second leg.
        Assert.Equal(1f, step.PositionX, 3);
        Assert.Equal(-1.4f, step.PositionY, 3);
        Assert.Equal(0f, step.HeadingDegrees, 3);
        Assert.Equal(1, step.WaypointIndex);
    }
}
