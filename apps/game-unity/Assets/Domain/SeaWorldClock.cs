using System;

namespace Sea.Client
{
    public static class SeaWorldClock
    {
        public static ulong Estimate(
            ulong anchorTick,
            uint tickRate,
            double anchorTimeSeconds,
            double currentTimeSeconds)
        {
            var elapsed = Math.Max(0d, currentTimeSeconds - anchorTimeSeconds);
            return anchorTick + (ulong)Math.Floor(elapsed * Math.Max(1u, tickRate));
        }
    }
}
