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

        if ((ChannelCode)channel.ChannelTypeCode == ChannelCode.Repair)
        {
            ProcessRepairChannel(ctx, ships, channel, source, tick);
            return;
        }

        CloseUnknownChannel(ctx, ships, channel, source);
    }

    private static void ProcessRepairChannel(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ShipChannel channel,
        Ship source,
        ulong tick)
    {
        var elapsed = Math.Min(
            (ulong)TacticalRules.RepairDurationTicks,
            tick - channel.StartedAtTick);
        var restored = TacticalRules.ProgressiveRestore(
            channel.InitialHull, source.MaxHull, 50, elapsed, TacticalRules.RepairDurationTicks);
        // A burning ship repairs at half rate, which is the only thing incendiary rounds do
        // beyond their own damage over time.
        var healing = EffectRules.HealingMultiplier(
            HasActiveEffect(ctx, source.EntityId, EffectCode.Burning, tick));
        source.Hull = channel.InitialHull +
            (uint)((restored - channel.InitialHull) * healing);

        if (tick >= channel.CompletesAtTick)
        {
            source.ModeCode = (byte)ShipMode.Operational;
            ctx.Db.ShipChannel.ShipEntityId.Delete(channel.ShipEntityId);
            AppendEvent(ctx, tick, channel.ShipEntityId, "repair_completed", "");
        }
        else
        {
            ScheduleChannel(ctx, channel, tick);
        }

        ships.Stage(source);
    }

    private static void ScheduleChannel(ReducerContext ctx, ShipChannel channel, ulong tick)
    {
        channel.NextProcessTick = tick + 1;
        ctx.Db.ShipChannel.ShipEntityId.Update(channel);
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
