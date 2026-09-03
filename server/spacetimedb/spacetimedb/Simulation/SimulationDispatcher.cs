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

        if (!SimulationWorkRules.ShouldAdvanceWorld(clock.ConnectedPlayerCount))
        {
            return;
        }

        Profile("start");
        clock.Tick++;
        ctx.Db.SimulationClock.Id.Update(clock);
        var tick = clock.Tick;
        UpdateWind(ctx, tick);
        Profile("wind");

        var world = new TickWorld(tick);
        // Reloads run before anything stages a ship so the sweep and the tick buffer never
        // write the same row twice.
        ProcessReloads(ctx, tick);
        Profile("reloads");
        var ships = new ShipTickBuffer();
        ProcessEffects(ctx, ships, tick);
        Profile("effects");
        ProcessChannels(ctx, ships, tick);
        Profile("channels");
        RetireVolleys(ctx, tick);
        Profile("volleys");
        ProcessRespawns(ctx, ships, tick);
        Profile("respawns");
        if (SimulationWorkRules.ShouldApplyHazards(tick))
        {
            ApplyEnvironmentalHazards(ctx, ships, tick);
        }

        Profile("hazards");
        ships.Flush(ctx, tick);
        Profile("flush");
        ProcessLootExpiry(ctx, tick);
        Profile("loot");

        // Decisions run before movement so a course issued this tick sails this tick.
        RecordNpcTelemetry(ctx, tick, ProcessNpcDecisions(ctx, world));
        Profile("npc");
        RecordMovementTelemetry(ctx, tick, AdvanceMovingShips(ctx, world));
        Profile("movement");
    }

    private static void Profile(string phase)
    {
        if (SimulationWorkRules.ProfileDispatchPhases)
        {
            Log.Info($"PROF {phase}");
        }
    }
}
