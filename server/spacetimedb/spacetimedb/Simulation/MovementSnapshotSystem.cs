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
                MovementSnapshotDispatchState state ||
            ctx.Db.SimulationClock.Id.Find(1) is not SimulationClock clock)
        {
            return;
        }

        var shardId = SimulationWorkRules.MovementSnapshotShard(state.Cursor);
        var partition = SimulationWorkRules.MovementSnapshotPartition(state.Cursor);
        var shard = FindMovementShard(ctx, shardId);
        for (var index = partition;
             index < shard.Ships.Count;
             index += SimulationWorkRules.MovementSnapshotPartitionCount)
        {
            WriteMovementSnapshot(ctx, shard.Ships[index], clock.Tick);
        }

        state.Cursor = SimulationWorkRules.NextMovementSnapshotCursor(state.Cursor);
        ctx.Db.MovementSnapshotDispatchState.Id.Update(state);
    }
}
