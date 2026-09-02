using Xunit;

namespace Sea.LoadTests.Tests;

public sealed class LoadActivityPolicyTests
{
    [Fact]
    public void ActiveClientsAreEvenlyStaggeredAcrossTheCourseInterval()
    {
        var delays = Enumerable.Range(0, 1_000)
            .Select(index => LoadActivityPolicy.InitialCourseDelay(index, 1_000))
            .ToArray();

        Assert.Equal(TimeSpan.Zero, delays[0]);
        Assert.Equal(TimeSpan.FromSeconds(5), delays[500]);
        Assert.True(delays[^1] < LoadActivityPolicy.CourseRefreshInterval);
        Assert.Equal(delays.Length, delays.Distinct().Count());
    }

    [Fact]
    public void ScheduleUsesItsStaggerOnlyOnce()
    {
        var schedule = new LoadCourseSchedule(TimeSpan.FromMilliseconds(2_500));

        Assert.Equal(TimeSpan.FromMilliseconds(2_500), schedule.TakeDelay());
        Assert.Equal(
            LoadActivityPolicy.CourseRefreshInterval,
            schedule.TakeDelay());
    }

    [Fact]
    public void LongRunsRefreshCoursesBeforeLongRoutesCanFinish()
    {
        var now = DateTimeOffset.UnixEpoch;

        var delay = LoadActivityPolicy.DelayUntilNextCourse(
            now,
            now + TimeSpan.FromMinutes(1));

        Assert.Equal(TimeSpan.FromSeconds(10), delay);
    }

    [Fact]
    public void FinalDelayStopsAtTheMeasurementBoundary()
    {
        var now = DateTimeOffset.UnixEpoch;

        var delay = LoadActivityPolicy.DelayUntilNextCourse(
            now,
            now + TimeSpan.FromMilliseconds(500));

        Assert.Equal(TimeSpan.FromMilliseconds(500), delay);
    }

    [Fact]
    public void ExpiredRunsDoNotDelayOrIssueAnotherCourse()
    {
        var now = DateTimeOffset.UnixEpoch;

        Assert.Equal(
            TimeSpan.Zero,
            LoadActivityPolicy.DelayUntilNextCourse(now, now));
    }
}
