using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void ProcessLootClaimsForMovingShip(ReducerContext ctx, Ship movingShip)
    {
        if (!movingShip.IsActive || !movingShip.IsAlive ||
            movingShip.FactionCode != (byte)FactionCode.Player)
        {
            return;
        }

        var bounds = SpatialRules.BoundsAround(
            movingShip.PositionX,
            movingShip.PositionY,
            LootRules.PickupRadius);
        foreach (var loot in ActiveLootIn(ctx, bounds))
        {
            TryClaimLoot(ctx, loot, movingShip);
        }
    }

    private static void TryClaimLoot(ReducerContext ctx, Loot loot, Ship movingShip)
    {
        var selection = new LootClaimSelection(0, float.PositiveInfinity);
        var bounds = SpatialRules.BoundsAround(
            loot.PositionX,
            loot.PositionY,
            LootRules.PickupRadius);
        foreach (var ship in ActiveShipsIn(ctx, bounds))
        {
            var candidateShip = ship.EntityId == movingShip.EntityId
                ? movingShip
                : ship;
            if (candidateShip.FactionCode != (byte)FactionCode.Player ||
                !candidateShip.IsAlive)
            {
                continue;
            }

            selection = LootRules.Consider(
                selection,
                new LootCandidate(
                    candidateShip.EntityId,
                    CombatRules.Distance(
                    loot.PositionX,
                    loot.PositionY,
                    candidateShip.PositionX,
                    candidateShip.PositionY)));
        }

        var claimant = selection.EntityId;
        if (claimant == 0 || ctx.Db.Loot.LootId.Find(loot.LootId) is null)
        {
            return;
        }

        ctx.Db.Loot.LootId.Delete(loot.LootId);
        AwardProgression(
            ctx,
            claimant,
            experience: loot.Quantity / 4,
            gold: string.Equals(loot.LootType, "gold", StringComparison.Ordinal)
                ? loot.Quantity
                : 0);
        AppendEvent(
            ctx,
            claimant,
            "loot_claimed",
            $"loot_id={loot.LootId},type={loot.LootType},quantity={loot.Quantity}");
    }

    private static void SpawnNpcLoot(ReducerContext ctx, Ship npc, ulong tick)
    {
        if (ctx.Db.NpcDefinition.ArchetypeCode.Find(npc.ArchetypeCode) is not
            NpcDefinition definition)
        {
            throw new InvalidOperationException("Sunk NPC definition is missing.");
        }

        ctx.Db.Loot.Insert(new Loot
        {
            PositionX = npc.PositionX,
            PositionY = npc.PositionY,
            ChunkX = npc.ChunkX,
            ChunkY = npc.ChunkY,
            LootType = "gold",
            Quantity = definition.GoldReward,
            IsActive = true,
            ExpiresAtTick = tick + LootRules.LifetimeTicks,
            ClaimedByEntityId = 0,
        });
    }
}
