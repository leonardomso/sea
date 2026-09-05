using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

/// <summary>
/// The range limit on an ammunition's after-effect. Both sides of the comparison have been
/// in squares since Phase 1, and until now nothing proved it: a limit read in the old units
/// would have let Grape Shot slow a reload from twenty times as far away as it should.
/// </summary>
public sealed class EffectRulesTests
{
    private static readonly GameContent Catalog = ContentCatalog.CreateDefault();

    private static AmmunitionContent Ammunition(string id) =>
        Catalog.Ammunition.Single(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));

    [Fact]
    public void Grapeshot_slows_a_reload_inside_its_short_range()
    {
        var grapeshot = Ammunition("grapeshot");
        Assert.Equal((byte)4, grapeshot.RangeLimitSquares);

        Assert.True(EffectRules.AppliesAtRange(grapeshot, distanceSquares: 3.9f));
        Assert.False(EffectRules.AppliesAtRange(grapeshot, distanceSquares: 4.1f));
    }

    /// <summary>
    /// The limit is inclusive: a volley that lands exactly on it has not passed it.
    /// </summary>
    [Fact]
    public void The_limit_itself_is_still_inside_it()
    {
        Assert.True(EffectRules.AppliesAtRange(Ammunition("grapeshot"), distanceSquares: 4f));
    }

    [Theory]
    [InlineData("round")]
    [InlineData("chain")]
    [InlineData("incendiary")]
    public void Ammunition_with_no_limit_carries_at_any_range(string id)
    {
        var ammunition = Ammunition(id);
        Assert.Equal((byte)0, ammunition.RangeLimitSquares);

        Assert.True(EffectRules.AppliesAtRange(ammunition, distanceSquares: 30f));
    }

    /// <summary>
    /// A distance that is not a number is not a distance inside the limit. It reaches here
    /// only from a corrupt row or a NaN position, and the effect is dropped rather than
    /// applied on the strength of a comparison that is false either way.
    /// </summary>
    [Fact]
    public void A_distance_that_is_not_a_number_is_outside_every_limit()
    {
        Assert.False(EffectRules.AppliesAtRange(Ammunition("grapeshot"), float.NaN));
    }

    [Fact]
    public void The_limit_gates_the_effect_the_volley_actually_leaves()
    {
        var grapeshot = Ammunition("grapeshot");

        Assert.True(EffectRules.TryResolve(grapeshot, 3.9f, currentTick: 100, out var inside));
        Assert.Equal(EffectCode.ReloadSlowed, inside.Code);
        Assert.False(EffectRules.TryResolve(grapeshot, 4.1f, currentTick: 100, out _));
    }
}
