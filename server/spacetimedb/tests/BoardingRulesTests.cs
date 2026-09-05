using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class BoardingRulesTests
{
    [Fact]
    public void GrapplingReachesFourSquares()
    {
        Assert.True(BoardingRules.IsInReach(3.9f));
        Assert.False(BoardingRules.IsInReach(4.1f));
    }

    [Fact]
    public void AShipAboveHalfHealthCannotBeBoarded()
    {
        Assert.False(BoardingRules.CanBoard(defenderHull: 51, defenderMaxHull: 100));
        Assert.True(BoardingRules.CanBoard(50, 100));
    }

    [Fact]
    public void AShipWithNoHullAtAllIsNotABoardingTarget()
    {
        Assert.False(BoardingRules.CanBoard(0, 0));
        Assert.False(BoardingRules.HasHandsToBoard(0, 0));
    }

    [Fact]
    public void ACaptainWithHalfHerHandsGoneCannotBoard()
    {
        Assert.False(BoardingRules.HasHandsToBoard(hands: 4, maxHands: 10));
        Assert.True(BoardingRules.HasHandsToBoard(5, 10));
    }

    [Fact]
    public void TheStrongerCrewWins()
    {
        var attacker = new BoardingParty(Hands: 20, MoraleFraction: 1.0f, Tier: 3);
        var defender = new BoardingParty(Hands: 12, MoraleFraction: 1.0f, Tier: 3);

        Assert.True(BoardingRules.Score(attacker) > BoardingRules.Score(defender));
    }

    [Fact]
    public void ABiggerHullFightsBetterWithTheSameHands()
    {
        var small = new BoardingParty(20, 1f, 2);
        var large = new BoardingParty(20, 1f, 5);

        Assert.True(BoardingRules.Score(large) > BoardingRules.Score(small));
    }

    [Fact]
    public void APlayerWaitsAMinuteAndAnEnemyFifteenSeconds()
    {
        Assert.Equal(600UL, BoardingRules.PlayerCooldownTicks);
        Assert.Equal(150UL, BoardingRules.NpcCooldownTicks);
    }

    [Fact]
    public void TheSameVictimCannotBeBoardedTwiceInFiveMinutes()
    {
        Assert.Equal(3000UL, BoardingRules.VictimImmunityTicks);
    }

    [Fact]
    public void NoBoardingIsEverCertainAndNoneIsEverHopeless()
    {
        var hopeless = new BoardingParty(1, 1f, 1);
        var overwhelming = new BoardingParty(1000, 1f, 5);

        Assert.Equal(0.05f, BoardingRules.WinChance(hopeless, overwhelming), 4);
        Assert.Equal(0.90f, BoardingRules.WinChance(overwhelming, hopeless), 4);
    }

    [Fact]
    public void AnEvenlyMatchedBoardingIsACoinFlip()
    {
        var even = new BoardingParty(30, 1f, 3);

        Assert.Equal(0.5f, BoardingRules.WinChance(even, even), 4);
    }

    [Fact]
    public void TheHaulRidesOnHowOneSidedTheFightWas()
    {
        var hopeless = new BoardingParty(1, 1f, 1);
        var overwhelming = new BoardingParty(1000, 1f, 5);

        Assert.Equal(0.5f, BoardingRules.LootMultiplier(hopeless, overwhelming), 4);
        Assert.Equal(2.0f, BoardingRules.LootMultiplier(overwhelming, hopeless), 4);
    }

    [Fact]
    public void AWinTakesATenthOfHerHullAndThreeSecondsOfHerGuns()
    {
        var outcome = BoardingRules.Resolve(
            new BoardingParty(20, 1f, 3), new BoardingParty(10, 1f, 3), defenderMaxHull: 1000);

        Assert.True(outcome.AttackerWon);
        Assert.Equal(100u, outcome.HullDamage);
        Assert.Equal(30UL, outcome.SilenceTicks);
    }

    [Fact]
    public void AWinCostsTheLoserATenthOfHerHandsAndTheWinnerATwentieth()
    {
        var outcome = BoardingRules.Resolve(
            new BoardingParty(40, 1f, 3), new BoardingParty(30, 1f, 3), 1000);

        Assert.True(outcome.AttackerWon);
        Assert.Equal(2u, outcome.AttackerHandsLost);
        Assert.Equal(3u, outcome.DefenderHandsLost);
        Assert.Equal(0f, outcome.AttackerHullFractionLost, 4);
    }

    [Fact]
    public void ALossCostsTheAttackerHerHandsAndNothingElse()
    {
        var outcome = BoardingRules.Resolve(
            new BoardingParty(5, 1f, 2), new BoardingParty(25, 1f, 5), 1000);

        Assert.False(outcome.AttackerWon);
        Assert.Equal(0u, outcome.HullDamage);
        Assert.True(outcome.AttackerHandsLost > 0);
        Assert.Equal(0u, outcome.DefenderHandsLost);
        Assert.Equal(0UL, outcome.SilenceTicks);
    }

    [Fact]
    public void AFailedBoardingCostsTheAttackerATenthOfHerOwnHull()
    {
        var outcome = BoardingRules.Resolve(
            new BoardingParty(5, 1f, 2), new BoardingParty(25, 1f, 5), 1000);

        Assert.False(outcome.AttackerWon);
        Assert.Equal(0.10f, outcome.AttackerHullFractionLost, 4);
    }

    [Fact]
    public void ALongShotThatFailsKillsMoreSailorsThanAFairOne()
    {
        var fair = BoardingRules.Resolve(
            new BoardingParty(40, 1f, 3), new BoardingParty(40, 1f, 3), 1000);
        var longShot = BoardingRules.Resolve(
            new BoardingParty(40, 1f, 1), new BoardingParty(400, 1f, 5), 1000);

        Assert.False(fair.AttackerWon);
        Assert.False(longShot.AttackerWon);
        Assert.Equal(6u, fair.AttackerHandsLost);
        Assert.True(longShot.AttackerHandsLost > fair.AttackerHandsLost);
    }

    [Fact]
    public void AFavouredBoardingStillLosesOnABadRoll()
    {
        var attacker = new BoardingParty(20, 1f, 3);
        var defender = new BoardingParty(10, 1f, 3);

        Assert.True(BoardingRules.Resolve(attacker, defender, 1000, roll: 0.1f).AttackerWon);
        Assert.False(BoardingRules.Resolve(attacker, defender, 1000, roll: 0.9f).AttackerWon);
    }

    [Fact]
    public void TwoEmptyDecksSettleNothing()
    {
        var outcome = BoardingRules.Resolve(
            new BoardingParty(0, 1f, 1), new BoardingParty(0, 1f, 1), 1000);

        Assert.False(outcome.AttackerWon);
        Assert.Equal(0u, outcome.HullDamage);
        Assert.Equal(0u, outcome.AttackerHandsLost);
        Assert.Equal(0f, outcome.AttackerHullFractionLost, 4);
    }
}
