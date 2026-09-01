namespace Sea.Server;

public static class EventRetentionRules
{
    public const ulong LifetimeTicks = 100;

    public static bool IsExpired(ulong expiresAtTick, ulong currentTick) =>
        currentTick > expiresAtTick;
}
