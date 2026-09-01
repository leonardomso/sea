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
            if (!ships.TryGet(ctx, channel.ShipEntityId, out var source) ||
                !source.IsActive || !source.IsAlive)
            {
                ctx.Db.ShipChannel.ShipEntityId.Delete(channel.ShipEntityId);
                continue;
            }

            if (channel.ChannelTypeCode == (byte)ChannelCode.Repair)
            {
                var elapsed = Math.Min(
                    (ulong)TacticalRules.RepairDurationTicks,
                    tick - channel.StartedAtTick);
                var repaired = source;
                repaired.Hull = TacticalRules.ProgressiveRestore(
                    channel.InitialHull,
                    source.MaxHull,
                    restoreAmount: 50,
                    elapsed,
                    TacticalRules.RepairDurationTicks);
                repaired.Sails = TacticalRules.ProgressiveRestore(
                    channel.InitialSails,
                    source.MaxSails,
                    restoreAmount: 40,
                    elapsed,
                    TacticalRules.RepairDurationTicks);
                repaired.Cannons = TacticalRules.ProgressiveRestore(
                    channel.InitialCannons,
                    source.MaxCannons,
                    restoreAmount: 40,
                    elapsed,
                    TacticalRules.RepairDurationTicks);
                repaired.Crew = TacticalRules.ProgressiveRestore(
                    channel.InitialCrew,
                    source.MaxCrew,
                    restoreAmount: 20,
                    elapsed,
                    TacticalRules.RepairDurationTicks);
                if (tick >= channel.CompletesAtTick)
                {
                    repaired.ModeCode = (byte)ShipMode.Operational;
                    ctx.Db.ShipChannel.ShipEntityId.Delete(channel.ShipEntityId);
                    AppendEvent(ctx, channel.ShipEntityId, "repair_completed", "");
                }
                else
                {
                    var scheduled = channel;
                    scheduled.NextProcessTick = tick + 1;
                    ctx.Db.ShipChannel.ShipEntityId.Update(scheduled);
                }

                ships.Stage(repaired);
                SynchronizeDisabledSails(ctx, repaired, tick);

                continue;
            }

            if (channel.ChannelTypeCode != (byte)ChannelCode.Boarding)
            {
                ctx.Db.ShipChannel.ShipEntityId.Delete(channel.ShipEntityId);
                source.ModeCode = (byte)ShipMode.Operational;
                ships.Stage(source);
                continue;
            }

            if (!ships.TryGet(ctx, channel.TargetEntityId, out var target) ||
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
                    ReadyAtTick: tick)) != BoardingRejection.None)
            {
                InterruptBoarding(ctx, channel.ShipEntityId, tick, "boarding_interrupted");
                source.ModeCode = (byte)ShipMode.Operational;
                ships.Stage(source);
                continue;
            }

            if (tick < channel.CompletesAtTick)
            {
                var scheduled = channel;
                scheduled.NextProcessTick = tick + 1;
                ctx.Db.ShipChannel.ShipEntityId.Update(scheduled);
                continue;
            }

            var fatigued = HasActiveStatus(
                ctx,
                source.EntityId,
                StatusCode.BoardingFatigue,
                tick);
            var succeeded = TacticalRules.BoardingSucceeds(source.Crew, target.Crew, fatigued);
            if (succeeded)
            {
                var boarded = target;
                boarded.Crew = WorldRules.ApplyDamage(boarded.Crew, 25);
                ships.Stage(boarded);
                AddInventory(ctx, source.EntityId, "boarding_cache", 1);
                AppendEvent(
                    ctx,
                    source.EntityId,
                    "boarding_succeeded",
                    $"target={target.EntityId}");
            }
            else
            {
                ApplyStatus(
                    ctx,
                    source.EntityId,
                    StatusCode.BoardingFatigue,
                    tick,
                    TacticalRules.BoardingFatigueTicks,
                    maximumStacks: 1);
                AppendEvent(
                    ctx,
                    source.EntityId,
                    "boarding_failed",
                    $"target={target.EntityId}");
            }

            SetCooldown(
                ctx,
                source.EntityId,
                CooldownCode.Boarding,
                tick + TacticalRules.BoardingCooldownTicks);
            ctx.Db.ShipChannel.ShipEntityId.Delete(channel.ShipEntityId);
            source.ModeCode = (byte)ShipMode.Operational;
            ships.Stage(source);
        }
    }

}
