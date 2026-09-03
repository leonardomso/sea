namespace Sea.Server;

public readonly record struct TacticalStatusState(
    bool IsActive,
    uint Stacks,
    ulong ExpiresAtTick,
    ulong ImmunityUntilTick);

public readonly record struct StatusApplication(bool Applied, TacticalStatusState State);

public enum AbilityRejection
{
    None,
    SourceSunk,
    UnknownAbility,
    Cooldown,
    Busy,
}

public readonly record struct AbilityRequest(
    bool SourceAlive,
    bool AbilityKnown,
    bool IsIdle,
    ulong CurrentTick,
    ulong ReadyAtTick);

public enum RepairRejection
{
    None,
    SourceSunk,
    Busy,
    NoRepairKit,
    NothingToRepair,
}

public readonly record struct RepairRequest(
    bool SourceAlive,
    bool IsIdle,
    bool HasRepairKit,
    bool IsDamaged);

public enum BoardingRejection
{
    None,
    SourceSunk,
    TargetSunk,
    Busy,
    TargetTooStrong,
    OutOfRange,
    Cooldown,
}

public readonly record struct BoardingRequest(
    bool SourceAlive,
    bool TargetAlive,
    bool IsIdle,
    uint TargetHull,
    uint TargetMaxHull,
    float Distance,
    ulong CurrentTick,
    ulong ReadyAtTick);

public readonly record struct TacticalModifiers(
    float MaximumSpeed,
    float Acceleration,
    float TurnRate,
    float WeaponEffectiveness);

public readonly record struct HazardPosition(float X, float Y);

public static class TacticalRules
{
    public const uint StatusImmunityTicks = 20;
    public const uint StatusDurationTicks = 50;
    public const uint RepairDurationTicks = 50;
    public const uint BoardingDurationTicks = 30;
    public const uint BoardingCooldownTicks = 100;
    public const uint BoardingFatigueTicks = 50;
    public const float BoardingRange = 8f;
    public const float SmokeCloseRange = 20f;

    public static StatusApplication ApplyStatus(
        TacticalStatusState existing,
        ulong currentTick,
        uint durationTicks,
        uint maximumStacks)
    {
        ArgumentOutOfRangeException.ThrowIfZero(durationTicks);
        ArgumentOutOfRangeException.ThrowIfZero(maximumStacks);

        if (!existing.IsActive && currentTick < existing.ImmunityUntilTick)
        {
            return new StatusApplication(false, existing);
        }

        var stacks = existing.IsActive
            ? Math.Min(maximumStacks, existing.Stacks + 1)
            : 1u;
        return new StatusApplication(
            true,
            new TacticalStatusState(
                true,
                stacks,
                checked(currentTick + durationTicks),
                existing.ImmunityUntilTick));
    }

    public static TacticalStatusState ExpireStatus(
        TacticalStatusState existing,
        ulong currentTick,
        uint immunityTicks)
    {
        if (!existing.IsActive || currentTick < existing.ExpiresAtTick)
        {
            return existing;
        }

        return new TacticalStatusState(
            false,
            0,
            existing.ExpiresAtTick,
            checked(currentTick + immunityTicks));
    }

    public static AbilityRejection ValidateAbility(AbilityRequest request)
    {
        if (!request.SourceAlive)
        {
            return AbilityRejection.SourceSunk;
        }

        if (!request.AbilityKnown)
        {
            return AbilityRejection.UnknownAbility;
        }

        if (request.CurrentTick < request.ReadyAtTick)
        {
            return AbilityRejection.Cooldown;
        }

        return request.IsIdle ? AbilityRejection.None : AbilityRejection.Busy;
    }

    public static TacticalModifiers MovementModifiers(
        bool fullSail,
        uint slowedStacks,
        bool sailsDisabled,
        float sailIntegrity,
        bool inShoal,
        bool inStorm,
        bool repairing)
    {
        var maximumSpeed = fullSail ? 1.35f : 1f;
        maximumSpeed *= MathF.Max(0.4f, 1f - 0.2f * slowedStacks);
        var handling = 0.5f + 0.5f * Math.Clamp(sailIntegrity, 0f, 1f);
        maximumSpeed *= handling;

        if (inShoal)
        {
            maximumSpeed *= 0.65f;
        }

        if (repairing)
        {
            maximumSpeed *= 0.5f;
        }

        return new TacticalModifiers(
            maximumSpeed,
            sailsDisabled ? 0f : (fullSail ? 1.35f : 1f) * handling,
            (inStorm ? 0.65f : 1f) * handling,
            inStorm ? 0.75f : 1f);
    }

    public static uint AdjustedReloadTicks(
        uint baseTicks,
        uint cannons,
        uint maximumCannons)
    {
        ArgumentOutOfRangeException.ThrowIfZero(baseTicks);
        ArgumentOutOfRangeException.ThrowIfZero(maximumCannons);

        var integrity = Math.Clamp((float)cannons / maximumCannons, 1f / 3f, 1f);
        return checked((uint)MathF.Ceiling(baseTicks / integrity));
    }

