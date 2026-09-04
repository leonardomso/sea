namespace Sea.Server;

public enum NpcActionKind : byte
{
    Hold = 0,
    SetCourse = 1,
    StopCourse = 2,
    SelectTarget = 3,
    ClearTarget = 4,
    SetAmmo = 5,
    Fire = 6,
    StartRepair = 7,
}

public readonly record struct NpcSnapshot
{
    public bool Active { get; init; }
    public ShipMode Mode { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float HeadingDegrees { get; init; }
    public bool HasCourse { get; init; }
    public float CourseX { get; init; }
    public float CourseY { get; init; }
    public uint Hull { get; init; }
    public uint MaximumHull { get; init; }
    public bool HasRepairKit { get; init; }
    public ulong TargetEntityId { get; init; }
    public bool TargetAvailable { get; init; }
    public float TargetX { get; init; }
    public float TargetY { get; init; }
    public float DistanceToTarget { get; init; }
    public ulong CandidateTargetId { get; init; }
    public float DesiredRange { get; init; }
    /// <summary>Whether this hull breaks off a fight it is losing instead of dying in it.</summary>
    public bool FleesWhenCrippled { get; init; }
    /// <summary>
    /// True only for an escort whose captain has not called yet: it lies at its mooring and does
    /// nothing at all until she does. Every ship that answers to nobody is under way from the
    /// moment it is spawned.
    /// </summary>
    public bool AwaitingSignal { get; init; }
    public AmmunitionCode PreferredAmmunition { get; init; }
    public AmmunitionCode SelectedAmmunition { get; init; }
    /// <summary>Whether the magazine and the one-second floor both allow a shot this decision.</summary>
    public bool CanFire { get; init; }
    public ulong DecisionSeed { get; init; }
    public IReadOnlyCollection<NavigationBlocker>? Blockers { get; init; }
}

/// <summary>A patrol loop: the ring an idle ship sails, centred anywhere on the chart.</summary>
public readonly record struct PatrolRoute(float CenterX, float CenterY, float Radius);

public readonly record struct NpcDecision(
    NpcActionKind Action,
    ulong TargetEntityId = 0,
    float DestinationX = 0f,
    float DestinationY = 0f,
    AmmunitionCode Ammunition = AmmunitionCode.None);

public static class NpcRules
{
    // Half a second between decisions reads as a hostile that has not noticed you. Five
    // times a second is still a fifth of the AI work of a per-tick brain, and it is inside
    // the window where a captain reads a turn as an answer to what they just did.
    public const ulong DecisionIntervalTicks = 2;

    // How many hulls a captain will look over before giving up on finding a fight this
    // decision. Each one costs a datastore read, and the ones after the third are almost
    // always as swarmed or as sheltered as the first three were.
    public const int MaximumTargetProbes = 3;
    public const int MaximumAutomaticAttackersPerPlayer = 1;
    private const float RangeTolerance = 4f;
    private const float RepairHullRatio = 0.3f;

    /// <summary>
    /// A quarter of the hull left. The Sea Dogs are raiders, not fanatics: past this they put
    /// their helm over and run, which is what makes finishing one a chase rather than a formality.
    /// </summary>
    public const float FleeHullRatio = 0.25f;

    /// <summary>Half the hull left is when a named captain sends up the signal.</summary>
    public const float CallHelpHullRatio = 0.5f;

    /// <summary>How many hulls answer that signal.</summary>
    public const int CallHelpCount = 2;

    private const float TurnCourseDistance = 8f;

    // Every idle ship sails a patrol route: a wide loop across the chart, fixed for the life of
    // the ship by its seed. The old model kept each hull inside a forty-unit bubble around its
    // spawn, which meant the sea was full of ships that never went anywhere and a player could
    // sail the whole map without meeting one under way.
    public const float MinimumRouteRadius = 30f;
    public const float MaximumRouteRadius = 75f;
    public const float MinimumRoamLeg = 12f;
    private const int RoamCandidates = 4;

    // How far off its ring a leg may sit and still count as on route - blockers and the chart
    // edge push a destination around, and re-plotting for every nudge would stall the ship.
    public const float RouteTolerance = 15f;

    // Each new leg swings the ship's bearing around its route by this step, so a patrol
    // sails its loop rather than darting between random points.
    public const float CircuitStepDegrees = 60f;

    // A target that opens this much water has broken contact: it is past the longest gun and
    // past any aggro range on the map, so the ship gives it up and returns to its route rather
    // than trailing a fleeing player forever.
    public const float DisengageRange = 90f;

    /// <summary>
    /// How far a fleeing ship steers for. Past <see cref="DisengageRange"/>, so a hull that
    /// makes it there has broken contact and goes back to its route rather than turning about.
    /// </summary>
    public const float FleeRange = DisengageRange + 10f;

    // Respawns scatter this far around a ship's home, and hostile homes are seeded this far
    // clear of the harbor so nothing spawns on top of the players' waters.
    public const float HomeAnchorRadius = 40f;
    public const float HostileHomeClearance = HomeAnchorRadius + WorldRules.HarborSafeRadius;

    private const float MapHalfSpan = (WorldRules.MapMax - WorldRules.MapMin) / 2f;

    // Spawn shields and harbor waters keep fresh players out of NPC gunsights; a
    // protected target is dropped and no NPC picks it up again until it sails out.
    public static bool IsProtectedFromNpcs(
        ulong invulnerableUntilTick,
        ulong tick,
        float distanceFromHarbor) =>
        invulnerableUntilTick > tick || distanceFromHarbor <= WorldRules.HarborSafeRadius;

    public static bool HasAutomaticAggroCapacity(int currentAttackers) =>
        currentAttackers < MaximumAutomaticAttackersPerPlayer;

    // A ship on patrol hunts wherever its route takes it. Aggro range is the only gate; a
    // second gate on the distance from home is what kept hostiles loitering at their spawn.
    public static bool ShouldSearchForTarget(bool targetAvailable, float aggroRange) =>
        !targetAvailable && aggroRange > 0f;

    public static bool ShouldAttemptRepair(uint hull, uint maximumHull) =>
        maximumHull > 0 && (float)hull / maximumHull <= RepairHullRatio;

    /// <summary>
    /// Whether this hull has taken enough to break off. Only the families that run are asked;
    /// a reef beast has nowhere to run to.
    /// </summary>
    public static bool ShouldFlee(bool fleesWhenCrippled, uint hull, uint maximumHull) =>
        fleesWhenCrippled && maximumHull > 0 && (float)hull / maximumHull <= FleeHullRatio;

    /// <summary>
    /// Whether the signal goes up this decision. It is asked only of a captain who has one to
    /// send and has not sent it yet, so the answer is true on exactly one decision per life.
    /// </summary>
    public static bool ShouldCallForHelp(
        bool callsForHelp,
        bool alreadyCalled,
        uint hull,
        uint maximumHull) =>
        callsForHelp && !alreadyCalled && maximumHull > 0 &&
        (float)hull / maximumHull <= CallHelpHullRatio;

    public static NpcDecision Decide(NpcSnapshot snapshot)
    {
        var loadout = new NpcLoadout(snapshot.PreferredAmmunition);
        if (!snapshot.Active || snapshot.Mode == ShipMode.Sunk)
        {
            return new NpcDecision(NpcActionKind.Hold);
        }

        if (snapshot.Mode != ShipMode.Operational || snapshot.AwaitingSignal)
        {
            return new NpcDecision(NpcActionKind.Hold);
        }

        if (snapshot.TargetEntityId != 0 &&
            snapshot.TargetAvailable &&
            snapshot.DistanceToTarget > DisengageRange)
        {
            return new NpcDecision(NpcActionKind.ClearTarget);
        }

        // Running comes before mending: a crippled raider that stopped to patch itself under the
        // guns that crippled it would only be shot again. It opens the range first, breaks
        // contact at DisengageRange, and repairs once it is clear.
        if (snapshot.TargetAvailable &&
            ShouldFlee(snapshot.FleesWhenCrippled, snapshot.Hull, snapshot.MaximumHull))
        {
            return HoldRange(snapshot, snapshot.DistanceToTarget - FleeRange, loadout);
        }

        if (snapshot.HasRepairKit &&
            ShouldAttemptRepair(snapshot.Hull, snapshot.MaximumHull))
        {
            return new NpcDecision(NpcActionKind.StartRepair);
        }

        return snapshot.TargetEntityId == 0
            ? DecideWithoutTarget(snapshot, loadout)
            : DecideEngagement(snapshot, loadout);
    }

    private static bool IsNearCourseEnd(NpcSnapshot snapshot) =>
        CombatRules.Distance(snapshot.X, snapshot.Y, snapshot.CourseX, snapshot.CourseY) <=
        TurnCourseDistance;

    private static bool IsOnPatrolRoute(NpcSnapshot snapshot, float x, float y)
    {
        var route = RouteFor(snapshot.DecisionSeed);
        return MathF.Abs(
            CombatRules.Distance(route.CenterX, route.CenterY, x, y) - route.Radius) <=
            RouteTolerance;
    }

    private static IReadOnlyCollection<NavigationBlocker> Blockers(NpcSnapshot snapshot) =>
        snapshot.Blockers ?? [];

    private static NpcDecision DecideWithoutTarget(
        NpcSnapshot snapshot,
        NpcLoadout loadout)
    {
        if (snapshot.CandidateTargetId != 0)
        {
            return new NpcDecision(
                NpcActionKind.SelectTarget,
                snapshot.CandidateTargetId,
                Ammunition: loadout.Ammunition);
        }

        // A leg still under way is kept unless it was plotted while chasing something off the
        // route; that one is replaced by a leg back onto it. The next leg is plotted just
        // before the current one ends so the ship swings straight into it instead of
        // stopping, waiting, and setting off.
        if (snapshot.HasCourse &&
            IsOnPatrolRoute(snapshot, snapshot.CourseX, snapshot.CourseY) &&
            !IsNearCourseEnd(snapshot))
        {
            return new NpcDecision(NpcActionKind.Hold);
        }

        var roam = RoamDestination(snapshot);
        return new NpcDecision(
            NpcActionKind.SetCourse,
            DestinationX: roam.X,
            DestinationY: roam.Y,
            Ammunition: loadout.Ammunition);
    }

    private static NpcDecision DecideEngagement(
        NpcSnapshot snapshot,
        NpcLoadout loadout)
    {
        if (!snapshot.TargetAvailable)
        {
            return new NpcDecision(NpcActionKind.ClearTarget);
        }

        var travel = snapshot.DistanceToTarget - snapshot.DesiredRange;
        if (MathF.Abs(travel) > RangeTolerance)
        {
            return HoldRange(snapshot, travel, loadout);
        }

        if (snapshot.SelectedAmmunition != loadout.Ammunition)
        {
            return new NpcDecision(
                NpcActionKind.SetAmmo,
                Ammunition: loadout.Ammunition);
        }

        // Guns bear in every direction now, so a ship at its desired range simply shoots
        // whenever the magazine allows and otherwise holds the range it has.
        return snapshot.CanFire
            ? new NpcDecision(
                NpcActionKind.Fire,
                snapshot.TargetEntityId,
                Ammunition: loadout.Ammunition)
            : new NpcDecision(NpcActionKind.Hold);
    }

    /// <summary>
    /// The loop a ship patrols for its whole life. Derived from the decision seed alone, never
    /// from the tick: a route that changed between decisions would not be a route.
    /// </summary>
    public static PatrolRoute RouteFor(ulong seed)
    {
        var state = seed ^ 0xD1B54A32D192ED03UL;
        var radius = MinimumRouteRadius +
            NextUnit(ref state) * (MaximumRouteRadius - MinimumRouteRadius);

        // The centre is held back far enough that the whole ring stays on the chart; a ring
        // clipped by the edge would flatten into a ship sliding along the border.
        var reach = MathF.Max(0f, MapHalfSpan - SpawnRules.EdgeMargin - radius);
        return new PatrolRoute(
            (NextUnit(ref state) * 2f - 1f) * reach,
            (NextUnit(ref state) * 2f - 1f) * reach,
            radius);
    }

    public static SpawnPoint RoamDestination(NpcSnapshot snapshot)
    {
        var route = RouteFor(snapshot.DecisionSeed);
        var blockers = Blockers(snapshot);
        // Each ship keeps one turning direction for its whole patrol, so the loop reads as a
        // circuit rather than a ship changing its mind at every waypoint.
        var direction = (snapshot.DecisionSeed & 1) == 0 ? 1f : -1f;
        var bearing = CircuitBearing(snapshot, route);
        var destination = new SpawnPoint(Clamp(route.CenterX), Clamp(route.CenterY));
        for (var candidate = 1; candidate <= RoamCandidates; candidate++)
        {
            var angle = bearing + direction * candidate * CircuitStepDegrees * MathF.PI / 180f;
            destination = new SpawnPoint(
                Clamp(route.CenterX + MathF.Cos(angle) * route.Radius),
                Clamp(route.CenterY + MathF.Sin(angle) * route.Radius));
            if (CombatRules.Distance(snapshot.X, snapshot.Y, destination.X, destination.Y) >= MinimumRoamLeg &&
                !NavigationRules.IsDestinationBlocked(destination.X, destination.Y, blockers))
            {
                break;
            }
        }

        return ClearPoint(destination.X, destination.Y, blockers);
    }

    // The loop continues from wherever the ship happens to be on it, so a ship that has just
    // broken off a chase rejoins its route at the nearest point rather than sailing back to
    // where it left. A ship sitting exactly on the centre starts the loop due east.
    private static float CircuitBearing(NpcSnapshot snapshot, PatrolRoute route)
    {
        var deltaX = snapshot.X - route.CenterX;
        var deltaY = snapshot.Y - route.CenterY;
        return deltaX * deltaX + deltaY * deltaY < 1f
            ? 0f
            : MathF.Atan2(deltaY, deltaX);
    }

    // Sails along the line to the target by `travel` units: forward to close a gap,
    // backward (negative) to open one, so the ship comes to rest at its desired range.
    private static NpcDecision HoldRange(
        NpcSnapshot snapshot,
        float travel,
        NpcLoadout loadout)
    {
        var deltaX = snapshot.TargetX - snapshot.X;
        var deltaY = snapshot.TargetY - snapshot.Y;
        var length = MathF.Max(0.001f, MathF.Sqrt(deltaX * deltaX + deltaY * deltaY));
        return SailTo(
            snapshot,
            snapshot.X + deltaX / length * travel,
            snapshot.Y + deltaY / length * travel,
            loadout);
    }

    private static NpcDecision SailTo(
        NpcSnapshot snapshot,
        float x,
        float y,
        NpcLoadout loadout)
    {
        var destination = ClearPoint(Clamp(x), Clamp(y), Blockers(snapshot));
        x = destination.X;
        y = destination.Y;
        if (snapshot.HasCourse &&
            CombatRules.Distance(snapshot.CourseX, snapshot.CourseY, x, y) <= RangeTolerance)
        {
            return new NpcDecision(NpcActionKind.Hold);
        }

        return new NpcDecision(
            NpcActionKind.SetCourse,
            snapshot.TargetEntityId,
            x,
            y,
            loadout.Ammunition);
    }

    // A destination inside an island would be rejected outright and leave the ship
    // idling on the spot, so it is nudged to the nearest open water instead.
    private static SpawnPoint ClearPoint(
        float x,
        float y,
        IReadOnlyCollection<NavigationBlocker> blockers)
    {
        var point = NavigationRules.NearestClearPoint(x, y, blockers);
        return new SpawnPoint(Clamp(point.X), Clamp(point.Y));
    }

    private static float Clamp(float value) => Math.Clamp(
        value,
        WorldRules.MapMin + SpawnRules.EdgeMargin,
        WorldRules.MapMax - SpawnRules.EdgeMargin);

    private static float NextUnit(ref ulong state)
    {
        state = unchecked(state * 6364136223846793005UL + 1442695040888963407UL);
        return (float)((state >> 40) / 16_777_216d);
    }

    private readonly record struct NpcLoadout(AmmunitionCode Ammunition);
}
