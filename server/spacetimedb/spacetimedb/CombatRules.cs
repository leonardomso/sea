namespace Sea.Server;

/// <summary>
/// Which of a hull's three armour faces a volley lands on. Math section 5.1.
/// </summary>
public enum ArmorFace
{
    Front,
    Sides,
    Back,
}

public enum FireRejection
{
    None,
    SourceSunk,
    NoTarget,
    TargetSunk,
    Reloading,
    FiringTooFast,
    OutOfRange,
    InPort,
    SpawnShielded,
    Busy,

    /// <summary>A boarding party is on deck and the guns are spiked (SEA_3 4.3).</summary>
    Silenced,
}

public readonly record struct FireRequest
{
    public bool SourceAlive { get; init; }
    public bool TargetSelected { get; init; }
    public bool TargetAlive { get; init; }

    /// <summary>Port Lowell blocks firing: the harbour is a truce, not a firing step.</summary>
    public bool InPort { get; init; }

    /// <summary>
    /// The spawn shield cannot be spent on the first shot. It stops a ship being hit, so letting
    /// it shoot would make the ten seconds after a respawn a free volley.
    /// </summary>
    public bool SpawnShielded { get; init; }

    public bool IsChanneling { get; init; }

    /// <summary>
    /// The tick her guns come back after a boarding. Zero for a ship nobody has grappled, which
    /// is every ship most of the time, so the check costs one comparison against a field the row
    /// already carries.
    /// </summary>
    public ulong SilencedUntilTick { get; init; }

    public uint ReadyVolleys { get; init; }
    public ulong CurrentTick { get; init; }

    /// <summary>
    /// False until the ship has ever fired, because tick 0 is the world's construction tick and
    /// cannot be told apart from "no shot yet" by <see cref="LastShotTick"/> alone.
    /// </summary>
    public bool HasFired { get; init; }

    public ulong LastShotTick { get; init; }
    public float SourceX { get; init; }
    public float SourceY { get; init; }
    public float TargetX { get; init; }
    public float TargetY { get; init; }

    /// <summary>
    /// How far this volley reaches, in squares: the gun's rating with her fit's
    /// bonus capped in and the shot's own multiplier already applied.
    /// </summary>
    public float RangeSquares { get; init; }
}

/// <summary>One ship's magazine: volleys ready to fire, and progress towards the next one.</summary>
public readonly record struct MagazineState(uint ReadyVolleys, uint ReloadProgressTicks);

public static class CombatRules
{
    /// <summary>1.0 s at the play tick rate. The floor between two volleys, magazine or not.</summary>
    public const uint FireIntervalTicks = WorldRules.TickRateHz;

    /// <summary>15 s without firing or being fired at refills the magazine outright.</summary>
    public const uint IdleRefillTicks = WorldRules.TickRateHz * 15;

    /// <summary>
    /// How long a fired volley stays subscribable. Damage already landed; the row exists only so
    /// a client that is mid-frame still has something to draw the shot from.
    /// </summary>
    public const uint VolleyDisplayTicks = WorldRules.TickRateHz;

    /// <summary>Math section 5.1: within 45 degrees of the target's heading is its bow.</summary>
    public const float FrontArcHalfDegrees = 45f;

    /// <summary>Math section 5.1: 135 degrees or more off the heading is its stern.</summary>
    public const float BackArcThresholdDegrees = 135f;

    public static FireRejection ValidateFire(FireRequest request)
    {
        if (!request.SourceAlive)
        {
            return FireRejection.SourceSunk;
        }

        if (!request.TargetSelected)
        {
            return FireRejection.NoTarget;
        }

        if (!request.TargetAlive)
        {
            return FireRejection.TargetSunk;
        }

        if (request.IsChanneling)
        {
            return FireRejection.Busy;
        }

        if (request.InPort)
        {
            return FireRejection.InPort;
        }

        if (request.SpawnShielded)
        {
            return FireRejection.SpawnShielded;
        }

        if (request.CurrentTick < request.SilencedUntilTick)
        {
            return FireRejection.Silenced;
        }

        if (request.ReadyVolleys == 0)
        {
            return FireRejection.Reloading;
        }

        if (request.HasFired &&
            request.CurrentTick < checked(request.LastShotTick + FireIntervalTicks))
        {
            return FireRejection.FiringTooFast;
        }

        // SEA_5 7.2's half-square of grace, which only RangeRules knows about. The
        // check used to be a bare circle test and lost every shot at the edge.
        return RangeRules.IsWithinRange(
            GeometryRules.Distance(
                request.SourceX,
                request.SourceY,
                request.TargetX,
                request.TargetY),
            request.RangeSquares)
            ? FireRejection.None
            : FireRejection.OutOfRange;
    }

