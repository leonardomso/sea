using System.Linq;
using Sea.Client;
using Sea.LoadTests;
using Xunit;

namespace Sea.LoadTests.Tests;

public sealed class LoadSubscriptionPlanTests
{
    [Fact]
    public void OwnershipSubscriptionIsOwnerFiltered()
    {
        var queries = LoadSubscriptionPlan.Ownership("0x1234");

        var query = Assert.Single(queries);
        Assert.Equal(
            "SELECT * FROM player_ownership WHERE owner = 0x1234",
            query);
    }

    [Fact]
    public void ActiveSubscriptionContainsOwnedStateAndCommandResults()
    {
        var queries = LoadSubscriptionPlan.ActiveShip(42, "0x1234");

        Assert.Collection(
            queries,
            query => Assert.Equal("SELECT * FROM ship WHERE entity_id = 42", query),
            query => Assert.Equal(
                "SELECT * FROM ship_movement WHERE entity_id = 42",
                query),
            query => Assert.Equal(
                "SELECT * FROM command_result_event WHERE owner = 0x1234",
                query));
    }

    [Fact]
    public void MissingOwnerLiteralIsRejected()
    {
        Assert.Throws<ArgumentException>(() => LoadSubscriptionPlan.Ownership(""));
        Assert.Throws<ArgumentException>(() => LoadSubscriptionPlan.ActiveShip(42, ""));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoadSubscriptionPlan.ActiveShip(0, "0x1234"));
    }

    [Fact]
    public void PlansSatisfyTheWorldContract()
    {
        var queries = LoadSubscriptionPlan.Ownership("0x1234").Concat(LoadSubscriptionPlan.ActiveShip(42, "0x1234")).ToList();

        SeaWorldContract.Require(queries);
        Assert.NotEmpty(SeaWorldContract.Violations("SELECT * FROM player_ship"));
    }
}
