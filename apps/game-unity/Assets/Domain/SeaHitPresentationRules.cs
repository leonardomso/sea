using System.Collections.Generic;
using System.Globalization;

namespace Sea.Client
{
    /// <summary>
    /// One volley's worth of numbers, waiting for its cannonball to arrive.
    /// </summary>
    public readonly struct SeaPendingHit
    {
        public SeaPendingHit(
            ulong attackerEntityId,
            ulong defenderEntityId,
            uint damage,
            bool isCritical,
            byte face,
            float impactAtSeconds)
        {
            AttackerEntityId = attackerEntityId;
            DefenderEntityId = defenderEntityId;
            Damage = damage;
            IsCritical = isCritical;
            Face = face;
            ImpactAtSeconds = impactAtSeconds;
        }

        public ulong AttackerEntityId { get; }

        public ulong DefenderEntityId { get; }

        public uint Damage { get; }

        public bool IsCritical { get; }

        public byte Face { get; }

        /// <summary>When the ball reaches the hull, on the client's own unscaled clock.</summary>
        public float ImpactAtSeconds { get; }
    }

    /// <summary>
    /// When a shot's number is allowed to appear (SEA_5 8.3).
    /// </summary>
    /// <remarks>
    /// The server settled the whole volley on the tick the trigger was pulled and told the client
    /// how long the ball is in the air. Drawing the number the moment the row arrives puts it on
    /// screen before the shot lands, which reads as a hit that missed; waiting the flight out puts
    /// the ball and its number together. Nothing here changes an outcome -- the hull already lost
    /// the hit points -- so a client that lags simply sees the number late.
    /// </remarks>
    public static class SeaHitPresentationRules
    {
        /// <summary>
        /// The longest a number is ever held back. A ball crosses the longest gun on the chart in
        /// three quarters of a second (SEA_5 7.1 and 8.3); anything past this came from a corrupt
        /// row or a clock that jumped, and a captain would rather see the number than wait.
        /// </summary>
        public const float MaximumHoldSeconds = 1f;

        public static float ImpactAt(float raisedAtSeconds, float flightSeconds)
        {
            if (!(flightSeconds > 0f))
            {
                return raisedAtSeconds;
            }

            return raisedAtSeconds + (flightSeconds < MaximumHoldSeconds
                ? flightSeconds
                : MaximumHoldSeconds);
        }

        public static bool IsDue(float impactAtSeconds, float nowSeconds) =>
            nowSeconds >= impactAtSeconds;

        /// <summary>
        /// What a captain reads over the hull she just hit. A critical is marked rather than
        /// coloured, because the number has to survive being read on a minimap-sized label.
        /// </summary>
        public static string DamageLabel(uint damage, bool isCritical)
        {
            var number = damage.ToString(CultureInfo.InvariantCulture);
            return isCritical ? "-" + number + "!" : "-" + number;
        }

        /// <summary>The damage a shot has to do before the jolt it gives is half of a full one.</summary>
        private const float HalfShockDamage = 120f;

        /// <summary>
        /// How hard a hull is thrown about by one volley, from nothing to a full jolt. It
        /// saturates rather than scaling, because a hull cannot be shaken twice as hard by twice
        /// the damage without leaving the water: what a captain has to be able to read off the
        /// jolt is that she was hit and roughly how badly, not the number itself.
        /// </summary>
        public static float Shock(uint damage, bool isCritical)
        {
            if (damage == 0u)
            {
                return 0f;
            }

            var shock = damage / (damage + HalfShockDamage);
            if (isCritical)
            {
                shock *= 1.25f;
            }

            return shock > 1f ? 1f : shock;
        }
    }

    /// <summary>
    /// The hits whose cannonballs are still in the air, in the order they will land.
    /// </summary>
    /// <remarks>
    /// A fight is a handful of volleys a second at the very most, so this is a list rather than a
    /// heap: the cost of keeping it ordered is smaller than the cost of an allocation per shot,
    /// and it is drained from the front on every frame either way.
    /// </remarks>
    public sealed class SeaHitQueue
    {
        private readonly List<SeaPendingHit> pending = new();

        public int Count => pending.Count;

        public void Enqueue(SeaPendingHit hit)
        {
            var index = pending.Count;
            while (index > 0 && pending[index - 1].ImpactAtSeconds > hit.ImpactAtSeconds)
            {
                index--;
            }

            pending.Insert(index, hit);
        }

        /// <summary>
        /// Takes the next hit whose ball has landed, or reports that none has. Call it until it
        /// says no: several volleys can come due on one frame.
        /// </summary>
        public bool TryTakeDue(float nowSeconds, out SeaPendingHit hit)
        {
            if (pending.Count > 0 &&
                SeaHitPresentationRules.IsDue(pending[0].ImpactAtSeconds, nowSeconds))
            {
                hit = pending[0];
                pending.RemoveAt(0);
                return true;
            }

            hit = default;
            return false;
        }

        /// <summary>
        /// Whether a hull still has a shot in the air. A hull the server has already sunk waits
        /// for it before she goes down, so she never sinks ahead of the ball that sank her
        /// (SEA_5 8.3).
        /// </summary>
        public bool HasShotInTheAir(ulong defenderEntityId)
        {
            foreach (var hit in pending)
            {
                if (hit.DefenderEntityId == defenderEntityId)
                {
                    return true;
                }
            }

            return false;
        }

        public void Clear() => pending.Clear();
    }
}
