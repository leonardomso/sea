using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    /// <summary>
    /// The channelled repair. It costs nothing to open and mends nothing until it completes, so
    /// the price of it is the three seconds the crew spends off the guns.
    /// </summary>
    private static void ApplyStartRepair(ReducerContext ctx, TickWorld world, ref Ship ship)
    {
        var completesAt = world.Tick + ship.RepairChannelTicks;
        ctx.Db.ShipChannel.Insert(new ShipChannel
        {
            ShipEntityId = ship.EntityId,
            ChannelType = HotPathCodes.ChannelId(ChannelCode.Repair),
            ChannelTypeCode = (byte)ChannelCode.Repair,
            TargetEntityId = ship.EntityId,
            StartedAtTick = world.Tick,
            CompletesAtTick = completesAt,

            // Nothing happens on the way, so the channel is looked at once, on the tick it is due.
            NextProcessTick = completesAt,
            InitialHull = ship.Hull,
            DamageTaken = 0,
            IsActive = true,
        });
        AppendEvent(ctx, world.Tick, ship.EntityId, "repair_started", "");
    }

    /// <summary>
    /// The kit: a crate opened on deck. It mends less than a full channel and runs on a cooldown
    /// of its own, which is what makes it the answer to being caught mid-fight rather than a
    /// faster version of the repair.
    /// </summary>
    private static void ApplyUseRepairKit(ReducerContext ctx, TickWorld world, ref Ship ship)
    {
        var kit = FindInventory(ctx, ship.EntityId, "repair_kit") ??
            throw new InvalidOperationException("Accepted repair kit is missing.");
        kit.Quantity--;
        ctx.Db.Inventory.InventoryId.Update(kit);

        var healed = RepairRules.Heal(
            ship.MaxHull,
            RepairRules.KitAmount,
            CountRecentHeals(ctx, ship.EntityId, world.Tick),
            HasActiveEffect(ctx, ship.EntityId, EffectCode.Burning, world.Tick));
        ship.Hull = RepairRules.Restore(ship.Hull, ship.MaxHull, healed);
        RecordHeal(ctx, ship.EntityId, world.Tick);
        SetCooldown(
            ctx,
            ship.EntityId,
            CooldownCode.RepairKit,
            world.Tick + RepairRules.KitCooldownTicks);
        AppendEvent(ctx, world.Tick, ship.EntityId, "repair_kit_used", $"hull={healed}");
    }

    private static void ApplyCancelChannel(ReducerContext ctx, TickWorld world, ref Ship ship)
    {
        var castOff = FindActiveChannel(ctx, ship.EntityId) is ShipChannel channel &&
            channel.ChannelTypeCode == (byte)ChannelCode.CastOff;
        if (!CancelActiveChannel(ctx, ref ship, world.Tick))
        {
            throw new InvalidOperationException("Accepted cancellation has no channel.");
        }

        // A cast-off is the whole of the course out of the port; abandoning it leaves the ship
        // where it is rather than sailing it out unpaid for.
        if (castOff)
        {
            ClearRoute(ctx, world, ref ship);
        }
    }

    /// <summary>
    /// Ending a channel early. A repair still owes its cooldown -- the crew came off the pumps
    /// either way -- while a cast-off is only an intention and costs nothing to give up.
    /// </summary>
    private static bool CancelActiveChannel(ReducerContext ctx, ref Ship ship, ulong tick)
    {
        if (FindActiveChannel(ctx, ship.EntityId) is not ShipChannel channel)
        {
            return false;
        }

        ctx.Db.ShipChannel.ShipEntityId.Delete(ship.EntityId);
        if (channel.ChannelTypeCode == (byte)ChannelCode.Repair)
        {
            SetCooldown(
                ctx,
                ship.EntityId,
                CooldownCode.Repair,
                tick + RepairRules.CooldownTicks);
        }

        ship.ModeCode = (byte)ShipMode.Operational;
        AppendEvent(ctx, tick, ship.EntityId, $"{channel.ChannelType}_cancelled", "");
        return true;
    }
}
