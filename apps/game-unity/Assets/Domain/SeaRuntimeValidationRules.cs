using UnityEngine;

namespace Sea.Client
{
    public static class SeaRuntimeValidationRules
    {
        public const float CombatObservationRange = 12f;

        private const float SeededStormX = -72f;
        private const float SeededStormY = 3f;
        private const float SeededStormDirectionDegrees = 72f;
        private const float SeededStormSpeed = 1.5f;
        private const float SimulationTicksPerSecond = 10f;

        public static bool ShouldHoldPositionBeforeFire(
            float distance,
            bool targetSelected) =>
            targetSelected && distance <= CombatObservationRange;

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
