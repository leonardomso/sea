namespace Sea.Server;

public enum NpcActionKind : byte
{
    Hold = 0,
    SetCourse = 1,
    StopCourse = 2,
    SelectTarget = 3,
    ClearTarget = 4,
    SetAmmo = 5,
    FirePort = 6,
    FireStarboard = 7,
    StartRepair = 8,
}

public readonly record struct NpcSnapshot
{
    public ShipArchetypeCode Archetype { get; init; }
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
    public AmmunitionCode PreferredAmmunition { get; init; }
    public WeakPointCode PreferredWeakPoint { get; init; }
    public AmmunitionCode SelectedAmmunition { get; init; }
    public bool PortReady { get; init; }
    public bool StarboardReady { get; init; }
    public ulong DecisionSeed { get; init; }
    public ulong DecisionTick { get; init; }
    public float HomeX { get; init; }
    public float HomeY { get; init; }
    public IReadOnlyCollection<NavigationBlocker>? Blockers { get; init; }
}

public readonly record struct NpcDecision(
    NpcActionKind Action,
    ulong TargetEntityId = 0,
    float DestinationX = 0f,
    float DestinationY = 0f,
    AmmunitionCode Ammunition = AmmunitionCode.None,
    WeakPointCode WeakPoint = WeakPointCode.Hull);

public static class NpcRules
{
    public const ulong DecisionIntervalTicks = 5;
    public const int MaximumAutomaticAttackersPerPlayer = 1;
    private const float RangeTolerance = 4f;
    private const float RepairHullRatio = 0.3f;
    private const float TurnCourseDistance = 8f;

    // Idle ships patrol the waters around their spawn instead of criss-crossing the chart,
    // and each leg is long enough to read as a sail rather than a twitch.
    public const float RoamRadius = 40f;
    public const float MinimumRoamLeg = 12f;
    private const int RoamCandidates = 4;

    // Each new leg swings the ship's bearing from home on by this step, so a patrol
    // sails a loop around its home waters rather than darting between random points.
    public const float CircuitStepDegrees = 60f;

    // A ship dragged this far from home lets its target go and roams back; it only
    // hunts again once it is inside its home waters, so a fleeing player is not
    // chased across the whole chart.
    public const float LeashRadius = 60f;

    // Hostile ships make their home far enough out that no roam leg reaches the
    // harbor's safe waters.
    public const float HostileHomeClearance = RoamRadius + WorldRules.HarborSafeRadius;

    // Spawn shields and harbor waters keep fresh players out of NPC gunsights; a
    // protected target is dropped and no NPC picks it up again until it sails out.
    public static bool IsProtectedFromNpcs(
        ulong invulnerableUntilTick,
        ulong tick,
        float distanceFromHarbor) =>
        invulnerableUntilTick > tick || distanceFromHarbor <= WorldRules.HarborSafeRadius;

    public static bool HasAutomaticAggroCapacity(int currentAttackers) =>
        currentAttackers < MaximumAutomaticAttackersPerPlayer;

    public static bool ShouldSearchForTarget(
        bool targetAvailable,
        float aggroRange,
        float distanceFromHome) =>
        !targetAvailable && aggroRange > 0f && distanceFromHome <= RoamRadius;

    public static bool ShouldAttemptRepair(uint hull, uint maximumHull) =>
        maximumHull > 0 && (float)hull / maximumHull <= RepairHullRatio;

    public static NpcDecision Decide(NpcSnapshot snapshot)
    {
        var loadout = new NpcLoadout(
            snapshot.PreferredAmmunition,
            snapshot.PreferredWeakPoint);
        if (!snapshot.Active || snapshot.Mode == ShipMode.Sunk)
        {
            return new NpcDecision(NpcActionKind.Hold);
        }

        if (snapshot.Mode != ShipMode.Operational)
        {
            return new NpcDecision(NpcActionKind.Hold);
        }

        if (snapshot.HasRepairKit &&
            ShouldAttemptRepair(snapshot.Hull, snapshot.MaximumHull))
        {
            return new NpcDecision(NpcActionKind.StartRepair);
        }

        if (snapshot.TargetEntityId != 0 && DistanceFromHome(snapshot) > LeashRadius)
        {
            return new NpcDecision(NpcActionKind.ClearTarget);
        }

        return snapshot.TargetEntityId == 0
            ? DecideWithoutTarget(snapshot, loadout)
            : DecideEngagement(snapshot, loadout);
    }

    private static float DistanceFromHome(NpcSnapshot snapshot) =>
        CombatRules.Distance(snapshot.X, snapshot.Y, snapshot.HomeX, snapshot.HomeY);

    private static bool IsInHomeWaters(NpcSnapshot snapshot, float x, float y) =>
        CombatRules.Distance(snapshot.HomeX, snapshot.HomeY, x, y) <= RoamRadius;

    private static IReadOnlyCollection<NavigationBlocker> Blockers(NpcSnapshot snapshot) =>
        snapshot.Blockers ?? [];

