namespace Sea.Client
{
    public static class SeaFrameRatePolicy
    {
        public const int ForegroundTarget = 60;
        public const int BackgroundTarget = 15;

        public static int TargetForFocus(bool hasFocus) =>
            hasFocus ? ForegroundTarget : BackgroundTarget;
    }
}
