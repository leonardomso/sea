using SpacetimeDB;

public static partial class Module
{
    private sealed class ShipTickBuffer
    {
        private readonly Dictionary<ulong, Ship> ships = new();

        public int Count => ships.Count;

        public bool TryGetStaged(ulong entityId, out Ship ship) =>
            ships.TryGetValue(entityId, out ship);

        public bool TryGet(ReducerContext ctx, ulong entityId, out Ship ship)
        {
            if (ships.TryGetValue(entityId, out ship))
            {
                return true;
            }

            if (ctx.Db.Ship.EntityId.Find(entityId) is not Ship stored)
            {
                ship = default;
                return false;
            }

            ship = stored;
            return true;
        }

        public void Stage(Ship ship) => ships[ship.EntityId] = ship;

        public void Flush(ReducerContext ctx)
        {
            foreach (var ship in ships.Values)
            {
                PersistShip(ctx, ship);
            }
        }
    }
}
