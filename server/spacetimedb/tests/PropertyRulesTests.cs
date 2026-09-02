using FsCheck.Xunit;
using Sea.Server;

namespace Sea.Server.Tests;

public sealed class PropertyRulesTests
{
    [Property(MaxTest = 250, Arbitrary = new[] { typeof(GameArbitraries) })]
    public bool Coordinate_centers_round_trip(GeneratedCoordinate coordinate)
    {
        var center = ChartCoordinates.CellCenter(coordinate.Column, coordinate.Row);
        return string.Equals(
            ChartCoordinates.LabelAt(center.X, center.Y),
            $"{ChartCoordinates.ColumnLabel(coordinate.Column)} {coordinate.Row}",
            StringComparison.Ordinal);
    }

    [Property(MaxTest = 250, Arbitrary = new[] { typeof(GameArbitraries) })]
    public bool Damage_never_increases_hull(GeneratedShip ship, ushort damage) =>
        WorldRules.ApplyDamage(ship.Hull, damage) <= ship.Hull;

    [Property(MaxTest = 250, Arbitrary = new[] { typeof(GameArbitraries) })]
    public bool Chunk_lookup_is_always_bounded(GeneratedShip ship)
    {
        var chunkX = SpatialRules.ChunkCoordinate(ship.X);
        var chunkY = SpatialRules.ChunkCoordinate(ship.Y);
        return chunkX >= 0 && chunkX < SpatialRules.ChunkCountPerAxis &&
            chunkY >= 0 && chunkY < SpatialRules.ChunkCountPerAxis;
    }

    [Property(MaxTest = 150, Arbitrary = new[] { typeof(GameArbitraries) })]
    public bool Generated_tick_sequences_are_monotonic(GeneratedTickSequence sequence) =>
        sequence.Ticks.Zip(sequence.Ticks.Skip(1), (left, right) => left <= right).All(value => value);
}
