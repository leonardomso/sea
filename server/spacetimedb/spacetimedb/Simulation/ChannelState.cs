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

        if (channel.ChannelType == "boarding")
        {
            SetCooldown(
                ctx,
                shipEntityId,
                "boarding",
                tick + TacticalRules.BoardingCooldownTicks);
        }

        ctx.Db.ShipChannel.ShipEntityId.Delete(shipEntityId);
        AppendEvent(
            ctx,
            shipEntityId,
            $"{channel.ChannelType}_interrupted",
            $"cause={cause}");
        return true;
    }

    private static void InterruptBoarding(
        ReducerContext ctx,
        ulong shipEntityId,
        ulong tick,
        string eventType)
    {
        SetCooldown(
            ctx,
            shipEntityId,
            "boarding",
            tick + TacticalRules.BoardingCooldownTicks);
        ctx.Db.ShipChannel.ShipEntityId.Delete(shipEntityId);
        SetShipMode(ctx, shipEntityId, ShipMode.Operational);
        AppendEvent(ctx, shipEntityId, eventType, "");
    }

    private static void SetShipMode(
        ReducerContext ctx,
        ulong shipEntityId,
        ShipMode mode)
    {
        if (ctx.Db.Ship.EntityId.Find(shipEntityId) is not Ship ship)
        {
            return;
        }

        ship.ModeCode = (byte)mode;
        ctx.Db.Ship.EntityId.Update(ship);
    }

    private static Cooldown? FindCooldown(
        ReducerContext ctx,
        ulong shipEntityId,
        string cooldownType)
    {
        foreach (var cooldown in ctx.Db.Cooldown.ByShip.Filter(shipEntityId))
        {
            if (cooldown.CooldownType == cooldownType)
            {
                return cooldown;
            }
        }

        return null;
    }

    private static void SetCooldown(
        ReducerContext ctx,
        ulong shipEntityId,
        string cooldownType,
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
            CooldownType = cooldownType,
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
