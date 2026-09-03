using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    /// <summary>
    /// Walks the effects that come due this tick. Only burning asks for periodic work; the other
    /// two are scheduled straight to their expiry, so a slow costs one row write per application
    /// and one more when it lapses.
    /// </summary>
    private static void ProcessEffects(ReducerContext ctx, ShipTickBuffer ships, ulong tick)
    {
        foreach (var effect in ctx.Db.Effect.ByEffectDue.Filter(
                     (true, new Bound<ulong>(0, tick))))
        {
            ProcessDueEffect(ctx, ships, effect, tick);
        }
    }

    private static void ProcessDueEffect(
        ReducerContext ctx,
        ShipTickBuffer ships,
        Effect effect,
        ulong tick)
    {
        if (EffectRules.HasExpired(effect.ExpiresAtTick, tick))
        {
            ExpireEffect(ctx, ships, effect);
            return;
        }

        if (!ships.TryGet(ctx, effect.ShipEntityId, out var ship) ||
            !ship.IsActive || !ship.IsAlive)
        {
            DeactivateEffect(ctx, effect);
            return;
        }

        if ((EffectCode)effect.EffectCode == EffectCode.Burning)
        {
            var damage = EffectRules.BurnDamage(ship.MaxHull, effect.Magnitude);
            ApplyDamageToShip(
                ctx,
                ships,
                effect.SourceEntityId,
                ref ship,
                damage,
                tick,
                "burning");
            ships.Stage(ship);
        }

        var scheduled = effect;
        scheduled.NextProcessTick = Math.Min(
            tick + WorldRules.TickRateHz,
            effect.ExpiresAtTick);
        ctx.Db.Effect.EffectId.Update(scheduled);
    }

    private static void ExpireEffect(ReducerContext ctx, ShipTickBuffer ships, Effect effect)
    {
        DeactivateEffect(ctx, effect);
        var mask = HotPathCodes.MovementMask((EffectCode)effect.EffectCode);
        if (mask == 0 || !ships.TryGet(ctx, effect.ShipEntityId, out var ship))
        {
            return;
        }

        ship.MovementStatusMask &= (byte)~mask;
        ship.MovementSlowMagnitude = 0f;
        ships.Stage(ship);
    }

    private static void DeactivateEffect(ReducerContext ctx, Effect effect)
    {
        var inactive = effect;
        inactive.IsActive = false;
        inactive.NextProcessTick = ulong.MaxValue;
        ctx.Db.Effect.EffectId.Update(inactive);
    }

    /// <summary>
    /// Advances every magazine that is mid-reload. Ships sitting on a full magazine are not in the
    /// index at all, so a quiet world pays nothing for this and a battle pays one row per fighter.
    /// </summary>
    private static void ProcessReloads(ReducerContext ctx, ulong tick)
    {
        foreach (var reloading in ctx.Db.Ship.ByReloading.Filter(true))
        {
            var ship = reloading;
            if (!ship.IsActive || !ship.IsAlive ||
                ship.MagazineSize == 0 || ship.ReloadTicks == 0)
            {
                ship.IsReloading = false;
                ctx.Db.Ship.EntityId.Update(ship);
                continue;
            }

            var slow = FindEffect(ctx, ship.EntityId, EffectCode.ReloadSlowed);
            var slowed = slow is Effect active && active.IsActive && tick < active.ExpiresAtTick;
            var advanced = CombatRules.Advance(
                new MagazineState(ship.ReadyVolleys, ship.ReloadProgressTicks),
                ship.MagazineSize,
                EffectRules.ReloadTicks(
                    ship.ReloadTicks,
                    slowed,
                    slowed ? slow!.Value.Magnitude : 0f),
                tick - Math.Min(tick, ship.LastCombatTick));
            ship.ReadyVolleys = advanced.ReadyVolleys;
            ship.ReloadProgressTicks = advanced.ReloadProgressTicks;
            ship.IsReloading = advanced.ReadyVolleys < ship.MagazineSize;
            ctx.Db.Ship.EntityId.Update(ship);
        }
    }
}
