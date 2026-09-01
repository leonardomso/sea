using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Reducer]
    public static void FireBroadside(ReducerContext ctx, string side, string weakPoint)
    {
        if (!Enum.TryParse<BroadsideSide>(side, ignoreCase: true, out var parsedSide) ||
            !Enum.IsDefined(parsedSide))
        {
            throw new Exception("Broadside side must be port or starboard.");
        }

        if (!CombatRules.TryParseWeakPoint(weakPoint, out var parsedWeakPoint))
        {
            throw new Exception("Weak point must be hull, sails, or cannons.");
        }

        var world = ctx.Db.WorldState.Id.Find(1) ??
            throw new Exception("World state is missing.");
        var source = FindPlayerShip(ctx, ctx.Sender);
        var target = source.TargetEntityId == 0
            ? default(Ship?)
            : ctx.Db.Ship.EntityId.Find(source.TargetEntityId);
        var ammunition = ctx.Db.AmmoDefinition.AmmoId.Find(source.SelectedAmmoId) ??
            throw new Exception("The selected ammunition definition is missing.");
        var inventory = FindInventory(ctx, source.EntityId, source.SelectedAmmoId);
        var readyAtTick = parsedSide == BroadsideSide.Port
            ? source.NextPortFireTick
            : source.NextStarboardFireTick;
        var rejection = CombatRules.ValidateFire(new FireRequest
        {
            SourceAlive = source.IsActive && source.IsAlive,
            TargetSelected = target.HasValue,
            TargetAlive = target is Ship selected && selected.IsActive && selected.IsAlive,
            Cannons = source.Cannons,
            Ammunition = inventory?.Quantity ?? 0,
            CurrentTick = world.Tick,
            ReadyAtTick = readyAtTick,
            SourceX = source.PositionX,
            SourceY = source.PositionY,
            SourceHeadingDegrees = source.HeadingDegrees,
            TargetX = target?.PositionX ?? source.PositionX,
            TargetY = target?.PositionY ?? source.PositionY,
            MaximumRange = WorldRules.CannonRange,
            RangeMultiplier = ammunition.RangeMultiplier,
            Side = parsedSide,
            IsChanneling = FindActiveChannel(ctx, source.EntityId) is not null,
        });
        if (rejection != FireRejection.None)
        {
            throw new Exception(FireRejectionMessage(rejection));
        }

        var selectedTarget = target!.Value;
        var selectedInventory = inventory!.Value;
        var damage = CombatRules.DamageProfile(
            new AmmunitionContent
            {
                Id = ammunition.AmmoId,
                HullDamage = ammunition.HullDamage,
                SailDamage = ammunition.SailDamage,
                CannonDamage = ammunition.CannonDamage,
                CrewDamage = ammunition.CrewDamage,
                RangeMultiplier = ammunition.RangeMultiplier,
                AppliedStatus = ammunition.AppliedStatus,
            },
            parsedWeakPoint,
            source.CannonDamage,
            source.Cannons,
            source.MaxCannons);
        var hazards = HazardsAt(ctx, source.PositionX, source.PositionY);
        if (hazards.InStorm)
        {
            damage = ScaleCombatDamage(damage, hazards.Modifiers.WeaponEffectiveness);
        }
        var distance = CombatRules.Distance(
            source.PositionX,
            source.PositionY,
            selectedTarget.PositionX,
            selectedTarget.PositionY);
        var impactAtTick = world.Tick + CombatRules.VolleyTravelTicks(
            distance,
            CombatRules.ProjectileSpeed,
            world.TickRateHz);

        selectedInventory.Quantity--;
        ctx.Db.Inventory.InventoryId.Update(selectedInventory);
        source.SelectedWeakPoint = weakPoint.ToLowerInvariant();
        source.IsEngaged = true;
        var reloadTicks = TacticalRules.AdjustedReloadTicks(
            source.CannonCooldownTicks,
            source.Cannons,
            source.MaxCannons);
        if (parsedSide == BroadsideSide.Port)
        {
            source.NextPortFireTick = world.Tick + reloadTicks;
        }
        else
        {
            source.NextStarboardFireTick = world.Tick + reloadTicks;
        }

        ctx.Db.Ship.EntityId.Update(source);
        ctx.Db.Volley.Insert(new Volley
        {
            SourceEntityId = source.EntityId,
            TargetEntityId = selectedTarget.EntityId,
            Side = side.ToLowerInvariant(),
            AmmoId = ammunition.AmmoId,
            WeakPoint = weakPoint.ToLowerInvariant(),
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
            $"target={selectedTarget.EntityId},side={side},ammo={ammunition.AmmoId},impact_tick={impactAtTick}");
    }

    [SpacetimeDB.Reducer]
    public static void ActivateAbility(ReducerContext ctx, string abilityId)
    {
        var ability = ctx.Db.AbilityDefinition.AbilityId.Find(abilityId);
        var world = ctx.Db.WorldState.Id.Find(1) ??
            throw new Exception("World state is missing.");
        var ship = FindPlayerShip(ctx, ctx.Sender);
        var cooldown = FindCooldown(ctx, ship.EntityId, abilityId);
        var rejection = TacticalRules.ValidateAbility(new AbilityRequest(
            ship.IsActive && ship.IsAlive,
            ability is not null,
            FindActiveChannel(ctx, ship.EntityId) is null,
            world.Tick,
            cooldown?.ReadyAtTick ?? 0));
        if (rejection != AbilityRejection.None)
        {
            throw new Exception(AbilityRejectionMessage(rejection));
        }

        var selectedAbility = ability!.Value;
        if (abilityId == "emergency_pump")
        {
            DeactivateStatus(ctx, ship.EntityId, "flooding", world.Tick);
        }

        ApplyStatus(
            ctx,
            ship.EntityId,
            abilityId,
            world.Tick,
            selectedAbility.DurationTicks,
            maximumStacks: 1);
        SetCooldown(
            ctx,
            ship.EntityId,
            abilityId,
            world.Tick + selectedAbility.CooldownTicks);
        AppendEvent(ctx, ship.EntityId, "ability_activated", $"ability={abilityId}");
    }

}
