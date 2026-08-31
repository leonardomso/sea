namespace Sea.Server;

using System.Collections.Generic;

public enum PlayerLoadSource
{
    ClientLifecycle,
    ExplicitLoad,
}

public static class PlayerConnectionRules
{
    public static bool MayCreatePlayer(PlayerLoadSource source) =>
        source == PlayerLoadSource.ExplicitLoad;
}

public static class WorldRules
{
    public const float MapMin = -100f;
    public const float MapMax = 100f;
    public const uint InitialHealth = 100;
    public const uint InitialGold = 0;
    public const uint TickRateHz = 10;
    public const float CollisionPadding = 0.5f;
    public const uint InitialCannonDamage = 25;
    public const uint InitialCannonCooldownTicks = 20;
    public const uint EnemyInitialHealth = 100;
    public const uint EnemyCannonDamage = 5;
    public const uint EnemyCannonCooldownTicks = 40;
    public const uint EnemyGoldReward = 100;
    public const float CannonRange = 60f;
    public const uint InitialProgressionLevel = 1;
    public const uint InitialCannonUpgradeLevel = 0;
    public const uint CannonUpgradeBaseCost = 100;
    public const uint CannonUpgradeCostStep = 100;
    public const uint CannonDamagePerUpgrade = 5;
    public const float PlayerShipSpeed = 12f;
    public const float PlayerShipTurnRateDegrees = 360f;

    public readonly struct SailingStep
    {
        public SailingStep(float x, float y, bool arrived)
        {
            X = x;
            Y = y;
            Arrived = arrived;
        }

        public float X { get; }
        public float Y { get; }
        public bool Arrived { get; }
    }

    public static bool IsInsideMap(float x, float y) =>
        float.IsFinite(x) &&
        float.IsFinite(y) &&
        x >= MapMin &&
        x <= MapMax &&
        y >= MapMin &&
        y <= MapMax;

    public static bool IsValidMove(float x, float y) => IsInsideMap(x, y);

    public static SailingStep AdvanceTowards(
        float currentX,
        float currentY,
        float destinationX,
        float destinationY,
        float maximumDistance)
    {
        if (!float.IsFinite(maximumDistance) || maximumDistance <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDistance));
        }

        var deltaX = destinationX - currentX;
        var deltaY = destinationY - currentY;
        var distance = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        if (distance <= maximumDistance)
        {
            return new SailingStep(destinationX, destinationY, true);
        }

        var scale = maximumDistance / distance;
        return new SailingStep(currentX + deltaX * scale, currentY + deltaY * scale, false);
    }

    public static bool IsBlocked(string kind, float entityX, float entityY, float radius, float x, float y)
    {
        if (kind != "island" && kind != "reef")
        {
            return false;
        }

        var dx = x - entityX;
        var dy = y - entityY;
        var collisionRadius = radius + CollisionPadding;
        return dx * dx + dy * dy < collisionRadius * collisionRadius;
    }

    public static bool IsInRange(float sourceX, float sourceY, float targetX, float targetY, float range)
    {
        var dx = targetX - sourceX;
        var dy = targetY - sourceY;
        return dx * dx + dy * dy <= range * range;
    }

    public static uint ApplyDamage(uint health, uint damage) => damage >= health ? 0 : health - damage;

    public static uint CannonUpgradeCost(uint upgradeLevel) =>
        checked(CannonUpgradeBaseCost + upgradeLevel * CannonUpgradeCostStep);

    public static uint CannonDamageAfterUpgrade(uint damage, uint upgradeLevel) =>
        checked(damage + CannonDamagePerUpgrade * upgradeLevel);
}

public static class SpatialRules
{
    public const float ChunkSize = 25f;
    public const int ChunkCountPerAxis = 8;

