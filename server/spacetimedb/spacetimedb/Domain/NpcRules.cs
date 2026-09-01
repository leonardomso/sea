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

    public static bool HasAutomaticAggroCapacity(int currentAttackers) =>
        currentAttackers < MaximumAutomaticAttackersPerPlayer;

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

        return snapshot.TargetEntityId == 0
            ? DecideWithoutTarget(snapshot, loadout)
            : DecideEngagement(snapshot, loadout);
    }

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

        if (snapshot.HasCourse)
        {
            return new NpcDecision(NpcActionKind.Hold);
        }

        var roam = RoamDestination(snapshot.DecisionSeed, snapshot.DecisionTick);
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

        if (snapshot.DistanceToTarget > snapshot.DesiredRange + RangeTolerance)
        {
            return CourseToward(snapshot, retreat: false, loadout);
        }

        if (snapshot.DistanceToTarget < snapshot.DesiredRange - RangeTolerance)
        {
            return CourseToward(snapshot, retreat: true, loadout);
        }

        if (snapshot.HasCourse)
        {
            return new NpcDecision(NpcActionKind.StopCourse);
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

        return BroadsideTurn(snapshot, loadout);
    }

    public static SpawnPoint RoamDestination(ulong seed, ulong decisionTick)
    {
        var state = seed ^ unchecked(decisionTick * 0x9E3779B97F4A7C15UL);
        var margin = SpawnRules.EdgeMargin + 2f;
        var span = WorldRules.MapMax - WorldRules.MapMin - margin * 2f;
        return new SpawnPoint(
            WorldRules.MapMin + margin + NextUnit(ref state) * span,
            WorldRules.MapMin + margin + NextUnit(ref state) * span);
    }

    private static NpcDecision CourseToward(
        NpcSnapshot snapshot,
        bool retreat,
        NpcLoadout loadout)
    {
        var deltaX = snapshot.TargetX - snapshot.X;
        var deltaY = snapshot.TargetY - snapshot.Y;
        var length = MathF.Max(0.001f, MathF.Sqrt(deltaX * deltaX + deltaY * deltaY));
        var direction = retreat ? -1f : 1f;
        var distance = retreat ? snapshot.DesiredRange : MathF.Min(length, snapshot.DesiredRange);
        return new NpcDecision(
            NpcActionKind.SetCourse,
            snapshot.TargetEntityId,
            Clamp(snapshot.X + deltaX / length * distance * direction),
            Clamp(snapshot.Y + deltaY / length * distance * direction),
            loadout.Ammunition,
            loadout.WeakPoint);
    }

    private static NpcDecision BroadsideTurn(NpcSnapshot snapshot, NpcLoadout loadout)
    {
        var deltaX = snapshot.TargetX - snapshot.X;
        var deltaY = snapshot.TargetY - snapshot.Y;
        var length = MathF.Max(0.001f, MathF.Sqrt(deltaX * deltaX + deltaY * deltaY));
        return new NpcDecision(
            NpcActionKind.SetCourse,
            snapshot.TargetEntityId,
            Clamp(snapshot.X + deltaY / length * TurnCourseDistance),
            Clamp(snapshot.Y - deltaX / length * TurnCourseDistance),
            loadout.Ammunition,
            loadout.WeakPoint);
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
