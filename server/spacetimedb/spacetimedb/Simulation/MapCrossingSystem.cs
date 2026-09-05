using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    /// <summary>
    /// A hull found lying against a border on this tick's movement. The movement loop records
    /// one rather than acting on it, so a ship is touched once per tick and the offer rows are
    /// written from outside the loop that is trying not to write rows.
    /// </summary>
    private readonly record struct BorderBand(
        ulong EntityId,
        byte FactionCode,
        byte MapId,
        MapEdge Edge,
        float HeldX,
        float HeldY);

    /// <summary>
    /// The borders, once a tick. A captain lying against one that leads somewhere is offered
    /// the crossing; one lying against a border that leads nowhere is only held. Nothing here
    /// moves a ship between charts: that is <see cref="ApplyChangeMap"/>, which she has to ask
    /// for (SEA_5 §10.2).
    /// </summary>
    private static void ProcessBorderBands(ReducerContext ctx, TickWorld world)
    {
        if (ctx.Db.MapCrossingOffer.Count > 0)
        {
            WithdrawStaleOffers(ctx);
        }

        foreach (var band in world.BorderBands)
        {
            OfferCrossing(ctx, world, band);
        }
    }

    private static void OfferCrossing(ReducerContext ctx, TickWorld world, BorderBand band)
    {
        // A hostile leashes home long before a border and has no one to ask, so only a captain
        // is ever offered a crossing; one that fetches up on a border is simply held there.
        if (band.FactionCode != (byte)FactionCode.Player ||
            MapCrossingRules.Offer(band.MapId, band.Edge, band.HeldX, band.HeldY)
                is not MapCrossingRules.CrossingOffer crossing)
        {
            return;
        }

        var offer = new MapCrossingOffer
        {
            EntityId = band.EntityId,
            ToMapId = crossing.ToMapId,
            EdgeCode = (byte)crossing.Edge,
            SpawnX = crossing.SpawnX,
            SpawnY = crossing.SpawnY,
            OfferedTick = world.Tick,
        };
        if (ctx.Db.MapCrossingOffer.EntityId.Find(band.EntityId) is not MapCrossingOffer standing)
        {
            ctx.Db.MapCrossingOffer.Insert(offer);
            return;
        }

        // Nothing to say: she is lying where she was, so the prompt on her screen is still
        // true and costs no row. It is only rewritten when a current has carried her along
        // the wall far enough to move where she would come out, and the tick it went up is
        // kept when that happens because it is the same prompt.
        if (standing.ToMapId == offer.ToMapId && standing.EdgeCode == offer.EdgeCode &&
            standing.SpawnX == offer.SpawnX && standing.SpawnY == offer.SpawnY)
        {
            return;
        }

        offer.OfferedTick = standing.OfferedTick;
        ctx.Db.MapCrossingOffer.EntityId.Update(offer);
    }

    /// <summary>
    /// A prompt no longer answerable: she sailed off the wall, sank, or left the world.
    /// Walking the offers is cheap because there is at most one row per ship standing at a
    /// border, which is a handful on a busy chart.
    /// </summary>
    private static void WithdrawStaleOffers(ReducerContext ctx)
    {
        List<ulong>? withdrawn = null;
        foreach (var offer in ctx.Db.MapCrossingOffer.Iter())
        {
            if (ctx.Db.Ship.EntityId.Find(offer.EntityId) is Ship ship &&
                ship.IsActive && ship.IsAlive &&
                MapEdgeRules.IsHeldAgainst(
                    ship.PositionX, ship.PositionY, (MapEdge)offer.EdgeCode))
            {
                continue;
            }

            withdrawn ??= [];
            withdrawn.Add(offer.EntityId);
        }

        if (withdrawn is null)
        {
            return;
        }

        foreach (var entityId in withdrawn)
        {
            ctx.Db.MapCrossingOffer.EntityId.Delete(entityId);
        }
    }
}
