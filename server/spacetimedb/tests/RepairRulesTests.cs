using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class RepairRulesTests
{
    private const uint MaxHull = 100;

    [Theory]
    [InlineData(false, true, true, true, true, RepairRejection.SourceSunk)]
    [InlineData(true, false, true, true, true, RepairRejection.Busy)]
    [InlineData(true, true, false, true, true, RepairRejection.OnCooldown)]
    [InlineData(true, true, true, false, true, RepairRejection.None)]
    [InlineData(true, true, true, true, false, RepairRejection.NothingToRepair)]
    [InlineData(true, true, true, true, true, RepairRejection.None)]
    public void ValidateRepair_ChecksTheCrewBeforeTheHullAndNeverTheKit(
        bool alive,
        bool idle,
        bool ready,
        bool hasKit,
        bool damaged,
        RepairRejection expected)
    {
        Assert.Equal(
            expected,
            RepairRules.ValidateRepair(new RepairRequest(alive, idle, ready, hasKit, damaged)));
    }

    [Theory]
    [InlineData(false, true, true, true, true, RepairRejection.SourceSunk)]
    [InlineData(true, false, true, true, true, RepairRejection.None)]
    [InlineData(true, true, false, true, true, RepairRejection.OnCooldown)]
    [InlineData(true, true, true, false, true, RepairRejection.NoRepairKit)]
    [InlineData(true, true, true, true, false, RepairRejection.NothingToRepair)]
    [InlineData(true, true, true, true, true, RepairRejection.None)]
    public void ValidateKit_TakesTheKitAndIgnoresWhetherTheCrewIsBusy(
        bool alive,
        bool idle,
        bool ready,
        bool hasKit,
        bool damaged,
        RepairRejection expected)
    {
        Assert.Equal(
            expected,
            RepairRules.ValidateKit(new RepairRequest(alive, idle, ready, hasKit, damaged)));
    }

    [Fact]
    public void TheKitAndTheChannelRunOnCooldownsOfDifferentLengths()
    {
        Assert.Equal(15UL * WorldRules.TickRateHz, RepairRules.CooldownTicks);
        Assert.Equal(45UL * WorldRules.TickRateHz, RepairRules.KitCooldownTicks);
    }

    [Theory]
    [InlineData(0, 20u)]
    [InlineData(1, 12u)]
    [InlineData(2, 7u)]
    [InlineData(3, 4u)]
    [InlineData(4, 2u)]
    public void FatigueShrinksEveryHealInsideTheWindow(int healsInWindow, uint expected)
    {
        Assert.Equal(expected, RepairRules.Heal(MaxHull, 0.2f, healsInWindow, burning: false));
    }

    [Fact]
    public void AFifthHealIsWorthAlmostNothingButNeverNegative()
    {
        Assert.Equal(1u, RepairRules.Heal(MaxHull, 0.2f, 5, burning: false));
        Assert.Equal(0u, RepairRules.Heal(MaxHull, 0.2f, 12, burning: false));
    }

    [Fact]
    public void BurningHalvesTheHealOnTopOfFatigue()
    {
        Assert.Equal(10u, RepairRules.Heal(MaxHull, 0.2f, 0, burning: true));
        Assert.Equal(6u, RepairRules.Heal(MaxHull, 0.2f, 1, burning: true));
    }

    [Theory]
    [InlineData(0u, 0.2f)]
    [InlineData(100u, 0f)]
    [InlineData(100u, -0.5f)]
    public void AHealWithNothingBehindItMendsNothing(uint maximumHull, float amount)
    {
        Assert.Equal(0u, RepairRules.Heal(maximumHull, amount, 0, burning: false));
    }

    [Theory]
    [InlineData(40u, 30u, 70u)]
    [InlineData(90u, 30u, 100u)]
    [InlineData(0u, 250u, 100u)]
    public void RestoreNeverCarriesAHullOverItsCeiling(uint hull, uint healed, uint expected)
    {
        Assert.Equal(expected, RepairRules.Restore(hull, MaxHull, healed));
    }

    [Theory]
    [InlineData(100u, 15u)]
    [InlineData(101u, 16u)]
    [InlineData(3u, 1u)]
    public void TheCancelThresholdRoundsUpAndIsNeverZero(uint maximumHull, uint expected)
    {
        Assert.Equal(expected, RepairRules.CancelDamage(maximumHull));
    }

    [Theory]
    [InlineData(0u, false, false)]
    [InlineData(14u, false, false)]
    [InlineData(15u, false, true)]
    [InlineData(40u, false, true)]
    [InlineData(1u, true, true)]
    public void OnlyEnoughDamageOrAFireShotBreaksAChannel(
        uint damageTaken,
        bool fireShotHit,
        bool expected)
    {
        Assert.Equal(expected, RepairRules.ShouldCancel(damageTaken, MaxHull, fireShotHit));
    }

    [Theory]
    [InlineData(0ul, 599ul, true)]
    [InlineData(0ul, 600ul, false)]
    [InlineData(100ul, 699ul, true)]
    public void TheFatigueWindowIsAMinuteLongAndRolls(
        ulong completedAtTick,
        ulong tick,
        bool expected)
    {
        Assert.Equal(expected, RepairRules.IsInFatigueWindow(completedAtTick, tick));
    }

    [Theory]
    [InlineData(3000u, 30u)]
    [InlineData(3050u, 31u)]
    [InlineData(0u, 1u)]
    public void AChannelAlwaysLastsAtLeastOneTick(uint milliseconds, uint expected)
    {
        Assert.Equal(expected, RepairRules.ChannelTicks(milliseconds));
    }
}
