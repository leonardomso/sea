using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Reducer]
    public static void RunSimulationDispatch(
        ReducerContext ctx,
        SimulationDispatchTimer timer)
    {
        if (ctx.Db.SimulationClock.Id.Find(1) is not SimulationClock clock ||
            ctx.Db.SimulationDispatchState.Id.Find(1) is not SimulationDispatchState state)
        {
            return;
        }

        switch (state.Slot)
        {
            case 0:
                RunWorldDueSlot(ctx, ref clock, ref state);
                break;
            default:
                RunMovementSlot(ctx, clock, ref state);
                break;
        }

        state.Slot = (byte)((state.Slot + 1) %
            SimulationWorkRules.DispatchSlotsPerWorldTick);
        ctx.Db.SimulationDispatchState.Id.Update(state);
    }

    private static void RunWorldDueSlot(
        ReducerContext ctx,
        ref SimulationClock clock,
        ref SimulationDispatchState state)
    {
        clock.Tick++;
        ctx.Db.SimulationClock.Id.Update(clock);
        UpdateWind(ctx, clock.Tick);

        var ships = new ShipTickBuffer();
        ProcessStatuses(ctx, ships, clock.Tick);
        ProcessChannels(ctx, ships, clock.Tick);
        ResolveVolleys(ctx, ships, clock.Tick);
        ProcessRespawns(ctx, ships, clock.Tick);
        ships.Flush(ctx);
        ProcessLootExpiry(ctx, clock.Tick);

        state.NpcAccumulator += (byte)(
            SimulationWorkRules.NpcReducerRateHz * SimulationWorkRules.NpcShardCount);
        if (state.NpcAccumulator >= WorldRules.TickRateHz)
        {
            state.NpcAccumulator -= (byte)WorldRules.TickRateHz;
            var work = ProcessNpcDecisions(ctx, clock.Tick, state.NpcShard);
            RecordNpcTelemetry(ctx, clock.Tick, work);
            state.NpcShard = (byte)((state.NpcShard + 1) %
                SimulationWorkRules.NpcShardCount);
        }
    }

    private static void RunMovementSlot(
        ReducerContext ctx,
        SimulationClock clock,
        ref SimulationDispatchState state)
    {
        var work = AdvanceMovingShips(
            ctx,
            new SpatialTickCache(),
            clock.Tick,
            state.MovementShard,
            clock.ActiveLootCount > 0);
        RecordMovementTelemetry(ctx, clock.Tick, work);
        state.MovementShard = (byte)((state.MovementShard + 1) %
            SimulationWorkRules.MovementShardCount);
    }

}
