using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    /// <summary>
    /// Recomputes and stores <see cref="ShipStats"/> for one hull. Content comes from the static
    /// catalog (Task 7 amendment); content tables are a client projection only and are never read
    /// here. Runs on every login, not just on insert: it is how a content rebalance reaches an
    /// existing player's stored sheet.
    /// </summary>
    private static ShipStatSheet RecomputeShipStats(ReducerContext ctx, Hull hull)
    {
        if (!Catalog.HullById.TryGetValue(hull.HullDefId, out var hullContent))
        {
            throw new InvalidOperationException($"Hull definition '{hull.HullDefId}' is missing.");
        }

        if (!Catalog.CannonById.TryGetValue(hull.CannonDefId, out var cannonContent))
        {
            throw new InvalidOperationException($"Cannon definition '{hull.CannonDefId}' is missing.");
        }

        var ammo = Catalog.BaselineAmmunition;
        var loadout = new ShipLoadout(
            hullContent,
            cannonContent,
            hull.CannonCount,
            ammo.DamageMultiplier,
            ammo.ReloadMultiplier);
        var sheet = ShipStatRules.Compute(loadout, Array.Empty<BonusSource>(), Catalog.Content.StatCaps);
        var stats = ShipStats.From(hull, sheet);

        if (ctx.Db.ShipStats.HullId.Find(hull.HullId) is not ShipStats current)
        {
            ctx.Db.ShipStats.Insert(stats);
        }
        // The generated IEquatable<ShipStats>.Equals is field-wise and allocation-free; the generated
        // == operator boxes, so .Equals is the one to call here.
        else if (!current.Equals(stats))
        {
            ctx.Db.ShipStats.HullId.Update(stats);
        }

        return sheet;
    }

    /// <summary>
    /// The stat sheet a hull afloat fights with before any dock refit: the starter sloop. Ships
    /// are created with these numbers so no code path can ever produce a hull with a zero-length
    /// magazine, which <see cref="CombatRules.Advance"/> refuses to reload.
    /// </summary>
    private static ShipStatSheet BaselineStatSheet() => ShipStatRules.Compute(
        new ShipLoadout(
            Catalog.StarterHull,
            Catalog.StarterCannon,
            Catalog.StarterHull.CannonSlots,
            Catalog.BaselineAmmunition.DamageMultiplier,
            Catalog.BaselineAmmunition.ReloadMultiplier),
        Array.Empty<BonusSource>(),
        Catalog.Content.StatCaps);

    /// <summary>
    /// Copies a stat sheet onto the live hull. The fat <see cref="Ship"/> row carries the combat
    /// numbers outright so the tick never joins the dock tables to fire a volley; a refit is one
    /// more call to this.
    /// </summary>
    /// <param name="restock">
    /// True on spawn and on respawn, where the ship comes back whole with a full magazine. False
    /// on a login recompute, which must not heal a ship that is already in a fight.
    /// </param>
    private static void ApplyStatSheet(ref Ship ship, ShipStatSheet sheet, bool restock)
    {
        ship.HullTier = sheet.Tier;
        ship.VolleyDamage = sheet.VolleyDamage;
        ship.ReloadTicks = CombatRules.ReloadTicks(sheet.ReloadMilliseconds);
        ship.MagazineSize = sheet.Magazine;
        ship.RangeSquares = sheet.RangeSquares;
        ship.BaseSpeedSquaresPerSecond = sheet.SpeedSquaresPerSecond;
        ship.ArmorFront = sheet.ArmorFront;
        ship.ArmorSides = sheet.ArmorSides;
        ship.ArmorBack = sheet.ArmorBack;
        ship.MaxHull = sheet.MaxHitPoints;
        ship.RepairAmount = sheet.RepairAmount;
        ship.RepairChannelTicks = RepairRules.ChannelTicks(sheet.RepairChannelMilliseconds);
        ship.MaxHands = BoardingRules.Complement(sheet.Tier);

        if (restock)
        {
            ship.Hull = sheet.MaxHitPoints;
            ship.ReadyVolleys = sheet.Magazine;
            ship.ReloadProgressTicks = 0;
            ship.IsReloading = false;
            ship.Hands = ship.MaxHands;
            return;
        }

        // A shrinking sheet must not leave a ship over its own ceiling; a growing one leaves the
        // damage taken so far in place rather than handing out a free repair.
        ship.Hull = Math.Min(ship.Hull, sheet.MaxHitPoints);
        ship.ReadyVolleys = Math.Min(ship.ReadyVolleys, sheet.Magazine);
        ship.IsReloading = ship.ReadyVolleys < sheet.Magazine;
        ship.Hands = Math.Min(ship.Hands, ship.MaxHands);
    }
}
