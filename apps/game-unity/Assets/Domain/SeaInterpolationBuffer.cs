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

    public sealed class SeaInterpolationBuffer
    {
        private SeaInterpolationSample previous;
        private SeaInterpolationSample latest;
        private double previousAt;
        private double latestAt;
        private bool hasSample;

        public void Push(Vector3 position, float headingDegrees, double receivedAt)
        {
            if (!double.IsFinite(receivedAt) || (hasSample && receivedAt < latestAt))
            {
                return;
            }

            var sample = new SeaInterpolationSample(position, headingDegrees);
            if (!hasSample)
            {
                previous = sample;
                latest = sample;
                previousAt = receivedAt;
                latestAt = receivedAt;
                hasSample = true;
                return;
            }

            previous = latest;
            previousAt = latestAt;
            latest = sample;
            latestAt = receivedAt;
        }

        public SeaInterpolationSample Sample(double renderedAt, double interpolationDelay)
        {
            if (!hasSample || latestAt <= previousAt)
            {
                return latest;
            }

            var sampleAt = renderedAt - interpolationDelay;
            var progress = Mathf.Clamp01((float)((sampleAt - previousAt) / (latestAt - previousAt)));
            return new SeaInterpolationSample(
                Vector3.LerpUnclamped(previous.Position, latest.Position, progress),
                Mathf.LerpAngle(previous.HeadingDegrees, latest.HeadingDegrees, progress));
        }
    }
}
