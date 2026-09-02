using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class ContentValidationTests
{
    [Fact]
    public void InvalidContentReportsEveryIndependentDefinitionError()
    {
        var invalid = new CombatContent
        {
            Ammunition =
            [
                Ammunition("", float.NaN),
                Ammunition("duplicate", 0),
                Ammunition("duplicate", 1),
            ],
            Abilities =
            [
                Ability("", 0, 0),
                Ability("duplicate", 1, 1),
                Ability("duplicate", 1, 1),
            ],
            Npcs =
            [
                Npc("", 0, 0, 0, 0, 0, 0),
                Npc("duplicate", 1, 1, 1, 1, 1, 1),
                Npc("duplicate", 1, 1, 1, 1, 1, 1),
            ],
        };

        var errors = ContentCatalog.Validate(invalid);

        Assert.Contains(errors, error => error.Contains("ammunition id is empty",
            StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Duplicate ammunition",
            StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("invalid range multiplier",
            StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("positive timing",
            StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("invalid combat or reward",
            StringComparison.Ordinal));
    }

    [Fact]
    public void NullContentIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ContentCatalog.Validate(null!));
    }

    private static AmmunitionContent Ammunition(string id, float range) => new()
    {
        Id = id,
        Code = AmmunitionCode.Round,
        HullDamage = 1,
        SailDamage = 1,
        CannonDamage = 1,
        CrewDamage = 1,
        RangeMultiplier = range,
        AppliedStatus = "none",
        AppliedStatusCode = StatusCode.None,
    };

    private static AbilityContent Ability(string id, uint cooldown, uint duration) => new()
    {
        Id = id,
        Code = AbilityCode.Brace,
        CooldownTicks = cooldown,
        DurationTicks = duration,
    };

    private static NpcContent Npc(
        string id,
        float range,
        float speed,
        uint hull,
        uint damage,
        uint gold,
        ulong experience) => new()
        {
            Id = id,
            Code = ShipArchetypeCode.Patrol,
            AggroRange = 0,
            DesiredRange = range,
            MaximumSpeed = speed,
            Hull = hull,
            CannonDamage = damage,
            PreferredAmmunition = AmmunitionCode.Round,
            PreferredWeakPoint = WeakPointCode.Hull,
            GoldReward = gold,
            ExperienceReward = experience,
        };
}
