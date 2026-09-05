using UnityEngine;

namespace Sea.Client
{
    public readonly struct RuntimeFirePlan
    {
        public RuntimeFirePlan(bool canFire, float desiredHeadingDegrees)
        {
            CanFire = canFire;
            DesiredHeadingDegrees = desiredHeadingDegrees;
        }

        public bool CanFire { get; }
        public float DesiredHeadingDegrees { get; }
    }

    public static class SeaRuntimeValidationRules
    {
        public const float CombatObservationRange = 52f;
        public const float CombatApproachRange = 42f;
        public const string RuntimeNpcSubscriptionQuery =
            "SELECT * FROM ship WHERE faction_code = 2";
        public const string RuntimeMovementSubscriptionQuery =
            "SELECT * FROM ship_movement WHERE is_active = true";

        // The storm entity 13 carries in maps.json. The probe sails to where the module will
        // have put it, so all four have to match that row; change one and change the other.
        private const float SeededStormX = 56f;
        private const float SeededStormY = 206f;
        private const float SeededStormDirectionDegrees = 72f;
        private const float SeededStormSpeed = 0.5f;
        private const float SimulationTicksPerSecond = 10f;
        /// <summary>
        /// There is no firing arc left: the magazine bears in every direction, so the only thing
        /// the probe still plans is the approach heading that puts the target dead ahead.
        /// </summary>
        public static RuntimeFirePlan PlanFire(
            Vector2 source,
            float headingDegrees,
            Vector2 target)
        {
            var delta = target - source;
            if (delta.sqrMagnitude <= Mathf.Epsilon)
            {
                return new RuntimeFirePlan(false, headingDegrees);
            }

            // A chart bearing: north is -y, the same way SeededStormPosition below already
            // reckoned it. One file held both conventions until this line was fixed.
            var bearing = Mathf.Atan2(delta.x, 0f - delta.y) * Mathf.Rad2Deg;
            return new RuntimeFirePlan(true, bearing);
        }

        /// <summary>
        /// Proof that a volley actually left the racks. Ammunition is unlimited in Milestone 1, so
        /// the magazine is the only thing a fired shot moves on the ship row: either a ready volley
        /// is gone or the module stamped a newer shot tick on the hull.
        /// </summary>
        public static bool HasLaunchedVolley(
            uint volleysBeforeFiring,
            uint volleysNow,
            ulong shotTickBeforeFiring,
            ulong shotTickNow) =>
            volleysNow < volleysBeforeFiring || shotTickNow > shotTickBeforeFiring;

        /// <summary>
        /// The presentation benchmark seeds its own fleet up to the platform ship budget, so a
        /// live world would cost it hulls it has already counted: one real ship inside the ring
        /// pushes a synthetic one past the limit, the probe never reaches its required count and
        /// reseeds forever instead of measuring. The benchmark therefore sails alone.
        /// </summary>
        public static bool ShouldConnectOnStart(
            bool connectOnStart,
            bool presentationPerformanceRequested) =>
            connectOnStart && !presentationPerformanceRequested;

        public static bool ShouldRestoreSyntheticFleet(
            int visibleCount,
            int requiredCount) =>
            visibleCount < requiredCount;

        public static bool HasObservedStop(
            bool stopRequested,
            float travelled,
            float speedBeforeStop,
            float currentSpeed,
            bool isMoving,
            bool isStopping)
        {
            if (!stopRequested || travelled <= 0.1f || speedBeforeStop <= 0f)
            {
                return false;
            }

            return isStopping && currentSpeed < speedBeforeStop ||
                !isMoving && currentSpeed <= Mathf.Epsilon;
        }

        public static bool CanIssueTacticalCommand(
            bool isActive,
            bool isAlive,
            byte modeCode) =>
            isActive && isAlive && modeCode == 0;

        public static bool ShouldRetryTacticalCommand(
            bool observed,
            float requestedAt,
            float now) =>
            !observed && now - requestedAt >= 2f;

        public static bool HasStormExposure(byte exposureCode) =>
            (exposureCode & 1) != 0;

        public static Vector2 SyntheticFleetPosition(
            int index,
            int totalCount,
            Vector2 center,
            int columns = 10,
            float spacing = 6f)
        {
            var row = index / columns;
            var column = index % columns;
            var populatedColumns = Mathf.Min(columns, totalCount);
            var rows = Mathf.CeilToInt(totalCount / (float)columns);
            var halfWidth = (populatedColumns - 1) * spacing * 0.5f;
            var halfHeight = (rows - 1) * spacing * 0.5f;
            return center + new Vector2(
                column * spacing - halfWidth,
                row * spacing - halfHeight);
        }

        public static Vector2 SeededStormPosition(ulong worldTick)
        {
            var elapsedSeconds = worldTick / SimulationTicksPerSecond;
            var radians = SeededStormDirectionDegrees * Mathf.Deg2Rad;
            // Mirrors the module's TacticalRules.MoveStorm. Y is subtracted because a bearing is
            // a compass bearing and north is -y on a chart whose origin is the top-left corner;
            // adding cos here drove the predicted storm the opposite way to the real one.
            return new Vector2(
                WrapMapCoordinate(
                    SeededStormX + Mathf.Sin(radians) * SeededStormSpeed * elapsedSeconds),
                WrapMapCoordinate(
                    SeededStormY - Mathf.Cos(radians) * SeededStormSpeed * elapsedSeconds));
        }

        private static float WrapMapCoordinate(float value)
        {
            var span = SeaChartCoordinates.MapMaximum - SeaChartCoordinates.MapMinimum;
            while (value > SeaChartCoordinates.MapMaximum)
            {
                value -= span;
            }

            while (value < SeaChartCoordinates.MapMinimum)
            {
                value += span;
            }

            return value;
        }
    }
}
