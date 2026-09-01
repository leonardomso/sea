using UnityEngine;

namespace Sea.Client
{
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

        public static bool ShouldHoldPositionBeforeFire(
            float distance,
            bool targetSelected) =>
            targetSelected && distance <= CombatObservationRange;

        public static bool ShouldRestoreSyntheticFleet(
            int visibleCount,
            int requiredCount) =>
            visibleCount < requiredCount;

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
