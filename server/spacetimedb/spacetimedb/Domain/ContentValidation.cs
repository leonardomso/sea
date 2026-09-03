using System.Globalization;

namespace Sea.Server;

public static partial class ContentCatalog
{
    private const int TierCount = 6;

    public static IReadOnlyList<string> Validate(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var errors = new List<string>();
        ValidateStatCaps(content.StatCaps, errors);
        ValidateMaps(content.Maps, errors);
        ValidateHulls(content.Hulls, content.StatCaps, errors);
        ValidateCannons(content.Cannons, content.StatCaps, errors);
        ValidateAmmunition(content.Ammunition, errors);
        ValidateNpcs(content.Npcs, content.Maps, errors);
        return errors;
    }

    private static void ValidateStatCaps(StatCapsContent caps, List<string> errors)
    {
        const string subject = "StatCaps";

        Positive(subject, "damage bonus cap", caps.DamageBonusCap, errors);
        Positive(subject, "magazine bonus cap", caps.MagazineBonusCap, errors);
        Positive(subject, "hit point bonus cap", caps.HitPointBonusCap, errors);
        Positive(subject, "armor points cap", caps.ArmorPointsCap, errors);
        Positive(subject, "speed bonus cap", caps.SpeedBonusCap, errors);
        Positive(subject, "turn bonus cap", caps.TurnBonusCap, errors);
        Positive(subject, "range bonus cap", caps.RangeBonusCapSquares, errors);
        Positive(subject, "repair amount bonus cap", caps.RepairAmountBonusCap, errors);
        Positive(subject, "cannon slot bonus cap", caps.CannonSlotBonusCap, errors);

        UnitInterval(subject, "reload bonus cap", caps.ReloadBonusCap, errors);
        UnitInterval(subject, "repair channel bonus cap", caps.RepairChannelBonusCap, errors);
        UnitInterval(subject, "armor absolute max", caps.ArmorAbsoluteMax, errors);
        UnitInterval(subject, "repair base amount", caps.RepairBaseAmount, errors);

        Positive(subject, "combat power budget", caps.CombatPowerBudget, errors);
        Positive(subject, "combat power armor weight", caps.CombatPowerArmorWeight, errors);

        Positive(subject, "reload floor", caps.ReloadFloorSeconds, errors);
        Positive(subject, "fire minimum interval", caps.FireMinIntervalSeconds, errors);
        Positive(subject, "magazine refill idle seconds", caps.MagazineRefillIdleSeconds, errors);
        Positive(subject, "burn per second", caps.BurnPerSecond, errors);
        Positive(subject, "burn duration", caps.BurnDurationSeconds, errors);
        NotNegative(subject, "burn heal multiplier", caps.BurnHealMultiplier, errors);
        Positive(subject, "repair channel", caps.RepairChannelSeconds, errors);
        Positive(subject, "repair cooldown", caps.RepairCooldownSeconds, errors);
        Positive(subject, "repair fatigue", caps.RepairFatigue, errors);
        Positive(subject, "repair fatigue window", caps.RepairFatigueWindowSeconds, errors);
        Positive(subject, "repair cancel threshold", caps.RepairCancelThreshold, errors);
        Positive(subject, "kit heal amount", caps.KitHealAmount, errors);
        Positive(subject, "kit cooldown", caps.KitCooldownSeconds, errors);
        Positive(subject, "respawn seconds", caps.RespawnSeconds, errors);
        Positive(subject, "spawn shield seconds", caps.SpawnShieldSeconds, errors);

        ValidateTierTable(subject, "NPC hit point multipliers", caps.NpcHitPointMultipliers, errors);
        ValidateTierTable(subject, "NPC dps multipliers", caps.NpcDpsMultipliers, errors);
        ValidateTierTable(subject, "NPC armor by tier", caps.NpcArmorByTier, errors);
        for (var tier = 0; tier < caps.NpcArmorByTier.Count; tier++)
        {
            AtMost(
                subject,
                $"NPC armor for tier {tier + 1}",
                caps.NpcArmorByTier[tier],
                caps.ArmorAbsoluteMax,
                errors);
        }

        Positive(subject, "gold base", caps.GoldBase, errors);
        if (!(caps.GoldGrowth > 1f))
        {
            errors.Add($"{subject}: gold growth must be above 1.");
        }
    }

