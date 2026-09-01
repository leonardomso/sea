using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void ProcessChannels(ReducerContext ctx, ulong tick)
    {
        foreach (var channel in ctx.Db.ShipChannel.ByActive.Filter(true))
        {
            if (ctx.Db.Ship.EntityId.Find(channel.ShipEntityId) is not Ship source ||
                !source.IsActive || !source.IsAlive)
            {
                ctx.Db.ShipChannel.ShipEntityId.Delete(channel.ShipEntityId);
                continue;
            }

            if (channel.ChannelType == "repair")
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

                ctx.Db.Ship.EntityId.Update(repaired);
                SynchronizeDisabledSails(ctx, repaired, tick);

                continue;
            }

            if (channel.ChannelType != "boarding")
            {
                ctx.Db.ShipChannel.ShipEntityId.Delete(channel.ShipEntityId);
                SetShipMode(ctx, channel.ShipEntityId, ShipMode.Operational);
                continue;
            }

            if (ctx.Db.Ship.EntityId.Find(channel.TargetEntityId) is not Ship target ||
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
                continue;
            }

            if (tick < channel.CompletesAtTick)
            {
                continue;
            }

            var fatigued = HasActiveStatus(ctx, source.EntityId, "boarding_fatigue", tick);
            var succeeded = TacticalRules.BoardingSucceeds(source.Crew, target.Crew, fatigued);
            if (succeeded)
            {
                var boarded = target;
                boarded.Crew = WorldRules.ApplyDamage(boarded.Crew, 25);
                ctx.Db.Ship.EntityId.Update(boarded);
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
                    "boarding_fatigue",
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
                "boarding",
                tick + TacticalRules.BoardingCooldownTicks);
            ctx.Db.ShipChannel.ShipEntityId.Delete(channel.ShipEntityId);
            SetShipMode(ctx, source.EntityId, ShipMode.Operational);
        }
    }

}
