using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class MoveRateRulesTests
{
    [Fact]
    public void TwelveCoursesInOneSecondGiveEightAndFourDrops()
    {
        var windowStart = 0UL;
        var used = 0u;
        var accepted = 0;
        var dropped = 0;
        for (var attempt = 0; attempt < 12; attempt++)
        {
            if (MoveRateRules.Allow(ref windowStart, ref used, tick: 100UL))
            {
                accepted++;
            }
            else
            {
                dropped++;
            }
        }

        Assert.Equal(8, accepted);
        Assert.Equal(4, dropped);
    }

    [Fact]
    public void TheNextSecondStartsFresh()
    {
        var windowStart = 100UL;
        var used = 8u;

        Assert.False(MoveRateRules.Allow(ref windowStart, ref used, 109UL));
        Assert.True(MoveRateRules.Allow(ref windowStart, ref used, 110UL));
        Assert.Equal(110UL, windowStart);
        Assert.Equal(1u, used);
    }
}
