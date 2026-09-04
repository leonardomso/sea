using System;

namespace Sea.Client
{
    /// <summary>
    /// Continuous estimate of the server simulation tick, driven by the ticks stamped on
    /// incoming movement snapshots. Rendering trails the estimate by RenderDelayTicks so the
    /// samples bracketing the render tick have normally already arrived.
    /// </summary>
    public sealed class SeaSnapshotClock
    {
        // Mirrors the server WorldRules.TickRateHz until the world contract module owns it.
        public const uint DefaultTickRate = 10;
        // Every ship now sails on every tick, so one tick of delay is enough to have the
        // bracketing samples in hand. This is the whole of what a captain waits to see a
        // hostile move, so it is bought back as soon as the server can afford it. The local
        // ship does not pay it at all - it is dead reckoned forward.
        public const double RenderDelayTicks = 1d;
        public const double ForwardSnapTicks = 1d;
        public const double BackwardSnapTicks = 20d;
        public const double SlewTicksPerSecond = 0.5d;
        public const double DriftTicksPerSecond = 0.02d;

        private readonly double ticksPerSecond;
        private double offsetTicks;
        private double targetOffsetTicks;
        private double slewedAt;
        private double observedAt;

        public SeaSnapshotClock(uint tickRate)
        {
            ticksPerSecond = Math.Max(1u, tickRate);
        }

        public bool IsRunning { get; private set; }

        public void Observe(ulong tick, double now)
        {
            var observed = tick - now * ticksPerSecond;
            var lead = observed - targetOffsetTicks;
            if (!IsRunning || lead > ForwardSnapTicks || lead < -BackwardSnapTicks)
            {
                offsetTicks = observed;
                targetOffsetTicks = observed;
                slewedAt = now;
                observedAt = now;
                IsRunning = true;
                return;
            }

            // Track the earliest-arriving samples, letting the estimate drift down slowly so a
            // fast local clock cannot push rendering permanently ahead of the data.
            var drifted = targetOffsetTicks - Math.Max(0d, now - observedAt) * DriftTicksPerSecond;
            targetOffsetTicks = Math.Max(observed, drifted);
            observedAt = now;
        }

        public double ServerTick(double now)
        {
            if (!IsRunning)
            {
                return 0d;
            }

            var step = Math.Max(0d, now - slewedAt) * SlewTicksPerSecond;
            slewedAt = now;
            offsetTicks = offsetTicks < targetOffsetTicks
                ? Math.Min(targetOffsetTicks, offsetTicks + step)
                : Math.Max(targetOffsetTicks, offsetTicks - step);
            return now * ticksPerSecond + offsetTicks;
        }

        public double RenderTick(double now) => RenderTickFrom(ServerTick(now));

        // Advancing the estimate is a side effect, so a caller that needs both the server tick
        // and the render tick in one frame reads the server tick once and converts it here.
        public static double RenderTickFrom(double serverTick) => serverTick - RenderDelayTicks;
    }
}
