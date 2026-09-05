using UnityEngine;

namespace Sea.Client
{
    public static class SeaVolleyPresentationRules
    {
        /// <summary>
        /// Mirrors <c>CombatRules.FrontArcHalfDegrees</c> and
        /// <c>CombatRules.BackArcThresholdDegrees</c>. The server owns the damage; the HUD only
        /// names the face so the captain can read why a shot bit or bounced.
        /// </summary>
        public const float FrontArcHalfDegrees = 45f;

        public const float BackArcThresholdDegrees = 135f;

        public static float Progress(ulong firedAtTick, ulong impactAtTick, ulong currentTick)
        {
            if (impactAtTick <= firedAtTick)
            {
                return currentTick >= impactAtTick ? 1f : 0f;
            }

            var elapsed = currentTick <= firedAtTick ? 0ul : currentTick - firedAtTick;
            var duration = impactAtTick - firedAtTick;
            return Mathf.Clamp01((float)elapsed / duration);
        }

        /// <summary>
        /// Guns bear in every direction, so the muzzle sits wherever the target does: this is the
        /// firing ship's local offset along the bearing to what it is shooting at.
        /// </summary>
        public static Vector3 LocalMuzzleOffset(
            float headingDegrees,
            Vector2 source,
            Vector2 target,
            float distance)
        {
            var delta = target - source;
            if (delta.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector3.forward * Mathf.Abs(distance);
            }

            // A chart bearing, so north is the smaller y and the y term is negated. The offset
            // below is not: it is an angle off the bow in the ship's own space, where +z is
            // ahead, and Sin/Cos of it are already the right way round.
            var bearing = Mathf.Atan2(delta.x, 0f - delta.y) * Mathf.Rad2Deg;
            var offset = Mathf.DeltaAngle(headingDegrees, bearing) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(offset), 0f, Mathf.Cos(offset)) * Mathf.Abs(distance);
        }

        /// <summary>
        /// The armour face a shot from <paramref name="source"/> meets, read from the target's
        /// own heading exactly as <c>CombatRules.ResolveFacing</c> reads it server-side.
        /// </summary>
        public static string ArmorFaceAt(
            float targetHeadingDegrees,
            Vector2 target,
            Vector2 source)
        {
            var delta = source - target;
            if (delta.sqrMagnitude <= Mathf.Epsilon)
            {
                return "sides";
            }

            // The same chart compass CombatRules.ResolveFacing reads: north is -y.
            var bearingToSource = Mathf.Atan2(delta.x, 0f - delta.y) * Mathf.Rad2Deg;
            var offset = Mathf.Abs(Mathf.DeltaAngle(targetHeadingDegrees, bearingToSource));
            if (offset <= FrontArcHalfDegrees)
            {
                return "front";
            }

            return offset >= BackArcThresholdDegrees ? "back" : "sides";
        }
    }
}
