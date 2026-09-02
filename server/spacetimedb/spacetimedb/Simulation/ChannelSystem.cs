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
                ProcessRepairChannel(ctx, ships, channel, source, tick);
                break;
            case ChannelCode.Boarding:
                ProcessBoardingChannel(ctx, ships, channel, source, tick);
                break;
            default:
                CloseUnknownChannel(ctx, ships, channel, source);
                break;
        }
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
        source.Hull = TacticalRules.ProgressiveRestore(
            channel.InitialHull, source.MaxHull, 50, elapsed, TacticalRules.RepairDurationTicks);
        source.Sails = TacticalRules.ProgressiveRestore(
            channel.InitialSails, source.MaxSails, 40, elapsed, TacticalRules.RepairDurationTicks);
        source.Cannons = TacticalRules.ProgressiveRestore(
            channel.InitialCannons, source.MaxCannons, 40, elapsed, TacticalRules.RepairDurationTicks);
        source.Crew = TacticalRules.ProgressiveRestore(
            channel.InitialCrew, source.MaxCrew, 20, elapsed, TacticalRules.RepairDurationTicks);

        if (tick >= channel.CompletesAtTick)
        {
            source.ModeCode = (byte)ShipMode.Operational;
            ctx.Db.ShipChannel.ShipEntityId.Delete(channel.ShipEntityId);
            AppendEvent(ctx, channel.ShipEntityId, "repair_completed", "");
        }
        else
        {
            ScheduleChannel(ctx, channel, tick);
        }

        ships.Stage(source);
        SynchronizeDisabledSails(ctx, source, tick);
    }

    private static void ProcessBoardingChannel(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ShipChannel channel,
        Ship source,
        ulong tick)
    {
        if (!TryGetValidBoardingTarget(ctx, ships, channel, source, tick, out var target))
        {
            InterruptBoarding(ctx, channel.ShipEntityId, tick, "boarding_interrupted");
            source.ModeCode = (byte)ShipMode.Operational;
            ships.Stage(source);
            return;
        }

        if (tick < channel.CompletesAtTick)
        {
            ScheduleChannel(ctx, channel, tick);
            return;
        }

        ResolveBoarding(ctx, ships, source, target, tick);
        SetCooldown(
            ctx,
            source.EntityId,
            CooldownCode.Boarding,
            tick + TacticalRules.BoardingCooldownTicks);
        ctx.Db.ShipChannel.ShipEntityId.Delete(channel.ShipEntityId);
        source.ModeCode = (byte)ShipMode.Operational;
        ships.Stage(source);
    }

    private static bool TryGetValidBoardingTarget(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ShipChannel channel,
        Ship source,
        ulong tick,
        out Ship target)
    {
        return ships.TryGet(ctx, channel.TargetEntityId, out target) &&
            TacticalRules.ValidateBoarding(new BoardingRequest(
                source.IsActive && source.IsAlive,
                target.IsActive && target.IsAlive,
                IsIdle: true,
                target.Hull,
                target.MaxHull,
                CombatRules.Distance(
                    source.PositionX,
                    source.PositionY,
                    target.PositionX,
                    target.PositionY),
                CurrentTick: tick,
                ReadyAtTick: tick)) == BoardingRejection.None;
    }

    private static void ResolveBoarding(
        ReducerContext ctx,
        ShipTickBuffer ships,
        Ship source,
        Ship target,
        ulong tick)
    {
        var fatigued = HasActiveStatus(
            ctx, source.EntityId, StatusCode.BoardingFatigue, tick);
        if (TacticalRules.BoardingSucceeds(source.Crew, target.Crew, fatigued))
        {
            target.Crew = WorldRules.ApplyDamage(target.Crew, 25);
            ships.Stage(target);
            AddInventory(ctx, source.EntityId, "boarding_cache", 1);
            RecordBoardingProgress(ctx, source.EntityId, target);
            AppendEvent(ctx, source.EntityId, "boarding_succeeded", $"target={target.EntityId}");
            return;
        }

        ApplyStatus(
            ctx,
            source.EntityId,
            StatusCode.BoardingFatigue,
            tick,
            TacticalRules.BoardingFatigueTicks,
            maximumStacks: 1);
        AppendEvent(ctx, source.EntityId, "boarding_failed", $"target={target.EntityId}");
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
