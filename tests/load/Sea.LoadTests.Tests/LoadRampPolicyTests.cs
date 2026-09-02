using Sea.LoadTests;
using Xunit;

namespace Sea.LoadTests.Tests;

public sealed class LoadRampPolicyTests
{
    [Fact]
    public void FiveThousandClientsAreSpreadAcrossTheConfiguredRamp()
    {
        var ramp = TimeSpan.FromMinutes(5);
        var delays = Enumerable.Range(0, 5_000)
            .Select(index => LoadRampPolicy.DelayFor(index, 5_000, ramp))
            .ToArray();

        Assert.Equal(TimeSpan.Zero, delays[0]);
        Assert.True(delays[^1] < ramp);
        Assert.True(delays[^1] > ramp - TimeSpan.FromMilliseconds(100));
        Assert.True(delays.SequenceEqual(delays.Order()));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void InvalidClientCoordinatesAreRejected(int clientIndex, int totalClients)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoadRampPolicy.DelayFor(clientIndex, totalClients, TimeSpan.Zero));
    }
}
