using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void SetSimulationCadence(
        ReducerContext ctx,
        bool hasConnectedPlayers)
    {
        var dispatch = ctx.Db.SimulationDispatchTimer.Iter().Single();
        dispatch.ScheduledAt = Interval(
            SimulationWorkRules.DispatchIntervalMilliseconds(hasConnectedPlayers));
        ctx.Db.SimulationDispatchTimer.ScheduledId.Update(dispatch);

        var snapshot = ctx.Db.MovementSnapshotDispatchTimer.Iter().Single();
        snapshot.ScheduledAt = Interval(
            SimulationWorkRules.SnapshotIntervalMilliseconds(hasConnectedPlayers));
        ctx.Db.MovementSnapshotDispatchTimer.ScheduledId.Update(snapshot);

        var hazard = ctx.Db.HazardDispatchTimer.Iter().Single();
        hazard.ScheduledAt = Interval(
            SimulationWorkRules.HazardIntervalMilliseconds(hasConnectedPlayers));
        ctx.Db.HazardDispatchTimer.ScheduledId.Update(hazard);
    }

    private static ScheduleAt.Interval Interval(double milliseconds) =>
        new ScheduleAt.Interval(TimeSpan.FromMilliseconds(milliseconds));
}
