namespace Sea.Client
{
    /// <summary>
    /// How often the player draws, and against what.
    /// </summary>
    /// <remarks>
    /// A window that is looked at draws on the display's own beat. Handing a 120Hz panel
    /// 60 unsynchronised frames means each one waits a different fraction of a refresh
    /// before it is shown, so a ship crossing the chart at a steady speed arrives in
    /// uneven steps: the frames are cheap and the motion still reads as a stutter. A
    /// window nobody is looking at is capped low instead, because there is no beat worth
    /// keeping and the machine has better uses for the time.
    /// </remarks>
    public static class SeaFrameRatePolicy
    {
        /// <summary>Let the display set the pace rather than a number of our own.</summary>
        public const int DisplayPacedTarget = -1;

        public const int BackgroundTarget = 15;

        public const int VerticalSyncOn = 1;
        public const int VerticalSyncOff = 0;

        public static int TargetForFocus(bool hasFocus) =>
            hasFocus ? DisplayPacedTarget : BackgroundTarget;

        public static int VerticalSyncForFocus(bool hasFocus) =>
            hasFocus ? VerticalSyncOn : VerticalSyncOff;
    }
}
