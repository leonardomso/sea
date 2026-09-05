using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class PathfindingRulesTests
{
    private const int Size = 64;

    private static LandMask WithWall(int wallX, int gapY, int gapHeight)
    {
        var bits = new ulong[LandMask.WordCount(Size)];
        for (var cellY = 0; cellY < Size; cellY++)
        {
            if (cellY >= gapY && cellY < gapY + gapHeight)
            {
                continue;
            }

            var index = (cellY * Size) + wallX;
            bits[index >> 6] |= 1UL << (index & 63);
        }

        return new LandMask(Size, bits);
    }

    private static LandMask OpenSea() => new(Size, new ulong[LandMask.WordCount(Size)]);

    private static LandMask WalledLake()
    {
        var bits = new ulong[LandMask.WordCount(Size)];
        var mask = new LandMask(Size, bits);
        void Fill(int fromX, int fromY, int toX, int toY)
        {
            for (var cellY = fromY; cellY <= toY; cellY++)
            {
                for (var cellX = fromX; cellX <= toX; cellX++)
                {
                    var index = (cellY * Size) + cellX;
                    bits[index >> 6] |= 1UL << (index & 63);
                }
            }
        }

        Fill(40, 40, 50, 50);          // an island
        Fill(44, 44, 46, 46);          // ... with a lake cut back out of it
        for (var cellY = 44; cellY <= 46; cellY++)
        {
            for (var cellX = 44; cellX <= 46; cellX++)
            {
                var index = (cellY * Size) + cellX;
                bits[index >> 6] &= ~(1UL << (index & 63));
            }
        }

        return mask;
    }

    [Fact]
    public void OpenWaterIsOneSegment()
    {
        Span<RouteWaypoint> route = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];
        var scratch = new PathfindingScratch(Size);

        var outcome = PathfindingRules.TryBuildRoute(
            OpenSea(), scratch, 4f, 4f, 60f, 60f, route, out var count);

        Assert.Equal(PathOutcome.Direct, outcome);
        Assert.Equal(1, count);
        Assert.Equal(60f, route[0].X, 4);
        Assert.Equal(60f, route[0].Y, 4);
    }

    [Fact]
    public void AWallIsRoundedThroughItsGap()
    {
        Span<RouteWaypoint> route = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];
        var scratch = new PathfindingScratch(Size);
        var mask = WithWall(wallX: 32, gapY: 50, gapHeight: 4);

        var outcome = PathfindingRules.TryBuildRoute(
            mask, scratch, 4f, 4f, 60f, 4f, route, out var count);

        Assert.Equal(PathOutcome.Routed, outcome);
        Assert.InRange(count, 2, RouteRules.MaximumWaypoints);

        var fromX = 4f;
        var fromY = 4f;
        for (var index = 0; index < count; index++)
        {
            Assert.True(
                mask.SegmentIsClear(fromX, fromY, route[index].X, route[index].Y),
                $"leg {index} crosses land");
            fromX = route[index].X;
            fromY = route[index].Y;
        }

        Assert.Equal(60f, route[count - 1].X, 3);
        Assert.Equal(4f, route[count - 1].Y, 3);
    }

    [Fact]
    public void ALandLockedLakeIsRefused()
    {
        Span<RouteWaypoint> route = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];
        var scratch = new PathfindingScratch(Size);

        var outcome = PathfindingRules.TryBuildRoute(
            WalledLake(), scratch, 4f, 4f, 45.5f, 45.5f, route, out var count);

        Assert.Equal(PathOutcome.NoPath, outcome);
        Assert.Equal(0, count);
    }

    [Fact]
    public void AGoalOnLandIsRefused()
    {
        Span<RouteWaypoint> route = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];
        var scratch = new PathfindingScratch(Size);

        var outcome = PathfindingRules.TryBuildRoute(
            WithWall(32, 50, 4), scratch, 4f, 4f, 32.5f, 10.5f, route, out var count);

        Assert.Equal(PathOutcome.NoPath, outcome);
        Assert.Equal(0, count);
    }

    [Fact]
    public void TheSameRequestTwiceGivesTheSameRoute()
    {
        Span<RouteWaypoint> first = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];
        Span<RouteWaypoint> second = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];
        var scratch = new PathfindingScratch(Size);
        var mask = WithWall(32, 50, 4);

        PathfindingRules.TryBuildRoute(mask, scratch, 4f, 4f, 60f, 4f, first, out var firstCount);
        PathfindingRules.TryBuildRoute(mask, scratch, 4f, 4f, 60f, 4f, second, out var secondCount);

        Assert.Equal(firstCount, secondCount);
        for (var index = 0; index < firstCount; index++)
        {
            Assert.Equal(first[index], second[index]);
        }
    }

    [Fact]
    public void ScratchIsReusedRatherThanReallocated()
    {
        var scratch = new PathfindingScratch(Size);
        var mask = WithWall(32, 50, 4);
        Span<RouteWaypoint> route = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];

        PathfindingRules.TryBuildRoute(mask, scratch, 4f, 4f, 60f, 4f, route, out _);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var run = 0; run < 50; run++)
        {
            PathfindingRules.TryBuildRoute(mask, scratch, 4f, 4f, 60f, 4f, route, out _);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
