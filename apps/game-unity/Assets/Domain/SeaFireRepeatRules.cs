namespace Sea.Client
{
    /// <summary>
    /// Holding the fire key keeps the guns going, but a held key must not become a stream of
    /// commands the server will only reject. A repeat is sent when the racks say a volley is
    /// ready and the wire has had the module's own minimum interval to reply.
    /// </summary>
    public static class SeaFireRepeatRules
    {
        /// <summary>Mirrors <c>stat_caps.fireMinIntervalSeconds</c>.</summary>
        public const float MinimumIntervalSeconds = 1f;

        public static bool ShouldRepeat(
            bool held,
            bool loaded,
            float secondsSinceLastRequest) =>
            held && loaded && secondsSinceLastRequest >= MinimumIntervalSeconds;
    }
}
