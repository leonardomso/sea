using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Reducer]
    public static void StartRepair(ReducerContext ctx)
    {
        var world = ctx.Db.WorldState.Id.Find(1) ??
            throw new Exception("World state is missing.");
        var ship = FindPlayerShip(ctx, ctx.Sender);
        var kit = FindInventory(ctx, ship.EntityId, "repair_kit");
        var rejection = TacticalRules.ValidateRepair(new RepairRequest(
            ship.IsActive && ship.IsAlive,
            FindActiveChannel(ctx, ship.EntityId) is null,
            kit is Inventory item && item.Quantity > 0,
            ship.Hull < ship.MaxHull || ship.Sails < ship.MaxSails ||
                ship.Cannons < ship.MaxCannons || ship.Crew < ship.MaxCrew));
        if (rejection != RepairRejection.None)
        {
            throw new Exception(RepairRejectionMessage(rejection));
        }

        var repairKit = kit!.Value;
        repairKit.Quantity--;
        ctx.Db.Inventory.InventoryId.Update(repairKit);
        ctx.Db.ShipChannel.Insert(new ShipChannel
        {
            ShipEntityId = ship.EntityId,
            ChannelType = "repair",
            TargetEntityId = ship.EntityId,
            StartedAtTick = world.Tick,
            CompletesAtTick = world.Tick + TacticalRules.RepairDurationTicks,
            InitialHull = ship.Hull,
            InitialSails = ship.Sails,
            InitialCannons = ship.Cannons,
            InitialCrew = ship.Crew,
            IsActive = true,
        });
        AppendEvent(ctx, ship.EntityId, "repair_started", "");
    }

    [SpacetimeDB.Reducer]
    public static void CancelRepair(ReducerContext ctx)
    {
        var ship = FindPlayerShip(ctx, ctx.Sender);
        CancelChannel(ctx, ship.EntityId, "repair", "repair_cancelled");
    }

    [SpacetimeDB.Reducer]
    public static void StartBoarding(ReducerContext ctx)
    {
        var world = ctx.Db.WorldState.Id.Find(1) ??
            throw new Exception("World state is missing.");
        var source = FindPlayerShip(ctx, ctx.Sender);
        var target = source.TargetEntityId == 0
            ? default(Ship?)
            : ctx.Db.Ship.EntityId.Find(source.TargetEntityId);
        var cooldown = FindCooldown(ctx, source.EntityId, "boarding");
        var rejection = TacticalRules.ValidateBoarding(new BoardingRequest(
            source.IsActive && source.IsAlive,
            target is Ship selected && selected.IsActive && selected.IsAlive,
            FindActiveChannel(ctx, source.EntityId) is null,
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
            cooldown?.ReadyAtTick ?? 0));
        if (rejection != BoardingRejection.None)
        {
            throw new Exception(BoardingRejectionMessage(rejection));
        }

        ctx.Db.ShipChannel.Insert(new ShipChannel
        {
            ShipEntityId = source.EntityId,
            ChannelType = "boarding",
            TargetEntityId = target!.Value.EntityId,
            StartedAtTick = world.Tick,
            CompletesAtTick = world.Tick + TacticalRules.BoardingDurationTicks,
            InitialHull = source.Hull,
            InitialSails = source.Sails,
            InitialCannons = source.Cannons,
            InitialCrew = source.Crew,
            IsActive = true,
        });
        AppendEvent(ctx, source.EntityId, "boarding_started", $"target={target.Value.EntityId}");
    }

    [SpacetimeDB.Reducer]
    public static void CancelBoarding(ReducerContext ctx)
    {
        var ship = FindPlayerShip(ctx, ctx.Sender);
        CancelChannel(ctx, ship.EntityId, "boarding", "boarding_cancelled");
    }

    [SpacetimeDB.Reducer]
    public static void MoveTo(ReducerContext ctx, float x, float y) => SetCourse(ctx, x, y);

    [SpacetimeDB.Reducer]
    public static void UpgradeCannon(ReducerContext ctx)
    {
        var progression = FindProgression(ctx, ctx.Sender);
        var cost = checked(100u * progression.Level);
        if (progression.Gold < cost)
        {
            throw new Exception("The player cannot afford this cannon upgrade.");
        }

        progression.Gold -= cost;
        ctx.Db.PlayerProgression.Owner.Update(progression);
        var ship = FindPlayerShip(ctx, ctx.Sender);
        ship.CannonDamage += WorldRules.CannonDamagePerUpgrade;
        ctx.Db.Ship.EntityId.Update(ship);
        AppendEvent(ctx, ship.EntityId, "cannon_upgraded", $"cost={cost}");
    }

}
