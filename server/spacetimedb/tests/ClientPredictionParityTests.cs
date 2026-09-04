using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

/// <summary>
/// The client reckons the player's own ship forward with its own copy of the sailing rule
/// (Sea.Client.SeaSailingRules), because a captain cannot wait a round trip to see the bow come
/// round. Two copies of a rule drift, and when these two drift the hull is drawn somewhere the
/// server will not agree with and gets tugged back - which is what a player reads as the ship
/// behaving oddly rather than as lag.
///
/// These figures are asserted verbatim on the client side in
/// apps/game-unity/Assets/Tests/EditMode/SeaLocalShipPredictionTests.cs. If a change to the
/// sailing rule breaks one of these, the other copy has to move with it.
/// </summary>
public sealed class ClientPredictionParityTests
{
    // A sloop rated the way the seed content rates one, sailed at the world tick.
    private static readonly SailingParameters Sloop = new(24f, 12f, 12f, 90f);

    private const float Tick = 1f / WorldRules.TickRateHz;

    [Fact]
    public void A_ship_lying_still_gets_under_way_on_the_first_tick_of_a_new_course()
    {
        var step = SailingRules.Step(
            new SailingState(0f, 0f, 90f, 0f),
            destinationX: 100f,
            destinationY: 0f,
            stopping: false,
            Sloop,
            Tick);

        Assert.Equal(0.06f, step.PositionX, 3);
        Assert.Equal(1.2f, step.Speed, 3);
        Assert.True(step.IsMoving);
    }

    [Fact]
    public void A_ship_at_her_rated_speed_holds_it_across_a_tick()
    {
        var step = SailingRules.Step(
            new SailingState(0f, 0f, 90f, 24f),
            destinationX: 500f,
            destinationY: 0f,
            stopping: false,
            Sloop,
            Tick);

        Assert.Equal(2.4f, step.PositionX, 3);
        Assert.Equal(24f, step.Speed, 3);
    }

    [Fact]
    public void A_ship_told_to_stop_carries_her_way_off_rather_than_freezing()
    {
        var step = SailingRules.Step(
            new SailingState(0f, 0f, 90f, 10f),
            destinationX: 0f,
            destinationY: 0f,
            stopping: true,
            Sloop,
            Tick);

        Assert.Equal(0.94f, step.PositionX, 3);
        Assert.Equal(8.8f, step.Speed, 3);
    }

    [Fact]
    public void A_hard_turn_costs_a_ship_her_way_and_a_straight_course_does_not()
    {
        var straight = SailingRules.Step(
            new SailingState(0f, 0f, 90f, 12f),
            destinationX: 100f,
            destinationY: 0f,
            stopping: false,
            Sloop,
            Tick);
        var swinging = SailingRules.Step(
            new SailingState(0f, 0f, 0f, 12f),
            destinationX: 100f,
            destinationY: 0f,
            stopping: false,
            Sloop,
            Tick);

        var straightTravel = MathF.Sqrt(
            straight.PositionX * straight.PositionX + straight.PositionY * straight.PositionY);
        var swingingTravel = MathF.Sqrt(
            swinging.PositionX * swinging.PositionX + swinging.PositionY * swinging.PositionY);
        Assert.True(swingingTravel < straightTravel);
        Assert.Equal(9f, swinging.HeadingDegrees, 3);
    }
}
