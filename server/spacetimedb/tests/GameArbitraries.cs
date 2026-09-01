using FsCheck;
using FsCheck.Fluent;

namespace Sea.Server.Tests;

public readonly record struct GeneratedShip(
    float X,
    float Y,
    float Heading,
    float Speed,
    uint Hull);

public readonly record struct GeneratedCommand(
    ReplayCommandKind Kind,
    float X,
    float Y);

public readonly record struct GeneratedTarget(ulong EntityId, float X, float Y, uint Hull);
public readonly record struct GeneratedStatus(byte Type, byte Stacks, uint DurationTicks);
public readonly record struct GeneratedAmmunition(byte Type, uint Quantity);
public readonly record struct GeneratedCoordinate(int Column, int Row);
public readonly record struct GeneratedTickSequence(uint[] Ticks);

public static class GameArbitraries
{
    public static Arbitrary<GeneratedShip> Ships() => Arb.From(
        from x in FiniteFloat(-99f, 99f)
        from y in FiniteFloat(-99f, 99f)
        from heading in FiniteFloat(0f, 359.99f)
        from speed in FiniteFloat(0f, 20f)
        from hull in Gen.Choose(0, 10_000)
        select new GeneratedShip(x, y, heading, speed, (uint)hull));

    public static Arbitrary<GeneratedCommand> Commands() => Arb.From(
        from kind in Gen.Elements(ReplayCommandKind.SetCourse, ReplayCommandKind.StopCourse)
        from x in FiniteFloat(-100f, 100f)
        from y in FiniteFloat(-100f, 100f)
        select new GeneratedCommand(kind, x, y));

    public static Arbitrary<GeneratedTarget> Targets() => Arb.From(
        from id in Gen.Choose(1, int.MaxValue)
        from x in FiniteFloat(-100f, 100f)
        from y in FiniteFloat(-100f, 100f)
        from hull in Gen.Choose(1, 10_000)
        select new GeneratedTarget((ulong)id, x, y, (uint)hull));

    public static Arbitrary<GeneratedStatus> Statuses() => Arb.From(
        from type in Gen.Choose(0, 3)
        from stacks in Gen.Choose(1, 5)
        from duration in Gen.Choose(1, 600)
        select new GeneratedStatus((byte)type, (byte)stacks, (uint)duration));

    public static Arbitrary<GeneratedAmmunition> Ammunition() => Arb.From(
        from type in Gen.Choose(0, 3)
        from quantity in Gen.Choose(0, 10_000)
        select new GeneratedAmmunition((byte)type, (uint)quantity));

    public static Arbitrary<GeneratedCoordinate> Coordinates() => Arb.From(
        from column in Gen.Choose(0, ChartCoordinates.ColumnCount - 1)
        from row in Gen.Choose(0, ChartCoordinates.MaximumRow)
        select new GeneratedCoordinate(column, row));

    public static Arbitrary<GeneratedTickSequence> TickSequences() => Arb.From(
        Gen.Choose(0, 10_000).ListOf()
            .Select(values => new GeneratedTickSequence(
                values.Select(value => (uint)value).OrderBy(value => value).ToArray())));

    private static Gen<float> FiniteFloat(float minimum, float maximum) =>
        Gen.Choose(0, 1_000_000)
            .Select(value => minimum + (maximum - minimum) * value / 1_000_000f);
}
