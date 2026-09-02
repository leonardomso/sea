using Xunit;

namespace Sea.LoadTests.Tests;

public sealed class LoadMeasurementWindowTests
{
    [Fact]
    public void ExcludesWarmupAndIncludesTheMeasurementBoundary()
    {
        var start = DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(30);
        var window = new LoadMeasurementWindow(start);

        Assert.False(window.Contains(start - TimeSpan.FromTicks(1)));
        Assert.True(window.Contains(start));
        Assert.True(window.Contains(start + TimeSpan.FromMinutes(1)));
    }
}
