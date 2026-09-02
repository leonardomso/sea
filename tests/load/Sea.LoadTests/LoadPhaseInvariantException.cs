namespace Sea.LoadTests;

public sealed class LoadPhaseInvariantException(string phase, string message)
    : InvalidOperationException(message)
{
    public string Phase { get; } = phase;
}
