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
    private static void RecomputeShipStats(ReducerContext ctx, Hull hull)
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
    }
}
