namespace Sea.Server;

public enum BroadsideSide
{
    Port,
    Starboard,
}

public enum FireRejection
{
    None,
    SourceSunk,
    NoTarget,
    TargetSunk,
    CannonsDisabled,
    NoAmmunition,
    Reloading,
    OutOfRange,
    OutsideArc,
    Busy,
}

public enum WeakPoint
{
    Hull,
    Sails,
    Cannons,
}

public enum VolleyResolution
{
    Waiting,
    Impact,
    Harmless,
}

public readonly record struct CombatDamage(uint Hull, uint Sails, uint Cannons, uint Crew);

public readonly record struct FireRequest
{
    public bool SourceAlive { get; init; }
    public bool TargetSelected { get; init; }
    public bool TargetAlive { get; init; }
    public uint Cannons { get; init; }
    public uint Ammunition { get; init; }
    public ulong CurrentTick { get; init; }
    public ulong ReadyAtTick { get; init; }
    public float SourceX { get; init; }
    public float SourceY { get; init; }
    public float SourceHeadingDegrees { get; init; }
    public float TargetX { get; init; }
    public float TargetY { get; init; }
    public float MaximumRange { get; init; }
    public float RangeMultiplier { get; init; }
    public BroadsideSide Side { get; init; }
    public bool IsChanneling { get; init; }
}

public static class CombatRules
{
    public const float BroadsideArcDegrees = 100f;
    public const float WeakPointMultiplier = 1.25f;
    public const float ProjectileSpeed = 40f;

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

        if (request.Cannons == 0)
        {
            return FireRejection.CannonsDisabled;
        }

        if (request.IsChanneling)
        {
            return FireRejection.Busy;
        }

        if (request.Ammunition == 0)
        {
            return FireRejection.NoAmmunition;
        }

        if (request.CurrentTick < request.ReadyAtTick)
        {
            return FireRejection.Reloading;
        }

        var range = request.MaximumRange * request.RangeMultiplier;
        if (!WorldRules.IsInRange(
                request.SourceX,
                request.SourceY,
                request.TargetX,
                request.TargetY,
                range))
        {
            return FireRejection.OutOfRange;
        }

        return IsInsideBroadsideArc(
            request.SourceX,
            request.SourceY,
            request.SourceHeadingDegrees,
            request.TargetX,
            request.TargetY,
            request.Side)
            ? FireRejection.None
            : FireRejection.OutsideArc;
    }

    public static bool IsInsideBroadsideArc(
        float sourceX,
        float sourceY,
        float headingDegrees,
        float targetX,
        float targetY,
        BroadsideSide side)
    {
        var targetBearing = MathF.Atan2(targetX - sourceX, targetY - sourceY) *
            (180f / MathF.PI);
        var broadsideCenter = headingDegrees + (side == BroadsideSide.Port ? -90f : 90f);
        return MathF.Abs(NormalizeSignedAngle(targetBearing - broadsideCenter)) <=
            BroadsideArcDegrees * 0.5f + 0.0001f;
    }

    public static ulong VolleyTravelTicks(float distance, float projectileSpeed, uint tickRateHz)
    {
        if (!float.IsFinite(distance) || distance < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(distance));
        }

        if (!float.IsFinite(projectileSpeed) || projectileSpeed <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(projectileSpeed));
        }

        ArgumentOutOfRangeException.ThrowIfZero(tickRateHz);

        return Math.Max(1ul, (ulong)MathF.Ceiling(distance / projectileSpeed * tickRateHz));
    }

    public static VolleyResolution ResolveVolley(
        ulong impactAtTick,
        ulong currentTick,
        bool targetAlive)
    {
        if (currentTick < impactAtTick)
        {
            return VolleyResolution.Waiting;
        }

        return targetAlive ? VolleyResolution.Impact : VolleyResolution.Harmless;
    }

    public static CombatDamage DamageProfile(
        AmmunitionContent ammunition,
        WeakPoint weakPoint,
        uint cannonPower,
        uint cannons,
        uint maxCannons)
    {
        ArgumentNullException.ThrowIfNull(ammunition);
        ArgumentOutOfRangeException.ThrowIfZero(maxCannons);

        var effectiveness = (float)cannonPower / WorldRules.InitialCannonDamage *
            cannons / maxCannons;
        return new CombatDamage(
            ScaleDamage(ammunition.HullDamage, effectiveness,
                weakPoint == WeakPoint.Hull ? WeakPointMultiplier : 1f),
            ScaleDamage(ammunition.SailDamage, effectiveness,
                weakPoint == WeakPoint.Sails ? WeakPointMultiplier : 1f),
            ScaleDamage(ammunition.CannonDamage, effectiveness,
                weakPoint == WeakPoint.Cannons ? WeakPointMultiplier : 1f),
            ScaleDamage(ammunition.CrewDamage, effectiveness, 1f));
    }

    public static bool TryParseWeakPoint(string? value, out WeakPoint weakPoint) =>
        Enum.TryParse(value, ignoreCase: true, out weakPoint) &&
        Enum.IsDefined(weakPoint);

    public static float Distance(float sourceX, float sourceY, float targetX, float targetY)
    {
        var deltaX = targetX - sourceX;
        var deltaY = targetY - sourceY;
        return MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    private static uint ScaleDamage(uint value, float effectiveness, float aimMultiplier)
    {
        if (value == 0 || effectiveness <= 0f)
        {
            return 0;
        }

        return checked((uint)MathF.Round(
            value * effectiveness * aimMultiplier,
            MidpointRounding.AwayFromZero));
    }

    private static float NormalizeSignedAngle(float degrees)
    {
        var normalized = (degrees + 180f) % 360f;
        if (normalized < 0f)
        {
            normalized += 360f;
        }

        return normalized - 180f;
    }
}
