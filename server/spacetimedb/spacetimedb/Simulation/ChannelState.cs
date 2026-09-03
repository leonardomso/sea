using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static ShipChannel? FindActiveChannel(ReducerContext ctx, ulong shipEntityId) =>
        ctx.Db.ShipChannel.ShipEntityId.Find(shipEntityId) is ShipChannel channel &&
        channel.IsActive
            ? channel
            : null;

    private static bool InterruptActiveChannel(
        ReducerContext ctx,
        ulong shipEntityId,
        ulong tick,
        string cause)
    {
        if (FindActiveChannel(ctx, shipEntityId) is not ShipChannel channel)
        {
            return false;
        }

        ctx.Db.ShipChannel.ShipEntityId.Delete(shipEntityId);
        AppendEvent(
            ctx,
            tick,
            shipEntityId,
            $"{channel.ChannelType}_interrupted",
            $"cause={cause}");
        return true;
    }

    private static Cooldown? FindCooldown(
        ReducerContext ctx,
        ulong shipEntityId,
        CooldownCode cooldownType)
    {
        foreach (var cooldown in ctx.Db.Cooldown.ByShipCooldown.Filter(
                     (shipEntityId, (byte)cooldownType)))
        {
            return cooldown;
        }

        return null;
    }

    private static void SetCooldown(
        ReducerContext ctx,
        ulong shipEntityId,
        CooldownCode cooldownType,
        ulong readyAtTick)
    {
        if (FindCooldown(ctx, shipEntityId, cooldownType) is Cooldown cooldown)
        {
            cooldown.ReadyAtTick = readyAtTick;
            ctx.Db.Cooldown.CooldownId.Update(cooldown);
            return;
        }

        ctx.Db.Cooldown.Insert(new Cooldown
        {
            ShipEntityId = shipEntityId,
            CooldownType = HotPathCodes.CooldownId(cooldownType),
            CooldownTypeCode = (byte)cooldownType,
            ReadyAtTick = readyAtTick,
        });
    }

    private static void AddInventory(
        ReducerContext ctx,
        ulong shipEntityId,
        string itemId,
        uint quantity)
    {
        if (FindInventory(ctx, shipEntityId, itemId) is Inventory existing)
        {
            existing.Quantity = checked(existing.Quantity + quantity);
            ctx.Db.Inventory.InventoryId.Update(existing);
            return;
        }

        ctx.Db.Inventory.Insert(new Inventory
        {
            ShipEntityId = shipEntityId,
            ItemId = itemId,
            Quantity = quantity,
        });
    }

}
