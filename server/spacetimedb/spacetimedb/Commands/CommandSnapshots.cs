using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static CommandSnapshot CourseSnapshot(
        ReducerContext ctx,
        CommandSnapshot snapshot,
        SetCourseCommand command) => snapshot with
        {
            CourseValid = WorldRules.IsValidMove(command.X, command.Y),
            DestinationBlocked = NavigationRules.IsDestinationBlocked(
                command.X,
                command.Y,
                NavigationBlockersAt(ctx, command.X, command.Y)),
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
        var isPlayer = valid && target!.Value.FactionCode == (byte)FactionCode.Player;
        var concealed = false;
        if (valid)
        {
            var world = ctx.Db.WorldState.Id.Find(1) ??
                throw new InvalidOperationException("World state is missing.");
            var selected = target!.Value;
            concealed = !TacticalRules.CanAcquireTarget(
                HasActiveStatus(ctx, selected.EntityId, StatusCode.SmokeScreen, world.Tick),
                CombatRules.Distance(
                    source.PositionX,
                    source.PositionY,
                    selected.PositionX,
                    selected.PositionY));
        }

        return snapshot with
        {
            TargetValid = valid,
            TargetIsPlayer = isPlayer,
            TargetConcealed = concealed,
        };
    }

    private static CommandSnapshot AmmoSnapshot(
        ReducerContext ctx,
        Ship ship,
        CommandSnapshot snapshot,
        SetAmmoCommand command)
    {
        var known = !string.IsNullOrWhiteSpace(command.AmmoId) &&
            ctx.Db.AmmoDefinition.AmmoId.Find(command.AmmoId) is not null;
        return snapshot with
        {
            AmmoKnown = known,
            AmmoOwned = known && FindInventory(ctx, ship.EntityId, command.AmmoId) is not null,
        };
    }

    private static CommandSnapshot FireSnapshot(
        ReducerContext ctx,
        Ship source,
        CommandSnapshot snapshot,
        FireBroadsideCommand command)
    {
        if (!Enum.TryParse<BroadsideSide>(command.Side, ignoreCase: true, out var side) ||
            !Enum.IsDefined(side))
        {
            return snapshot with
            {
                ArgumentRejection = CommandRejectionCode.InvalidBroadsideSide,
            };
        }

        if (!CombatRules.TryParseWeakPoint(command.WeakPoint, out _))
        {
            return snapshot with
            {
                ArgumentRejection = CommandRejectionCode.InvalidWeakPoint,
            };
        }

        var world = ctx.Db.WorldState.Id.Find(1) ??
            throw new InvalidOperationException("World state is missing.");
        var ammunition = ctx.Db.AmmoDefinition.AmmoCode.Find(source.SelectedAmmoCode) ??
            throw new InvalidOperationException("Selected ammunition definition is missing.");
        var target = source.TargetEntityId == 0
            ? default(Ship?)
            : ctx.Db.Ship.EntityId.Find(source.TargetEntityId);
        var inventory = FindInventory(ctx, source.EntityId, ammunition.AmmoId);
        var readyAtTick = side == BroadsideSide.Port
            ? source.NextPortFireTick
            : source.NextStarboardFireTick;
        return snapshot with
        {
            FireRejection = CombatRules.ValidateFire(new FireRequest
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
                Side = side,
                IsChanneling = snapshot.HasActiveChannel,
            }),
        };
    }

    private static CommandSnapshot AbilitySnapshot(
        ReducerContext ctx,
        Ship ship,
        CommandSnapshot snapshot,
        ActivateAbilityCommand command)
    {
        var world = ctx.Db.WorldState.Id.Find(1) ??
            throw new InvalidOperationException("World state is missing.");
        var knownCode = HotPathCodes.TryParseAbility(command.AbilityId, out var abilityCode);
        var ability = !knownCode
            ? default(AbilityDefinition?)
            : ctx.Db.AbilityDefinition.AbilityId.Find(command.AbilityId);
        var cooldown = FindCooldown(
            ctx,
            ship.EntityId,
            HotPathCodes.CooldownFor(abilityCode));
        return snapshot with
        {
            AbilityRejection = TacticalRules.ValidateAbility(new AbilityRequest(
                ship.IsActive && ship.IsAlive,
                ability is not null,
                !snapshot.HasActiveChannel,
                world.Tick,
                cooldown?.ReadyAtTick ?? 0)),
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
                ship.Hull < ship.MaxHull || ship.Sails < ship.MaxSails ||
                    ship.Cannons < ship.MaxCannons || ship.Crew < ship.MaxCrew)),
        };
    }

    private static CommandSnapshot BoardingSnapshot(
        ReducerContext ctx,
        Ship source,
        CommandSnapshot snapshot)
    {
        var world = ctx.Db.WorldState.Id.Find(1) ??
            throw new InvalidOperationException("World state is missing.");
        var target = source.TargetEntityId == 0
            ? default(Ship?)
            : ctx.Db.Ship.EntityId.Find(source.TargetEntityId);
        var cooldown = FindCooldown(ctx, source.EntityId, CooldownCode.Boarding);
        return snapshot with
        {
            BoardingRejection = TacticalRules.ValidateBoarding(new BoardingRequest(
                source.IsActive && source.IsAlive,
                target is Ship selected && selected.IsActive && selected.IsAlive,
                !snapshot.HasActiveChannel,
                target?.Hull ?? 0,
                target?.MaxHull ?? 0,
                target is Ship boardingTarget
                    ? CombatRules.Distance(
                        source.PositionX,
                        source.PositionY,
                        boardingTarget.PositionX,
                        boardingTarget.PositionY)
                    : float.PositiveInfinity,
                world.Tick,
                cooldown?.ReadyAtTick ?? 0)),
        };
    }
}
