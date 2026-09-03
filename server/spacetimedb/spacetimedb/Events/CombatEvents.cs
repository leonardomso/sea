using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static Inventory? FindInventory(ReducerContext ctx, ulong shipEntityId, string itemId)
    {
        foreach (var item in ctx.Db.Inventory.ByShip.Filter(shipEntityId))
        {
            if (string.Equals(item.ItemId, itemId, StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }

    private static void ProcessLootExpiry(ReducerContext ctx, ulong tick)
    {
        foreach (var loot in ctx.Db.Loot.ByLootExpiryDue.Filter(
                     (true, new Bound<ulong>(0, tick))))
        {
            ctx.Db.Loot.LootId.Delete(loot.LootId);
        }
    }

    private static void AppendEvent(
        ReducerContext ctx,
        ulong tick,
        ulong ownerEntityId,
        string eventType,
        string details) =>
        ctx.Db.CombatEvent.Insert(new CombatEvent
        {
            OwnerEntityId = ownerEntityId,
            EventType = eventType,
            Details = details,
            Tick = tick,
        });

    private static void SeedPlayerInventory(ReducerContext ctx, ulong shipEntityId)
    {
        foreach (var ammunition in Catalog.Content.Ammunition)
        {
            ctx.Db.Inventory.Insert(new Inventory
            {
                ShipEntityId = shipEntityId,
                ItemId = ammunition.Id,
                Quantity = 100,
            });
        }

        ctx.Db.Inventory.Insert(new Inventory
        {
            ShipEntityId = shipEntityId,
            ItemId = "repair_kit",
            Quantity = 10,
        });
    }

    private static void SeedNpcInventory(ReducerContext ctx, ulong shipEntityId)
    {
        foreach (var ammunition in Catalog.Content.Ammunition)
        {
            ctx.Db.Inventory.Insert(new Inventory
            {
                ShipEntityId = shipEntityId,
                ItemId = ammunition.Id,
                Quantity = 10_000,
            });
        }

        ctx.Db.Inventory.Insert(new Inventory
        {
            ShipEntityId = shipEntityId,
            ItemId = "repair_kit",
            Quantity = 10_000,
        });
    }

}
