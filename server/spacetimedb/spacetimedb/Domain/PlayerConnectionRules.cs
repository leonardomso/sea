namespace Sea.Server;

public enum PlayerLoadSource
{
    ClientLifecycle,
    ExplicitLoad,
}

public static class PlayerConnectionRules
{
    public static bool MayCreatePlayer(PlayerLoadSource source) =>
        source == PlayerLoadSource.ExplicitLoad;
}