    /// <summary>
    /// Which face a shot lands on, from the angle between the defender's heading and the
    /// bearing to whoever fired. There is no firing arc left in the model, so this is the only
    /// geometry a volley needs beyond range.
    /// </summary>
    /// <remarks>
    /// <paramref name="bearingToAttackerDegrees"/> must come from
    /// <see cref="GeometryRules.HeadingTo"/> (defender position to attacker position, the
    /// defender's own heading as the fallback), because a defender's heading is a compass
    /// bearing and the two have to be measured off the same compass. This method takes the
    /// bearing rather than the two positions so a boundary case can be pinned exactly: placing a
    /// fixture with <see cref="GeometryRules.Direction"/> and reading the bearing back through
    /// <see cref="GeometryRules.HeadingTo"/> cannot land on 45.01 degrees, because
    /// <c>Direction</c> reads a table sampled every quarter degree.
    /// A shot from inside the defender's own hull has no bearing; <c>HeadingTo</c> falls back to
    /// her own heading there, so a volley at nought range lands on the bow.
    /// </remarks>
    public static ArmorFace FaceHit(float defenderHeadingDegrees, float bearingToAttackerDegrees)
    {
        var offset = MathF.Abs(GeometryRules.NormalizeSignedAngle(
            bearingToAttackerDegrees - defenderHeadingDegrees));

        if (offset <= FrontArcHalfDegrees)
        {
            return ArmorFace.Front;
        }

        return offset >= BackArcThresholdDegrees ? ArmorFace.Back : ArmorFace.Sides;
    }

    public static float ArmorOn(ArmorFace face, float front, float sides, float back) => face switch
    {
        ArmorFace.Front => front,
        ArmorFace.Back => back,
        _ => sides,
    };

    /// <summary>
    /// Math section 5.2: <c>floor(VolleyDamage x ammo multiplier x (1 - armor_face))</c>. The
    /// armour face is already capped when the stat sheet is derived; clamping again here keeps a
    /// hand-written NPC row from ever healing its target.
    /// </summary>
    public static uint ResolveDamage(uint volleyDamage, float ammoDamageMultiplier, float armorFace)
    {
        if (!float.IsFinite(ammoDamageMultiplier) || ammoDamageMultiplier < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(ammoDamageMultiplier));
        }

        var armor = float.IsFinite(armorFace) ? Math.Clamp(armorFace, 0f, 1f) : 0f;
        var damage = volleyDamage * ammoDamageMultiplier * (1f - armor);
        return damage <= 0f ? 0u : (uint)MathF.Floor(damage);
    }

    /// <summary>
    /// Advances one ship's magazine by a single tick. Reload runs whether or not the ship fired,
    /// so a magazine that is already full still snaps a volley back the tick after one leaves.
    /// <paramref name="reloadTicks"/> arrives already scaled by any reload effect.
    /// </summary>
    public static MagazineState Advance(
        MagazineState state,
        uint magazineSize,
        uint reloadTicks,
        ulong ticksSinceCombat)
    {
        ArgumentOutOfRangeException.ThrowIfZero(magazineSize);
        ArgumentOutOfRangeException.ThrowIfZero(reloadTicks);

        if (ticksSinceCombat >= IdleRefillTicks)
        {
            return new MagazineState(magazineSize, 0);
        }

        if (state.ReadyVolleys >= magazineSize)
        {
            return new MagazineState(magazineSize, 0);
        }

        var progress = checked(state.ReloadProgressTicks + 1);
        return progress < reloadTicks
            ? new MagazineState(state.ReadyVolleys, progress)
            : new MagazineState(checked(state.ReadyVolleys + 1), 0);
    }

    /// <summary>Spends one ready volley and restarts the reload behind it.</summary>
    public static MagazineState Spend(MagazineState state)
    {
        if (state.ReadyVolleys == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                state.ReadyVolleys,
                "An empty magazine has no volley to spend.");
        }

        return new MagazineState(state.ReadyVolleys - 1, 0);
    }

    /// <summary>
    /// The stat sheet carries reload in milliseconds because that is the number the dock shows;
    /// the tick loop counts ticks. Rounding up keeps a fast cannon from reloading in no time at
    /// all, which <see cref="Advance"/> rejects outright.
    /// </summary>
    public static uint ReloadTicks(uint reloadMilliseconds) => (uint)Math.Max(
        1UL,
        ((ulong)reloadMilliseconds * WorldRules.TickRateHz + 999UL) / 1000UL);

    public static float Distance(float sourceX, float sourceY, float targetX, float targetY)
    {
        var deltaX = targetX - sourceX;
        var deltaY = targetY - sourceY;
        return MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }
}
