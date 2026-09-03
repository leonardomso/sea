using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class TacticalRulesTests
{
    [Fact]
    public void StatusApplication_StacksAndRefreshesAnActiveEffect()
    {
        var existing = new TacticalStatusState(
            IsActive: true,
            Stacks: 2,
            ExpiresAtTick: 30,
            ImmunityUntilTick: 0);

        var result = TacticalRules.ApplyStatus(
            existing,
            currentTick: 10,
            durationTicks: 40,
            maximumStacks: 3);

        Assert.True(result.Applied);
        Assert.True(result.State.IsActive);
        Assert.Equal(3u, result.State.Stacks);
        Assert.Equal(50ul, result.State.ExpiresAtTick);
    }

    [Fact]
    public void StatusApplication_IsRejectedDuringImmunity()
    {
        var existing = new TacticalStatusState(
            IsActive: false,
            Stacks: 0,
            ExpiresAtTick: 20,
            ImmunityUntilTick: 35);

        var result = TacticalRules.ApplyStatus(
            existing,
            currentTick: 30,
            durationTicks: 40,
            maximumStacks: 3);

        Assert.False(result.Applied);
        Assert.Equal(existing, result.State);
    }

    [Fact]
    public void ExpiredStatus_BecomesInactiveAndReceivesImmunity()
    {
        var result = TacticalRules.ExpireStatus(
            new TacticalStatusState(true, 2, 25, 0),
            currentTick: 25,
            immunityTicks: 20);

        Assert.False(result.IsActive);
        Assert.Equal(0u, result.Stacks);
        Assert.Equal(45ul, result.ImmunityUntilTick);
    }

    [Theory]
    [InlineData(false, true, true, 10ul, 10ul, AbilityRejection.SourceSunk)]
    [InlineData(true, false, true, 10ul, 10ul, AbilityRejection.UnknownAbility)]
    [InlineData(true, true, true, 9ul, 10ul, AbilityRejection.Cooldown)]
    [InlineData(true, true, false, 10ul, 10ul, AbilityRejection.Busy)]
    [InlineData(true, true, true, 10ul, 10ul, AbilityRejection.None)]
    public void AbilityValidation_RejectsInvalidAuthoritativeState(
        bool alive,
        bool known,
        bool idle,
        ulong tick,
        ulong readyAtTick,
        AbilityRejection expected)
    {
        Assert.Equal(expected, TacticalRules.ValidateAbility(
            new AbilityRequest(alive, known, idle, tick, readyAtTick)));
    }

    [Fact]
    public void TacticalModifiers_CombineWithoutAllowingNegativeMovement()
    {
        var movement = TacticalRules.MovementModifiers(
            fullSail: true,
            slowedStacks: 3,
            sailsDisabled: true,
            sailIntegrity: 0f,
            inShoal: true,
            inStorm: true,
            repairing: true);

        Assert.Equal(0.08775f, movement.MaximumSpeed, 5);
        Assert.Equal(0f, movement.Acceleration);
        Assert.Equal(0.325f, movement.TurnRate, 5);
        Assert.Equal(0.75f, movement.WeaponEffectiveness, 5);
    }

    [Fact]
    public void DamagedSails_ReduceSpeedAccelerationAndTurningProportionally()
    {
        var movement = TacticalRules.MovementModifiers(
            fullSail: false,
            slowedStacks: 0,
            sailsDisabled: false,
            sailIntegrity: 0.5f,
            inShoal: false,
            inStorm: false,
            repairing: false);

        Assert.Equal(0.75f, movement.MaximumSpeed, 5);
        Assert.Equal(0.75f, movement.Acceleration, 5);
        Assert.Equal(0.75f, movement.TurnRate, 5);
    }

    [Theory]
    [InlineData(10u, 100u, 100u, 10u)]
    [InlineData(10u, 50u, 100u, 20u)]
    [InlineData(10u, 25u, 100u, 30u)]
    public void DamagedCannons_IncreaseReloadWithinTheConfiguredCap(
        uint baseTicks,
        uint cannons,
        uint maximumCannons,
        uint expected)
    {
        Assert.Equal(expected, TacticalRules.AdjustedReloadTicks(
            baseTicks,
            cannons,
            maximumCannons));
    }

    [Fact]
    public void Brace_ReducesIncomingDamageByFortyPercent()
    {
        Assert.Equal(15u, TacticalRules.ApplyIncomingDamage(25, braceActive: true));
        Assert.Equal(25u, TacticalRules.ApplyIncomingDamage(25, braceActive: false));
    }

    [Theory]
    [InlineData("burning", 2u, 20ul, 4u)]
    [InlineData("flooding", 3u, 20ul, 3u)]
    [InlineData("burning", 2u, 21ul, 0u)]
    [InlineData("slowed", 2u, 20ul, 0u)]
    public void PeriodicStatusDamage_OnlyTicksAtTheSimulationCadence(
        string status,
        uint stacks,
        ulong tick,
        uint expected)
    {
        Assert.Equal(expected, TacticalRules.PeriodicStatusDamage(status, stacks, tick));
    }

    [Fact]
    public void SmokeScreen_BlocksOnlyNewLongRangeLocks()
    {
        Assert.False(TacticalRules.CanAcquireTarget(smokeActive: true, distance: 20.01f));
        Assert.True(TacticalRules.CanAcquireTarget(smokeActive: true, distance: 20f));
        Assert.True(TacticalRules.CanAcquireTarget(smokeActive: false, distance: 60f));
    }

    [Theory]
    [InlineData(false, true, true, true, RepairRejection.SourceSunk)]
    [InlineData(true, false, true, true, RepairRejection.Busy)]
    [InlineData(true, true, false, true, RepairRejection.NoRepairKit)]
    [InlineData(true, true, true, false, RepairRejection.NothingToRepair)]
    [InlineData(true, true, true, true, RepairRejection.None)]
    public void RepairValidation_CoversEveryStartCondition(
        bool alive,
        bool idle,
        bool hasKit,
        bool damaged,
        RepairRejection expected)
    {
        Assert.Equal(expected, TacticalRules.ValidateRepair(
            new RepairRequest(alive, idle, hasKit, damaged)));
    }

    [Fact]
    public void RepairProgress_IsProgressiveAndClamped()
    {
        Assert.Equal(50u, TacticalRules.ProgressiveRestore(
            initial: 40,
            maximum: 100,
            restoreAmount: 50,
            elapsedTicks: 10,
            durationTicks: 50));
        Assert.Equal(90u, TacticalRules.ProgressiveRestore(40, 100, 50, 50, 50));
        Assert.Equal(100u, TacticalRules.ProgressiveRestore(90, 100, 50, 50, 50));
    }

    [Theory]
    [InlineData(false, true, true, 10u, 100u, 5f, 10ul, 10ul, BoardingRejection.SourceSunk)]
    [InlineData(true, false, true, 10u, 100u, 5f, 10ul, 10ul, BoardingRejection.TargetSunk)]
    [InlineData(true, true, false, 10u, 100u, 5f, 10ul, 10ul, BoardingRejection.Busy)]
    [InlineData(true, true, true, 25u, 100u, 5f, 10ul, 10ul, BoardingRejection.TargetTooStrong)]
    [InlineData(true, true, true, 24u, 100u, 8.01f, 10ul, 10ul, BoardingRejection.OutOfRange)]
    [InlineData(true, true, true, 24u, 100u, 8f, 9ul, 10ul, BoardingRejection.Cooldown)]
    [InlineData(true, true, true, 24u, 100u, 8f, 10ul, 10ul, BoardingRejection.None)]
    public void BoardingValidation_CoversThresholdRangeAndCooldown(
        bool sourceAlive,
        bool targetAlive,
        bool idle,
        uint targetHull,
        uint targetMaxHull,
        float distance,
        ulong tick,
        ulong readyAtTick,
        BoardingRejection expected)
    {
        Assert.Equal(expected, TacticalRules.ValidateBoarding(new BoardingRequest(
            sourceAlive,
            targetAlive,
            idle,
            targetHull,
            targetMaxHull,
            distance,
            tick,
            readyAtTick)));
    }

    [Theory]
    [InlineData(51u, 50u, false, true)]
    [InlineData(50u, 50u, false, true)]
    [InlineData(49u, 50u, false, false)]
    [InlineData(75u, 50u, true, false)]
    public void BoardingResolution_ComparesEffectiveCrewPower(
        uint attackerCrew,
        uint defenderCrew,
        bool fatigued,
        bool expected)
    {
        Assert.Equal(expected, TacticalRules.BoardingSucceeds(
            attackerCrew,
            defenderCrew,
            fatigued));
    }

    [Fact]
    public void MovingStorm_WrapsAtTheChartBoundary()
    {
        var moved = TacticalRules.MoveStorm(
            x: 99f,
            y: 0f,
            directionDegrees: 90f,
            speed: 4f,
            deltaSeconds: 1f);

        Assert.Equal(-97f, moved.X, 4);
        Assert.Equal(0f, moved.Y, 4);
    }

    [Fact]
    public void StatusProc_IsDeterministicForTheSameVolley()
    {
        var first = TacticalRules.ShouldApplyStatus(72, 35);
        var second = TacticalRules.ShouldApplyStatus(72, 35);

        Assert.Equal(first, second);
        Assert.True(TacticalRules.ShouldApplyStatus(72, 100));
        Assert.False(TacticalRules.ShouldApplyStatus(72, 0));
    }

    [Theory]
    [InlineData(0u, 1u)]
    [InlineData(1u, 0u)]
    public void StatusApplicationRejectsZeroTimingOrStackLimits(
        uint durationTicks,
        uint maximumStacks)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TacticalRules.ApplyStatus(
            default,
            currentTick: 1,
            durationTicks,
            maximumStacks));
    }

    [Fact]
    public void UnexpiredAndInactiveStatusesRemainUnchanged()
    {
        var inactive = new TacticalStatusState(false, 0, 10, 20);
        var active = new TacticalStatusState(true, 1, 20, 0);

        Assert.Equal(inactive, TacticalRules.ExpireStatus(inactive, 30, 10));
        Assert.Equal(active, TacticalRules.ExpireStatus(active, 19, 10));
    }

    [Theory]
    [InlineData(0u, 1u)]
    [InlineData(1u, 0u)]
    public void ReloadRejectsZeroBaseOrMaximum(uint baseTicks, uint maximumCannons)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TacticalRules.AdjustedReloadTicks(baseTicks, 1, maximumCannons));
    }

    [Fact]
    public void StatusCodeDamageAvoidsStringDispatch()
    {
        Assert.Equal(4u, TacticalRules.PeriodicStatusDamage(StatusCode.Burning, 2));
        Assert.Equal(2u, TacticalRules.PeriodicStatusDamage(StatusCode.Flooding, 2));
        Assert.Equal(0u, TacticalRules.PeriodicStatusDamage(StatusCode.Slowed, 2));
        Assert.Equal(0u, TacticalRules.PeriodicStatusDamage(StatusCode.Burning, 0));
        Assert.Equal(0u, TacticalRules.ApplyIncomingDamage(0, braceActive: true));
    }

    [Fact]
    public void RepairProgressRejectsZeroDurationAndClampsElapsedTime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TacticalRules.ProgressiveRestore(1, 10, 5, 1, 0));
        Assert.Equal(1u, TacticalRules.ProgressiveRestore(1, 10, 5, 0, 10));
        Assert.Equal(6u, TacticalRules.ProgressiveRestore(1, 10, 5, 100, 10));
    }

    [Fact]
    public void BoardingRejectsZeroTargetHullCapacityAndNonFiniteDistance()
    {
        Assert.Equal(BoardingRejection.TargetTooStrong, TacticalRules.ValidateBoarding(
            new BoardingRequest(true, true, true, 0, 0, 1, 10, 10)));
        Assert.Equal(BoardingRejection.OutOfRange, TacticalRules.ValidateBoarding(
            new BoardingRequest(true, true, true, 1, 100, float.NaN, 10, 10)));
    }

    [Fact]
    public void StormMovementWrapsBelowAndAcrossMultipleMapSpans()
    {
        var below = TacticalRules.MoveStorm(-99, 0, 270, 4, 1);
        var multiple = TacticalRules.MoveStorm(0, 0, 90, 450, 1);

        Assert.Equal(97f, below.X, 4);
        Assert.Equal(50f, multiple.X, 4);
    }

    [Fact]
    public void StatusApplication_AppliesAgainOnceImmunityEnds()
    {
        var existing = new TacticalStatusState(
            IsActive: false,
            Stacks: 3,
            ExpiresAtTick: 20,
            ImmunityUntilTick: 35);

        var result = TacticalRules.ApplyStatus(
            existing,
            currentTick: 35,
            durationTicks: 40,
            maximumStacks: 3);

        Assert.True(result.Applied);
        Assert.True(result.State.IsActive);
        Assert.Equal(1u, result.State.Stacks);
        Assert.Equal(75ul, result.State.ExpiresAtTick);
        Assert.Equal(35ul, result.State.ImmunityUntilTick);
    }

    [Fact]
    public void StatusApplication_NeverStacksPastTheMaximum()
    {
        var existing = new TacticalStatusState(true, 3, 30, 0);

        var result = TacticalRules.ApplyStatus(existing, 10, 40, 3);

        Assert.True(result.Applied);
        Assert.Equal(3u, result.State.Stacks);
    }

    [Fact]
    public void StatusTimestampsBeyondTheTickRangeAreRejected()
    {
        var inactive = new TacticalStatusState(false, 0, 0, 0);
        var expired = new TacticalStatusState(true, 1, 0, 0);

        Assert.Throws<OverflowException>(() => TacticalRules.ApplyStatus(inactive, ulong.MaxValue, 1, 1));
        Assert.Throws<OverflowException>(() => TacticalRules.ExpireStatus(expired, ulong.MaxValue, 1));
    }

    [Fact]
    public void FullSail_BoostsSpeedAndAccelerationInClearWeather()
    {
        var movement = TacticalRules.MovementModifiers(
            fullSail: true,
            slowedStacks: 0,
            sailsDisabled: false,
            sailIntegrity: 1f,
            inShoal: false,
            inStorm: false,
            repairing: false);

        Assert.Equal(1.35f, movement.MaximumSpeed, 5);
        Assert.Equal(1.35f, movement.Acceleration, 5);
        Assert.Equal(1f, movement.TurnRate, 5);
        Assert.Equal(1f, movement.WeaponEffectiveness, 5);
    }

    [Fact]
    public void ReloadBeyondTheTickRangeIsRejected()
    {
        Assert.Throws<OverflowException>(() => TacticalRules.AdjustedReloadTicks(uint.MaxValue, 1, 100));
    }

    [Fact]
    public void BurningDamageBeyondTheHullRangeIsRejected()
    {
        Assert.Throws<OverflowException>(() => TacticalRules.PeriodicStatusDamage("burning", uint.MaxValue, 20));
        Assert.Throws<OverflowException>(() => TacticalRules.PeriodicStatusDamage(StatusCode.Burning, uint.MaxValue));
    }

    [Fact]
    public void RepairBeyondTheHullRangeIsRejected()
    {
        Assert.Throws<OverflowException>(() => TacticalRules.ProgressiveRestore(uint.MaxValue, uint.MaxValue, 1, 50, 50));
    }

    [Fact]
    public void StormMovementScalesWithSpeedAndElapsedTime()
    {
        var moved = TacticalRules.MoveStorm(0f, 0f, directionDegrees: 30f, speed: 2f, deltaSeconds: 0.5f);

        Assert.Equal(0.5f, moved.X, 4);
        Assert.Equal(0.8660f, moved.Y, 3);
    }

    [Theory]
    [InlineData(99f, 0f, WorldRules.MapMax)]
    [InlineData(-99f, 180f, WorldRules.MapMin)]
    public void StormRestingOnTheChartEdgeStaysThere(float y, float directionDegrees, float expectedY)
    {
        var moved = TacticalRules.MoveStorm(0f, y, directionDegrees, speed: 1f, deltaSeconds: 1f);

        Assert.Equal(expectedY, moved.Y, 4);
    }

    [Theory]
    [InlineData(1UL, 89u)]
    [InlineData(2UL, 30u)]
    [InlineData(3UL, 56u)]
    [InlineData(4UL, 52u)]
    [InlineData(7UL, 4u)]
    [InlineData(90UL, 50u)]
    [InlineData(0x9E3779B97F4A7C15UL, 35u)]
    [InlineData(ulong.MaxValue, 67u)]
    public void StatusProc_RollsTheSplitMixResidue(ulong seed, uint residue)
    {
        Assert.False(TacticalRules.ShouldApplyStatus(seed, residue));
        Assert.True(TacticalRules.ShouldApplyStatus(seed, residue + 1));
    }
}
