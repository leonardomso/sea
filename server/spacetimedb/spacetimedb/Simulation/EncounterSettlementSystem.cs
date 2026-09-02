using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void OpenNpcEncounter(
        ReducerContext ctx,
        Ship npc,
        uint goldPool,
        ulong experiencePool,
        ulong tick)
    {
        if (npc.EncounterId == 0 || npc.FactionCode != (byte)FactionCode.Npc)
        {
            throw new InvalidOperationException("Only an NPC with an encounter ID can open an encounter.");
        }

        ctx.Db.CombatEncounter.Insert(new CombatEncounter
        {
            EncounterId = npc.EncounterId,
            NpcEntityId = npc.EntityId,
            StateCode = (byte)EncounterStateCode.Open,
            GoldPool = goldPool,
            ExperiencePool = experiencePool,
            OpenedAtTick = tick,
            SettledAtTick = 0,
        });
    }

    private static void SettleNpcEncounter(
        ReducerContext ctx,
        Ship npc,
        ulong tick)
    {
        if (npc.FactionCode != (byte)FactionCode.Npc || npc.EncounterId == 0)
        {
            return;
        }

        var encounter = ctx.Db.CombatEncounter.EncounterId.Find(npc.EncounterId)
            ?? throw new InvalidOperationException("Sunk NPC encounter is missing.");
        if (encounter.StateCode == (byte)EncounterStateCode.Settled)
        {
            return;
        }

        if (encounter.StateCode != (byte)EncounterStateCode.Open ||
            encounter.NpcEntityId != npc.EntityId)
        {
            throw new InvalidOperationException("Sunk NPC encounter state is corrupt.");
        }

        var contributions = ctx.Db.CombatContribution.ByEncounter
            .Filter(encounter.EncounterId)
            .Select(row => new RewardContribution(
                row.ContributorEntityId,
                row.Damage,
                row.Boarding,
                row.Support))
            .ToArray();
        var grants = EncounterSettlementRules.Settle(
            encounter.GoldPool,
            encounter.ExperiencePool,
            contributions);
        foreach (var grant in grants)
        {
            AwardEncounterReward(ctx, encounter.EncounterId, grant, tick);
        }

        foreach (var contribution in ctx.Db.CombatContribution.ByEncounter
                     .Filter(encounter.EncounterId).ToArray())
        {
            ctx.Db.CombatContribution.ContributionId.Delete(contribution.ContributionId);
        }

        encounter.StateCode = (byte)EncounterStateCode.Settled;
        encounter.SettledAtTick = tick;
        ctx.Db.CombatEncounter.EncounterId.Update(encounter);
    }

    private static void AwardEncounterReward(
        ReducerContext ctx,
        ulong encounterId,
        EncounterRewardGrant grant,
        ulong tick)
    {
        if (ctx.Db.PlayerOwnership.ShipEntityId.Find(grant.EntityId) is not
            PlayerOwnership ownership)
        {
            throw new InvalidOperationException("Eligible contributor ownership is missing.");
        }

        foreach (var existing in ctx.Db.EncounterReward.ByEncounterContributor.Filter(
                     (encounterId, grant.EntityId)))
        {
            if (existing.Owner != ownership.Owner ||
                existing.Gold != grant.Gold ||
                existing.Experience != grant.Experience)
            {
                throw new InvalidOperationException("Encounter reward state is corrupt.");
            }

            return;
        }

        AwardProgression(ctx, grant.EntityId, grant.Experience, grant.Gold);
        ctx.Db.EncounterReward.Insert(new EncounterReward
        {
            EncounterId = encounterId,
            Owner = ownership.Owner,
            ContributorEntityId = grant.EntityId,
            Gold = grant.Gold,
            Experience = grant.Experience,
            AwardedAtTick = tick,
        });
        ctx.Db.EncounterRewardEvent.Insert(new EncounterRewardEvent
        {
            Owner = ownership.Owner,
            EncounterId = encounterId,
            ContributorEntityId = grant.EntityId,
            Gold = grant.Gold,
            Experience = grant.Experience,
        });
        AppendEvent(
            ctx,
            grant.EntityId,
            "shared_reward",
            $"encounter_id={encounterId},gold={grant.Gold},experience={grant.Experience}");
    }
}
