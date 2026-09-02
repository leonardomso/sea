namespace Sea.LoadTests;

public static class LoadActivityPolicy
{
    public static readonly TimeSpan CourseRefreshInterval = TimeSpan.FromSeconds(10);

    public static TimeSpan InitialCourseDelay(int clientIndex, int activeClients)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(clientIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(activeClients);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(clientIndex, activeClients);
        return TimeSpan.FromTicks(
            CourseRefreshInterval.Ticks * clientIndex / activeClients);
    }

    public static TimeSpan DelayUntilNextCourse(
        DateTimeOffset now,
        DateTimeOffset stopAt)
    {
        var remaining = stopAt - now;
        if (remaining <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return remaining < CourseRefreshInterval
            ? remaining
            : CourseRefreshInterval;
    }
}
