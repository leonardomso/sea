namespace Sea.Performance;

public static class LoadEvidenceContract
{
    public static void Validate(
        LoadClientMeasurement measurement,
        int expectedClients,
        int expectedActiveClients)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedClients);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedActiveClients);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            expectedActiveClients,
            expectedClients);

        var minimumRetained = (int)Math.Ceiling(expectedClients * 0.999);
        if (measurement.SchemaVersion != 1 ||
            measurement.AttemptedClients != expectedClients ||
            measurement.ConnectedClients != expectedClients ||
            measurement.RetainedClients < minimumRetained ||
            measurement.ActiveClients != expectedActiveClients ||
            measurement.DormantClients != expectedClients - expectedActiveClients ||
            measurement.FailedClients != 0)
        {
            throw new InvalidDataException(
                "Load evidence does not satisfy the requested client population.");
        }
    }
}
