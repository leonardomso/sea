namespace Sea.LoadTests;

public static class LoadSubscriptionPlan
{
    public static string[] Ownership(string ownerLiteral)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerLiteral);
        return [$"SELECT * FROM player_ownership WHERE owner = {ownerLiteral}"];
    }

    public static string[] ActiveShip(ulong shipEntityId, string ownerLiteral)
    {
        ArgumentOutOfRangeException.ThrowIfZero(shipEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerLiteral);
        return
        [
            $"SELECT * FROM ship WHERE entity_id = {shipEntityId}",
            $"SELECT * FROM ship_movement WHERE entity_id = {shipEntityId}",
            $"SELECT * FROM command_result_event WHERE owner = {ownerLiteral}",
        ];
    }
}
