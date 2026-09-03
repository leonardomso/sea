using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Reducer]
    public static void RunSimulationDispatch(
        ReducerContext ctx,
        SimulationDispatchTimer timer)
    {
        if (ctx.Db.SimulationClock.Id.Find(1) is not SimulationClock clock)
        {
            return;
        }

        clock.Tick++;
        ctx.Db.SimulationClock.Id.Update(clock);
        var tick = clock.Tick;
        UpdateWind(ctx, tick);

        var ships = new ShipTickBuffer();
        ProcessStatuses(ctx, ships, tick);
        ProcessChannels(ctx, ships, tick);
        ResolveVolleys(ctx, ships, tick);
        ProcessRespawns(ctx, ships, tick);
        if (SimulationWorkRules.ShouldApplyHazards(tick))
        {
            ApplyEnvironmentalHazards(ctx, ships, tick);
        }

        ships.Flush(ctx);
        ProcessLootExpiry(ctx, tick);

        // Decisions run before movement so a course issued this tick sails this tick.
        RecordNpcTelemetry(ctx, tick, ProcessNpcDecisions(ctx, tick));
        RecordMovementTelemetry(
            ctx,
            tick,
            AdvanceMovingShips(ctx, tick, clock.ActiveLootCount > 0));
    }
}