    private static void ValidateTierTable(
        string subject,
        string field,
        IReadOnlyList<float> values,
        List<string> errors)
    {
        if (values.Count != TierCount)
        {
            errors.Add($"{subject}: {field} must have {TierCount} entries.");
        }

        for (var tier = 0; tier < values.Count; tier++)
        {
            Positive(subject, $"{field} for tier {tier + 1}", values[tier], errors);
        }
    }

    private static void ValidateHulls(IReadOnlyList<HullContent> hulls, StatCapsContent caps, List<string> errors)
    {
        if (hulls.Count == 0)
        {
            errors.Add("At least one hull is required.");
        }

        var ids = new IdSet("hull");
        foreach (var hull in hulls)
        {
            ids.Add(hull.Id, errors);
            NotEmpty(hull.Id, "name", hull.Name, errors);
            Positive(hull.Id, "hit points", hull.HitPoints, errors);
            AtMost(hull.Id, "front armor", hull.ArmorFront, caps.ArmorAbsoluteMax, errors);
            AtMost(hull.Id, "side armor", hull.ArmorSides, caps.ArmorAbsoluteMax, errors);
            AtMost(hull.Id, "back armor", hull.ArmorBack, caps.ArmorAbsoluteMax, errors);
            Positive(hull.Id, "cannon slots", hull.CannonSlots, errors);
            Positive(hull.Id, "magazine", hull.Magazine, errors);
            Positive(hull.Id, "tier", hull.Tier, errors);
            Positive(hull.Id, "speed", hull.SpeedSquaresPerSecond, errors);
            Positive(hull.Id, "turn rate", hull.TurnDegreesPerSecond, errors);
            Positive(hull.Id, "map rank required", hull.MapRankRequired, errors);
        }
    }

    private static void ValidateCannons(IReadOnlyList<CannonContent> cannons, StatCapsContent caps, List<string> errors)
    {
        if (cannons.Count == 0)
        {
            errors.Add("At least one cannon is required.");
        }

        var ids = new IdSet("cannon");
        foreach (var cannon in cannons)
        {
            ids.Add(cannon.Id, errors);
            NotEmpty(cannon.Id, "name", cannon.Name, errors);
            Positive(cannon.Id, "damage", cannon.Damage, errors);
            Positive(cannon.Id, "range", cannon.RangeSquares, errors);
            Positive(cannon.Id, "tier", cannon.Tier, errors);

            if (!(cannon.ReloadSeconds >= caps.ReloadFloorSeconds))
            {
                errors.Add(
                    $"{cannon.Id}: reload {Format(cannon.ReloadSeconds)}s is below the floor {Format(caps.ReloadFloorSeconds)}s.");
            }
        }
    }

    private static void ValidateAmmunition(IReadOnlyList<AmmunitionContent> ammunition, List<string> errors)
    {
        if (ammunition.Count == 0)
        {
            errors.Add("At least one ammunition is required.");
        }

        var ids = new IdSet("ammunition");
        var codes = new CodeSet<AmmunitionCode>("ammunition");
        foreach (var ammo in ammunition)
        {
            ids.Add(ammo.Id, errors);
            NotEmpty(ammo.Id, "name", ammo.Name, errors);

            if (ammo.Code == AmmunitionCode.None)
            {
                errors.Add($"{ammo.Id}: ammunition code must not be None.");
            }
            else
            {
                codes.Add(ammo.Id, ammo.Code, errors);
                if (!string.Equals(ammo.Id, HotPathCodes.AmmunitionId(ammo.Code), StringComparison.Ordinal))
                {
                    errors.Add($"{ammo.Id}: code '{ammo.Code}' does not match the id.");
                }
            }

            Positive(ammo.Id, "damage multiplier", ammo.DamageMultiplier, errors);
            Positive(ammo.Id, "reload multiplier", ammo.ReloadMultiplier, errors);
            Positive(ammo.Id, "range multiplier", ammo.RangeMultiplier, errors);
            NotNegative(ammo.Id, "effect magnitude", ammo.EffectMagnitude, errors);
            NotNegative(ammo.Id, "effect duration", ammo.EffectDurationSeconds, errors);
        }

        if (!codes.Contains(AmmunitionCode.Round))
        {
            errors.Add("Ammunition must include the Round baseline.");
        }
    }

