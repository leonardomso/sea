using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Reducer]
    public static void RunHazardDispatch(ReducerContext ctx, HazardDispatchTimer timer)
    {
        if (ctx.Db.HazardDispatchState.Id.Find(1) is not HazardDispatchState state ||
            ctx.Db.SimulationClock.Id.Find(1) is not SimulationClock clock)
        {
            return;
        }

        var ships = new ShipTickBuffer();
        var kind = SimulationWorkRules.HazardKind(state.Cursor);
        var shard = SimulationWorkRules.HazardDispatchShard(state.Cursor);
        if (kind == WorldObjectCode.Storm && shard == 0)
        {
            MoveStorms(ctx, clock.Tick);
        }

        ApplyEnvironmentalHazardKind(ctx, ships, clock.Tick, kind, shard);
        ships.Flush(ctx);
        state.Cursor = SimulationWorkRules.NextHazardCursor(state.Cursor);
        ctx.Db.HazardDispatchState.Id.Update(state);
    }
}
