namespace Sea.Server;

public static class RangeRules
{
    /// <summary>How far a captain can see, in squares (SEA_5 §7.5).</summary>
    public const float ViewDistanceSquares = 60f;

    /// <summary>
    /// Interest is subscribed a little wider than sight so a ship is already on
    /// the client when it becomes visible (SEA_5 §7.5).
    /// </summary>
    public const float SubscriptionMarginSquares = 5f;

    public const float SubscriptionRadiusSquares = ViewDistanceSquares + SubscriptionMarginSquares;
}
