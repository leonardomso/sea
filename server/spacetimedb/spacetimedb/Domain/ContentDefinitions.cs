namespace Sea.Server;

public sealed class AmmunitionContent
{
    public required string Id { get; init; }
    public required AmmunitionCode Code { get; init; }
    public required uint HullDamage { get; init; }
    public required uint SailDamage { get; init; }
    public required uint CannonDamage { get; init; }
    public required uint CrewDamage { get; init; }
    public required float RangeMultiplier { get; init; }
    public required string AppliedStatus { get; init; }
    public required StatusCode AppliedStatusCode { get; init; }
}

public sealed class AbilityContent
{
    public required string Id { get; init; }
    public required AbilityCode Code { get; init; }
    public required uint CooldownTicks { get; init; }
    public required uint DurationTicks { get; init; }
}

public sealed class NpcContent
{
    public required string Id { get; init; }
    public required ShipArchetypeCode Code { get; init; }
    public required float AggroRange { get; init; }
    public required float DesiredRange { get; init; }
    public required float MaximumSpeed { get; init; }
    public required uint Hull { get; init; }
    public required uint CannonDamage { get; init; }
    public required AmmunitionCode PreferredAmmunition { get; init; }
    public required WeakPointCode PreferredWeakPoint { get; init; }
    public required uint GoldReward { get; init; }
    public required ulong ExperienceReward { get; init; }
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
            new() { Id = "round", Code = AmmunitionCode.Round, HullDamage = 25, SailDamage = 5, CannonDamage = 5, CrewDamage = 2, RangeMultiplier = 1f, AppliedStatus = "flooding", AppliedStatusCode = StatusCode.Flooding },
            new() { Id = "chain", Code = AmmunitionCode.Chain, HullDamage = 5, SailDamage = 28, CannonDamage = 2, CrewDamage = 2, RangeMultiplier = 0.9f, AppliedStatus = "slowed", AppliedStatusCode = StatusCode.Slowed },
            new() { Id = "grapeshot", Code = AmmunitionCode.Grapeshot, HullDamage = 4, SailDamage = 3, CannonDamage = 4, CrewDamage = 30, RangeMultiplier = 0.55f, AppliedStatus = "none", AppliedStatusCode = StatusCode.None },
            new() { Id = "incendiary", Code = AmmunitionCode.Incendiary, HullDamage = 14, SailDamage = 8, CannonDamage = 8, CrewDamage = 5, RangeMultiplier = 0.85f, AppliedStatus = "burning", AppliedStatusCode = StatusCode.Burning },
        },
        Abilities = new AbilityContent[]
        {
            new() { Id = "full_sail", Code = AbilityCode.FullSail, CooldownTicks = 200, DurationTicks = 50 },
            new() { Id = "brace", Code = AbilityCode.Brace, CooldownTicks = 180, DurationTicks = 40 },
            new() { Id = "emergency_pump", Code = AbilityCode.EmergencyPump, CooldownTicks = 300, DurationTicks = 50 },
            new() { Id = "smoke_screen", Code = AbilityCode.SmokeScreen, CooldownTicks = 240, DurationTicks = 40 },
        },
        Npcs = new NpcContent[]
        {
            new() { Id = "patrol", Code = ShipArchetypeCode.Patrol, AggroRange = 0f, DesiredRange = 45f, MaximumSpeed = 10f, Hull = 100, CannonDamage = 18, PreferredAmmunition = AmmunitionCode.Round, PreferredWeakPoint = WeakPointCode.Hull, GoldReward = 80, ExperienceReward = 100 },
            new() { Id = "raider", Code = ShipArchetypeCode.Raider, AggroRange = 65f, DesiredRange = 18f, MaximumSpeed = 14f, Hull = 90, CannonDamage = 20, PreferredAmmunition = AmmunitionCode.Chain, PreferredWeakPoint = WeakPointCode.Sails, GoldReward = 100, ExperienceReward = 125 },
            new() { Id = "gunship", Code = ShipArchetypeCode.Gunship, AggroRange = 75f, DesiredRange = 48f, MaximumSpeed = 9f, Hull = 130, CannonDamage = 28, PreferredAmmunition = AmmunitionCode.Incendiary, PreferredWeakPoint = WeakPointCode.Hull, GoldReward = 140, ExperienceReward = 175 },
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

        foreach (var npc in content.Npcs)
        {
            if (npc.MaximumSpeed <= 0f || npc.DesiredRange <= 0f || npc.Hull == 0 ||
                npc.CannonDamage == 0 || npc.GoldReward == 0 || npc.ExperienceReward == 0)
            {
                errors.Add($"NPC '{npc.Id}' has invalid combat or reward values.");
            }
        }

        return errors;
    }

    private static void ValidateIds(
        IEnumerable<string> ids,
        string kind,
        List<string> errors)
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
