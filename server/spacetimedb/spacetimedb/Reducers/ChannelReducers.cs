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
            IsActive = true,
        });
        AppendEvent(ctx, world.Tick, ship.EntityId, "repair_started", "");
    }

    private static void ApplyCancelChannel(ReducerContext ctx, TickWorld world, Ship ship)
    {
        var channel = FindActiveChannel(ctx, ship.EntityId) ??
            throw new InvalidOperationException("Accepted cancellation has no channel.");
        ctx.Db.ShipChannel.ShipEntityId.Delete(ship.EntityId);
        AppendEvent(ctx, world.Tick, ship.EntityId, $"{channel.ChannelType}_cancelled", "");
    }
}
