using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static (uint Processed, uint Dormant) ProcessNpcDecisions(
        ReducerContext ctx,
        TickWorld world)
    {
        var processed = 0u;
        var dormant = 0u;
        for (byte shardId = 0; shardId < SimulationWorkRules.NpcShardCount; shardId++)
        {
            foreach (var ai in ctx.Db.NpcAi.ByDecisionDueShard.Filter(
                         (true, shardId, new Bound<ulong>(0, world.Tick))))
            {
                processed++;
                if (!ai.IsActive)
                {
                    dormant++;
                }

                ProcessNpcDecision(ctx, world, ai);
            }
        }

        return (processed, dormant);
    }

    private static void ProcessNpcDecision(ReducerContext ctx, TickWorld world, NpcAi ai)
    {
        var tick = world.Tick;
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
        HydrateTrackedKinematics(ctx, world, ref ship);
        var definition = Catalog.NpcByArchetypeCode[ship.ArchetypeCode] ??
            throw new InvalidOperationException("NPC content definition is missing.");
        var stats = Catalog.NpcStatsByArchetypeCode[ship.ArchetypeCode];
        var target = FindHydratedShip(ctx, world, ship.TargetEntityId);
        var targetAvailable = target is Ship selected && world.IsAttackablePlayer(ctx, selected);

        // A captain sends her signal up before she acts on it, so the escorts are already under
        // way on the decision that follows this one.
        if (NpcRules.ShouldCallForHelp(
                definition.CallsForHelp,
                ai.HasCalledHelp,
                ship.Hull,
                ship.MaxHull))
        {
            ai.HasCalledHelp = true;
        }

        var orders = EscortOrders(ctx, world, ai);
        var aggroRange = stats.AggroRangeSquares;
        var candidate = orders.Target ??
            (NpcRules.ShouldSearchForTarget(targetAvailable, aggroRange)
                ? FindNearestPlayer(ctx, world, ship, aggroRange)
                : default(Ship?));
        ExecuteNpcDecision(ctx, world, ship, NpcRules.Decide(BuildNpcSnapshot(
            ctx,
            ai,
            ship,
            definition,
            target,
            candidate,
            targetAvailable,
            orders.AwaitingSignal,
            tick,
            world.PatrolBlockers(ctx))));
        ctx.Db.NpcAi.ShipEntityId.Update(ai);
    }

    /// <summary>
    /// What an escort is doing about its captain. Until she calls it is moored and has no orders
    /// at all; once she has, it is handed her target outright rather than left to find one, because
    /// the automatic aggro cap is there to stop a player being swarmed by hostiles that picked the
    /// fight themselves, and this fight was picked for them.
    /// </summary>
    private static (bool AwaitingSignal, Ship? Target) EscortOrders(
        ReducerContext ctx,
        TickWorld world,
        NpcAi ai)
    {
        if (ai.LeaderEntityId == 0)
        {
            return (false, null);
        }

        if (ctx.Db.NpcAi.ShipEntityId.Find(ai.LeaderEntityId) is not NpcAi leader ||
            !leader.HasCalledHelp)
        {
            return (true, null);
        }

        return ctx.Db.Ship.EntityId.Find(ai.LeaderEntityId) is Ship captain
            ? (false, FindHydratedShip(ctx, world, captain.TargetEntityId))
            : (false, null);
    }

    private static Ship? FindHydratedShip(ReducerContext ctx, TickWorld world, ulong entityId)
    {
        if (entityId == 0 || ctx.Db.Ship.EntityId.Find(entityId) is not Ship ship)
        {
            return null;
        }

        HydrateTrackedKinematics(ctx, world, ref ship);
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
        bool awaitingSignal,
        ulong tick,
        IReadOnlyCollection<NavigationBlocker> blockers) => new()
        {
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
            DesiredRange = definition.DesiredRangeSquares,
            FleesWhenCrippled = definition.FleesWhenCrippled,
            AwaitingSignal = awaitingSignal,
            PreferredAmmunition = definition.PreferredAmmunition,
            SelectedAmmunition = (AmmunitionCode)ship.SelectedAmmoCode,
            CanFire = ship.ReadyVolleys > 0 &&
            (!ship.HasFired || tick >= ship.LastShotTick + CombatRules.FireIntervalTicks),
            DecisionSeed = ai.HomeSeed,
            Blockers = blockers,
        };

    private static Ship? FindNearestPlayer(
        ReducerContext ctx,
        TickWorld world,
        Ship source,
        float range)
    {
        // The roster is read once for the whole tick, so the hunt itself is arithmetic. Only
        // the nearest hull in range is worth a datastore read: if she turns out to be swarmed
        // already or sheltered, the next one in is tried, and a decision hardly ever gets
        // past the first.
        var players = world.ActivePlayers(ctx);
        var refused = (Distance: -1f, EntityId: 0ul);
        for (var probe = 0; probe < NpcRules.MaximumTargetProbes; probe++)
        {
            var index = NextPlayerInRange(players, source, range, refused);
            if (index < 0)
            {
                return null;
            }

            var movement = players[index];
            refused = (
                CombatRules.Distance(
                    source.PositionX,
                    source.PositionY,
                    movement.PositionX,
                    movement.PositionY),
                movement.EntityId);
            if (AcquirablePlayer(ctx, world, movement) is Ship candidate)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// The nearest player in range beyond the one already refused, ordered by distance and
    /// then by entity id so every hull breaks a tie the same way.
    /// </summary>
    private static int NextPlayerInRange(
        List<ShipMovement> players,
        Ship source,
        float range,
        (float Distance, ulong EntityId) refused)
    {
        var bestIndex = -1;
        var bestDistance = float.PositiveInfinity;
        for (var index = 0; index < players.Count; index++)
        {
            var movement = players[index];
            var distance = CombatRules.Distance(
                source.PositionX,
                source.PositionY,
                movement.PositionX,
                movement.PositionY);
            if (!movement.IsAlive || distance > range ||
                distance < refused.Distance ||
                distance == refused.Distance && movement.EntityId <= refused.EntityId ||
                distance > bestDistance ||
                distance == bestDistance && movement.EntityId > players[bestIndex].EntityId)
            {
                continue;
            }

            bestIndex = index;
            bestDistance = distance;
        }

        return bestIndex;
    }

    private static Ship? AcquirablePlayer(
        ReducerContext ctx,
        TickWorld world,
        ShipMovement movement)
    {
        // The roster was read before the tick's first decision, so it still lists a player
        // another hull has sunk since; the fat row is the truth about who is still afloat.
        if (!NpcRules.HasAutomaticAggroCapacity(CountNpcAttackers(ctx, movement.EntityId)) ||
            ctx.Db.Ship.EntityId.Find(movement.EntityId) is not Ship candidate ||
            !candidate.IsActive || !candidate.IsAlive)
        {
            return null;
        }

        CopyKinematics(movement, ref candidate);
        candidate.IsActive = true;
        candidate.IsAlive = true;
        return world.IsAttackablePlayer(ctx, candidate) ? candidate : null;
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
        TickWorld world,
        Ship ship,
        NpcDecision decision)
    {
        var command = ToShipCommand(decision);
        if (command is null)
        {
            return;
        }

        var decoded = DecodeCommand(command);
        var snapshot = BuildCommandSnapshot(ctx, world, ship, decoded);
        var commandDecision = CommandPolicy.Evaluate(snapshot, decoded.Kind);
        if (commandDecision.Accepted)
        {
            ApplyAcceptedCommand(ctx, world, ref ship, decoded, commandDecision);
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
        NpcActionKind.Fire => new ShipCommand.Fire(new FireCommand()),
        NpcActionKind.StartRepair => new ShipCommand.StartRepair(new StartRepairCommand()),
        _ => null,
    };
}
