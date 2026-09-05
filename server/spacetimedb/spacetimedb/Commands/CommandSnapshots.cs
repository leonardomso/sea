using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    /// <summary>
    /// A click is blocked only when it lands inland and there is no water within the
    /// nudge radius SEA_5 4.1.2 allows. Water the click cannot be sailed to -- the far
    /// side of an isthmus, a lake -- is not blocked here; that is the search's answer,
    /// and it is NO_PATH.
    /// </summary>
    private static CommandSnapshot CourseSnapshot(
        Ship ship,
        CommandSnapshot snapshot,
        SetCourseCommand command)
    {
        var (clampedX, clampedY) = WorldRules.ClampToMap(command.X, command.Y);
        return snapshot with
        {
            CourseValid = WorldRules.IsValidMove(command.X, command.Y),
            DestinationBlocked = !ContentCatalog.LandMaskFor(ship.MapId).TryNearestWater(
                clampedX,
                clampedY,
                PathfindingRules.NudgeSearchSquares,
                out _,
                out _),
        };
    }

    private static CommandSnapshot TargetSnapshot(
        ReducerContext ctx,
        Ship source,
        CommandSnapshot snapshot,
        SelectTargetCommand command)
    {
        var target = ctx.Db.Ship.EntityId.Find(command.EntityId);
        var valid = target is Ship selectedShip &&
            selectedShip.IsActive && selectedShip.IsAlive;

        return snapshot with
        {
            TargetValid = valid,
            TargetIsFriendly = valid && target!.Value.FactionCode == source.FactionCode,
        };
    }

    /// <summary>
    /// Ammunition is unlimited in Milestone 1, so selecting it only has to name a profile the
    /// catalog knows. Stock, and a gold cost per volley, arrive with the Milestone 2 economy.
    /// </summary>
    private static CommandSnapshot AmmoSnapshot(
        CommandSnapshot snapshot,
        SetAmmoCommand command) => snapshot with
        {
            AmmoKnown = HotPathCodes.TryParseAmmunition(command.AmmoId, out var code) &&
                Catalog.AmmunitionByCode[(byte)code] is not null,
        };

    private static CommandSnapshot FireSnapshot(
        ReducerContext ctx,
        TickWorld world,
        Ship source,
        CommandSnapshot snapshot)
    {
        var ammunition = Catalog.AmmunitionByCode[source.SelectedAmmoCode] ??
            throw new InvalidOperationException("Selected ammunition definition is missing.");
        var target = source.TargetEntityId == 0
            ? default(Ship?)
            : ctx.Db.Ship.EntityId.Find(source.TargetEntityId);
        if (target is Ship tracked)
        {
            // The fat row republishes only on a chunk change, so admission has to range the target
            // against its live position or a ship that sailed out would still be shootable.
            HydrateTrackedKinematics(ctx, world, ref tracked);
            target = tracked;
        }

        return snapshot with
        {
            TargetIsFriendly = target is Ship selectedTarget &&
                selectedTarget.FactionCode == source.FactionCode,
            FireRejection = CombatRules.ValidateFire(new FireRequest
            {
                SourceAlive = source.IsActive && source.IsAlive,
                TargetSelected = target.HasValue,
                TargetAlive = target is Ship selected && selected.IsActive && selected.IsAlive,
                InPort = source.IsInPort,
                SpawnShielded = world.Tick < source.InvulnerableUntilTick,
                IsChanneling = snapshot.HasActiveChannel,
                ReadyVolleys = source.ReadyVolleys,
                CurrentTick = world.Tick,
                HasFired = source.HasFired,
                LastShotTick = source.LastShotTick,
                SourceX = source.PositionX,
                SourceY = source.PositionY,
                TargetX = target?.PositionX ?? source.PositionX,
                TargetY = target?.PositionY ?? source.PositionY,
                RangeSquares = source.RangeSquares * ammunition.RangeMultiplier,
            }),
        };
    }

    /// <summary>
    /// The channelled repair. It costs no kit, so what admission has to know is whether the crew
    /// is free, whether the last repair has finished paying for itself, and whether the hull is
    /// short of anything at all.
    /// </summary>
    private static CommandSnapshot RepairSnapshot(
        ReducerContext ctx,
        TickWorld world,
        Ship ship,
        CommandSnapshot snapshot) => snapshot with
        {
            RepairRejection = RepairRules.ValidateRepair(BuildRepairRequest(
                ctx,
                world,
                ship,
                snapshot,
                CooldownCode.Repair)),
        };

    /// <summary>
    /// The kit. It runs on a cooldown of its own and takes no time, so a ship already channelling,
    /// or already casting off, may still open one.
    /// </summary>
    private static CommandSnapshot KitSnapshot(
        ReducerContext ctx,
        TickWorld world,
        Ship ship,
        CommandSnapshot snapshot) => snapshot with
        {
            KitRejection = RepairRules.ValidateKit(BuildRepairRequest(
                ctx,
                world,
                ship,
                snapshot,
                CooldownCode.RepairKit)),
        };

    private static RepairRequest BuildRepairRequest(
        ReducerContext ctx,
        TickWorld world,
        Ship ship,
        CommandSnapshot snapshot,
        CooldownCode cooldown)
    {
        var kit = FindInventory(ctx, ship.EntityId, "repair_kit");
        return new RepairRequest(
            ship.IsActive && ship.IsAlive,
            !snapshot.HasActiveChannel,
            FindCooldown(ctx, ship.EntityId, cooldown) is not Cooldown pending ||
                world.Tick >= pending.ReadyAtTick,
            kit is Inventory item && item.Quantity > 0,
            ship.Hull < ship.MaxHull);
    }

    /// <summary>
    /// A wreck may only pick a berth the port actually offers, and only while its own respawn is
    /// still waiting on the answer.
    /// </summary>
    private static CommandSnapshot RespawnSnapshot(
        ReducerContext ctx,
        Ship ship,
        CommandSnapshot snapshot,
        ChooseRespawnCommand command) => snapshot with
        {
            ArgumentRejection = command.OptionCode == (byte)RespawnOptionCode.HomePort
                ? CommandRejectionCode.None
                : CommandRejectionCode.NotAvailable,
            RespawnPending =
                ctx.Db.RespawnWork.ShipEntityId.Find(ship.EntityId) is RespawnWork work &&
                work.OptionCode == (byte)RespawnOptionCode.Unchosen,
        };
}
