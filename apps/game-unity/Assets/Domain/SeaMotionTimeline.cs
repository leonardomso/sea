using System;
using UnityEngine;

namespace Sea.Client
{
    public readonly struct SeaInterpolationSample
    {
        public SeaInterpolationSample(Vector3 position, float headingDegrees)
        {
            Position = position;
            HeadingDegrees = headingDegrees;
        }

        public Vector3 Position { get; }

        public float HeadingDegrees { get; }
    }

    /// <summary>
    /// Server movement snapshots keyed by simulation tick. Sampling interpolates between the
    /// bracketing ticks and extrapolates at most one tick past the newest sample.
    /// </summary>
    public sealed class SeaMotionTimeline
    {
        public const int Capacity = 8;
        public const double MaximumExtrapolationTicks = 1d;

        private readonly ulong[] ticks = new ulong[Capacity];
        private readonly Vector3[] positions = new Vector3[Capacity];
        private readonly float[] headings = new float[Capacity];
        private int count;

        public bool HasSamples => count > 0;

        public ulong LatestTick => count > 0 ? ticks[count - 1] : 0;

        public void Push(ulong tick, Vector3 position, float headingDegrees)
        {
            if (count > 0 && tick < ticks[count - 1])
            {
                return;
            }

            if (count > 0 && tick == ticks[count - 1])
            {
                positions[count - 1] = position;
                headings[count - 1] = headingDegrees;
                return;
            }

            if (count == Capacity)
            {
                Array.Copy(ticks, 1, ticks, 0, Capacity - 1);
                Array.Copy(positions, 1, positions, 0, Capacity - 1);
                Array.Copy(headings, 1, headings, 0, Capacity - 1);
                count--;
            }

            ticks[count] = tick;
            positions[count] = position;
            headings[count] = headingDegrees;
            count++;
        }

        public SeaInterpolationSample Sample(double tick)
        {
            if (count == 0)
            {
                throw new InvalidOperationException("The motion timeline has no samples.");
            }

            if (tick <= ticks[0])
            {
                return At(0);
            }

            for (var index = 1; index < count; index++)
            {
                if (tick <= ticks[index])
                {
                    return Between(index - 1, index, tick);
                }
            }

            return Extrapolate(tick);
        }

        private SeaInterpolationSample At(int index) => new(positions[index], headings[index]);

        private SeaInterpolationSample Between(int from, int to, double tick)
        {
            var progress = (float)((tick - ticks[from]) / (ticks[to] - ticks[from]));
            return new SeaInterpolationSample(
                Vector3.LerpUnclamped(positions[from], positions[to], progress),
                Mathf.LerpAngle(headings[from], headings[to], progress));
        }

        private SeaInterpolationSample Extrapolate(double tick)
        {
            var last = count - 1;
            if (count < 2)
            {
                return At(last);
            }

            var velocity = (positions[last] - positions[last - 1]) / (ticks[last] - ticks[last - 1]);
            var overshoot = (float)Math.Min(tick - ticks[last], MaximumExtrapolationTicks);
            return new SeaInterpolationSample(positions[last] + velocity * overshoot, headings[last]);
        }
    }
}
