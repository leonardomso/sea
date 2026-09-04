using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    /// <summary>
    /// A wreck answering where it comes back. Port Lowell offers one berth, so the choice is really
    /// a confirmation -- but it is the confirmation that puts the wreck in the queue at all, which
    /// is what keeps a player who has walked away from being sailed back out without them.
    /// </summary>
    private static void ApplyChooseRespawn(
        ReducerContext ctx,
        TickWorld world,
        Ship ship,
        ChooseRespawnCommand command)
    {
        var work = ctx.Db.RespawnWork.ShipEntityId.Find(ship.EntityId) ??
            throw new InvalidOperationException("Accepted respawn choice has no pending wreck.");
        work.OptionCode = command.OptionCode;
        work.IsPending = true;
        ctx.Db.RespawnWork.ShipEntityId.Update(work);
        AppendEvent(
            ctx,
            world.Tick,
            ship.EntityId,
            "respawn_chosen",
            $"option={command.OptionCode}");
    }
}
