using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static (uint Processed, uint Dormant) ProcessNpcDecisions(
        ReducerContext ctx,
        ulong tick)
    {
        var processed = 0u;
        var dormant = 0u;
        // Every decision this tick steers around the same islands and respects the
        // same harbor waters; read both once.
        NpcWorldContext? world = null;
        for (byte shardId = 0; shardId < SimulationWorkRules.NpcShardCount; shardId++)
        {
            foreach (var ai in ctx.Db.NpcAi.ByDecisionDueShard.Filter(
                         (true, shardId, new Bound<ulong>(0, tick))))
            {
                processed++;
                if (!ai.IsActive)
                {
                    dormant++;
                }

                world ??= new NpcWorldContext(NavigationBlockers(ctx), FindHarbor(ctx));
                ProcessNpcDecision(ctx, ai, tick, world.Value);
            }
        }

        return (processed, dormant);
    }

    private readonly record struct NpcWorldContext(
        IReadOnlyCollection<NavigationBlocker> Blockers,
        WorldObject? Harbor)
    {
        public bool IsAttackablePlayer(Ship ship, ulong tick) =>
            ship.IsActive && ship.IsAlive &&
            ship.FactionCode == (byte)FactionCode.Player &&
            !NpcRules.IsProtectedFromNpcs(
                ship.InvulnerableUntilTick,
                tick,
                Harbor is WorldObject harbor
                    ? CombatRules.Distance(
                        ship.PositionX,
                        ship.PositionY,
                        harbor.PositionX,
                        harbor.PositionY)
                    : float.PositiveInfinity);
    }

    private static void ProcessNpcDecision(
        ReducerContext ctx,
        NpcAi ai,
        ulong tick,
        NpcWorldContext world)
    {
        ai.NextDecisionTick = tick + NpcRules.DecisionIntervalTicks;
        if (ctx.Db.Ship.EntityId.Find(ai.ShipEntityId) is not Ship ship ||
            !ship.IsActive || !ship.IsAlive)
        {
            ai.IsActive = false;
            ctx.Db.NpcAi.ShipEntityId.Update(ai);
            return;
        }

        // Ship rows only republish kinematics on chunk changes; decisions need the
        // live position and course or every NPC keeps re-plotting from stale points.
        HydrateTrackedKinematics(ctx, ref ship);
        var definition = Catalog.NpcByArchetypeCode[ship.ArchetypeCode] ??
            throw new InvalidOperationException("NPC content definition is missing.");
        var target = FindHydratedShip(ctx, ship.TargetEntityId);
        var targetAvailable = target is Ship selected && world.IsAttackablePlayer(selected, tick);
        var candidate = NpcRules.ShouldSearchForTarget(
                targetAvailable,
                definition.AggroRange,
                CombatRules.Distance(ship.PositionX, ship.PositionY, ai.HomeX, ai.HomeY))
            ? FindNearestPlayer(ctx, ship, definition.AggroRange, tick, world)
            : default(Ship?);
        ExecuteNpcDecision(ctx, ship, NpcRules.Decide(BuildNpcSnapshot(
            ctx,
            ai,
            ship,
            definition,
            target,
            candidate,
            targetAvailable,
            tick,
            world.Blockers)));
        ctx.Db.NpcAi.ShipEntityId.Update(ai);
    }

    private static Ship? FindHydratedShip(ReducerContext ctx, ulong entityId)
    {
        if (entityId == 0 || ctx.Db.Ship.EntityId.Find(entityId) is not Ship ship)
        {
            return null;
        }

        HydrateTrackedKinematics(ctx, ref ship);
        return ship;
    }

    private static NpcSnapshot BuildNpcSnapshot(
        ReducerContext ctx,
        NpcAi ai,
        Ship ship,
        NpcContent definition,
        Ship? target,
        Ship? candidate,
        bool targetAvailable,
        ulong tick,
        IReadOnlyCollection<NavigationBlocker> blockers) => new()
        {
            Archetype = (ShipArchetypeCode)ship.ArchetypeCode,
            Active = ship.IsActive && ship.IsAlive,
            Mode = ResolveMode(ship),
            X = ship.PositionX,
            Y = ship.PositionY,
            HeadingDegrees = ship.HeadingDegrees,
            HasCourse = ship.HasCourse,
            CourseX = ship.DestinationX,
            CourseY = ship.DestinationY,
            Hull = ship.Hull,
            MaximumHull = ship.MaxHull,
            HasRepairKit = NpcRules.ShouldAttemptRepair(ship.Hull, ship.MaxHull) &&
            HasInventory(ctx, ship.EntityId, "repair_kit"),
            TargetEntityId = ship.TargetEntityId,
            TargetAvailable = targetAvailable,
            TargetX = targetAvailable ? target!.Value.PositionX : ship.PositionX,
            TargetY = targetAvailable ? target!.Value.PositionY : ship.PositionY,
            DistanceToTarget = targetAvailable
            ? CombatRules.Distance(
                ship.PositionX,
                ship.PositionY,
                target!.Value.PositionX,
                target.Value.PositionY)
            : float.PositiveInfinity,
            CandidateTargetId = candidate?.EntityId ?? 0,
            DesiredRange = definition.DesiredRange,
            PreferredAmmunition = definition.PreferredAmmunition,
            PreferredWeakPoint = definition.PreferredWeakPoint,
            SelectedAmmunition = (AmmunitionCode)ship.SelectedAmmoCode,
            PortReady = tick >= ship.NextPortFireTick,
            StarboardReady = tick >= ship.NextStarboardFireTick,
            DecisionSeed = ai.HomeSeed,
            DecisionTick = tick,
            HomeX = ai.HomeX,
            HomeY = ai.HomeY,
            Blockers = blockers,
        };

    private static Ship? FindNearestPlayer(
        ReducerContext ctx,
        Ship source,
        float range,
        ulong tick,
        NpcWorldContext world)
    {
        // Players are few, so walking their thin published rows beats scanning every
        // ship in the surrounding chunks; the fat Ship row is only read for a player
        // that is actually in range.
        Ship? nearest = null;
        var nearestDistance = float.PositiveInfinity;
        foreach (var movement in ctx.Db.ShipMovement.ByActiveFaction.Filter(
                     (true, (byte)FactionCode.Player)))
        {
            var distance = CombatRules.Distance(
                source.PositionX,
                source.PositionY,
                movement.PositionX,
                movement.PositionY);
            if (!movement.IsAlive || distance > range || distance > nearestDistance ||
                distance == nearestDistance && nearest is Ship current &&
                movement.EntityId > current.EntityId)
            {
                continue;
            }

            if (!NpcRules.HasAutomaticAggroCapacity(CountNpcAttackers(ctx, movement.EntityId)) ||
                ctx.Db.Ship.EntityId.Find(movement.EntityId) is not Ship candidate)
            {
                continue;
            }

            CopyKinematics(movement, ref candidate);
            if (!world.IsAttackablePlayer(candidate, tick))
            {
                continue;
            }

            nearest = candidate;
            nearestDistance = distance;
        }

        return nearest;
    }

    private static int CountNpcAttackers(ReducerContext ctx, ulong playerEntityId)
    {
        var count = 0;
        foreach (var attacker in ctx.Db.Ship.ByTarget.Filter(playerEntityId))
        {
            if (attacker.FactionCode != (byte)FactionCode.Npc ||
                !attacker.IsActive ||
                !attacker.IsAlive)
            {
                continue;
            }

            count++;
            if (!NpcRules.HasAutomaticAggroCapacity(count))
            {
                break;
            }
        }

        return count;
    }

    private static bool HasInventory(ReducerContext ctx, ulong shipEntityId, string itemId) =>
        FindInventory(ctx, shipEntityId, itemId) is Inventory item && item.Quantity > 0;

    private static void ExecuteNpcDecision(
        ReducerContext ctx,
        Ship ship,
        NpcDecision decision)
    {
        var command = ToShipCommand(decision);
        if (command is null)
        {
            return;
        }

        var decoded = DecodeCommand(command);
        var snapshot = BuildCommandSnapshot(ctx, ship, decoded);
        var commandDecision = CommandPolicy.Evaluate(snapshot, decoded.Kind);
        if (commandDecision.Accepted)
        {
            ApplyAcceptedCommand(ctx, ref ship, decoded, commandDecision);
        }
    }

    private static ShipCommand? ToShipCommand(NpcDecision decision) => decision.Action switch
    {
        NpcActionKind.SetCourse => new ShipCommand.SetCourse(new SetCourseCommand
        {
            X = decision.DestinationX,
            Y = decision.DestinationY,
        }),
        NpcActionKind.StopCourse => new ShipCommand.StopCourse(new StopCourseCommand()),
        NpcActionKind.SelectTarget => new ShipCommand.SelectTarget(new SelectTargetCommand
        {
            EntityId = decision.TargetEntityId,
        }),
        NpcActionKind.ClearTarget => new ShipCommand.ClearTarget(new ClearTargetCommand()),
        NpcActionKind.SetAmmo => new ShipCommand.SetAmmo(new SetAmmoCommand
        {
            AmmoId = HotPathCodes.AmmunitionId(decision.Ammunition),
        }),
        NpcActionKind.FirePort => FireCommand("port", decision.WeakPoint),
        NpcActionKind.FireStarboard => FireCommand("starboard", decision.WeakPoint),
        NpcActionKind.StartRepair => new ShipCommand.StartRepair(new StartRepairCommand()),
        _ => null,
    };

    private static ShipCommand.FireBroadside FireCommand(
        string side,
        WeakPointCode weakPoint) =>
        new ShipCommand.FireBroadside(new FireBroadsideCommand
        {
            Side = side,
            WeakPoint = HotPathCodes.WeakPointId(weakPoint),
        });
}
