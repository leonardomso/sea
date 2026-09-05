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

    /// <summary>
    /// How many heals still count against the next one. The window rolls, so the log is pruned on
    /// the way past rather than on a timer of its own.
    /// </summary>
    private static int CountRecentHeals(ReducerContext ctx, ulong shipEntityId, ulong tick)
    {
        if (ctx.Db.ShipHealLog.ShipEntityId.Find(shipEntityId) is not ShipHealLog log)
        {
            return 0;
        }

        var kept = new List<ulong>(log.CompletedTicks.Count);
        foreach (var completed in log.CompletedTicks)
        {
            if (RepairRules.IsInFatigueWindow(completed, tick))
            {
                kept.Add(completed);
            }
        }

        if (kept.Count == log.CompletedTicks.Count)
        {
            return kept.Count;
        }

        log.CompletedTicks = kept;
        ctx.Db.ShipHealLog.ShipEntityId.Update(log);
        return kept.Count;
    }

    private static void RecordHeal(ReducerContext ctx, ulong shipEntityId, ulong tick)
    {
        if (ctx.Db.ShipHealLog.ShipEntityId.Find(shipEntityId) is ShipHealLog log)
        {
            log.CompletedTicks.Add(tick);
            ctx.Db.ShipHealLog.ShipEntityId.Update(log);
            return;
        }

        ctx.Db.ShipHealLog.Insert(new ShipHealLog
        {
            ShipEntityId = shipEntityId,
            CompletedTicks = [tick],
        });
    }

    // A ship that went down and came back is a fresh hull: it owes nothing to the heals the
    // wreck had taken.
    private static void ClearHealLog(ReducerContext ctx, ulong shipEntityId) =>
        ctx.Db.ShipHealLog.ShipEntityId.Delete(shipEntityId);

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
