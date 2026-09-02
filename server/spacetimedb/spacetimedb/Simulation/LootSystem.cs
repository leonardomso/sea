using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void ProcessLootClaimsForMovingShip(
        ReducerContext ctx,
        ShipKinematics kinematics)
    {
        if (ctx.Db.Ship.EntityId.Find(kinematics.EntityId) is not Ship ship)
        {
            return;
        }

        CopyKinematics(kinematics, ref ship);
        ProcessLootClaimsForMovingShip(ctx, ship);
    }

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
        ChangeActiveLootCount(ctx, -1);
        if (string.Equals(loot.LootType, "gold", StringComparison.Ordinal))
        {
            var ownership = ctx.Db.PlayerOwnership.ShipEntityId.Find(claimant) ??
                throw new InvalidOperationException("Loot claimant ownership is missing.");
            AwardGold(ctx, ownership.Owner, loot.Quantity);
        }

        AppendEvent(
            ctx,
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
        ChangeActiveLootCount(ctx, 1);
    }

    private static void ChangeActiveLootCount(ReducerContext ctx, int delta)
    {
        var clock = ctx.Db.SimulationClock.Id.Find(1) ??
            throw new InvalidOperationException("Simulation clock is missing.");
        if (delta < 0 && clock.ActiveLootCount == 0)
        {
            throw new InvalidOperationException("Active loot count cannot be negative.");
        }

        clock.ActiveLootCount = delta < 0
            ? clock.ActiveLootCount - 1
            : checked(clock.ActiveLootCount + 1);
        ctx.Db.SimulationClock.Id.Update(clock);
    }
}
