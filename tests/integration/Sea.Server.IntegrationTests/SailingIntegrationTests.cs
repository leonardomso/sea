using System.Diagnostics;
using SpacetimeDB.Types;
using Xunit;

namespace Sea.Server.IntegrationTests;

/// <summary>
/// What a captain does more than anything else: click a point and have the ship stop on it.
/// Against a live module, because the circle these cover was not in the arithmetic - the unit
/// tests of the day all passed - but in the arrival test never being satisfiable for a mark
/// the hull could not point at.
/// </summary>
public sealed class SailingIntegrationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    /// <summary>
    /// The ship has to be at rest within this of the mark. It is the server's arrival radius
    /// with room for the drift a current adds after the step has been taken.
    /// </summary>
    private const float RestedWithin = 4f;

    /// <summary>
    /// What the long leg is allowed to take. A hundred and sixty squares at the Skiff's rating
    /// of 5.6 squares a second is twenty-nine seconds sailed straight; the reef in the way and
    /// a head wind are worth the rest of it.
    /// </summary>
    private static readonly TimeSpan LongCourseAllowance = TimeSpan.FromSeconds(60);

    /// <summary>
    /// She used to orbit anything nearer than about six squares off the beam forever. Every one
    /// of these is a mark inside her old turning circle.
    /// </summary>
    [Theory]
    [InlineData(3f, 0f)]
    [InlineData(0f, -3f)]
    [InlineData(-2f, -2f)]
    [InlineData(5f, -5f)]
    public void AShortCourseOffTheBeamComesToRestInsteadOfCircling(float offsetX, float offsetY)
    {
        using var client = AtSea();
        var underway = client.OwnedShip();
        var markX = underway.PositionX + offsetX;
        var markY = underway.PositionY + offsetY;

        Assert.True(client.IssueSetCourse(9_001, markX, markY).Accepted);
        PumpUntil(client, () => !client.OwnedShip().IsMoving);

        var rested = client.OwnedShip();
        Assert.False(rested.HasRoute);
        Assert.Equal(0f, rested.Speed);
        Assert.True(
            Distance(rested.PositionX, rested.PositionY, markX, markY) <= RestedWithin,
            $"She rested {Distance(rested.PositionX, rested.PositionY, markX, markY)} " +
            "squares off the mark.");
        Assert.Null(client.UnhandledReducerError);
    }

    /// <summary>
    /// The chart is four hundred squares across. A hull that answers a leg of a hundred and
    /// sixty of them inside the allowance has handling a captain can plan around; the old
    /// figures needed better than a third of the sea just to come to rest.
    /// </summary>
    [Fact]
    public void ALongCourseIsAnsweredWithinTheTimeTheChartAllows()
    {
        using var client = AtSea();
        var mark = client.FarWater();
        var stopwatch = Stopwatch.StartNew();

        Assert.True(client.IssueSetCourse(9_002, mark.X, mark.Y).Accepted);
        PumpUntil(client, () => !client.OwnedShip().IsMoving);

        Assert.True(
            stopwatch.Elapsed < LongCourseAllowance,
            $"She took {stopwatch.Elapsed.TotalSeconds:0.0}s to sail a hundred and sixty squares.");
        var rested = client.OwnedShip();
        Assert.True(
            Distance(rested.PositionX, rested.PositionY, mark.X, mark.Y) <= RestedWithin,
            $"She rested {Distance(rested.PositionX, rested.PositionY, mark.X, mark.Y)} " +
            "squares off the mark.");
        Assert.Null(client.UnhandledReducerError);
    }

    private static float Distance(float x, float y, float toX, float toY)
    {
        var deltaX = toX - x;
        var deltaY = toY - y;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    /// <summary>A client already clear of the harbour, with way on and a course of its own.</summary>
    private static IntegrationClient AtSea()
    {
        var client = IntegrationClient.Connect();
        try
        {
            client.LoadPlayer();
            var berth = client.OwnedShip();
            client.SubscribeSpatial(berth.ChunkX, berth.ChunkY, 2);
            PumpUntil(client, client.HasHarbor);
            var openWater = client.OpenWater();
            client.PutToSea(openWater.X, openWater.Y);
            PumpUntil(client, () => !client.OwnedShip().IsMoving);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static void PumpUntil(IntegrationClient client, Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            client.PumpOnce();
            if (stopwatch.Elapsed > Timeout)
            {
                throw new TimeoutException(
                    $"Sailing integration operation timed out: {client.Describe()}");
            }
        }
    }
}
