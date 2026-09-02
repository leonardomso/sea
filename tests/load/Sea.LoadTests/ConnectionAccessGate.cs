namespace Sea.LoadTests;

public sealed class ConnectionAccessGate
{
    private readonly Lock sync = new();

    public void Execute(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (sync)
        {
            action();
        }
    }

    public T Execute<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (sync)
        {
            return action();
        }
    }
}
