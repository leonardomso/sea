namespace Sea.Client
{
    /// <summary>
    /// Whether the chart camera follows the player's ship. Manual camera input detaches
    /// the camera so the player can scout ahead; it stays where it was pushed until the
    /// player asks for the ship again (Space or the recenter button).
    /// </summary>
    public sealed class SeaChartFollowState
    {
        public bool IsFollowing { get; private set; } = true;

        public void Interrupt() => IsFollowing = false;

        public void Resume() => IsFollowing = true;
    }
}