    public static int ChunkCoordinate(float position)
    {
        if (!float.IsFinite(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        var coordinate = (int)MathF.Floor((position - WorldRules.MapMin) / ChunkSize);
        return Math.Clamp(coordinate, 0, ChunkCountPerAxis - 1);
    }
}

public static class EventRetentionRules
{
    public const ulong LifetimeTicks = 100;

    public static bool IsExpired(ulong expiresAtTick, ulong currentTick) =>
        currentTick > expiresAtTick;
}

public sealed class AmmunitionContent
{
    public required string Id { get; init; }
    public required uint HullDamage { get; init; }
    public required uint SailDamage { get; init; }
    public required uint CannonDamage { get; init; }
    public required uint CrewDamage { get; init; }
    public required float RangeMultiplier { get; init; }
    public required string AppliedStatus { get; init; }
}

public sealed class AbilityContent
{
    public required string Id { get; init; }
    public required uint CooldownTicks { get; init; }
    public required uint DurationTicks { get; init; }
}

public sealed class NpcContent
{
    public required string Id { get; init; }
    public required float AggroRange { get; init; }
    public required float DesiredRange { get; init; }
    public required uint Hull { get; init; }
}

public sealed class CombatContent
{
    public required IReadOnlyList<AmmunitionContent> Ammunition { get; init; }
    public required IReadOnlyList<AbilityContent> Abilities { get; init; }
    public required IReadOnlyList<NpcContent> Npcs { get; init; }
}

public static class ContentCatalog
{
    public static CombatContent CreateDefault() => new()
    {
        Ammunition = new AmmunitionContent[]
        {
            new() { Id = "round", HullDamage = 25, SailDamage = 5, CannonDamage = 5, CrewDamage = 2, RangeMultiplier = 1f, AppliedStatus = "flooding" },
            new() { Id = "chain", HullDamage = 5, SailDamage = 28, CannonDamage = 2, CrewDamage = 2, RangeMultiplier = 0.9f, AppliedStatus = "slowed" },
            new() { Id = "grapeshot", HullDamage = 4, SailDamage = 3, CannonDamage = 4, CrewDamage = 30, RangeMultiplier = 0.55f, AppliedStatus = "none" },
            new() { Id = "incendiary", HullDamage = 14, SailDamage = 8, CannonDamage = 8, CrewDamage = 5, RangeMultiplier = 0.85f, AppliedStatus = "burning" },
        },
        Abilities = new AbilityContent[]
        {
            new() { Id = "full_sail", CooldownTicks = 200, DurationTicks = 50 },
            new() { Id = "brace", CooldownTicks = 180, DurationTicks = 40 },
            new() { Id = "emergency_pump", CooldownTicks = 300, DurationTicks = 50 },
            new() { Id = "smoke_screen", CooldownTicks = 240, DurationTicks = 40 },
        },
        Npcs = new NpcContent[]
        {
            new() { Id = "patrol", AggroRange = 0f, DesiredRange = 45f, Hull = 100 },
            new() { Id = "raider", AggroRange = 65f, DesiredRange = 18f, Hull = 90 },
            new() { Id = "gunship", AggroRange = 75f, DesiredRange = 48f, Hull = 130 },
        },
    };

    public static IReadOnlyList<string> Validate(CombatContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var errors = new List<string>();
        ValidateIds(content.Ammunition.Select(item => item.Id), "ammunition", errors);
        ValidateIds(content.Abilities.Select(item => item.Id), "ability", errors);
        ValidateIds(content.Npcs.Select(item => item.Id), "npc", errors);

        foreach (var ammunition in content.Ammunition)
        {
            if (ammunition.RangeMultiplier <= 0f || !float.IsFinite(ammunition.RangeMultiplier))
            {
                errors.Add($"Ammunition '{ammunition.Id}' has an invalid range multiplier.");
            }
        }

        foreach (var ability in content.Abilities)
        {
            if (ability.CooldownTicks == 0 || ability.DurationTicks == 0)
            {
                errors.Add($"Ability '{ability.Id}' must have positive timing values.");
            }
        }

        return errors;
    }

    private static void ValidateIds(
        IEnumerable<string> ids,
        string kind,
        ICollection<string> errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add($"A {kind} id is empty.");
            }
            else if (!seen.Add(id))
            {
                errors.Add($"Duplicate {kind} id '{id}'.");
            }
        }
    }
}
