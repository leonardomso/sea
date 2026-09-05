namespace Sea.Server;

/// <summary>
/// The set of the current (SEA_5 §5.2). A hull is carried whether or not she has a course,
/// so a ship left at anchor in a stream ends the watch somewhere else, and the carry stops
/// at a shore: she fetches up on the last water she was on rather than being pushed through
/// the beach.
/// </summary>
/// <remarks>
/// A tick's drift is taken whole or not at all. Taking the part of it that stays wet would
/// let a hull creep a fraction of a square into land over a few ticks, and the mask is the
/// only thing movement means by land, so nothing further along would stop her. Refusing the
/// step outright costs one lookup a tick and holds the invariant exactly.
/// </remarks>
public static class DriftRules
{
    /// <summary>Where the current leaves a hull after <paramref name="deltaSeconds"/>.</summary>
    /// <param name="mask">The chart's land. Anything off the map reads as land, so the border
    /// needs no separate check: drift cannot push a hull past it.</param>
    public static (float X, float Y) Drift(
        float x,
        float y,
        float velocityX,
        float velocityY,
        float deltaSeconds,
        LandMask mask)
    {
        ArgumentNullException.ThrowIfNull(mask);
        if (velocityX == 0f && velocityY == 0f)
        {
            return (x, y);
        }

        var driftedX = Math.Clamp(
            x + (velocityX * deltaSeconds), WorldRules.MapMin, WorldRules.MapMax);
        var driftedY = Math.Clamp(
            y + (velocityY * deltaSeconds), WorldRules.MapMin, WorldRules.MapMax);
        return mask.IsLand(driftedX, driftedY) ? (x, y) : (driftedX, driftedY);
    }
}
