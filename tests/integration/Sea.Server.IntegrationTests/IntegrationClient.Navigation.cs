using SpacetimeDB.Types;

namespace Sea.Server.IntegrationTests;

/// <summary>
/// Port Lowell and the water around it: which hostiles are far enough from the harbour to be
/// fought at all, how a berthed hull gets out to them, and what a ship's state reads as when a
/// wait for one of those things runs out.
/// </summary>
internal sealed partial class IntegrationClient
{
    /// <summary>
    /// How far past the harbour mouth a course out reaches. Wide enough to clear the thirty
    /// units of sheltered water an NPC will not shoot into, so a ship that has put to sea is a
    /// ship that can fight and be fought.
    /// </summary>
    private const float SeaRoom = 35f;

    /// <summary>
    /// How far past the mouth a hostile has to be before a test picks a fight with it. The
    /// approach circles a target rather than stopping dead on it, so the room to manoeuvre has
    /// to fit outside the harbour as well, or the run-in drifts back into water where nothing
    /// can be fired at all.
    /// </summary>
    private const float FightingRoom = 45f;

    /// <summary>
    /// Inside the chart with room to spare, so a course out never leaves it. Havenmere is four
    /// hundred squares on a side with its origin at the top-left corner (SEA_5 3.1), and the
    /// border reads as land, so a course has to stop short of it.
    /// </summary>
    private const float ChartMin = 5f;
    private const float ChartMax = 395f;

    /// <summary>
    /// Open water south-west of Port Lowell, given as an offset from the harbour so the mark
    /// moves with it. Seventy-eight squares off the mouth: outside the thirty squares of
    /// sheltered water, and clear of every island, reef and shoal in Content/Data/maps.json.
    /// </summary>
    public (float X, float Y) OpenWater()
    {
        var port = PortLowell();
        return (port.X - 50f, port.Y + 60f);
    }

    /// <summary>
    /// Open water a hundred and sixty squares north of <see cref="OpenWater"/>, on a bearing
    /// that passes the reef at (140, 150), so the leg between them is long enough to time and
    /// has something in the way for the router to bend around.
    /// </summary>
    public (float X, float Y) FarWater()
    {
        var port = PortLowell();
        return (port.X - 50f, port.Y - 100f);
    }

    private const byte DestinationBlockedRejection = 6;

    /// <summary>
    /// The nearest live hostile of an archetype that can be fought at all: one clear of Port
    /// Lowell, whose waters answer every fire command with InPort no matter who is shooting.
    /// </summary>
    public Ship ClosestNpcClearOfPort(byte archetypeCode) =>
        ClosestHostileClearOfPort(archetypeCode, _ => true);

    /// <summary>
    /// The same hostile, for a caller that can wait for one. Hostiles patrol, so a named ship
    /// whose beat takes her past the harbour mouth is only unfightable for as long as she is
    /// inside it; a test that throws on the first look fails on where she happened to be.
    /// </summary>
    public Ship? TryClosestNpcClearOfPort(byte archetypeCode) =>
        TryClosestHostileClearOfPort(archetypeCode, _ => true);

    private Ship ClosestHostileClearOfPort(byte archetypeCode, Func<Ship, bool> also) =>
        TryClosestHostileClearOfPort(archetypeCode, also)
            ?? throw new InvalidOperationException(
                $"No hostile of archetype {archetypeCode} is clear of Port Lowell.");

    private Ship? TryClosestHostileClearOfPort(byte archetypeCode, Func<Ship, bool> also)
    {
        var port = PortLowell();
        var fightable = port.Radius + FightingRoom;
        return connection.Db.Ship.Iter()
            .Where(ship =>
                ship.FactionCode == 2 &&
                ship.ArchetypeCode == archetypeCode &&
                ship.IsAlive &&
                also(ship))
            .Select(ship => (Ship: ship, Range: RangeFromPort(port, LivePosition(ship))))
            .Where(hostile => hostile.Range >= fightable * fightable)
            .OrderBy(hostile => hostile.Range)
            .Select(hostile => hostile.Ship)
            .FirstOrDefault();
    }

    private static float RangeFromPort(
        (float X, float Y, float Radius) port,
        (float X, float Y) position) =>
        DistanceSquared(position.X, position.Y, port.X, port.Y);

    /// <summary>Where Port Lowell is and how far its sheltered water reaches.</summary>
    private (float X, float Y, float Radius) PortLowell()
    {
        var harbor = Harbor();
        return (harbor.PositionX, harbor.PositionY, harbor.Radius);
    }

    /// <summary>
    /// Where a ship is this tick. The fat row is only republished on a chunk change, so a course
    /// laid on it can be a chunk's width out -- enough to put the destination on an island.
    /// </summary>
    private (float X, float Y) LivePosition(Ship ship) =>
        connection.Db.ShipMovement.EntityId.Find(ship.EntityId) is ShipMovement movement
            ? (movement.PositionX, movement.PositionY)
            : (ship.PositionX, ship.PositionY);

    /// <summary>
    /// The nearest hostile of an archetype that nothing has shot at yet, and that can be fought.
    /// A test that counts volleys has to start from a full hull, or a target another test left
    /// half sunk ends the encounter before every participant has fired.
    /// </summary>
    public Ship ClosestUntouchedNpcClearOfPort(byte archetypeCode) =>
        ClosestHostileClearOfPort(
            archetypeCode,
            ship => ship.IsActive && ship.Hull == ship.MaxHull);

