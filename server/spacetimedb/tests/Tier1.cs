using Sea.Server;

namespace Sea.Server.Tests;

/// <summary>
/// Tier-1 content and the base ship every rules test and balance test starts from
/// (Math section 12: "base vs base, sides, Round Shot").
/// </summary>
internal static class Tier1
{
    internal static readonly GameContent Content = ContentCatalog.CreateDefault();

    internal static readonly StatCapsContent Caps = Content.StatCaps;

    // Single, not First: a second tier-1 hull or cannon must break the build so the fixture is re-pointed
    // deliberately rather than silently picking whichever row the catalog happens to emit first.
    internal static readonly HullContent Hull = Content.Hulls.Single(hull => hull.Tier == 1);

    internal static readonly CannonContent Cannon = Content.Cannons.Single(cannon => cannon.Tier == 1);

    internal static readonly AmmunitionContent Round =
        Content.Ammunition.Single(ammo => ammo.Code == AmmunitionCode.Round);

    internal static ShipLoadout Loadout(AmmunitionContent ammo) =>
        new(Hull, Cannon, Hull.CannonSlots, ammo.DamageMultiplier, ammo.ReloadMultiplier);

    /// <summary>Round Shot is the baseline ammunition: its multipliers are both 1.0 by design.</summary>
    internal static ShipLoadout Loadout() => Loadout(Round);

    internal static ShipStatSheet Sheet() => ShipStatRules.Compute(Loadout(), [], Caps);
}
