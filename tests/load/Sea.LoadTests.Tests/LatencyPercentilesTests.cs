using Sea.LoadTests;
using Xunit;

namespace Sea.LoadTests.Tests;

public sealed class LatencyPercentilesTests
{
    [Fact]
    public void EmptySamplesProduceZeroPercentiles()
    {
        Assert.Equal(new LatencyPercentiles(0, 0), LatencyPercentiles.Calculate([]));
    }

    [Fact]
    public void NearestRankPercentilesDoNotRoundDown()
    {
        var samples = Enumerable.Range(1, 100).Select(value => (double)value);

        var result = LatencyPercentiles.Calculate(samples);

        Assert.Equal(95, result.P95Milliseconds);
        Assert.Equal(99, result.P99Milliseconds);
    }
}
