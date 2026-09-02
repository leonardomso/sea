using Xunit;

namespace Sea.LoadTests.Tests;

public sealed class ConnectionPumpPolicyTests
{
    [Theory]
    [InlineData(1, 8, 1)]
    [InlineData(8, 8, 0)]
    [InlineData(9, 8, 1)]
    [InlineData(5_000, 8, 0)]
    public void RegistrationsAreDistributedDeterministically(
        long registration,
        int shardCount,
        int expected)
    {
        Assert.Equal(expected, ConnectionPumpPolicy.ShardIndex(registration, shardCount));
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(-1, 8)]
    [InlineData(1, 0)]
    public void InvalidPumpConfigurationIsRejected(long registration, int shardCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ConnectionPumpPolicy.ShardIndex(registration, shardCount));
    }

    [Fact]
    public void DefaultPumpBoundsFiveThousandConnectionsPerShard()
    {
        var counts = new int[ConnectionPumpPolicy.DefaultShardCount];
        for (var registration = 1; registration <= 5_000; registration++)
        {
            counts[ConnectionPumpPolicy.ShardIndex(
                registration,
                ConnectionPumpPolicy.DefaultShardCount)]++;
        }

        Assert.Equal(128, ConnectionPumpPolicy.DefaultShardCount);
        Assert.Equal(TimeSpan.FromMilliseconds(10), ConnectionPumpPolicy.PumpInterval);
        Assert.InRange(counts.Max(), 39, 40);
        Assert.InRange(counts.Min(), 39, 40);
    }
}
