using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void ApplyStartRepair(ReducerContext ctx, TickWorld world, ref Ship ship)
    {
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
        AppendEvent(ctx, world.Tick, ship.EntityId, "repair_started", "");
    }

    private static void ApplyStartBoarding(ReducerContext ctx, TickWorld world, ref Ship source)
    {
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
        AppendEvent(ctx, world.Tick, source.EntityId, "boarding_started", $"target={target.EntityId}");
    }

    private static void ApplyCancelChannel(ReducerContext ctx, TickWorld world, Ship ship)
    {
        var channel = FindActiveChannel(ctx, ship.EntityId) ??
            throw new InvalidOperationException("Accepted cancellation has no channel.");
        ctx.Db.ShipChannel.ShipEntityId.Delete(ship.EntityId);
        AppendEvent(ctx, world.Tick, ship.EntityId, $"{channel.ChannelType}_cancelled", "");
    }
}
