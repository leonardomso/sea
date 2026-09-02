using Xunit;

namespace Sea.LoadTests.Tests;

public sealed class ConnectionAccessGateTests
{
    [Fact]
    public void ConcurrentSdkAccessIsSerialized()
    {
        var gate = new ConnectionAccessGate();
        var inside = 0;
        var maximumInside = 0;

        Parallel.For(0, 100, _ => gate.Execute(() =>
        {
            var current = Interlocked.Increment(ref inside);
            InterlockedExtensions.Max(ref maximumInside, current);
            Thread.SpinWait(1_000);
            Interlocked.Decrement(ref inside);
        }));

        Assert.Equal(1, maximumInside);
    }

    [Fact]
    public void ReturnValuesFlowThroughTheGate()
    {
        var gate = new ConnectionAccessGate();

        Assert.Equal(42, gate.Execute(() => 42));
    }

    private static class InterlockedExtensions
    {
        public static void Max(ref int target, int value)
        {
            var current = Volatile.Read(ref target);
            while (current < value)
            {
                var observed = Interlocked.CompareExchange(ref target, value, current);
                if (observed == current)
                {
                    return;
                }

                current = observed;
            }
        }
    }
}
