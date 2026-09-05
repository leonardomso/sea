using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void ProcessChannels(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ulong tick)
    {
        foreach (var channel in ctx.Db.ShipChannel.ByChannelDue.Filter(
                     (true, new Bound<ulong>(0, tick))))
        {
            ProcessDueChannel(ctx, ships, channel, tick);
        }
    }

    private static void ProcessDueChannel(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ShipChannel channel,
        ulong tick)
    {
        if (!ships.TryGet(ctx, channel.ShipEntityId, out var source) ||
            !source.IsActive || !source.IsAlive)
        {
            ctx.Db.ShipChannel.ShipEntityId.Delete(channel.ShipEntityId);
            return;
        }

        switch ((ChannelCode)channel.ChannelTypeCode)
        {
            case ChannelCode.Repair:
                CompleteRepairChannel(ctx, ships, channel, source, tick);
                return;
            case ChannelCode.CastOff:
                CompleteCastOffChannel(ctx, ships, channel, source, tick);
                return;
            default:
                CloseUnknownChannel(ctx, ships, channel, source);
                return;
        }
    }

    /// <summary>
    /// A channel that is only ever processed once, on the tick it completes. Nothing is mended
    /// while the crew works, so breaking the attempt at the last second costs the whole repair.
    /// </summary>
    private static void CompleteRepairChannel(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ShipChannel channel,
        Ship source,
        ulong tick)
    {
        // A burning ship repairs at half rate, which is the lasting part of what incendiary
        // rounds do beyond their own damage over time.
        var healed = RepairRules.Heal(
            source.MaxHull,
            source.RepairAmount,
            CountRecentHeals(ctx, source.EntityId, tick),
            HasActiveEffect(ctx, source.EntityId, EffectCode.Burning, tick));
        source.Hull = RepairRules.Restore(source.Hull, source.MaxHull, healed);
        source.ModeCode = (byte)ShipMode.Operational;
        ctx.Db.ShipChannel.ShipEntityId.Delete(channel.ShipEntityId);
        RecordHeal(ctx, source.EntityId, tick);
        SetCooldown(
            ctx,
            source.EntityId,
            CooldownCode.Repair,
            tick + RepairRules.CooldownTicks);
        AppendEvent(ctx, tick, channel.ShipEntityId, "repair_completed", $"hull={healed}");
        ships.Stage(source);
    }

    /// <summary>
    /// The course that opened the cast-off has been sitting on the ship the whole time; finishing
    /// it is what finally hands the ship to the sailing shard.
    /// </summary>
    private static void CompleteCastOffChannel(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ShipChannel channel,
        Ship source,
        ulong tick)
    {
        ctx.Db.ShipChannel.ShipEntityId.Delete(channel.ShipEntityId);
        source.ModeCode = (byte)ShipMode.Operational;
        source.IsMoving = source.HasRoute;
        AppendEvent(ctx, tick, channel.ShipEntityId, "cast_off_completed", "");
        ships.Stage(source);
    }

    private static void CloseUnknownChannel(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ShipChannel channel,
        Ship source)
    {
        ctx.Db.ShipChannel.ShipEntityId.Delete(channel.ShipEntityId);
        source.ModeCode = (byte)ShipMode.Operational;
        ships.Stage(source);
    }
}
