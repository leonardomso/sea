using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

/// <summary>
/// A captain clicks a square and the ship stops there. Every case in here was a circle the
/// ship orbited forever before <see cref="SailingRules.ArrivalRadius"/> and the handling
/// figures in <see cref="HandlingRules"/>: the two old arrival tests both wanted the hull
/// lined up on the mark, and a hull whose turning circle is wider than her distance to it
/// can never line up.
/// </summary>
public sealed class ShipStopsAtTheMarkTests
{
    private const float DeltaSeconds = 1f / WorldRules.TickRateHz;
    private const float MaximumSpeed = 24f;
    private const float TurnRateDegrees = 150f;

    private static SailingParameters Sloop => new(
        MaximumSpeed,
        HandlingRules.Acceleration,
        HandlingRules.Deceleration,
        TurnRateDegrees);

    /// <summary>Sails until she rests, and reports how long it took and where she ended.</summary>
    private static (float Seconds, float MissedBy) SailTo(
        float distance,
        float bearingDegrees,
        float startingSpeed,
        int tickLimit = 3_000)
    {
        var radians = bearingDegrees * (MathF.PI / 180f);
        var destinationX = distance * MathF.Sin(radians);
        var destinationY = distance * MathF.Cos(radians);
        var state = new SailingState(0f, 0f, 0f, startingSpeed);
        for (var tick = 1; tick <= tickLimit; tick++)
        {
            var step = SailingRules.Step(
                state,
                destinationX,
                destinationY,
                stopping: false,
                Sloop,
                DeltaSeconds);
            state = new SailingState(
                step.PositionX,
                step.PositionY,
                step.HeadingDegrees,
                step.Speed);
            if (step.Arrived)
            {
                var deltaX = destinationX - step.PositionX;
                var deltaY = destinationY - step.PositionY;
                return (tick * DeltaSeconds, MathF.Sqrt(deltaX * deltaX + deltaY * deltaY));
            }
        }

        return (float.PositiveInfinity, float.PositiveInfinity);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(12f)]
    [InlineData(24f)]
    public void A_ship_comes_to_rest_at_the_mark_from_every_bearing_and_range(float startingSpeed)
    {
        for (var distance = 1f; distance <= 140f; distance += 1f)
        {
            for (var bearing = 0f; bearing < 360f; bearing += 5f)
            {
                var (seconds, missedBy) = SailTo(distance, bearing, startingSpeed);

                Assert.True(
                    float.IsFinite(seconds),
                    $"She never stopped: {distance} units off, bearing {bearing}, " +
                    $"making {startingSpeed}.");
                Assert.True(
                    missedBy <= SailingRules.ArrivalRadius + 0.001f,
                    $"She rested {missedBy} units off the mark at bearing {bearing}.");
            }
        }
    }

    [Theory]
    [InlineData(3f, 90f)]
    [InlineData(2f, 120f)]
    [InlineData(1f, 180f)]
    [InlineData(5f, 60f)]
    public void A_short_click_off_the_bow_no_longer_circles(float distance, float bearing)
    {
        var (seconds, _) = SailTo(distance, bearing, startingSpeed: 0f);

        Assert.True(float.IsFinite(seconds));
        Assert.True(seconds <= 4f, $"She took {seconds}s to obey a click {distance} units off.");
    }

    /// <summary>
    /// The whole chart is 200 units across and a square is 10. A hull that needs seven squares
    /// to stop cannot answer a click, however the arrival test is written.
    /// </summary>
    [Fact]
    public void A_ship_stops_and_comes_about_inside_one_chart_square()
    {
        Assert.True(
            HandlingRules.StoppingDistance(MaximumSpeed) <= SectorRules.UnitsFromSquares(1f),
            $"She needs {HandlingRules.StoppingDistance(MaximumSpeed)} units to stop.");
        Assert.True(
            HandlingRules.TurningRadius(MaximumSpeed, TurnRateDegrees) <=
                SectorRules.UnitsFromSquares(1f),
            $"Her turning circle is {HandlingRules.TurningRadius(MaximumSpeed, TurnRateDegrees)} " +
            "units across the radius.");
    }

    [Fact]
    public void The_sloop_in_the_catalog_still_sails_by_these_figures()
    {
        var sloop = ContentCatalog.CreateDefault().Hulls[0];

        Assert.Equal(TurnRateDegrees, sloop.TurnDegreesPerSecond);
        Assert.Equal(MaximumSpeed, SectorRules.UnitsFromSquares(sloop.SpeedSquaresPerSecond));
    }
}
