using SpacetimeDB.Types;
using Xunit;

namespace Sea.Server.IntegrationTests;

/// <summary>
/// Covers what Milestone 1a added to load_player against a live module: the starter hull, the
/// stat sheet derived from the content catalog, the map rank that replaced level and experience,
/// and the owner visibility filters that keep another player's dock private. The expected numbers
/// are the ones in Content/Data, not a second implementation of ShipStatRules, so a formula that
/// silently changes shows up here as a failure rather than agreeing with itself.
/// </summary>
public sealed class ShipStatsIntegrationTests
{
    [Fact]
    public void LoadPlayerSeedsAStarterHullWithStatsDerivedFromTheContentCatalog()
    {
        using var client = IntegrationClient.Connect();
        client.LoadPlayer();
        client.SubscribeDock();

        var hull = client.OwnedHull();
        Assert.Equal("hull_t1", hull.HullDefId);
        Assert.Equal("Sloop", hull.Name);
        Assert.Equal("cannon_t1", hull.CannonDefId);
        Assert.Equal(8, hull.CannonCount);

        var stats = client.OwnedShipStats();
        Assert.Equal(hull.HullId, stats.HullId);
        // 8 cannons x 20 damage x 1.0 round shot, with no bonus sources on a fresh account.
        Assert.Equal(160u, stats.VolleyDamage);
        Assert.Equal(3000u, stats.ReloadMilliseconds);
        Assert.Equal(3, stats.Magazine);
        Assert.Equal(1600u, stats.MaxHitPoints);
        Assert.Equal(0.15f, stats.ArmorFront, 4);
        Assert.Equal(0.08f, stats.ArmorSides, 4);
        Assert.Equal(0.03f, stats.ArmorBack, 4);
        Assert.Equal(2.4f, stats.SpeedSquaresPerSecond, 4);
        Assert.Equal(60f, stats.TurnDegreesPerSecond, 4);
        Assert.Equal(8, stats.RangeSquares);
        Assert.Equal(0.2f, stats.RepairAmount, 4);
        Assert.Equal(3000u, stats.RepairChannelMilliseconds);
        Assert.Null(client.UnhandledReducerError);
    }

    [Fact]
    public void LoadingTwiceLeavesExactlyOneHullAndOneStatSheet()
    {
        using var client = IntegrationClient.Connect();
        client.LoadPlayer();
        client.SubscribeDock();
        var first = client.OwnedHull();
        var firstStats = client.OwnedShipStats();

        client.ReloadPlayer();

        var hulls = client.VisibleHulls();
        var stats = client.VisibleShipStats();
        Assert.Single(hulls);
        Assert.Single(stats);
        Assert.Equal(first.HullId, hulls[0].HullId);
        Assert.Equal(firstStats.VolleyDamage, stats[0].VolleyDamage);
        Assert.Equal(firstStats.MaxHitPoints, stats[0].MaxHitPoints);
        Assert.Null(client.UnhandledReducerError);
    }

    [Fact]
    public void OneDockIsNeverVisibleToAnotherPlayer()
    {
        using var first = IntegrationClient.Connect();
        using var second = IntegrationClient.Connect();
        first.LoadPlayer();
        second.LoadPlayer();
        first.SubscribeDock();
        second.SubscribeDock();

        var firstHull = first.OwnedHull();
        var secondHull = second.OwnedHull();

        Assert.NotEqual(firstHull.HullId, secondHull.HullId);
        Assert.Single(first.VisibleHulls());
        Assert.Single(second.VisibleHulls());
        Assert.Single(first.VisibleShipStats());
        Assert.Single(second.VisibleShipStats());
        Assert.Equal(firstHull.HullId, first.VisibleHulls()[0].HullId);
        Assert.Equal(secondHull.HullId, second.VisibleHulls()[0].HullId);
    }

    [Fact]
    public void AFreshPlayerStartsAtMapRankOneWithNoGold()
    {
        using var client = IntegrationClient.Connect();
        client.LoadPlayer();

        var progression = client.OwnedProgression();

        Assert.Equal(1, progression.MapRank);
        Assert.Equal(0u, progression.Gold);
    }

    [Fact]
    public void ContentDefinitionTablesProjectTheSeededCatalog()
    {
        using var client = IntegrationClient.Connect();
        client.LoadPlayer();
        client.SubscribeDock();

        var hull = Assert.Single(
            client.HullDefs(),
            definition => string.Equals(definition.HullDefId, "hull_t1", StringComparison.Ordinal));
        Assert.Equal("Sloop", hull.Name);
        Assert.Equal(1, hull.Tier);
        Assert.Equal(1600u, hull.HitPoints);
        Assert.Equal(8, hull.CannonSlots);
        Assert.Equal(3, hull.Magazine);
        Assert.Equal(1, hull.MapRankRequired);

        var cannon = Assert.Single(
            client.CannonDefs(),
            definition => string.Equals(definition.CannonDefId, "cannon_t1", StringComparison.Ordinal));
        Assert.Equal(20u, cannon.Damage);
        Assert.Equal(3.0f, cannon.ReloadSeconds, 4);
        Assert.Equal(8, cannon.RangeSquares);

        var ammoIds = client.AmmoDefs().Select(definition => definition.AmmoId).ToArray();
        Assert.Equal(4, ammoIds.Length);
        Assert.Contains("round", ammoIds, StringComparer.Ordinal);
        Assert.Contains("chain", ammoIds, StringComparer.Ordinal);
        Assert.Contains("grapeshot", ammoIds, StringComparer.Ordinal);
        Assert.Contains("incendiary", ammoIds, StringComparer.Ordinal);
    }

    [Fact]
    public void StoredStatsHonourTheSeededCaps()
    {
        using var client = IntegrationClient.Connect();
        client.LoadPlayer();
        client.SubscribeDock();

        var caps = client.SeededStatCaps();
        var stats = client.OwnedShipStats();

        Assert.Equal(45f, caps.CombatPowerBudget, 4);
        Assert.Equal(1.5f, caps.ReloadFloorSeconds, 4);
        Assert.True(
            stats.ReloadMilliseconds >= (uint)(caps.ReloadFloorSeconds * 1000f),
            $"Reload {stats.ReloadMilliseconds}ms is below the {caps.ReloadFloorSeconds}s floor.");
        Assert.True(
            stats.ArmorFront <= caps.ArmorAbsoluteMax &&
            stats.ArmorSides <= caps.ArmorAbsoluteMax &&
            stats.ArmorBack <= caps.ArmorAbsoluteMax,
            "An armour face exceeded the absolute maximum.");
        Assert.True(
            stats.CombatPowerUsed <= caps.CombatPowerBudget,
            $"Combat power {stats.CombatPowerUsed} exceeds the {caps.CombatPowerBudget} budget.");
    }
}
