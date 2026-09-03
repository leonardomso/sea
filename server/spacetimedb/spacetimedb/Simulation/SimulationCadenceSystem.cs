using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void SetSimulationCadence(
        ReducerContext ctx,
        bool hasConnectedPlayers)
    {
        var dispatch = ctx.Db.SimulationDispatchTimer.Iter().Single();
        dispatch.ScheduledAt = new ScheduleAt.Interval(TimeSpan.FromMilliseconds(
            SimulationWorkRules.DispatchIntervalMilliseconds(hasConnectedPlayers)));
        ctx.Db.SimulationDispatchTimer.ScheduledId.Update(dispatch);
    }
}
