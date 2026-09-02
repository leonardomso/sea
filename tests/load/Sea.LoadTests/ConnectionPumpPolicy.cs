namespace Sea.LoadTests;

public static class ConnectionPumpPolicy
{
    public const int DefaultShardCount = 128;
    public static readonly TimeSpan PumpInterval = TimeSpan.FromMilliseconds(10);

    public static int ShardIndex(long registration, int shardCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(registration);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shardCount);
        return (int)((ulong)registration % (ulong)shardCount);
    }
}
