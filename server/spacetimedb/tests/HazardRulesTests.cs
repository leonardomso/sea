using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class HazardRulesTests
{
    [Theory]
    [InlineData(WorldObjectCode.Storm, 0, true, 1)]
    [InlineData(WorldObjectCode.Shoal, 0, true, 2)]
    [InlineData(WorldObjectCode.Storm, 2, true, 3)]
    [InlineData(WorldObjectCode.Shoal, 1, true, 3)]
    [InlineData(WorldObjectCode.Storm, 3, false, 2)]
    [InlineData(WorldObjectCode.Shoal, 3, false, 1)]
    public void ExposureBitsChangeWithoutDestroyingTheOtherHazard(
        WorldObjectCode kind,
        byte current,
        bool exposed,
        byte expected)
    {
        Assert.Equal(expected, HazardRules.SetExposure(current, kind, exposed));
    }

    [Theory]
    [InlineData(WorldObjectCode.Island)]
    [InlineData(WorldObjectCode.Reef)]
    [InlineData(WorldObjectCode.Harbor)]
    public void NonHazardKindsAreRejected(WorldObjectCode kind)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HazardRules.ExposureMask(kind));
    }
}
