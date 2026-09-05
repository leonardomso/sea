namespace Sea.Server;

public enum RepairRejection
{
    None,
    SourceSunk,
    Busy,
    OnCooldown,
    NoRepairKit,
    NothingToRepair,
}

/// <summary>
/// What admission knows about a ship asking to mend itself. The channel is free, so
/// <see cref="HasRepairKit"/> only gates the kit; the kit is instant, so <see cref="IsIdle"/>
/// only gates the channel.
/// </summary>
public readonly record struct RepairRequest(
    bool SourceAlive,
    bool IsIdle,
    bool IsReady,
    bool HasRepairKit,
    bool IsDamaged);

/// <summary>
/// Repairing out of combat, and the price of repairing in it. The channel heals nothing until it
/// finishes, every heal makes the next one smaller for a minute, and enough damage taken while
/// the crew is at the pumps ends the attempt with the cooldown still owed.
/// </summary>
public static class RepairRules
{
    /// <summary>Each heal already inside the window multiplies the next one by this.</summary>
    public const float FatigueFactor = 0.6f;

    /// <summary>Mirrors <c>stat_caps.repairFatigueWindowSeconds</c>.</summary>
    public const ulong FatigueWindowTicks = 60 * WorldRules.TickRateHz;

    /// <summary>Mirrors <c>stat_caps.kitHealAmount</c>. The channel's share rides on the sheet.</summary>
    public const float KitAmount = 0.25f;

    /// <summary>Damage taken during a channel that breaks it, as a share of maximum hull.</summary>
    public const float CancelThreshold = 0.15f;

    public const ulong CooldownTicks = 15 * WorldRules.TickRateHz;
    public const ulong KitCooldownTicks = 45 * WorldRules.TickRateHz;

    public static float Fatigue(int healsInWindow) =>
        healsInWindow <= 0 ? 1f : MathF.Pow(FatigueFactor, healsInWindow);

    /// <summary>
    /// Heal = floor(MaxHP x amount x fatigue x burn). Floor, not round: a heal is never allowed
    /// to be worth more than the fraction the sheet promises.
    /// </summary>
    public static uint Heal(uint maximumHull, float amount, int healsInWindow, bool burning)
    {
        if (maximumHull == 0 || amount <= 0f)
        {
            return 0;
        }

        var healed = maximumHull * amount * Fatigue(healsInWindow) *
            (burning ? EffectRules.BurnHealMultiplier : 1f);
        return healed <= 0f ? 0u : (uint)MathF.Floor(healed);
    }

    public static uint Restore(uint hull, uint maximumHull, uint healed) =>
        Math.Min(maximumHull, hull + Math.Min(healed, maximumHull));

    /// <summary>
    /// The hit points that break a channel. Rounded up and never zero, so a ship with a hull too
    /// small to hold fifteen percent of itself still has a threshold a shot can cross. The epsilon
    /// keeps a share that lands exactly on a whole number -- fifteen of a hundred -- off the next
    /// one up, where the float that only approximates the fraction would otherwise put it.
    /// </summary>
    public static uint CancelDamage(uint maximumHull) =>
        Math.Max(1u, (uint)MathF.Ceiling(maximumHull * CancelThreshold - 0.001f));

    public static bool ShouldCancel(uint damageTaken, uint maximumHull, bool fireShotHit) =>
        fireShotHit || damageTaken >= CancelDamage(maximumHull);

    /// <summary>A heal still counts against the next one until its window closes.</summary>
    public static bool IsInFatigueWindow(ulong completedAtTick, ulong tick) =>
        tick < completedAtTick + FatigueWindowTicks;

    public static uint ChannelTicks(uint channelMilliseconds) =>
        Math.Max(1u, (channelMilliseconds * WorldRules.TickRateHz + 999u) / 1000u);

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

        if (!request.IsReady)
        {
            return RepairRejection.OnCooldown;
        }

        return request.IsDamaged ? RepairRejection.None : RepairRejection.NothingToRepair;
    }

    /// <summary>
    /// The kit runs on its own cooldown and cannot be interrupted, so it is the one heal a ship
    /// already channelling, or already under fire, can still count on.
    /// </summary>
    public static RepairRejection ValidateKit(RepairRequest request)
    {
        if (!request.SourceAlive)
        {
            return RepairRejection.SourceSunk;
        }

        if (!request.IsReady)
        {
            return RepairRejection.OnCooldown;
        }

        if (!request.HasRepairKit)
        {
            return RepairRejection.NoRepairKit;
        }

        return request.IsDamaged ? RepairRejection.None : RepairRejection.NothingToRepair;
    }
}
