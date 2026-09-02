using Sea.Performance;
using Xunit;

namespace Sea.PerformanceEvidence.Tests;

public sealed class ScaleEvidenceTests
{
    [Fact]
    public void LoadContractAcceptsExactPopulationAndMinimumRetention()
    {
        var measurement = LoadMeasurement(retained: 4_995);

        LoadEvidenceContract.Validate(measurement, 5_000, 1_000);
    }

    [Theory]
    [InlineData(4_999, 5_000, 4_995, 1_000, 4_000, 0)]
    [InlineData(5_000, 4_999, 4_995, 1_000, 4_000, 0)]
    [InlineData(5_000, 5_000, 4_994, 1_000, 4_000, 0)]
    [InlineData(5_000, 5_000, 4_995, 999, 4_001, 0)]
    [InlineData(5_000, 5_000, 4_995, 1_000, 4_000, 1)]
    public void LoadContractRejectsAnInvalidPopulation(
        int attempted,
        int connected,
        int retained,
        int active,
        int dormant,
        int failed)
    {
        var measurement = LoadMeasurement(retained) with
        {
            AttemptedClients = attempted,
            ConnectedClients = connected,
            ActiveClients = active,
            DormantClients = dormant,
            FailedClients = failed,
        };

        Assert.Throws<InvalidDataException>(() =>
            LoadEvidenceContract.Validate(measurement, 5_000, 1_000));
    }

    [Fact]
    public void ResourceMeasurementNormalizesCpuAndUsesWarmupWindows()
    {
        var lines = new[]
        {
            "120%|100MiB / 8GiB",
            "120%|100MiB / 8GiB",
            "120%|102MiB / 8GiB",
            "120%|104MiB / 8GiB",
            "120%|104MiB / 8GiB",
        };

        var measurement = ResourceMeasurement.FromDockerStats(lines, 12);

        Assert.Equal(10, measurement.NormalizedCpuPercent, precision: 3);
        Assert.Equal(4, measurement.MemoryGrowthPercent, precision: 3);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(20, 19)]
    [InlineData(100, 98)]
    public void ReducerPercentilesUseNearestRank(int count, int expectedP99Index)
    {
        var values = Enumerable.Range(1, count).Select(value => (double)value).ToArray();

        var measurement = ReducerTimingMeasurement.Calculate(
            [new ReducerTimingSeries("movement", values, 1)]);

        Assert.Equal(values[expectedP99Index], measurement.P99Milliseconds * 1000);
    }

    [Fact]
    public void SlowestReducerDefinesAggregateTiming()
    {
        var measurement = ReducerTimingMeasurement.Calculate(
        [
            new ReducerTimingSeries("global", [100, 200, 300], 3),
            new ReducerTimingSeries("movement", [1_000, 2_000, 3_000], 3),
        ]);

        Assert.Equal(3, measurement.P95Milliseconds);
        Assert.Equal(3, measurement.P99Milliseconds);
        Assert.Equal(2, measurement.Reducers.Count);
    }

    [Fact]
    public void MissingReducerSamplesCannotProduceEvidence()
    {
        Assert.Throws<InvalidDataException>(() =>
            ReducerTimingMeasurement.Calculate(
                [new ReducerTimingSeries("status", [100], 2)]));
    }

    private static LoadClientMeasurement LoadMeasurement(int retained) => new(
        1,
        5_000,
        5_000,
        retained,
        1_000,
        4_000,
        150,
        250,
        10,
        0);
}
