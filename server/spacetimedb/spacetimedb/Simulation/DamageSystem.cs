using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    /// <summary>
    /// Volleys no longer travel: damage lands on the tick the shot is fired, so all that is left
    /// for the tick to do is retire the rows the client has finished animating.
    /// </summary>
    private static void RetireVolleys(ReducerContext ctx, ulong tick)
    {
        foreach (var volley in ctx.Db.Volley.ByVolleyExpiry.Filter(
                     (true, new Bound<ulong>(0, tick))))
        {
            ctx.Db.Volley.VolleyId.Delete(volley.VolleyId);
        }
    }

    /// <summary>
    /// Subtracts one hit from a ship's single hit point pool and settles everything that follows
    /// from it: an interrupted channel, an encounter contribution, and sinking.
    /// </summary>
    private static uint ApplyDamageToShip(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ulong sourceEntityId,
        ref Ship defender,
        uint incoming,
        ulong tick,
        DamageSourceCode source)
    {
        // Port Lowell is a truce, not a shelter with a door: nothing reaches a hull inside it.
        if (defender.IsInPort || tick < defender.InvulnerableUntilTick || incoming == 0)
        {
            return 0;
        }

        var hullBefore = defender.Hull;
        defender.Hull = WorldRules.ApplyDamage(hullBefore, incoming);
        var applied = hullBefore - defender.Hull;
        defender.LastCombatTick = tick;
        if (RecordChannelDamage(ctx, defender, applied, tick, source))
        {
            defender.ModeCode = (byte)ShipMode.Operational;
        }
        var sunk = hullBefore > 0 && defender.Hull == 0;
        var attackerIsPlayer = sourceEntityId != 0 &&
            defender.FactionCode == (byte)FactionCode.Npc &&
            ctx.Db.PlayerOwnership.ShipEntityId.Find(sourceEntityId) is not null;
        if (attackerIsPlayer)
        {
            RecordContribution(ctx, defender.EncounterId, sourceEntityId, applied);
        }

        if (sunk)
        {
            SettleNpcEncounter(ctx, defender, tick);
            SinkShip(ctx, ships, sourceEntityId, ref defender, tick);
        }
        else if (attackerIsPlayer)
        {
            defender.TargetEntityId = sourceEntityId;
            defender.IsEngaged = true;
        }

        return applied;
    }

    /// <summary>
    /// A hit no longer ends a repair by itself. The channel tallies what it has cost the ship, and
    /// only enough of it, or the flames of a Fire Shot that a crew cannot work through, breaks the
    /// attempt; the cooldown is owed all the same.
    /// </summary>
    private static bool RecordChannelDamage(
        ReducerContext ctx,
        Ship defender,
        uint applied,
        ulong tick,
        DamageSourceCode source)
    {
        if (FindActiveChannel(ctx, defender.EntityId) is not ShipChannel channel)
        {
            return false;
        }

        channel.DamageTaken += applied;
        if (!RepairRules.ShouldCancel(
                channel.DamageTaken,
                defender.MaxHull,
                source == DamageSourceCode.Burning))
        {
            ctx.Db.ShipChannel.ShipEntityId.Update(channel);
            return false;
        }

        InterruptActiveChannel(
            ctx,
            defender.EntityId,
            tick,
            HotPathCodes.DamageSourceId(source));
        if (channel.ChannelTypeCode == (byte)ChannelCode.Repair)
        {
            SetCooldown(
                ctx,
                defender.EntityId,
                CooldownCode.Repair,
                tick + RepairRules.CooldownTicks);
        }

        return true;
    }

    private static void SinkShip(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ulong sourceEntityId,
        ref Ship defender,
        ulong tick)
    {
        defender.IsAlive = false;
        defender.IsActive = false;
        defender.IsMoving = false;
        defender.HasCourse = false;
        defender.IsStopping = false;
        defender.ModeCode = (byte)ShipMode.Sunk;
        ClearEffects(ctx, defender.EntityId);
        defender.MovementStatusMask = 0;
        ClearTargetLocks(ctx, ships, defender.EntityId);
        if (sourceEntityId != 0)
        {
            defender.TargetEntityId = 0;
        }

        ScheduleRespawn(ctx, ref defender, tick);
    }

    private static void ClearTargetLocks(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ulong targetEntityId)
    {
        foreach (var source in ctx.Db.Ship.ByTarget.Filter(targetEntityId))
        {
            if (!ships.TryGet(ctx, source.EntityId, out var cleared))
            {
                continue;
            }

            cleared.TargetEntityId = 0;
            cleared.IsEngaged = false;
            ships.Stage(cleared);
        }
    }

    private static Effect? FindEffect(
        ReducerContext ctx,
        ulong shipEntityId,
        EffectCode effectCode)
    {
        foreach (var effect in ctx.Db.Effect.ByShipEffect.Filter(
                     (shipEntityId, (byte)effectCode)))
        {
            return effect;
        }

        return null;
    }

    private static bool HasActiveEffect(
        ReducerContext ctx,
        ulong shipEntityId,
        EffectCode effectCode,
        ulong tick) =>
        FindEffect(ctx, shipEntityId, effectCode) is Effect effect &&
        effect.IsActive && tick < effect.ExpiresAtTick;

    /// <summary>
    /// Applies one effect to one ship. The same code refreshes the row it already has — taking the
    /// later of the two expiries — and a different code gets a row of its own, which is what makes
    /// effects stack across ammunition types but never against themselves.
    /// </summary>
    private static bool ApplyEffect(
        ReducerContext ctx,
        ulong shipEntityId,
        ulong sourceEntityId,
        EffectApplication application,
        ulong tick)
    {
        if (application.Code == EffectCode.None)
        {
            return false;
        }

        if (FindEffect(ctx, shipEntityId, application.Code) is Effect existing)
        {
            var refreshed = existing.IsActive
                ? EffectRules.Refresh(existing.ExpiresAtTick, application.ExpiresAtTick)
                : application.ExpiresAtTick;
            existing.SourceEntityId = sourceEntityId;
            existing.Magnitude = application.Magnitude;
            existing.AppliedAtTick = tick;
            existing.ExpiresAtTick = refreshed;
            existing.NextProcessTick = Math.Min(application.NextProcessTick, refreshed);
            existing.IsActive = true;
            ctx.Db.Effect.EffectId.Update(existing);
        }
        else
        {
            ctx.Db.Effect.Insert(new Effect
            {
                ShipEntityId = shipEntityId,
                SourceEntityId = sourceEntityId,
                EffectType = HotPathCodes.EffectId(application.Code),
                EffectCode = (byte)application.Code,
                Magnitude = application.Magnitude,
                AppliedAtTick = tick,
                ExpiresAtTick = application.ExpiresAtTick,
                NextProcessTick = application.NextProcessTick,
                IsActive = true,
            });
        }

        AppendEvent(
            ctx,
            tick,
            shipEntityId,
            "effect_applied",
            $"effect={HotPathCodes.EffectId(application.Code)}");
        return true;
    }

    private static void ClearEffects(ReducerContext ctx, ulong shipEntityId)
    {
        foreach (var effect in ctx.Db.Effect.ByShip.Filter(shipEntityId))
        {
            ctx.Db.Effect.EffectId.Delete(effect.EffectId);
        }
    }
}
