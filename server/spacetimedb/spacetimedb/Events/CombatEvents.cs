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

    private static string FireRejectionMessage(FireRejection rejection) => rejection switch
    {
        FireRejection.SourceSunk => "A sunk ship cannot fire.",
        FireRejection.NoTarget => "Select a target before firing.",
        FireRejection.TargetSunk => "The selected target has already sunk.",
        FireRejection.CannonsDisabled => "The ship's cannons are disabled.",
        FireRejection.NoAmmunition => "No selected ammunition remains.",
        FireRejection.Reloading => "That broadside is still reloading.",
        FireRejection.OutOfRange => "The selected target is out of range.",
        FireRejection.OutsideArc => "The selected target is outside that broadside arc.",
        FireRejection.Busy => "Repair or boarding must finish before firing.",
        _ => "The broadside cannot fire.",
    };

    private static string AbilityRejectionMessage(AbilityRejection rejection) => rejection switch
    {
        AbilityRejection.SourceSunk => "A sunk ship cannot use abilities.",
        AbilityRejection.UnknownAbility => "That ability does not exist.",
        AbilityRejection.Cooldown => "That ability is still cooling down.",
        AbilityRejection.Busy => "Finish the active channel before using an ability.",
        _ => "The ability cannot be activated.",
    };

    private static string RepairRejectionMessage(RepairRejection rejection) => rejection switch
    {
        RepairRejection.SourceSunk => "A sunk ship cannot be repaired.",
        RepairRejection.Busy => "Another channel is already active.",
        RepairRejection.NoRepairKit => "No repair kits remain.",
        RepairRejection.NothingToRepair => "The ship does not need repairs.",
        _ => "Repair cannot start.",
    };

    private static string BoardingRejectionMessage(BoardingRejection rejection) => rejection switch
    {
        BoardingRejection.SourceSunk => "A sunk ship cannot board.",
        BoardingRejection.TargetSunk => "Select a living target before boarding.",
        BoardingRejection.Busy => "Another channel is already active.",
        BoardingRejection.TargetTooStrong => "The target must be below 25% hull.",
        BoardingRejection.OutOfRange => "Move within boarding range.",
        BoardingRejection.Cooldown => "Boarding is still cooling down.",
        _ => "Boarding cannot start.",
    };

    private static void ProcessLootExpiry(ReducerContext ctx, ulong tick)
    {
        foreach (var loot in ctx.Db.Loot.ByLootExpiryDue.Filter(
                     (true, new Bound<ulong>(0, tick))))
        {
            ctx.Db.Loot.LootId.Delete(loot.LootId);
            ChangeActiveLootCount(ctx, -1);
        }
    }

    private static void AppendEvent(
        ReducerContext ctx,
        ulong ownerEntityId,
        string eventType,
        string details)
    {
        var tick = ctx.Db.SimulationClock.Id.Find(1)?.Tick ?? 0;
        ctx.Db.CombatEvent.Insert(new CombatEvent
        {
            OwnerEntityId = ownerEntityId,
            EventType = eventType,
            Details = details,
            Tick = tick,
        });
    }

    private static void SeedPlayerInventory(ReducerContext ctx, ulong shipEntityId)
    {
        foreach (var ammunition in ContentCatalog.CreateDefault().Ammunition)
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
        foreach (var ammunition in ContentCatalog.CreateDefault().Ammunition)
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
