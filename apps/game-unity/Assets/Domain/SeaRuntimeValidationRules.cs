using UnityEngine;

namespace Sea.Client
{
    public readonly struct RuntimeBroadsidePlan
    {
        public RuntimeBroadsidePlan(
            bool canFire,
            string side,
            float desiredHeadingDegrees)
        {
            CanFire = canFire;
            Side = side;
            DesiredHeadingDegrees = desiredHeadingDegrees;
        }

        public bool CanFire { get; }
        public string Side { get; }
        public float DesiredHeadingDegrees { get; }
    }

    public static class SeaRuntimeValidationRules
    {
        public const float CombatObservationRange = 52f;
        public const float CombatApproachRange = 42f;
        public const string RuntimeNpcSubscriptionQuery =
            "SELECT * FROM ship WHERE faction_code = 2";

        private const float SeededStormX = -72f;
        private const float SeededStormY = 3f;
        private const float SeededStormDirectionDegrees = 72f;
        private const float SeededStormSpeed = 1.5f;
        private const float SimulationTicksPerSecond = 10f;
        private const float SafeBroadsideHalfArcDegrees = 44f;

        public static RuntimeBroadsidePlan PlanBroadside(
            Vector2 source,
            float headingDegrees,
            Vector2 target)
        {
            var delta = target - source;
            if (delta.sqrMagnitude <= Mathf.Epsilon)
            {
                return new RuntimeBroadsidePlan(false, string.Empty, headingDegrees);
            }

            var bearing = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
            var portError = Mathf.Abs(Mathf.DeltaAngle(headingDegrees - 90f, bearing));
            var starboardError = Mathf.Abs(Mathf.DeltaAngle(headingDegrees + 90f, bearing));
            var portCanFire = portError <= SafeBroadsideHalfArcDegrees;
            var starboardCanFire = starboardError <= SafeBroadsideHalfArcDegrees;
            if (portCanFire || starboardCanFire)
            {
                var side = portCanFire && (!starboardCanFire || portError <= starboardError)
                    ? "port"
                    : "starboard";
                return new RuntimeBroadsidePlan(true, side, headingDegrees);
            }

            var portHeading = bearing + 90f;
            var starboardHeading = bearing - 90f;
            var portTurn = Mathf.Abs(Mathf.DeltaAngle(headingDegrees, portHeading));
            var starboardTurn = Mathf.Abs(Mathf.DeltaAngle(headingDegrees, starboardHeading));
            return new RuntimeBroadsidePlan(
                false,
                string.Empty,
                portTurn <= starboardTurn ? portHeading : starboardHeading);
        }

        public static bool ShouldRestoreSyntheticFleet(
            int visibleCount,
            int requiredCount) =>
            visibleCount < requiredCount;

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
            Vector2 center,
            int columns = 10,
            float spacing = 6f)
        {
            var row = index / columns;
            var column = index % columns;
            var halfSpan = (columns - 1) * spacing * 0.5f;
            return center + new Vector2(
                column * spacing - halfSpan,
                row * spacing - halfSpan);
        }

        public static Vector2 SeededStormPosition(ulong worldTick)
        {
            var elapsedSeconds = worldTick / SimulationTicksPerSecond;
            var radians = SeededStormDirectionDegrees * Mathf.Deg2Rad;
            return new Vector2(
                WrapMapCoordinate(
                    SeededStormX + Mathf.Sin(radians) * SeededStormSpeed * elapsedSeconds),
                WrapMapCoordinate(
                    SeededStormY + Mathf.Cos(radians) * SeededStormSpeed * elapsedSeconds));
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
