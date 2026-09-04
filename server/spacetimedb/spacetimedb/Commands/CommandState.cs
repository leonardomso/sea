using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Reducer]
    public static void IssueShipCommand(ReducerContext ctx, CommandEnvelope envelope)
    {
        if (ctx.Db.PlayerOwnership.Owner.Find(ctx.Sender) is not PlayerOwnership ownership)
        {
            PublishCommandResult(
                ctx,
                ctx.Sender,
                envelope.CommandId,
                accepted: false,
                CommandRejectionCode.PlayerNotLoaded,
                ShipMode.Sunk,
                isDuplicate: false);
            return;
        }

        var state = ctx.Db.PlayerCommandState.Owner.Find(ctx.Sender) ??
            throw new InvalidOperationException("Loaded player command state is missing.");
        if (TryPublishRepeatedCommand(ctx, envelope, state))
        {
            return;
        }

        ProcessNewCommand(ctx, envelope, ownership, state);
    }

    private static bool TryPublishRepeatedCommand(
        ReducerContext ctx,
        CommandEnvelope envelope,
        PlayerCommandState state)
    {
        var sequence = CommandSequencePolicy.Evaluate(
            state.LastProcessedCommandId,
            envelope.CommandId);
        if (sequence == CommandSequenceDecision.Duplicate)
        {
            PublishCommandResult(
                ctx,
                ctx.Sender,
                envelope.CommandId,
                state.LastAccepted,
                (CommandRejectionCode)state.LastRejectionCode,
                (ShipMode)state.LastModeCode,
                isDuplicate: true);
            return true;
        }

        if (sequence == CommandSequenceDecision.Stale)
        {
            PublishCommandResult(
                ctx,
                ctx.Sender,
                envelope.CommandId,
                accepted: false,
                CommandRejectionCode.StaleCommand,
                (ShipMode)state.LastModeCode,
                isDuplicate: false);
            return true;
        }

        return false;
    }

    private static void ProcessNewCommand(
        ReducerContext ctx,
        CommandEnvelope envelope,
        PlayerOwnership ownership,
        PlayerCommandState state)
    {
        var ship = ctx.Db.Ship.EntityId.Find(ownership.ShipEntityId) ??
            throw new InvalidOperationException("Loaded player ship is missing.");
        var world = TickWorld.Open(ctx);
        HydrateTrackedKinematics(ctx, world, ref ship);
        var decoded = DecodeCommand(envelope.Command);
        var snapshot = BuildCommandSnapshot(ctx, world, ship, decoded);
        var decision = CommandPolicy.Evaluate(snapshot, decoded.Kind);
        if (decision.Accepted)
        {
            ApplyAcceptedCommand(ctx, world, ref ship, decoded, decision);
        }

        state.LastProcessedCommandId = envelope.CommandId;
        state.LastAccepted = decision.Accepted;
        state.LastRejectionCode = (byte)decision.Rejection;
        state.LastModeCode = (byte)decision.NextMode;
        ctx.Db.PlayerCommandState.Owner.Update(state);
        PublishCommandResult(
            ctx,
            ctx.Sender,
            envelope.CommandId,
            decision.Accepted,
            decision.Rejection,
            decision.NextMode,
            isDuplicate: false);
    }

    private static void EnsureCommandState(
        ReducerContext ctx,
        Identity owner,
        ulong shipEntityId)
    {
        if (ctx.Db.PlayerCommandState.Owner.Find(owner) is not null)
        {
            return;
        }

        var ship = ctx.Db.Ship.EntityId.Find(shipEntityId) ??
            throw new InvalidOperationException("Cannot create command state without a ship.");
        ctx.Db.PlayerCommandState.Insert(new PlayerCommandState
        {
            Owner = owner,
            LastProcessedCommandId = 0,
            LastAccepted = false,
            LastRejectionCode = (byte)CommandRejectionCode.None,
            LastModeCode = ship.ModeCode,
        });
    }

    private static void PublishCommandResult(
        ReducerContext ctx,
        Identity owner,
        ulong commandId,
        bool accepted,
        CommandRejectionCode rejection,
        ShipMode mode,
        bool isDuplicate)
    {
        ctx.Db.CommandResultEvent.Insert(new CommandResultEvent
        {
            Owner = owner,
            CommandId = commandId,
            Accepted = accepted,
            RejectionCode = (byte)rejection,
            ModeCode = (byte)mode,
            IsDuplicate = isDuplicate,
        });
    }
}