    private static NpcDecision DecideWithoutTarget(
        NpcSnapshot snapshot,
        NpcLoadout loadout)
    {
        if (snapshot.Archetype != ShipArchetypeCode.Patrol &&
            snapshot.CandidateTargetId != 0)
        {
            return new NpcDecision(
                NpcActionKind.SelectTarget,
                snapshot.CandidateTargetId,
                Ammunition: loadout.Ammunition,
                WeakPoint: loadout.WeakPoint);
        }

        // A leg still under way is kept unless it was plotted while chasing something
        // out past the leash; that one is replaced by a leg back into home waters.
        if (snapshot.HasCourse && IsInHomeWaters(snapshot, snapshot.CourseX, snapshot.CourseY))
        {
            return new NpcDecision(NpcActionKind.Hold);
        }

        var roam = RoamDestination(snapshot);
        return new NpcDecision(
            NpcActionKind.SetCourse,
            DestinationX: roam.X,
            DestinationY: roam.Y,
            Ammunition: loadout.Ammunition,
            WeakPoint: loadout.WeakPoint);
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
                Ammunition: loadout.Ammunition,
                WeakPoint: loadout.WeakPoint);
        }

        if (snapshot.PortReady && CombatRules.IsInsideBroadsideArc(
                snapshot.X,
                snapshot.Y,
                snapshot.HeadingDegrees,
                snapshot.TargetX,
                snapshot.TargetY,
                BroadsideSide.Port))
        {
            return new NpcDecision(
                NpcActionKind.FirePort,
                snapshot.TargetEntityId,
                Ammunition: loadout.Ammunition,
                WeakPoint: loadout.WeakPoint);
        }

        if (snapshot.StarboardReady && CombatRules.IsInsideBroadsideArc(
                snapshot.X,
                snapshot.Y,
                snapshot.HeadingDegrees,
                snapshot.TargetX,
                snapshot.TargetY,
                BroadsideSide.Starboard))
        {
            return new NpcDecision(
                NpcActionKind.FireStarboard,
                snapshot.TargetEntityId,
                Ammunition: loadout.Ammunition,
                WeakPoint: loadout.WeakPoint);
        }

        // A turn already under way brings a broadside to bear; re-plotting it every
        // decision would leave the ship twitching between headings.
        return snapshot.HasCourse
            ? new NpcDecision(NpcActionKind.Hold)
            : BroadsideTurn(snapshot, loadout);
    }

    public static SpawnPoint RoamDestination(NpcSnapshot snapshot)
    {
        var state = snapshot.DecisionSeed ^ unchecked(snapshot.DecisionTick * 0x9E3779B97F4A7C15UL);
        var blockers = Blockers(snapshot);
        // The loop's radius wanders between legs so the circuit never looks drawn with
        // a compass, and each ship keeps one turning direction for its whole patrol.
        var radius = MinimumRoamLeg + NextUnit(ref state) * (RoamRadius - MinimumRoamLeg);
        var direction = (snapshot.DecisionSeed & 1) == 0 ? 1f : -1f;
        var bearing = CircuitBearing(snapshot, ref state);
        var destination = new SpawnPoint(Clamp(snapshot.HomeX), Clamp(snapshot.HomeY));
        for (var candidate = 1; candidate <= RoamCandidates; candidate++)
        {
            var angle = bearing + direction * candidate * CircuitStepDegrees * MathF.PI / 180f;
            destination = new SpawnPoint(
                Clamp(snapshot.HomeX + MathF.Cos(angle) * radius),
                Clamp(snapshot.HomeY + MathF.Sin(angle) * radius));
            if (CombatRules.Distance(snapshot.X, snapshot.Y, destination.X, destination.Y) >= MinimumRoamLeg &&
                !NavigationRules.IsDestinationBlocked(destination.X, destination.Y, blockers))
            {
                break;
            }
        }

        return ClearPoint(destination.X, destination.Y, blockers);
    }

    // The loop continues from wherever the ship is on it; a ship sitting on its home
    // point starts at a seeded bearing.
    private static float CircuitBearing(NpcSnapshot snapshot, ref ulong state)
    {
        var deltaX = snapshot.X - snapshot.HomeX;
        var deltaY = snapshot.Y - snapshot.HomeY;
        return deltaX * deltaX + deltaY * deltaY < 1f
            ? NextUnit(ref state) * MathF.PI * 2f
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

    private static NpcDecision BroadsideTurn(NpcSnapshot snapshot, NpcLoadout loadout)
    {
        var deltaX = snapshot.TargetX - snapshot.X;
        var deltaY = snapshot.TargetY - snapshot.Y;
        var length = MathF.Max(0.001f, MathF.Sqrt(deltaX * deltaX + deltaY * deltaY));
        return SailTo(
            snapshot,
            snapshot.X + deltaY / length * TurnCourseDistance,
            snapshot.Y - deltaX / length * TurnCourseDistance,
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
            loadout.Ammunition,
            loadout.WeakPoint);
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

    private readonly record struct NpcLoadout(
        AmmunitionCode Ammunition,
        WeakPointCode WeakPoint);
}
