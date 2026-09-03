using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static CommandSnapshot CourseSnapshot(
        ReducerContext ctx,
        TickWorld world,
        CommandSnapshot snapshot,
        SetCourseCommand command) => snapshot with
        {
            CourseValid = WorldRules.IsValidMove(command.X, command.Y),
            DestinationBlocked = NavigationRules.IsDestinationBlocked(
                command.X,
                command.Y,
                world.Blockers(ctx)),
        };

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
            HydrateTrackedKinematics(ctx, ref tracked);
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
                // Ports arrive in 1c; until then no water is a port.
                InPort = false,
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

    private static CommandSnapshot RepairSnapshot(
        ReducerContext ctx,
        Ship ship,
        CommandSnapshot snapshot)
    {
        var kit = FindInventory(ctx, ship.EntityId, "repair_kit");
        return snapshot with
        {
            RepairRejection = TacticalRules.ValidateRepair(new RepairRequest(
                ship.IsActive && ship.IsAlive,
                !snapshot.HasActiveChannel,
                kit is Inventory item && item.Quantity > 0,
                ship.Hull < ship.MaxHull)),
        };
    }
}
