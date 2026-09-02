using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void ApplyStartRepair(ReducerContext ctx, ref Ship ship)
    {
        var world = ctx.Db.SimulationClock.Id.Find(1) ??
            throw new InvalidOperationException("Simulation clock is missing.");
        var repairKit = FindInventory(ctx, ship.EntityId, "repair_kit") ??
            throw new InvalidOperationException("Accepted repair has no repair kit.");

        repairKit.Quantity--;
        ctx.Db.Inventory.InventoryId.Update(repairKit);
        ctx.Db.ShipChannel.Insert(new ShipChannel
        {
            ShipEntityId = ship.EntityId,
            ChannelType = "repair",
            ChannelTypeCode = (byte)ChannelCode.Repair,
            TargetEntityId = ship.EntityId,
            StartedAtTick = world.Tick,
            CompletesAtTick = world.Tick + TacticalRules.RepairDurationTicks,
            NextProcessTick = world.Tick + 1,
            InitialHull = ship.Hull,
            InitialSails = ship.Sails,
            InitialCannons = ship.Cannons,
            InitialCrew = ship.Crew,
            IsActive = true,
        });
        AppendEvent(ctx, ship.EntityId, "repair_started", "");
    }

    private static void ApplyStartBoarding(ReducerContext ctx, ref Ship source)
    {
        var world = ctx.Db.SimulationClock.Id.Find(1) ??
            throw new InvalidOperationException("Simulation clock is missing.");
        var target = ctx.Db.Ship.EntityId.Find(source.TargetEntityId) ??
            throw new InvalidOperationException("Accepted boarding has no target.");

        ctx.Db.ShipChannel.Insert(new ShipChannel
        {
            ShipEntityId = source.EntityId,
            ChannelType = "boarding",
            ChannelTypeCode = (byte)ChannelCode.Boarding,
            TargetEntityId = target.EntityId,
            StartedAtTick = world.Tick,
            CompletesAtTick = world.Tick + TacticalRules.BoardingDurationTicks,
            NextProcessTick = world.Tick + 1,
            InitialHull = source.Hull,
            InitialSails = source.Sails,
            InitialCannons = source.Cannons,
            InitialCrew = source.Crew,
            IsActive = true,
        });
        AppendEvent(ctx, source.EntityId, "boarding_started", $"target={target.EntityId}");
    }

    private static void ApplyCancelChannel(ReducerContext ctx, Ship ship)
    {
        var channel = FindActiveChannel(ctx, ship.EntityId) ??
            throw new InvalidOperationException("Accepted cancellation has no channel.");
        ctx.Db.ShipChannel.ShipEntityId.Delete(ship.EntityId);
        AppendEvent(ctx, ship.EntityId, $"{channel.ChannelType}_cancelled", "");
    }

    [SpacetimeDB.Reducer]
    public static void UpgradeCannon(ReducerContext ctx)
    {
        var progression = FindProgression(ctx, ctx.Sender);
        var cost = checked(100u * progression.Level);
        if (progression.Gold < cost)
        {
            throw new InvalidOperationException("The player cannot afford this cannon upgrade.");
        }

        progression.Gold -= cost;
        ctx.Db.PlayerProgression.Owner.Update(progression);
        var ship = FindPlayerShip(ctx, ctx.Sender);
        ship.CannonDamage += WorldRules.CannonDamagePerUpgrade;
        PersistShip(ctx, ship);
        AppendEvent(ctx, ship.EntityId, "cannon_upgraded", $"cost={cost}");
    }
}
