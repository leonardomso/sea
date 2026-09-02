namespace Sea.LoadTests;

public sealed class LoadPhaseTimeoutException(string phase)
    : TimeoutException($"Load client timed out during {phase}.")
{
    public string Phase { get; } = phase;
}