    /// <summary>
    /// The nearest hostile still afloat, whatever it is. A test that has to keep shooting past
    /// the moment its first target sinks needs a target it can pick up without caring which
    /// archetype answers.
    /// </summary>
    public Ship ClosestLiveNpcTo(float x, float y) => connection.Db.Ship.Iter()
        .Where(ship => ship.FactionCode == 2 && ship.IsAlive)
        .OrderBy(ship => DistanceSquared(ship.PositionX, ship.PositionY, x, y))
        .First();

    /// <summary>
    /// Puts to sea on a course towards the given point. Port Lowell shelters the ship inside it
    /// from everything, its own guns included, and the first course out is a channel the ship
    /// holds station for, so a test that wants a fight has to leave the harbour before it starts.
    /// </summary>
    public void PutToSea(float x, float y)
    {
        CommandResultEvent? last = null;
        foreach (var (destinationX, destinationY) in CoursesOutOfPort(PortLowell(), x, y))
        {
            last = Issue(
                nextCommandId++,
                new ShipCommand.SetCourse(new SetCourseCommand(destinationX, destinationY)));
            if (last.Accepted)
            {
                PumpUntil(connection, () => !OwnedShip().IsInPort || failure is not null);
                ThrowIfFailed();
                return;
            }

            // Only an unsailable destination is worth another bearing; every other answer is
            // about the ship rather than the water, and trying again would only repeat it.
            if (last.RejectionCode != DestinationBlockedRejection)
            {
                break;
            }
        }

        throw new InvalidOperationException(
            $"Every course out of Port Lowell was rejected, last with code {last?.RejectionCode}.");
    }

    /// <summary>
    /// Courses out of the harbour, best first: the point asked for when it already lies clear of
    /// the port, then open water beyond the mouth on that bearing and on the bearings to either
    /// side of it. A course that ends inside the circle is sailed without ever crossing the
    /// mouth, and the chart has islands on it, so one bearing is not enough.
    /// </summary>
    private static IEnumerable<(float X, float Y)> CoursesOutOfPort(
        (float X, float Y, float Radius) port,
        float x,
        float y)
    {
        var reach = port.Radius + SeaRoom;
        if (DistanceSquared(x, y, port.X, port.Y) >= reach * reach)
        {
            yield return (x, y);
        }

        var bearing = MathF.Atan2(y - port.Y, x - port.X);
        for (var step = 0; step < 8; step++)
        {
            // The bearing asked for, then the nearest water to either side of it: 0, -45, +45...
            var heading = bearing + ((step + 1) / 2 * (step % 2 == 0 ? 1 : -1) * (MathF.PI / 4f));
            yield return (
                InsideChart(port.X + (MathF.Cos(heading) * reach)),
                InsideChart(port.Y + (MathF.Sin(heading) * reach)));
        }
    }

    private static float InsideChart(float value) => Math.Clamp(value, ChartMin, ChartMax);

    /// <summary>
    /// Everything a stalled wait could be waiting on, in one line. A timeout that only says it
    /// timed out costs a whole re-run to tell a frozen world from a ship that never moved.
    /// </summary>
    public string Describe()
    {
        var ship = OwnedShip();
        var movement = connection.Db.ShipMovement.EntityId.Find(ship.EntityId);
        var channel = connection.Db.ShipChannel.ShipEntityId.Find(ship.EntityId);
        var worldTick = connection.Db.ShipMovement.Iter()
            .Select(row => row.SnapshotTick)
            .DefaultIfEmpty(0ul)
            .Max();
        var world = FormattableString.Invariant($"world tick {worldTick}");
        var hull = FormattableString.Invariant($"hull {ship.Hull}/{ship.MaxHull}");
        var mode = FormattableString.Invariant(
            $"mode {ship.ModeCode}, in port {ship.IsInPort}, course {ship.HasRoute}");
        var row = FormattableString.Invariant(
            $"row ({ship.PositionX:0.0}, {ship.PositionY:0.0})");
        var course = FormattableString.Invariant(
            $"dst ({ship.DestinationX:0.0}, {ship.DestinationY:0.0}), leg {ship.RouteIndex} of course {ship.RouteVersion}, speed {ship.Speed:0.00}, moving {ship.IsMoving}");
        var live = FormattableString.Invariant(
            $"live ({movement?.PositionX:0.0}, {movement?.PositionY:0.0})");
        var snapshot = FormattableString.Invariant($"movement tick {movement?.SnapshotTick}");
        var work = FormattableString.Invariant(
            $"channel {channel?.ChannelType ?? "none"} due {channel?.CompletesAtTick}");
        return string.Join(", ", world, hull, mode, row, course, live, snapshot, work);
    }

    /// <summary>
    /// Port Lowell. Every client subscribes to the harbour when it loads, because the circle is
    /// what says where a ship may fire and where it has to cast off from.
    /// </summary>
    public WorldObject Harbor() => connection.Db.WorldObject.Iter()
        .FirstOrDefault(worldObject =>
            string.Equals(worldObject.Kind, "harbor", StringComparison.Ordinal))
        ?? throw new InvalidOperationException("Port Lowell has not replicated.");

    public bool HasHarbor() => connection.Db.WorldObject.Iter()
        .Any(worldObject => string.Equals(worldObject.Kind, "harbor", StringComparison.Ordinal));

    public bool IsNear(float x, float y, float radius)
    {
        var ship = OwnedShip();
        var deltaX = ship.PositionX - x;
        var deltaY = ship.PositionY - y;
        return deltaX * deltaX + deltaY * deltaY <= radius * radius;
    }
}
