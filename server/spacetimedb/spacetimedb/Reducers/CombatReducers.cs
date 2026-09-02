using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void ApplyFireBroadside(
        ReducerContext ctx,
        ref Ship source,
        FireBroadsideCommand command,
        BroadsideSide side,
        WeakPoint weakPoint)
    {
        var world = ctx.Db.SimulationClock.Id.Find(1) ??
            throw new InvalidOperationException("Simulation clock is missing.");
        var target = ctx.Db.Ship.EntityId.Find(source.TargetEntityId) ??
            throw new InvalidOperationException("Accepted broadside has no target.");
        var ammunition = Catalog.AmmunitionByCode[source.SelectedAmmoCode] ??
            throw new InvalidOperationException("Selected ammunition definition is missing.");
        var inventory = FindInventory(ctx, source.EntityId, ammunition.Id) ??
            throw new InvalidOperationException("Accepted broadside has no ammunition.");
        var damage = BroadsideDamage(ctx, source, ammunition, weakPoint);
        var distance = CombatRules.Distance(
            source.PositionX,
            source.PositionY,
            target.PositionX,
            target.PositionY);
        var impactAtTick = world.Tick + CombatRules.VolleyTravelTicks(
            distance,
            CombatRules.ProjectileSpeed,
            WorldRules.TickRateHz);

        inventory.Quantity--;
        ctx.Db.Inventory.InventoryId.Update(inventory);
        source.SelectedWeakPointCode = (byte)weakPoint;
        source.IsEngaged = true;
        ApplyReload(ref source, side, world.Tick);
        ctx.Db.Volley.Insert(new Volley
        {
            SourceEntityId = source.EntityId,
            TargetEntityId = target.EntityId,
            Side = command.Side.ToLowerInvariant(),
            SideCode = (byte)(side == BroadsideSide.Port
                ? BroadsideCode.Port
                : BroadsideCode.Starboard),
            AmmoId = ammunition.Id,
            AmmoCode = (byte)ammunition.Code,
            WeakPoint = command.WeakPoint.ToLowerInvariant(),
            WeakPointCode = (byte)weakPoint,
            OriginX = source.PositionX,
            OriginY = source.PositionY,
            ChunkX = source.ChunkX,
            ChunkY = source.ChunkY,
            FiredAtTick = world.Tick,
            ImpactAtTick = impactAtTick,
            HullDamage = damage.Hull,
            SailDamage = damage.Sails,
            CannonDamage = damage.Cannons,
            CrewDamage = damage.Crew,
            IsActive = true,
        });
        AppendEvent(
            ctx,
            source.EntityId,
            "broadside_fired",
            $"target={target.EntityId},side={command.Side},ammo={ammunition.Id},impact_tick={impactAtTick}");
    }

    private static CombatDamage BroadsideDamage(
        ReducerContext ctx,
        Ship source,
        AmmunitionContent ammunition,
        WeakPoint weakPoint)
    {
        var damage = CombatRules.DamageProfile(
            ammunition,
            weakPoint,
            source.CannonDamage,
            source.Cannons,
            source.MaxCannons);
        var hazards = HazardsAt(ctx, source.PositionX, source.PositionY);
        return hazards.InStorm
            ? ScaleCombatDamage(damage, hazards.Modifiers.WeaponEffectiveness)
            : damage;
    }

    private static void ApplyReload(ref Ship source, BroadsideSide side, ulong tick)
    {
        var reloadTicks = TacticalRules.AdjustedReloadTicks(
            source.CannonCooldownTicks,
            source.Cannons,
            source.MaxCannons);
        if (side == BroadsideSide.Port)
        {
            source.NextPortFireTick = tick + reloadTicks;
        }
        else
        {
            source.NextStarboardFireTick = tick + reloadTicks;
        }
    }

    private static void ApplyActivateAbility(
        ReducerContext ctx,
        ref Ship ship,
        ActivateAbilityCommand command)
    {
        var ability = HotPathCodes.TryParseAbility(command.AbilityId, out var abilityCode)
            ? Catalog.AbilityByCode[(byte)abilityCode]
            : null;
        if (ability is null)
        {
            throw new InvalidOperationException("Accepted ability definition is missing.");
        }

        var world = ctx.Db.SimulationClock.Id.Find(1) ??
            throw new InvalidOperationException("Simulation clock is missing.");
        if (abilityCode == AbilityCode.EmergencyPump)
        {
            DeactivateStatus(ctx, ship.EntityId, StatusCode.Flooding, world.Tick);
        }

        var statusCode = HotPathCodes.StatusFor(abilityCode);
        ApplyStatus(
            ctx,
            ship.EntityId,
            statusCode,
            world.Tick,
            ability.DurationTicks,
            maximumStacks: 1);
        ship.MovementStatusMask |= HotPathCodes.MovementMask(statusCode);
        SetCooldown(
            ctx,
            ship.EntityId,
            HotPathCodes.CooldownFor(abilityCode),
            world.Tick + ability.CooldownTicks);
        AppendEvent(
            ctx,
            ship.EntityId,
            "ability_activated",
            $"ability={command.AbilityId}");
    }
}