    public static uint ApplyIncomingDamage(uint damage, bool braceActive)
    {
        if (!braceActive || damage == 0)
        {
            return damage;
        }

        return (uint)MathF.Round(damage * 0.6f, MidpointRounding.AwayFromZero);
    }

    public static uint PeriodicStatusDamage(string statusType, uint stacks, ulong currentTick)
    {
        if (currentTick % WorldRules.TickRateHz != 0)
        {
            return 0;
        }

        return statusType switch
        {
            "burning" => checked(stacks * 2),
            "flooding" => stacks,
            _ => 0,
        };
    }

    public static uint PeriodicStatusDamage(StatusCode status, uint stacks)
    {
        return status switch
        {
            StatusCode.Burning => checked(stacks * 2),
            StatusCode.Flooding => stacks,
            _ => 0,
        };
    }

    public static bool CanAcquireTarget(bool smokeActive, float distance) =>
        !smokeActive || distance <= SmokeCloseRange;

    public static RepairRejection ValidateRepair(RepairRequest request)
    {
        if (!request.SourceAlive)
        {
            return RepairRejection.SourceSunk;
        }

        if (!request.IsIdle)
        {
            return RepairRejection.Busy;
        }

        if (!request.HasRepairKit)
        {
            return RepairRejection.NoRepairKit;
        }

        return request.IsDamaged ? RepairRejection.None : RepairRejection.NothingToRepair;
    }

    public static uint ProgressiveRestore(
        uint initial,
        uint maximum,
        uint restoreAmount,
        ulong elapsedTicks,
        uint durationTicks)
    {
        ArgumentOutOfRangeException.ThrowIfZero(durationTicks);

        var progress = Math.Clamp((float)elapsedTicks / durationTicks, 0f, 1f);
        var restored = (uint)MathF.Round(
            restoreAmount * progress,
            MidpointRounding.AwayFromZero);
        return Math.Min(maximum, checked(initial + restored));
    }

    public static BoardingRejection ValidateBoarding(BoardingRequest request)
    {
        if (!request.SourceAlive)
        {
            return BoardingRejection.SourceSunk;
        }

        if (!request.TargetAlive)
        {
            return BoardingRejection.TargetSunk;
        }

        if (!request.IsIdle)
        {
            return BoardingRejection.Busy;
        }

        if (request.TargetMaxHull == 0 ||
            request.TargetHull * 4u >= request.TargetMaxHull)
        {
            return BoardingRejection.TargetTooStrong;
        }

        if (!float.IsFinite(request.Distance) || request.Distance > BoardingRange)
        {
            return BoardingRejection.OutOfRange;
        }

        return request.CurrentTick >= request.ReadyAtTick
            ? BoardingRejection.None
            : BoardingRejection.Cooldown;
    }

    public static bool BoardingSucceeds(
        uint attackerCrew,
        uint defenderCrew,
        bool fatigued)
    {
        var effectivePower = fatigued ? attackerCrew * 0.6f : attackerCrew;
        return effectivePower >= defenderCrew;
    }

    public static HazardPosition MoveStorm(
        float x,
        float y,
        float directionDegrees,
        float speed,
        float deltaSeconds)
    {
        var radians = directionDegrees * MathF.PI / 180f;
        var nextX = x + MathF.Sin(radians) * speed * deltaSeconds;
        var nextY = y + MathF.Cos(radians) * speed * deltaSeconds;
        return new HazardPosition(WrapMapCoordinate(nextX), WrapMapCoordinate(nextY));
    }

    /// <summary>
    /// Rolls a percentage chance from a deterministic seed with the splitmix64 finalizer,
    /// so the same volley always resolves the same way. A chance of 0 never applies and
    /// a chance of 100 or more always does, because the roll is a residue in [0, 100).
    /// </summary>
    public static bool ShouldApplyStatus(ulong deterministicSeed, uint chancePercent)
    {
        var mixed = XorShift(deterministicSeed, 30);
        mixed *= 0xbf58476d1ce4e5b9UL;
        mixed = XorShift(mixed, 27);
        mixed *= 0x94d049bb133111ebUL;
        mixed = XorShift(mixed, 31);
        return mixed % 100 < chancePercent;
    }

    private static ulong XorShift(ulong value, int shift)
    {
        // Stryker disable once Bitwise : `>>>` and `>>` are the same shift on an unsigned operand.
        var shifted = value >> shift;
        return value ^ shifted;
    }

    private static float WrapMapCoordinate(float value)
    {
        var span = WorldRules.MapMax - WorldRules.MapMin;
        while (value > WorldRules.MapMax)
        {
            value -= span;
        }

        while (value < WorldRules.MapMin)
        {
            value += span;
        }

        return value;
    }
}
