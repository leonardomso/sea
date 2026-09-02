namespace Sea.LoadTests;

public sealed class LoadCourseSchedule
{
    private readonly TimeSpan initialDelay;
    private int firstCoursePending = 1;

    public LoadCourseSchedule(TimeSpan initialDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(initialDelay, TimeSpan.Zero);
        this.initialDelay = initialDelay;
    }

    public TimeSpan TakeDelay()
    {
        return Interlocked.Exchange(ref firstCoursePending, 0) == 1
            ? initialDelay
            : LoadActivityPolicy.CourseRefreshInterval;
    }
}
