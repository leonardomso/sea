using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    /// <summary>
    /// Fires one volley. Shots resolve on the tick they are fired: there is no travel time to
    /// simulate, so the <see cref="Volley"/> row that goes out is purely something for the client
    /// to draw.
    /// </summary>
    private static void ApplyFire(ReducerContext ctx, TickWorld world, ref Ship source)
    {
        var target = ctx.Db.Ship.EntityId.Find(source.TargetEntityId) ??
            throw new InvalidOperationException("Accepted fire command has no target.");
        // Facing is read off the live course, not the fat row, which only republishes on a
        // chunk change; a stale heading would hand the shooter the wrong armour face.
        HydrateTrackedKinematics(ctx, world, ref target);
        var ammunition = Catalog.AmmunitionByCode[source.SelectedAmmoCode] ??
            throw new InvalidOperationException("Selected ammunition definition is missing.");

        var bearing = GeometryRules.HeadingTo(
            target.PositionX,
            target.PositionY,
            source.PositionX,
            source.PositionY,
            target.HeadingDegrees);
        var facing = CombatRules.FaceHit(target.HeadingDegrees, bearing);
        var damage = CombatRules.ResolveDamage(
            source.VolleyDamage,
            ammunition.DamageMultiplier,
            CombatRules.ArmorOn(facing, target.ArmorFront, target.ArmorSides, target.ArmorBack));

        // SEA_5 §8.7: one volley in ten lands for half again, taken after armour so the face a
        // shot found still decides what the crit is worth. The roll is a hash of the world seed,
        // the tick and both hulls rather than a running generator, so a replay of the same
        // command log crits on exactly the same volleys.
        var isCritical = CriticalHitRules.IsCritical(
            world.Environment(ctx)?.Seed ?? 0UL,
            world.Tick,
            source.EntityId,
            target.EntityId);
        damage = CriticalHitRules.Apply(damage, isCritical);

        var magazine = CombatRules.Spend(
            new MagazineState(source.ReadyVolleys, source.ReloadProgressTicks));
        source.ReadyVolleys = magazine.ReadyVolleys;
        source.ReloadProgressTicks = magazine.ReloadProgressTicks;
        source.IsReloading = source.ReadyVolleys < source.MagazineSize;
        source.HasFired = true;
        source.LastShotTick = world.Tick;
        source.LastCombatTick = world.Tick;
        source.IsEngaged = true;

        PublishVolley(ctx, world, source, target, ammunition, facing);
        LandVolley(ctx, world, source, target, ammunition, damage, isCritical, facing);
    }

    /// <summary>
    /// Writes the row the client draws the shot from. It carries the shooter's chunk so the volley
    /// reaches exactly the subscribers who can already see the shooter.
    /// </summary>
    private static void PublishVolley(
        ReducerContext ctx,
        TickWorld world,
        Ship source,
        Ship target,
        AmmunitionContent ammunition,
        ArmorFace facing)
    {
        ctx.Db.Volley.Insert(new Volley
        {
            SourceEntityId = source.EntityId,
            TargetEntityId = target.EntityId,
            AmmoId = ammunition.Id,
            AmmoCode = (byte)ammunition.Code,
            OriginX = source.PositionX,
            OriginY = source.PositionY,
            TargetX = target.PositionX,
            TargetY = target.PositionY,
            ChunkX = source.ChunkX,
            ChunkY = source.ChunkY,
            FiredAtTick = world.Tick,
            ExpiresAtTick = world.Tick + CombatRules.VolleyDisplayTicks,
            IsActive = true,
        });
        AppendEvent(
            ctx,
            world.Tick,
            source.EntityId,
            "volley_fired",
            $"target={target.EntityId},ammo={ammunition.Id},face={HotPathCodes.ArmorFaceId(facing)}");
    }

    /// <summary>
    /// Applies the damage and, on a target still afloat, whatever the ammunition leaves behind.
    /// The buffer is local because a volley is fired from the command path, which runs after the
    /// dispatcher has already flushed its own.
    /// </summary>
    private static void LandVolley(
        ReducerContext ctx,
        TickWorld world,
        Ship source,
        Ship target,
        AmmunitionContent ammunition,
        uint damage,
        bool isCritical,
        ArmorFace facing)
    {
        var ships = new ShipTickBuffer();
        var defender = target;
        var distance = CombatRules.Distance(
            source.PositionX,
            source.PositionY,
            defender.PositionX,
            defender.PositionY);
        ScoreEdgeOfRangeVolley(ctx, world, source, ammunition, distance);
        var applied = ApplyDamageToShip(
            ctx,
            ships,
            source.EntityId,
            ref defender,
            damage,
            world.Tick,
            DamageSourceCode.Volley);

        if (defender.IsAlive)
        {
            if (EffectRules.TryResolve(ammunition, distance, world.Tick, out var application))
            {
                ApplyEffect(ctx, defender.EntityId, source.EntityId, application, world.Tick);
                defender.MovementStatusMask |= HotPathCodes.MovementMask(application.Code);
                if (application.Code == EffectCode.Slowed)
                {
                    defender.MovementSlowMagnitude = application.Magnitude;
                }
            }
        }

        ships.Stage(defender);
        ships.Flush(ctx, world.Tick);
        ctx.Db.HitEvent.Insert(new HitEvent
        {
            AttackerEntityId = source.EntityId,
            DefenderEntityId = defender.EntityId,
            Damage = applied,
            IsCritical = isCritical,
            Face = (byte)facing,
            FlightSeconds = RangeRules.FlightSeconds(distance),
            Tick = world.Tick,
        });
        AppendEvent(
            ctx,
            world.Tick,
            source.EntityId,
            defender.IsAlive ? "volley_impact" : "enemy_sunk",
            $"target={defender.EntityId},damage={applied}");
    }
}
