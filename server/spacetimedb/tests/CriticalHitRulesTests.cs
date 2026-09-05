using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class CriticalHitRulesTests
{
    private const int Shots = 100_000;

    // Ten per cent of a hundred thousand, inside half a per cent either way.
    private const int LowRate = 9_500;
    private const int HighRate = 10_500;

    [Fact]
    public void AboutOneShotInTenIsACritical()
    {
        var criticals = 0;
        for (var shot = 0UL; shot < (ulong)Shots; shot++)
        {
            if (CriticalHitRules.IsCritical(seed: 7UL, tick: shot, attackerId: 1UL, defenderId: 2UL))
            {
                criticals++;
            }
        }

        Assert.InRange(criticals, LowRate, HighRate);
    }

    [Fact]
    public void TheRateHoldsWhenOnlyTheAttackerVaries()
    {
        var criticals = 0;
        for (var attacker = 0UL; attacker < (ulong)Shots; attacker++)
        {
            if (CriticalHitRules.IsCritical(seed: 7UL, tick: 900UL, attackerId: attacker, defenderId: 2UL))
            {
                criticals++;
            }
        }

        Assert.InRange(criticals, LowRate, HighRate);
    }

    [Fact]
    public void TheRateHoldsWhenOnlyTheDefenderVaries()
    {
        var criticals = 0;
        for (var defender = 0UL; defender < (ulong)Shots; defender++)
        {
            if (CriticalHitRules.IsCritical(seed: 7UL, tick: 900UL, attackerId: 1UL, defenderId: defender))
            {
                criticals++;
            }
        }

        Assert.InRange(criticals, LowRate, HighRate);
    }

    [Fact]
    public void TheRateHoldsWhenOnlyTheWorldSeedVaries()
    {
        var criticals = 0;
        for (var seed = 0UL; seed < (ulong)Shots; seed++)
        {
            if (CriticalHitRules.IsCritical(seed, tick: 900UL, attackerId: 1UL, defenderId: 2UL))
            {
                criticals++;
            }
        }

        Assert.InRange(criticals, LowRate, HighRate);
    }

    [Fact]
    public void NeighbouringTicksRollIndependently()
    {
        // Two independent one-in-ten rolls land together one time in a hundred.
        // A hash whose low bits walk with the tick clusters its criticals and
        // misses this even while the overall rate looks right.
        var pairs = 0;
        var previous = CriticalHitRules.IsCritical(7UL, 0UL, 1UL, 2UL);
        for (var tick = 1UL; tick < (ulong)Shots; tick++)
        {
            var current = CriticalHitRules.IsCritical(7UL, tick, 1UL, 2UL);
            if (previous && current)
            {
                pairs++;
            }

            previous = current;
        }

        Assert.InRange(pairs, 800, 1_200);
    }

    [Fact]
    public void TheTickAndTheDefenderAreNotInterchangeable()
    {
        // Folding the inputs together with a bare xor makes the tick and the
        // defender the same axis, so every volley below would agree.
        var disagreements = 0;
        for (var value = 1UL; value <= 1_000UL; value++)
        {
            var straight = CriticalHitRules.IsCritical(7UL, value, 1UL, value + 500UL);
            var swapped = CriticalHitRules.IsCritical(7UL, value + 500UL, 1UL, value);
            if (straight != swapped)
            {
                disagreements++;
            }
        }

        Assert.InRange(disagreements, 100, 260);
    }

    [Fact]
    public void TheAttackerAndTheDefenderAreNotInterchangeable()
    {
        var disagreements = 0;
        for (var value = 1UL; value <= 1_000UL; value++)
        {
            var straight = CriticalHitRules.IsCritical(7UL, 900UL, value, value + 500UL);
            var swapped = CriticalHitRules.IsCritical(7UL, 900UL, value + 500UL, value);
            if (straight != swapped)
            {
                disagreements++;
            }
        }

        Assert.InRange(disagreements, 100, 260);
    }

    [Fact]
    public void TwoWorldSeedsDisagreeOnWhichVolleysCrit()
    {
        var disagreements = 0;
        for (var tick = 0UL; tick < 1_000UL; tick++)
        {
            if (CriticalHitRules.IsCritical(7UL, tick, 1UL, 2UL)
                != CriticalHitRules.IsCritical(8UL, tick, 1UL, 2UL))
            {
                disagreements++;
            }
        }

        Assert.InRange(disagreements, 100, 260);
    }

    [Fact]
    public void TheSameShotAlwaysRollsTheSameWay()
    {
        var first = CriticalHitRules.IsCritical(7UL, 4242UL, 11UL, 22UL);
        var second = CriticalHitRules.IsCritical(7UL, 4242UL, 11UL, 22UL);

        Assert.Equal(first, second);
    }

    [Fact]
    public void TwoShipsFiringOnTheSameTickRollSeparately()
    {
        var rolls = new HashSet<bool>();
        for (var attacker = 1UL; attacker <= 40UL; attacker++)
        {
            rolls.Add(CriticalHitRules.IsCritical(7UL, 100UL, attacker, 500UL));
        }

        Assert.Equal(2, rolls.Count);
    }

    [Fact]
    public void ACriticalIsHalfAgainAsMuchDamage()
    {
        Assert.Equal(150u, CriticalHitRules.Apply(100u, isCritical: true));
        Assert.Equal(100u, CriticalHitRules.Apply(100u, isCritical: false));
    }

    [Fact]
    public void ACriticalRoundsDownSoOneStaysOne()
    {
        Assert.Equal(1u, CriticalHitRules.Apply(1u, isCritical: true));
        Assert.Equal(4u, CriticalHitRules.Apply(3u, isCritical: true));
    }

    [Fact]
    public void AGlancingBlowThatDidNothingStillDoesNothing()
    {
        Assert.Equal(0u, CriticalHitRules.Apply(0u, isCritical: true));
    }

    [Fact]
    public void AbsurdDamageIsCappedInsteadOfWrappingRound()
    {
        Assert.Equal(uint.MaxValue, CriticalHitRules.Apply(uint.MaxValue, isCritical: true));
    }
}
