using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class ReplicationRulesTests
{
    private static PublishedMotion Sailing(float velocityX, float velocityY) => new()
    {
        Tick = 100,
        PositionX = 10f,
        PositionY = 20f,
        HeadingDegrees = 90f,
        VelocityX = velocityX,
        VelocityY = velocityY,
    };

    [Fact]
    public void AShipHoldingHerCourseIsAlreadyDrawnWhereSheIs()
    {
        var published = Sailing(2f, 0f);

        Assert.False(ReplicationRules.ShouldPublish(published, 14f, 20f, 90f, tick: 102));
    }

    [Fact]
    public void AShipThatHasStrayedFromTheReckoningIsSentAgain()
    {
        var published = Sailing(2f, 0f);

        Assert.True(ReplicationRules.ShouldPublish(published, 14f, 22f, 90f, tick: 102));
    }

    [Fact]
    public void ATurnIsSentEvenWhenThePositionStillLinesUp()
    {
        var published = Sailing(2f, 0f);

        Assert.True(ReplicationRules.ShouldPublish(published, 14f, 20f, 105f, tick: 102));
    }

    [Fact]
    public void TheHeartbeatSendsAShipThatHasBeenQuietTooLong()
    {
        var published = Sailing(2f, 0f);
        var quiet = published.Tick + ReplicationRules.HeartbeatTicks;

        Assert.True(ReplicationRules.ShouldPublish(
            published,
            published.PositionX + 2f * ReplicationRules.HeartbeatTicks,
            published.PositionY,
            published.HeadingDegrees,
            quiet));
    }

    [Fact]
    public void AShipNeverPublishedBeforeIsAlwaysSent()
    {
        Assert.True(ReplicationRules.ShouldPublish(default, 0f, 0f, 0f, tick: 1));
    }

    [Fact]
    public void PublishingRecordsTheVelocityAClientWillReadOutOfTheRow()
    {
        var published = ReplicationRules.Publish(Sailing(0f, 0f), 16f, 26f, 45f, tick: 103);

        Assert.Equal(103UL, published.Tick);
        Assert.Equal(16f, published.PositionX);
        Assert.Equal(26f, published.PositionY);
        Assert.Equal(45f, published.HeadingDegrees);
        Assert.Equal(2f, published.VelocityX, 4);
        Assert.Equal(2f, published.VelocityY, 4);
    }

    [Fact]
    public void TheFirstSnapshotOfAShipCarriesNoVelocity()
    {
        var published = ReplicationRules.Publish(default, 5f, 6f, 12f, tick: 7);

        Assert.Equal(0f, published.VelocityX);
        Assert.Equal(0f, published.VelocityY);
    }

    // A ship sailing a straight line at any speed is drawn exactly by the reckoning, so the
    // only rows she costs are her heartbeats.
    [Fact]
    public void AStraightCourseOnlyCostsItsHeartbeats()
    {
        var published = ReplicationRules.Publish(default, 0f, 0f, 0f, tick: 1);
        var sent = 1;
        for (var tick = 2UL; tick <= 41UL; tick++)
        {
            var x = (tick - 1) * 1.5f;
            if (!ReplicationRules.ShouldPublish(published, x, 0f, 0f, tick))
            {
                continue;
            }

            published = ReplicationRules.Publish(published, x, 0f, 0f, tick);
            sent++;
        }

        Assert.Equal(5, sent);
    }
}
