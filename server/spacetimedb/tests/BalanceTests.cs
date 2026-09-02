using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class BalanceTests
{
    private static readonly StatCapsContent Caps = Tier1.Caps;

    // Math section 6.1/6.3: one repair per (channel + cooldown) seconds, each faded by RepairFatigue^n.
    private static float HealedFraction(StatCapsContent caps, int repairs)
    {
        var total = 0f;
        var fade = 1f;
        for (var n = 0; n < repairs; n++)
        {
            total += caps.RepairBaseAmount * fade;
            fade *= caps.RepairFatigue;
        }

        return total;
    }

    // The trailing channel must finish inside the window, so the first repair only costs its channel.
    private static int RepairsWithin(StatCapsContent caps, float seconds) =>
        1 + (int)((seconds - caps.RepairChannelSeconds) / (caps.RepairChannelSeconds + caps.RepairCooldownSeconds));

    // Math section 12.1: TTK = EHP / DPS.
    private static float TimeToKill(float effectiveHitPoints, float dps) => effectiveHitPoints / dps;

    // Math section 4: burn adds EffectMagnitude x MaxHP_target x EffectDurationSeconds to each volley.
    private static float SustainedDpsWithEffect(ShipStatSheet sheet, AmmunitionContent ammo, uint targetHitPoints)
    {
        var burn = ammo.Effect == AmmoEffectCode.Burn
            ? ammo.EffectMagnitude * targetHitPoints * ammo.EffectDurationSeconds
            : 0f;
        return ShipStatRules.SustainedDps(sheet) + (burn * 1000f / sheet.ReloadMilliseconds);
    }

    // Math section 12.1: two base ships broadside on kill each other in 32 to 38 seconds.
    [Fact]
    public void Section_12_1_base_versus_base_lasts_32_to_38_seconds()
    {
        var sheet = Tier1.Sheet();

        var timeToKill = TimeToKill(ShipStatRules.EffectiveHitPoints(sheet), ShipStatRules.SustainedDps(sheet));

        Assert.InRange(timeToKill, 32f, 38f);
    }

    // Math section 12.1: the two repairs that fit inside that fight stretch it to 42 to 50 seconds.
    [Fact]
    public void Section_12_1_two_repairs_extend_the_fight_to_42_to_50_seconds()
    {
        var sheet = Tier1.Sheet();
        var healedEffective = HealedFraction(Caps, 2) * ShipStatRules.EffectiveHitPoints(sheet);

        var timeToKill = TimeToKill(
            ShipStatRules.EffectiveHitPoints(sheet) + healedEffective,
            ShipStatRules.SustainedDps(sheet));

        Assert.InRange(timeToKill, 42f, 50f);
    }

    // Math section 12.2: no build beats the base ship by more than 60 percent.
    // The sweep walks every legal Combat Power spend (Math section 2.3 budget, section 12.2) in whole centis.
    [Fact]
    public void Section_12_2_fight_score_never_exceeds_1_60_within_the_budget()
    {
        var loadout = Tier1.Loadout();
        var damageCap = (int)MathF.Round(Caps.DamageBonusCap * 100f);
        var reloadCap = (int)MathF.Round(Caps.ReloadBonusCap * 100f);
        var hitPointCap = (int)MathF.Round(Caps.HitPointBonusCap * 100f);
        var armorCap = (int)Caps.ArmorPointsCap;
        var armorWeightCentis = (int)MathF.Round(Caps.CombatPowerArmorWeight * 100f);
        var budgetCentis = (int)MathF.Round(Caps.CombatPowerBudget * 100f);
        var maxScore = 0f;

        for (var damage = 0; damage <= damageCap; damage++)
        {
            for (var reload = 0; reload <= reloadCap; reload++)
            {
                for (var hitPoints = 0; hitPoints <= hitPointCap; hitPoints++)
                {
                    for (var armor = 0; armor <= armorCap; armor++)
                    {
                        if ((100 * (damage + reload + hitPoints)) + (armorWeightCentis * armor) > budgetCentis)
                        {
                            continue;
                        }

                        var bonuses = new StatBonuses(
                            damage / 100f, reload / 100f, 0, hitPoints / 100f, armor, 0f, 0f, 0, 0f, 0f, 0);
                        var sheet = ShipStatRules.Compute(
                            loadout, [new BonusSource(BonusSourceKind.Plates, 1, bonuses)], Caps);

                        Assert.Equal(0f, sheet.CombatPowerInactive);
                        Assert.True(
                            sheet.FightScore <= 1.60f,
                            $"d={damage} r={reload} h={hitPoints} a={armor} score={sheet.FightScore}");
                        maxScore = MathF.Max(maxScore, sheet.FightScore);
                    }
                }
            }
        }

        Assert.InRange(maxScore, 1.575f, 1.60f);
    }

    // Math section 12.4: with its effect counted, no ammunition out-damages Round Shot by more than 20 percent.
    [Fact]
    public void Section_12_4_no_ammo_beats_round_shot_by_more_than_20_percent()
    {
        var roundSheet = Tier1.Sheet();
        var targetHitPoints = roundSheet.MaxHitPoints;
        var roundDps = SustainedDpsWithEffect(roundSheet, Tier1.Round, targetHitPoints);

        foreach (var ammo in Tier1.Content.Ammunition)
        {
            var sheet = ShipStatRules.Compute(Tier1.Loadout(ammo), [], Caps);
            var sustained = SustainedDpsWithEffect(sheet, ammo, targetHitPoints);

            if (ammo.Effect == AmmoEffectCode.Burn)
            {
                // The burn an ammunition carries is the one the caps define: section 4 has a single burn.
                Assert.Equal(Caps.BurnPerSecond, ammo.EffectMagnitude);
                Assert.Equal(Caps.BurnDurationSeconds, ammo.EffectDurationSeconds);
            }

            Assert.True(sustained <= 1.2f * roundDps, $"{ammo.Id}: {sustained} > {1.2f * roundDps}");
        }
    }

    // Math section 12.5: a Common NPC dies inside 20 seconds and cannot kill a repairing base ship in 60.
    [Fact]
    public void Section_12_5_a_common_npc_dies_in_20_seconds_and_cannot_kill_a_repairing_base_ship_in_60()
    {
        var player = Tier1.Sheet();
        var playerDps = ShipStatRules.SustainedDps(player);
        var playerEffective = ShipStatRules.EffectiveHitPoints(player);

        // Math section 7.1: Common HP is already 0.50 x P_EHP, so the player's DPS meets it undivided.
        var commonEffectiveHitPoints = Caps.NpcHitPointMultipliers[0] * playerEffective;
        var commonDps = Caps.NpcDpsMultipliers[0] * playerDps;

        Assert.InRange(TimeToKill(commonEffectiveHitPoints, playerDps), 16f, 20f);

        // Math section 12.5: 0.25*P_DPS x 60 < P_EHP + 0.435*MaxHP (repairing on cooldown for the minute).
        var incomingOverMinute = commonDps * 60f;
        var minuteOfRepairs = HealedFraction(Caps, RepairsWithin(Caps, 60f)) * player.MaxHitPoints;

        Assert.True(
            incomingOverMinute < playerEffective + minuteOfRepairs,
            $"{incomingOverMinute} >= {playerEffective + minuteOfRepairs}");
    }
}
