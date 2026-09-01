using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void RecordCombatProgress(
        ReducerContext ctx,
        ulong sourceEntityId,
        Ship defender,
        CombatDamage damage,
        bool sunk)
    {
        if (sourceEntityId == 0 || defender.FactionCode != (byte)FactionCode.Npc ||
            ctx.Db.PlayerOwnership.ShipEntityId.Find(sourceEntityId) is null)
        {
            return;
        }

        var applied = (ulong)damage.Hull + damage.Sails + damage.Cannons + damage.Crew;
        if (applied > 0)
        {
            AddContribution(ctx, defender.EncounterId, sourceEntityId, applied, boarding: 0);
            AwardProgression(
                ctx,
                sourceEntityId,
                ProgressionRules.DamageExperience(applied),
                gold: 0);
        }

        if (sunk && ctx.Db.NpcDefinition.ArchetypeCode.Find(defender.ArchetypeCode) is
            NpcDefinition definition)
        {
            AwardProgression(ctx, sourceEntityId, definition.ExperienceReward, gold: 0);
        }
    }

    private static void RecordBoardingProgress(
        ReducerContext ctx,
        ulong sourceEntityId,
        Ship target)
    {
        if (target.FactionCode != (byte)FactionCode.Npc ||
            ctx.Db.PlayerOwnership.ShipEntityId.Find(sourceEntityId) is null)
        {
            return;
        }

        AddContribution(
            ctx,
            target.EncounterId,
            sourceEntityId,
            damage: 0,
            boarding: ProgressionRules.BoardingExperience);
        AwardProgression(
            ctx,
            sourceEntityId,
            ProgressionRules.BoardingExperience,
            gold: 0);
    }

    private static void AddContribution(
        ReducerContext ctx,
        ulong encounterId,
        ulong contributorEntityId,
        ulong damage,
        ulong boarding)
    {
        if (encounterId == 0)
        {
            return;
        }

        foreach (var existing in ctx.Db.CombatContribution.ByEncounter.Filter(encounterId))
        {
            if (existing.ContributorEntityId != contributorEntityId)
            {
                continue;
            }

            var updated = existing;
            updated.Damage = ProgressionRules.AddSaturating(updated.Damage, damage);
            updated.Boarding = ProgressionRules.AddSaturating(updated.Boarding, boarding);
            ctx.Db.CombatContribution.ContributionId.Update(updated);
            return;
        }

        ctx.Db.CombatContribution.Insert(new CombatContribution
        {
            EncounterId = encounterId,
            ContributorEntityId = contributorEntityId,
            Damage = damage,
            Boarding = boarding,
            Support = 0,
            Rewarded = false,
        });
    }

    private static void AwardProgression(
        ReducerContext ctx,
        ulong shipEntityId,
        ulong experience,
        uint gold)
    {
        if (ctx.Db.PlayerOwnership.ShipEntityId.Find(shipEntityId) is not
            PlayerOwnership ownership ||
            ctx.Db.PlayerProgression.Owner.Find(ownership.Owner) is not
                PlayerProgression progression)
        {
            return;
        }

        progression.Experience = ProgressionRules.AddSaturating(
            progression.Experience,
            experience);
        progression.Gold = uint.MaxValue - progression.Gold < gold
            ? uint.MaxValue
            : progression.Gold + gold;
        while (ctx.Db.LevelDefinition.Level.Find(progression.Level + 1) is
            LevelDefinition next && next.RequiredExperience <= progression.Experience)
        {
            progression.Level = next.Level;
        }

        ctx.Db.PlayerProgression.Owner.Update(progression);
    }
}
