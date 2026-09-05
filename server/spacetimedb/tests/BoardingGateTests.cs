using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

/// <summary>
/// The checks a captain's order to grapple has to pass, and the crew and gold a boarding moves.
/// <see cref="BoardingRulesTests"/> covers the melee itself; this covers everything around it.
/// </summary>
public sealed class BoardingGateTests
{
    private static BoardingRequest Ready() => new()
    {
        SourceAlive = true,
        TargetSelected = true,
        TargetAlive = true,
        InPort = false,
        DistanceSquares = 2f,
        DefenderHull = 40,
        DefenderMaxHull = 100,
        AttackerHands = 40,
        AttackerMaxHands = 40,
        CurrentTick = 1_000,
        AttackerCooldownUntilTick = 0,
        DefenderImmuneUntilTick = 0,
    };

    [Fact]
    public void AReadyBoardingIsAllowed()
    {
        Assert.Equal(BoardingRejection.None, BoardingRules.Validate(Ready()));
    }

    [Fact]
    public void AWreckDoesNotGrappleAndIsNotGrappled()
    {
        Assert.Equal(
            BoardingRejection.SourceSunk,
            BoardingRules.Validate(Ready() with { SourceAlive = false }));
        Assert.Equal(
            BoardingRejection.TargetSunk,
            BoardingRules.Validate(Ready() with { TargetAlive = false }));
    }

    [Fact]
    public void GrapplingNothingIsRefusedBeforeAnythingElseIsAsked()
    {
        Assert.Equal(
            BoardingRejection.NoTarget,
            BoardingRules.Validate(Ready() with { TargetSelected = false, DistanceSquares = 90f }));
    }

    [Fact]
    public void TheHarbourIsATruceForGrapplingHooksAsWellAsGuns()
    {
        Assert.Equal(
            BoardingRejection.InPort,
            BoardingRules.Validate(Ready() with { InPort = true }));
    }

    [Fact]
    public void FourSquaresIsTheReach()
    {
        Assert.Equal(
            BoardingRejection.None,
            BoardingRules.Validate(Ready() with { DistanceSquares = 4f }));
        Assert.Equal(
            BoardingRejection.OutOfRange,
            BoardingRules.Validate(Ready() with { DistanceSquares = 4.01f }));
    }

    [Fact]
    public void AHealthyShipIsNotBoardable()
    {
        Assert.Equal(
            BoardingRejection.TargetNotBoardable,
            BoardingRules.Validate(Ready() with { DefenderHull = 51 }));
    }

    [Fact]
    public void ACrewBelowHalfStrengthStaysOnItsOwnDeck()
    {
        Assert.Equal(
            BoardingRejection.NotEnoughHands,
            BoardingRules.Validate(Ready() with { AttackerHands = 19, AttackerMaxHands = 40 }));
    }

    [Fact]
    public void TheAttackerWaitsOutHerOwnCooldown()
    {
        Assert.Equal(
            BoardingRejection.OnCooldown,
            BoardingRules.Validate(Ready() with { AttackerCooldownUntilTick = 1_001 }));
        Assert.Equal(
            BoardingRejection.None,
            BoardingRules.Validate(Ready() with { AttackerCooldownUntilTick = 1_000 }));
    }

    [Fact]
    public void AVictimIsLeftAloneForFiveMinutes()
    {
        Assert.Equal(
            BoardingRejection.TargetRecentlyBoarded,
            BoardingRules.Validate(Ready() with { DefenderImmuneUntilTick = 1_001 }));
    }

