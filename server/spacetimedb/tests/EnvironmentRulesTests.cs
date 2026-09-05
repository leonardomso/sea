using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class EnvironmentRulesTests
{
    [Fact]
    public void AWindBandIsEightHoursOfTicks()
    {
        Assert.Equal(288000UL, EnvironmentRules.WindBandTicks);
    }

    [Theory]
    [InlineData(0UL, 0UL)]
    [InlineData(287999UL, 0UL)]
    [InlineData(288000UL, 1UL)]
    [InlineData(864000UL, 3UL)]
    public void TheBandComesFromTheTickCounterNotTheClock(ulong tick, ulong band)
    {
        Assert.Equal(band, EnvironmentRules.WindBand(tick));
    }

    [Fact]
    public void TheSameSeedAndBandAlwaysGiveTheSameWind()
    {
        var first = EnvironmentRules.WindForBand(seed: 12345UL, band: 7UL);
        var second = EnvironmentRules.WindForBand(seed: 12345UL, band: 7UL);

        Assert.Equal(first, second);
    }

    [Fact]
    public void TheWindHasADirectionButNoStrengthToRoll()
    {
        var wind = EnvironmentRules.WindForBand(seed: 99UL, band: 2UL);

        Assert.InRange(wind, 0f, 360f);
    }

    [Fact]
    public void EveryBandLaysOutAtMostTwoStormsPerMap()
    {
        for (var band = 0UL; band < 20UL; band++)
        {
            var storms = EnvironmentRules.StormsForBand(seed: 4242UL, band: band, mapId: 1);

            Assert.InRange(storms.Count, 0, 2);
        }
    }

    [Fact]
    public void TheSameSeedAndBandAlwaysLayOutTheSameStorms()
    {
        var first = EnvironmentRules.StormsForBand(4242UL, 5UL, 1);
        var second = EnvironmentRules.StormsForBand(4242UL, 5UL, 1);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentBandsUsuallyLayOutDifferentStorms()
    {
        // Not a determinism claim (that is the test above) -- just a sanity check that the
        // band actually reaches the roll instead of every band collapsing to the same layout.
        var seenLayouts = new HashSet<int>();
        for (var band = 0UL; band < 20UL; band++)
        {
            seenLayouts.Add(EnvironmentRules.StormsForBand(4242UL, band, 1).Count);
        }

        Assert.True(seenLayouts.Count > 1);
    }

    [Fact]
    public void AStormNeverCentresOnLand()
    {
        var mask = ContentCatalog.LandMaskFor(1);
        for (var band = 0UL; band < 50UL; band++)
        {
            foreach (var storm in EnvironmentRules.StormsForBand(seed: 777UL, band: band, mapId: 1))
            {
                Assert.False(mask.IsLand(storm.CentreX, storm.CentreY));
            }
        }
    }

    [Fact]
    public void AStormStaysInsideTheChart()
    {
        for (var band = 0UL; band < 50UL; band++)
        {
            foreach (var storm in EnvironmentRules.StormsForBand(seed: 555UL, band: band, mapId: 1))
            {
                Assert.InRange(storm.CentreX, WorldRules.MapMin, WorldRules.MapMax);
                Assert.InRange(storm.CentreY, WorldRules.MapMin, WorldRules.MapMax);
                Assert.InRange(storm.DriftDirectionDegrees, 0f, 360f);
            }
        }
    }
}
