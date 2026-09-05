namespace Sea.Server;

/// <summary>
/// The effects a volley can leave on a ship. One row per code per ship: the same code refreshes
/// its expiry, different codes stack.
/// </summary>
public enum EffectCode : byte
{
    None = 0,
    Slowed = 1,
    Burning = 2,
    ReloadSlowed = 3,
}

public readonly record struct EffectApplication(
    EffectCode Code,
    float Magnitude,
    ulong ExpiresAtTick,
    ulong NextProcessTick);

public static class EffectRules
{
    /// <summary>A burning ship heals at half rate. Read by 1c's repair channel.</summary>
    public const float BurnHealMultiplier = 0.5f;

    public static EffectCode From(AmmoEffectCode effect) => effect switch
    {
        AmmoEffectCode.Slow => EffectCode.Slowed,
        AmmoEffectCode.Burn => EffectCode.Burning,
        AmmoEffectCode.SlowReload => EffectCode.ReloadSlowed,
        _ => EffectCode.None,
    };

    /// <summary>
    /// Whether a volley that landed this far off leaves its after-effect behind. A limit of
    /// zero is no limit at all; a limit that is set is inclusive, and it is in squares --
    /// the same unit as the distance, which is the whole point of the check.
    /// </summary>
    /// <remarks>
    /// A distance that is not a number counts as outside every limit. It gets here only from
    /// a corrupt row, and dropping the effect is the safe answer; both <c>&gt;</c> and
    /// <c>&lt;=</c> would be false against NaN, so the test is written out rather than left
    /// to a comparison whose answer depends on which way round it is spelled.
    /// </remarks>
    public static bool AppliesAtRange(AmmunitionContent ammunition, float distanceSquares)
    {
        ArgumentNullException.ThrowIfNull(ammunition);

        return ammunition.RangeLimitSquares == 0 ||
            (float.IsFinite(distanceSquares) && distanceSquares <= ammunition.RangeLimitSquares);
    }

    /// <summary>
    /// The effect a volley of this ammunition leaves, if any. Grape Shot carries a range limit:
    /// beyond it the volley still lands, it just leaves nothing behind.
    /// </summary>
    public static bool TryResolve(
        AmmunitionContent ammunition,
        float distance,
        ulong currentTick,
        out EffectApplication application)
    {
        ArgumentNullException.ThrowIfNull(ammunition);

        application = default;
        var code = From(ammunition.Effect);
        if (code == EffectCode.None ||
            ammunition.EffectDurationSeconds <= 0f ||
            ammunition.EffectMagnitude <= 0f)
        {
            return false;
        }

        if (!AppliesAtRange(ammunition, distance))
        {
            return false;
        }

        var duration = DurationTicks(ammunition.EffectDurationSeconds);
        application = new EffectApplication(
            code,
            ammunition.EffectMagnitude,
            checked(currentTick + duration),
            NextProcessTick(code, currentTick, duration));
        return true;
    }

    public static uint DurationTicks(float seconds) => seconds <= 0f
        ? 0u
        : Math.Max(1u, (uint)MathF.Ceiling(seconds * WorldRules.TickRateHz));

    /// <summary>
    /// Refreshing an effect takes the later of the two expiries, so a weaker late hit can never
    /// cut a running effect short.
    /// </summary>
    public static ulong Refresh(ulong existingExpiry, ulong incomingExpiry) =>
        Math.Max(existingExpiry, incomingExpiry);

    public static bool HasExpired(ulong expiresAtTick, ulong currentTick) =>
        currentTick >= expiresAtTick;

    /// <summary>Grape Shot adds its magnitude to the reload time rather than scaling it down.</summary>
    public static uint ReloadTicks(uint baseTicks, bool reloadSlowed, float magnitude)
    {
        ArgumentOutOfRangeException.ThrowIfZero(baseTicks);

        if (!reloadSlowed || magnitude <= 0f)
        {
            return baseTicks;
        }

        return checked((uint)MathF.Ceiling(baseTicks * (1f + magnitude)));
    }

    /// <summary>
    /// Fire Shot burns a fraction of the target's maximum hit points every second, so the tick
    /// that carries the damage is one in every <see cref="WorldRules.TickRateHz"/>. Rounding away
    /// from zero keeps a small hull from burning for nothing.
    /// </summary>
    public static uint BurnDamage(uint maxHitPoints, float magnitudePerSecond)
    {
        if (magnitudePerSecond <= 0f || maxHitPoints == 0)
        {
            return 0;
        }

        return (uint)MathF.Round(
            maxHitPoints * magnitudePerSecond,
            MidpointRounding.AwayFromZero);
    }

    public static float HealingMultiplier(bool burning) => burning ? BurnHealMultiplier : 1f;

    private static ulong NextProcessTick(EffectCode code, ulong currentTick, uint durationTicks)
    {
        // Only Burning does anything between application and expiry; the others simply run out.
        var step = code == EffectCode.Burning
            ? Math.Min((ulong)WorldRules.TickRateHz, durationTicks)
            : durationTicks;
        return checked(currentTick + step);
    }
}
