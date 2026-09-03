using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void ProcessLootClaimsForMovingShip(
        ReducerContext ctx,
        ShipKinematics mover,
        ulong tick)
    {
        if (mover.FactionCode != (byte)FactionCode.Player)
        {
            return;
        }

        var bounds = SpatialRules.BoundsAround(
            mover.PositionX,
            mover.PositionY,
            LootRules.PickupRadius);
        foreach (var loot in ActiveLootIn(ctx, bounds))
        {
            TryClaimLoot(ctx, loot, mover, tick);
        }
    }

    // The mover sails inside this tick, so its own position comes from the shard it is
    // being integrated in; every rival is read from the thin ShipMovement row, which
    // republishes every tick, rather than the fat Ship row, which only follows on a
    // chunk change and would hand the loot to whoever happens to look closest in a
    // position they left minutes ago.
    private static void TryClaimLoot(
        ReducerContext ctx,
        Loot loot,
        ShipKinematics mover,
        ulong tick)
    {
        var selection = LootRules.Consider(
            new LootClaimSelection(0, float.PositiveInfinity),
            new LootCandidate(
                mover.EntityId,
                CombatRules.Distance(
                    loot.PositionX,
                    loot.PositionY,
                    mover.PositionX,
                    mover.PositionY)));
        var bounds = SpatialRules.BoundsAround(
            loot.PositionX,
            loot.PositionY,
            LootRules.PickupRadius);
        foreach (var movement in ActiveMovementIn(ctx, bounds))
        {
            if (movement.EntityId == mover.EntityId ||
                movement.FactionCode != (byte)FactionCode.Player ||
                !movement.IsAlive)
            {
                continue;
            }

            selection = LootRules.Consider(
                selection,
                new LootCandidate(
                    movement.EntityId,
                    CombatRules.Distance(
                        loot.PositionX,
                        loot.PositionY,
                        movement.PositionX,
                        movement.PositionY)));
        }

        var claimant = selection.EntityId;
        if (claimant == 0 || ctx.Db.Loot.LootId.Find(loot.LootId) is null)
        {
            return;
        }

        ctx.Db.Loot.LootId.Delete(loot.LootId);
        if (string.Equals(loot.LootType, "gold", StringComparison.Ordinal))
        {
            var ownership = ctx.Db.PlayerOwnership.ShipEntityId.Find(claimant) ??
                throw new InvalidOperationException("Loot claimant ownership is missing.");
            AwardGold(ctx, ownership.Owner, loot.Quantity);
        }

        AppendEvent(
            ctx,
            tick,
            claimant,
            "loot_claimed",
            $"loot_id={loot.LootId},type={loot.LootType},quantity={loot.Quantity}");
    }

    private static void SpawnNpcLoot(ReducerContext ctx, Ship npc, ulong tick)
    {
        var definition = Catalog.NpcByArchetypeCode[npc.ArchetypeCode] ??
            throw new InvalidOperationException("Sunk NPC definition is missing.");

        ctx.Db.Loot.Insert(new Loot
        {
            PositionX = npc.PositionX,
            PositionY = npc.PositionY,
            ChunkX = npc.ChunkX,
            ChunkY = npc.ChunkY,
            LootType = "salvage",
            Quantity = Math.Max(4u, definition.GoldReward / 10),
            IsActive = true,
            ExpiresAtTick = tick + LootRules.LifetimeTicks,
            ClaimedByEntityId = 0,
        });
    }
}
