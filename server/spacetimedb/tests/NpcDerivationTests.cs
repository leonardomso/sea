using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

/// <summary>
/// Section 7 of the maths, checked against the one map that exists. Every number here is the
/// player's own tier-one sloop put through the tier table, so if the sloop is retuned these
/// expectations move with it and the test says so.
/// </summary>
public sealed class NpcDerivationTests
{
    private const byte Havenmere = 1;

    private static readonly BaseShipProfile BaseShip =
        BaseShipProfile.From(Tier1.Hull, Tier1.Cannon);

    private static NpcStatLine Derive(byte tier) =>
        NpcDerivation.Derive(tier, Havenmere, BaseShip, Tier1.Caps);

    [Fact]
    public void The_baseline_is_the_sloop_a_captain_starts_with()
    {
        // Section 5.3: 1600 hull behind 8% side armour. Section 3.4: eight 20-damage guns
        // every three seconds.
        Assert.Equal(1739.13f, BaseShip.EffectiveHitPoints, 2);
        Assert.Equal(53.33f, BaseShip.SustainedDamagePerSecond, 2);
    }

    // Section 7.2's table for map 1: hull, volley, armour and gold, tier by tier.
    [Theory]
    [InlineData(1, 870u, 40u, 0.10f, 30u)]
    [InlineData(2, 1739u, 64u, 0.10f, 75u)]
    [InlineData(3, 3826u, 112u, 0.15f, 240u)]
    [InlineData(4, 8696u, 144u, 0.20f, 750u)]
    [InlineData(5, 52174u, 192u, 0.20f, 4500u)]
    [InlineData(6, 208696u, 240u, 0.20f, 12000u)]
    public void Havenmere_tiers_match_the_derivation_table(
        byte tier,
        uint hull,
        uint volley,
        float armor,
        uint gold)
    {
        var stats = Derive(tier);

        Assert.Equal(hull, stats.MaximumHull);
        Assert.Equal(volley, stats.VolleyDamage);
        Assert.Equal(armor, stats.Armor, 3);
        Assert.Equal(gold, stats.GoldReward);
    }

    [Fact]
    public void A_common_is_slower_than_the_player_and_watches_four_squares()
    {
        var common = Derive(1);

        // Appendix D: a common keeps four fifths of the player's speed, so a captain who does
        // not want the fight can always leave it.
        Assert.Equal(0.8f * Tier1.Hull.SpeedSquaresPerSecond, common.MaximumSpeedSquares, 3);
        Assert.Equal(4f, common.AggroRangeSquares, 3);
        Assert.True(common.MaximumSpeedSquares < Tier1.Hull.SpeedSquaresPerSecond);
    }

    [Fact]
    public void A_common_is_back_in_thirty_seconds_and_a_named_captain_is_not()
    {
        Assert.Equal(30ul * WorldRules.TickRateHz, Derive(1).RespawnDelayTicks);
        Assert.Equal(2700ul * WorldRules.TickRateHz, Derive(4).RespawnDelayTicks);
    }

    [Fact]
    public void Gold_grows_with_the_map_rather_than_being_authored_per_enemy()
    {
        // G(N) = goldBase x goldGrowth^(N-1), and every tier is a multiple of it.
        Assert.Equal(Tier1.Caps.GoldBase, NpcDerivation.BaseGold(1, Tier1.Caps), 3);
        Assert.Equal(
            Tier1.Caps.GoldBase * Tier1.Caps.GoldGrowth,
            NpcDerivation.BaseGold(2, Tier1.Caps),
            3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void A_tier_the_table_does_not_cover_cannot_be_derived(byte tier)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Derive(tier));
    }

    [Fact]
    public void Maps_are_numbered_from_one()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NpcDerivation.BaseGold(0, Tier1.Caps));
    }

    /// <summary>
    /// Section 12.1's shape, applied to the weakest enemy on the map: a captain who does nothing
    /// but shoot sinks a common inside the twenty seconds the plan asks for, and the common needs
    /// far longer than a minute to sink them back.
    /// </summary>
    [Fact]
    public void A_captain_sinks_a_common_quickly_and_outlasts_one_easily()
    {
        var common = Derive(1);
        var sheet = Tier1.Sheet();
        var playerDps = ShipStatRules.SustainedDps(sheet);
        var commonDps = common.VolleyDamage / Tier1.Cannon.ReloadSeconds;

        var timeToSinkIt = common.MaximumHull / (1f - common.Armor) / playerDps;
        var timeToSinkUs = ShipStatRules.EffectiveHitPoints(sheet) / commonDps;

        Assert.InRange(timeToSinkIt, 0f, 20f);
        Assert.True(timeToSinkUs > 60f);
    }
}
