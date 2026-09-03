using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Reducer]
    public static void RunMovementSnapshotDispatch(
        ReducerContext ctx,
        MovementSnapshotDispatchTimer timer)
    {
        if (ctx.Db.MovementSnapshotDispatchState.Id.Find(1) is not
                MovementSnapshotDispatchState state)
        {
            return;
        }

        var shardId = SimulationWorkRules.MovementSnapshotShard(state.Cursor);
        var partition = SimulationWorkRules.MovementSnapshotPartition(state.Cursor);
        var shard = FindMovementShard(ctx, shardId);
        // Stamp snapshots with the tick that produced the kinematics, not the wall-clock
        // tick of this dispatch, so clients can interpolate on the simulation timeline.
        for (var index = partition;
             index < shard.Ships.Count;
             index += SimulationWorkRules.MovementSnapshotPartitionCount)
        {
            WriteMovementSnapshot(ctx, shard.Ships[index], shard.LastSimulatedTick);
        }

        state.Cursor = SimulationWorkRules.NextMovementSnapshotCursor(state.Cursor);
        ctx.Db.MovementSnapshotDispatchState.Id.Update(state);
    }
}
