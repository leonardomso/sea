using System;

namespace Sea.Client
{
    public readonly struct SeaChunk : IEquatable<SeaChunk>
    {
        public SeaChunk(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(SeaChunk other) => X == other.X && Y == other.Y;

        public override bool Equals(object value) => value is SeaChunk other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);
    }

    public sealed class SeaSpatialInterest
    {
        public const double DebounceSeconds = 0.15d;

        private SeaChunk active;
        private SeaChunk pending;
        private SeaChunk requested;
        private double pendingAtSeconds;
        private bool hasActive;
        private bool hasPending;
        private bool hasRequested;

        public void Observe(int chunkX, int chunkY, double nowSeconds)
        {
            if (!double.IsFinite(nowSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(nowSeconds));
            }

            var observed = new SeaChunk(chunkX, chunkY);
            if (hasActive && observed.Equals(active))
            {
                hasPending = false;
                return;
            }

            if (hasRequested && observed.Equals(requested))
            {
                hasPending = false;
                return;
            }

            if (hasPending && observed.Equals(pending))
            {
                return;
            }

            pending = observed;
            pendingAtSeconds = hasActive ? nowSeconds + DebounceSeconds : nowSeconds;
            hasPending = true;
        }

        public bool TryTakeDue(double nowSeconds, out SeaChunk chunk)
        {
            if (hasPending && nowSeconds >= pendingAtSeconds)
            {
                requested = pending;
                hasRequested = true;
                hasPending = false;
                chunk = requested;
                return true;
            }

            chunk = default;
            return false;
        }

        public void Applied(SeaChunk chunk)
        {
            active = chunk;
            hasActive = true;
            if (hasRequested && requested.Equals(chunk))
            {
                hasRequested = false;
            }
        }

        public void Failed(SeaChunk chunk, double nowSeconds)
        {
            if (hasRequested && requested.Equals(chunk))
            {
                hasRequested = false;
            }

            pending = chunk;
            pendingAtSeconds = nowSeconds + DebounceSeconds;
            hasPending = true;
        }

        public void Reset()
        {
            hasActive = false;
            hasPending = false;
            hasRequested = false;
        }
    }
}
