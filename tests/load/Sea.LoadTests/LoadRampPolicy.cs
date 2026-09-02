namespace Sea.LoadTests;

public static class LoadRampPolicy
{
    public static TimeSpan DelayFor(
        int clientIndex,
        int totalClients,
        TimeSpan rampDuration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(clientIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalClients);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(clientIndex, totalClients);
        ArgumentOutOfRangeException.ThrowIfLessThan(rampDuration, TimeSpan.Zero);

        if (clientIndex == 0 || totalClients == 1 || rampDuration == TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromTicks(
            rampDuration.Ticks * clientIndex / totalClients);
    }
}
