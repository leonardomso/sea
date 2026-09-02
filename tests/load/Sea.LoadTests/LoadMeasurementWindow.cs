namespace Sea.LoadTests;

public sealed record LoadMeasurementWindow(DateTimeOffset StartsAt)
{
    public bool Contains(DateTimeOffset timestamp) => timestamp >= StartsAt;
}
