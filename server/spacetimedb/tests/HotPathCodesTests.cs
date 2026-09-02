using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class HotPathCodesTests
{
    [Fact]
    public void EveryAmmunitionCodeRoundTripsOrRejects()
    {
        AssertParses("round", AmmunitionCode.Round);
        AssertParses("chain", AmmunitionCode.Chain);
        AssertParses("grapeshot", AmmunitionCode.Grapeshot);
        AssertParses("incendiary", AmmunitionCode.Incendiary);
        Assert.False(HotPathCodes.TryParseAmmunition(null, out var missing));
        Assert.Equal(AmmunitionCode.None, missing);
        Assert.Equal("none", HotPathCodes.AmmunitionId(AmmunitionCode.None));

        static void AssertParses(string id, AmmunitionCode expected)
        {
            Assert.True(HotPathCodes.TryParseAmmunition(id, out var actual));
            Assert.Equal(expected, actual);
            Assert.Equal(id, HotPathCodes.AmmunitionId(actual));
        }
    }

    [Fact]
    public void EveryAbilityMapsToItsStatusAndCooldown()
    {
        AssertAbility("full_sail", AbilityCode.FullSail, StatusCode.FullSail,
            CooldownCode.FullSail);
        AssertAbility("brace", AbilityCode.Brace, StatusCode.Brace, CooldownCode.Brace);
        AssertAbility("emergency_pump", AbilityCode.EmergencyPump,
            StatusCode.EmergencyPump, CooldownCode.EmergencyPump);
        AssertAbility("smoke_screen", AbilityCode.SmokeScreen,
            StatusCode.SmokeScreen, CooldownCode.SmokeScreen);
        Assert.False(HotPathCodes.TryParseAbility("unknown", out var missing));
        Assert.Equal(AbilityCode.None, missing);
        Assert.Equal(StatusCode.None, HotPathCodes.StatusFor(missing));
        Assert.Equal(CooldownCode.None, HotPathCodes.CooldownFor(missing));

        static void AssertAbility(
            string id,
            AbilityCode ability,
            StatusCode status,
            CooldownCode cooldown)
        {
            Assert.True(HotPathCodes.TryParseAbility(id, out var actual));
            Assert.Equal(ability, actual);
            Assert.Equal(status, HotPathCodes.StatusFor(actual));
            Assert.Equal(cooldown, HotPathCodes.CooldownFor(actual));
        }
    }

    [Theory]
    [InlineData("burning", StatusCode.Burning)]
    [InlineData("flooding", StatusCode.Flooding)]
    [InlineData("slowed", StatusCode.Slowed)]
    [InlineData("disabled_sails", StatusCode.DisabledSails)]
    [InlineData("full_sail", StatusCode.FullSail)]
    [InlineData("brace", StatusCode.Brace)]
    [InlineData("emergency_pump", StatusCode.EmergencyPump)]
    [InlineData("smoke_screen", StatusCode.SmokeScreen)]
    [InlineData("boarding_fatigue", StatusCode.BoardingFatigue)]
    [InlineData("unknown", StatusCode.None)]
    public void StatusIdentifiersHaveStableCodes(string id, StatusCode expected)
    {
        var code = HotPathCodes.TryStatus(id);
        Assert.Equal(expected, code);
        Assert.Equal(expected == StatusCode.None ? "none" : id, HotPathCodes.StatusId(code));
    }

    [Theory]
    [InlineData("hull", WeakPointCode.Hull)]
    [InlineData("SAILS", WeakPointCode.Sails)]
    [InlineData("Cannons", WeakPointCode.Cannons)]
    public void WeakPointsParseCaseInsensitively(string id, WeakPointCode expected)
    {
        Assert.True(HotPathCodes.TryParseWeakPoint(id, out var actual));
        Assert.Equal(expected, actual);
        Assert.Equal(id.ToLowerInvariant(), HotPathCodes.WeakPointId(actual));
    }

    [Theory]
    [InlineData("PORT", BroadsideCode.Port)]
    [InlineData("starboard", BroadsideCode.Starboard)]
    public void BroadsidesParseCaseInsensitively(string id, BroadsideCode expected)
    {
        Assert.True(HotPathCodes.TryParseBroadside(id, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("harbor", WorldObjectCode.Harbor, false)]
    [InlineData("island", WorldObjectCode.Island, true)]
    [InlineData("reef", WorldObjectCode.Reef, true)]
    [InlineData("shoal", WorldObjectCode.Shoal, false)]
    [InlineData("storm", WorldObjectCode.Storm, false)]
    public void WorldObjectCodesOwnMovementBlocking(
        string id,
        WorldObjectCode expected,
        bool blocksMovement)
    {
        Assert.True(HotPathCodes.TryParseWorldObject(id, out var actual));
        Assert.Equal(expected, actual);
        Assert.Equal(blocksMovement, HotPathCodes.BlocksMovement(actual));
    }

    [Fact]
    public void InvalidIdentifiersAlwaysUseExplicitSentinels()
    {
        Assert.False(HotPathCodes.TryParseWeakPoint(null, out _));
        Assert.False(HotPathCodes.TryParseBroadside("aft", out _));
        Assert.False(HotPathCodes.TryParseWorldObject("ship", out _));
        Assert.Equal("hull", HotPathCodes.WeakPointId((WeakPointCode)byte.MaxValue));
        Assert.Equal("none", HotPathCodes.StatusId((StatusCode)byte.MaxValue));
        Assert.Equal("none", HotPathCodes.CooldownId((CooldownCode)byte.MaxValue));
    }

    [Fact]
    public void ArchetypesMovementMasksAndCooldownIdsRemainStable()
    {
        Assert.Equal(ShipArchetypeCode.Patrol, HotPathCodes.ShipArchetype("patrol"));
        Assert.Equal(ShipArchetypeCode.Raider, HotPathCodes.ShipArchetype("raider"));
        Assert.Equal(ShipArchetypeCode.Gunship, HotPathCodes.ShipArchetype("gunship"));
        Assert.Equal(ShipArchetypeCode.PlayerSloop, HotPathCodes.ShipArchetype("unknown"));
        Assert.Equal(HotPathCodes.FullSailMovementMask,
            HotPathCodes.MovementMask(StatusCode.FullSail));
        Assert.Equal(HotPathCodes.SlowedMovementMask,
            HotPathCodes.MovementMask(StatusCode.Slowed));
        Assert.Equal(0, HotPathCodes.MovementMask(StatusCode.Burning));
        Assert.Equal("full_sail", HotPathCodes.CooldownId(CooldownCode.FullSail));
        Assert.Equal("brace", HotPathCodes.CooldownId(CooldownCode.Brace));
        Assert.Equal("emergency_pump", HotPathCodes.CooldownId(CooldownCode.EmergencyPump));
        Assert.Equal("smoke_screen", HotPathCodes.CooldownId(CooldownCode.SmokeScreen));
        Assert.Equal("boarding", HotPathCodes.CooldownId(CooldownCode.Boarding));
    }
}
