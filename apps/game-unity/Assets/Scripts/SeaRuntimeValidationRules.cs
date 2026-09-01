namespace Sea.Client
{
    public static class SeaRuntimeValidationRules
    {
        public const float CombatObservationRange = 12f;

        public static bool ShouldHoldPositionBeforeFire(
            float distance,
            bool targetSelected) =>
            targetSelected && distance <= CombatObservationRange;
    }
}