    /// <summary>
    /// The plan's own words: a captain is told the nearest reason she cannot board, not the last
    /// one. Every check below is failing here; the answer is the first of them.
    /// </summary>
    [Fact]
    public void TheNearestReasonIsTheOneSheIsGiven()
    {
        var hopeless = Ready() with
        {
            InPort = true,
            DistanceSquares = 40f,
            DefenderHull = 100,
            AttackerHands = 0,
            AttackerCooldownUntilTick = 9_000,
            DefenderImmuneUntilTick = 9_000,
        };

        Assert.Equal(BoardingRejection.InPort, BoardingRules.Validate(hopeless));
        Assert.Equal(
            BoardingRejection.OutOfRange,
            BoardingRules.Validate(hopeless with { InPort = false }));
        Assert.Equal(
            BoardingRejection.TargetNotBoardable,
            BoardingRules.Validate(hopeless with { InPort = false, DistanceSquares = 2f }));
    }

    [Fact]
    public void EveryTierCarriesTenHandsMoreThanTheOneBelow()
    {
        // SEA_2 §5.7: 10 / 20 / 30 / 40 / 50 by hull tier, and 10 x tier for a hostile.
        Assert.Equal(10u, BoardingRules.Complement(1));
        Assert.Equal(30u, BoardingRules.Complement(3));
        Assert.Equal(50u, BoardingRules.Complement(5));
        Assert.Equal(60u, BoardingRules.Complement(6));
    }

    [Fact]
    public void AShipWithNoRateStillCarriesACrew()
    {
        // Nothing afloat has tier 0, but a row read before its stats are written might say so,
        // and a complement of nought would make her permanently unable to board.
        Assert.Equal(10u, BoardingRules.Complement(0));
    }

    [Fact]
    public void HandsComeBackOneAMinuteAndNoFasterThanFull()
    {
        var minute = 60 * WorldRules.TickRateHz;
        Assert.Equal(30u, BoardingRules.Recover(30, 40, minute - 1));
        Assert.Equal(31u, BoardingRules.Recover(30, 40, minute));
        Assert.Equal(33u, BoardingRules.Recover(30, 40, minute * 3));
        Assert.Equal(40u, BoardingRules.Recover(30, 40, minute * 500));
        Assert.Equal(40u, BoardingRules.Recover(40, 40, minute * 500));
    }

    [Fact]
    public void AFullCrewFightsBetterOnTheAttackAndWorseOnTheDefence()
    {
        // SEA_2 §5.7: attack reads 0.6 + 0.4 x HP, defence 0.4 + 0.6 x HP, so a mauled hull
        // loses more of her defence than of her attack.
        Assert.Equal(1f, BoardingRules.AttackerMorale(100, 100), 4);
        Assert.Equal(1f, BoardingRules.DefenderMorale(100, 100), 4);
        Assert.Equal(0.8f, BoardingRules.AttackerMorale(50, 100), 4);
        Assert.Equal(0.7f, BoardingRules.DefenderMorale(50, 100), 4);
        Assert.Equal(0.6f, BoardingRules.AttackerMorale(0, 100), 4);
        Assert.Equal(0.4f, BoardingRules.DefenderMorale(0, 100), 4);
    }

    [Fact]
    public void AHullWithNoMaximumIsTreatedAsWhole()
    {
        Assert.Equal(1f, BoardingRules.AttackerMorale(0, 0), 4);
    }

    [Fact]
    public void TheHaulIsFifteenMapDropsTimesHowOneSidedItWas()
    {
        Assert.Equal(1_500u, BoardingRules.Haul(baseGold: 100f, lootMultiplier: 1f));
        Assert.Equal(3_000u, BoardingRules.Haul(100f, 2f));
        Assert.Equal(750u, BoardingRules.Haul(100f, 0.5f));
        Assert.Equal(0u, BoardingRules.Haul(0f, 2f));
    }

    [Fact]
    public void FailingCostsTwentyFiveMapDropsOrATwentiethOfThePurseWhicheverIsLess()
    {
        // SEA_2 §5.7's worked example: 25 x 503 = 12,575, capped by 5% of 100,000 = 5,000.
        Assert.Equal(5_000u, BoardingRules.FailGold(baseGold: 503f, purse: 100_000));
        Assert.Equal(2_500u, BoardingRules.FailGold(100f, 100_000));
        Assert.Equal(0u, BoardingRules.FailGold(503f, purse: 0));
    }
}
