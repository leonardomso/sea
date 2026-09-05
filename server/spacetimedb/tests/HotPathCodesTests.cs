using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class HotPathCodesTests
{
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
        Assert.False(HotPathCodes.TryParseAmmunition(null, out _));
        Assert.False(HotPathCodes.TryParseWorldObject("ship", out _));
        Assert.False(HotPathCodes.TryParseTerrain('?', out _));
        Assert.Equal("none", HotPathCodes.AmmunitionId((AmmunitionCode)byte.MaxValue));
        Assert.Equal("none", HotPathCodes.EffectId((EffectCode)byte.MaxValue));
        Assert.Equal("none", HotPathCodes.CooldownId((CooldownCode)byte.MaxValue));
        Assert.Equal("sides", HotPathCodes.ArmorFaceId((ArmorFace)byte.MaxValue));
    }

    [Fact]
    public void ArchetypesMovementMasksAndCooldownIdsRemainStable()
    {
        Assert.Equal(ShipArchetypeCode.Skiff, HotPathCodes.ShipArchetype("skiff"));
        Assert.Equal(ShipArchetypeCode.ReefCrab, HotPathCodes.ShipArchetype("reef_crab"));
        Assert.Equal(ShipArchetypeCode.Fancy, HotPathCodes.ShipArchetype("fancy"));
        Assert.Equal(ShipArchetypeCode.RedMary, HotPathCodes.ShipArchetype("red_mary"));
        Assert.Equal(ShipArchetypeCode.PlayerSloop, HotPathCodes.ShipArchetype("unknown"));
        Assert.Equal(HotPathCodes.SlowedMovementMask,
            HotPathCodes.MovementMask(EffectCode.Slowed));
        Assert.Equal(0, HotPathCodes.MovementMask(EffectCode.Burning));
        Assert.Equal(0, HotPathCodes.MovementMask(EffectCode.ReloadSlowed));
        Assert.Equal("repair", HotPathCodes.CooldownId(CooldownCode.Repair));
    }

    [Theory]
    [InlineData(ArmorFace.Front, "front")]
    [InlineData(ArmorFace.Sides, "sides")]
    [InlineData(ArmorFace.Back, "back")]
    public void ArmorFacesHaveStableIdentifiers(ArmorFace face, string expected)
    {
        Assert.Equal(expected, HotPathCodes.ArmorFaceId(face));
    }

    [Theory]
    [InlineData(EffectCode.Slowed, "slowed")]
    [InlineData(EffectCode.Burning, "burning")]
    [InlineData(EffectCode.ReloadSlowed, "reload_slowed")]
    public void EffectsHaveStableIdentifiers(EffectCode code, string expected)
    {
        Assert.Equal(expected, HotPathCodes.EffectId(code));
    }
}