    private static void ValidateNpcs(IReadOnlyList<NpcContent> npcs, IReadOnlyList<MapContent> maps, List<string> errors)
    {
        if (npcs.Count == 0)
        {
            errors.Add("At least one npc is required.");
        }

        var ids = new IdSet("npc");
        var codes = new CodeSet<ShipArchetypeCode>("npc");
        var mapIds = new HashSet<byte>(maps.Select(map => map.MapId));
        foreach (var npc in npcs)
        {
            ids.Add(npc.Id, errors);
            NotEmpty(npc.Id, "name", npc.Name, errors);

            if (npc.Code == ShipArchetypeCode.PlayerSloop)
            {
                errors.Add($"{npc.Id}: npc code must not be PlayerSloop.");
            }
            else
            {
                codes.Add(npc.Id, npc.Code, errors);
                if (HotPathCodes.ShipArchetype(npc.Id) != npc.Code)
                {
                    errors.Add($"{npc.Id}: code '{npc.Code}' does not match the id.");
                }
            }

            if (!mapIds.Contains(npc.MapId))
            {
                errors.Add($"{npc.Id}: map {npc.MapId} does not exist.");
            }

            Positive(npc.Id, "tier", npc.Tier, errors);
            Positive(npc.Id, "maximum speed", npc.MaximumSpeed, errors);
            PositiveAtMost(npc.Id, "desired range", npc.DesiredRange, WorldRules.VisionRadius, errors);
            AtMost(npc.Id, "aggro range", npc.AggroRange, WorldRules.VisionRadius, errors);
            Positive(npc.Id, "hull", npc.Hull, errors);
            Positive(npc.Id, "cannon damage", npc.CannonDamage, errors);
            Positive(npc.Id, "gold reward", npc.GoldReward, errors);
            Positive(npc.Id, "experience reward", npc.ExperienceReward, errors);
        }
    }

    private sealed class IdSet(string kind, string noun = "id")
    {
        private readonly HashSet<string> seen = new(StringComparer.Ordinal);

        public bool Add(string id, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add($"{kind} {noun} is empty.");
                return false;
            }

            if (!seen.Add(id))
            {
                errors.Add($"Duplicate {kind} {noun} '{id}'.");
                return false;
            }

            return true;
        }
    }

    private sealed class CodeSet<TCode>(string kind)
        where TCode : struct, Enum
    {
        private readonly HashSet<TCode> seen = [];

        public void Add(string id, TCode code, List<string> errors)
        {
            if (!seen.Add(code))
            {
                errors.Add($"{id}: duplicate {kind} code '{code}'.");
            }
        }

        public bool Contains(TCode code) => seen.Contains(code);
    }

    private static void NotEmpty(string subject, string field, string value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{subject}: {field} is empty.");
        }
    }

    private static void Positive(string subject, string field, float value, List<string> errors)
    {
        if (!(value > 0f))
        {
            errors.Add($"{subject}: {field} must be positive.");
        }
    }

    private static void Positive(string subject, string field, ulong value, List<string> errors)
    {
        if (value == 0)
        {
            errors.Add($"{subject}: {field} must be positive.");
        }
    }

    private static void NotNegative(string subject, string field, float value, List<string> errors)
    {
        if (!(value >= 0f))
        {
            errors.Add($"{subject}: {field} must not be negative.");
        }
    }

    private static void UnitInterval(string subject, string field, float value, List<string> errors)
    {
        if (!(value > 0f && value < 1f))
        {
            errors.Add($"{subject}: {field} must be between 0 and 1.");
        }
    }

    private static void Between(string subject, string field, float value, float min, float max, List<string> errors)
    {
        if (!(value >= min && value <= max))
        {
            errors.Add($"{subject}: {field} must be between {Format(min)} and {Format(max)}.");
        }
    }

    /// <summary>
    /// Emits exactly one message: "must be positive" for a non-positive value, otherwise the
    /// upper-bound message. A plain <see cref="Between"/> would accept zero.
    /// </summary>
    private static void PositiveAtMost(string subject, string field, float value, float max, List<string> errors)
    {
        if (!(value > 0f))
        {
            errors.Add($"{subject}: {field} must be positive.");
            return;
        }

        AtMost(subject, field, value, max, errors);
    }

    private static void AtMost(string subject, string field, float value, float max, List<string> errors)
    {
        if (!(value >= 0f && value <= max))
        {
            errors.Add($"{subject}: {field} must be between 0 and {Format(max)}.");
        }
    }

    private static string Format(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
