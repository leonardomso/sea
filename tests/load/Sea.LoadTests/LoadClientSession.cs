namespace Sea.LoadTests;

public sealed class LoadClientSession
{
    private readonly LoadCourseSchedule courseSchedule;

    public LoadClientSession(
        SpacetimeLoadClient client,
        LoadWorkloadPlan plan,
        int activeClients)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        InitialCourseDelay = plan.Mode == LoadClientMode.Sailing
            ? LoadActivityPolicy.InitialCourseDelay(plan.ClientIndex, activeClients)
            : TimeSpan.Zero;
        courseSchedule = new LoadCourseSchedule(InitialCourseDelay);
    }

    public SpacetimeLoadClient Client { get; }

    public LoadWorkloadPlan Plan { get; }

    public TimeSpan InitialCourseDelay { get; }

    public TimeSpan TakeCourseDelay()
    {
        return courseSchedule.TakeDelay();
    }
}
