using System.Runtime.InteropServices;

namespace Sea.Server;

public enum ReplayCommandKind : byte
{
    SetCourse = 1,
    StopCourse = 2,
}

/// <summary>One order given at one tick.</summary>
/// <remarks>
/// A course is a list of corners, but the overwhelming majority of clicks are a straight
/// run in open water, so the common case is the positional one and <see cref="Corners"/>
/// is only set when the search actually had to bend the line around something.
/// </remarks>
public readonly record struct ReplayCommand(
    uint Tick,
    ReplayCommandKind Kind,
    float X,
    float Y)
{
    /// <summary>
    /// The whole course, corner by corner. Null means a straight run to
    /// (<see cref="X"/>, <see cref="Y"/>).
    /// </summary>
    public IReadOnlyList<RouteWaypoint>? Corners { get; init; }
}

/// <summary>
/// Everything a replay carries between ticks. Speed is not in here: a hull has none
/// between ticks (SEA_5 4.2), so the whole of her motion is where she is, which way she
/// points, and how far down her course she has got.
/// </summary>
public readonly record struct ReplayState(
    float PositionX,
    float PositionY,
    float HeadingDegrees,
    int RouteIndex,
    bool HasRoute);

public readonly record struct ReplayResult(ReplayState State, ulong StateHash);

/// <summary>
/// Sails a recorded set of orders and hashes every tick of the result. Two hosts that
/// disagree by one bit anywhere in the movement code disagree in the hash, which is the
/// only cheap way to catch it.
/// </summary>
public static class ReplayRules
{
    private const ulong HashOffset = 14695981039346656037UL;
    private const ulong HashPrime = 1099511628211UL;

    /// <param name="travelPerTick">Squares covered in one tick. Constant for the length of
    /// a run: the weather is somebody else's determinism problem (SEA_5 12.5).</param>
    public static ReplayResult Run(
        uint tickCount,
        ReplayState initial,
        IReadOnlyList<ReplayCommand> commands,
        float travelPerTick)
    {
        var state = initial;
        var route = new List<RouteWaypoint>(RouteRules.MaximumWaypoints);
        var commandIndex = 0;
        var hash = HashOffset;

        for (uint tick = 0; tick < tickCount; tick++)
        {
            while (commandIndex < commands.Count && commands[commandIndex].Tick == tick)
            {
                state = LayCourse(commands[commandIndex++], route, state);
            }

            var step = RouteRules.Advance(
                CollectionsMarshal.AsSpan(route),
                state.RouteIndex,
                state.PositionX,
                state.PositionY,
                state.HeadingDegrees,
                state.HasRoute ? travelPerTick : 0f);
            state = new ReplayState(
                step.PositionX,
                step.PositionY,
                step.HeadingDegrees,
                step.WaypointIndex,
                state.HasRoute && !step.Arrived);
            hash = Append(hash, tick);
            hash = Append(hash, BitConverter.SingleToUInt32Bits(state.PositionX));
            hash = Append(hash, BitConverter.SingleToUInt32Bits(state.PositionY));
            hash = Append(hash, BitConverter.SingleToUInt32Bits(state.HeadingDegrees));
            hash = Append(hash, (uint)state.RouteIndex);
        }

        return new ReplayResult(state, hash);
    }

    private static ReplayState LayCourse(
        ReplayCommand command,
        List<RouteWaypoint> route,
        ReplayState state)
    {
        // A new order always replaces the old course outright; there is no such thing as
        // appending to one, which is what makes a replay's route a function of the last
        // command alone.
        route.Clear();
        if (command.Kind == ReplayCommandKind.StopCourse)
        {
            return state with { RouteIndex = 0, HasRoute = false };
        }

        if (command.Corners is { } corners)
        {
            route.AddRange(corners);
        }
        else
        {
            route.Add(new RouteWaypoint(command.X, command.Y));
        }

        return state with { RouteIndex = 0, HasRoute = route.Count > 0 };
    }

    private static ulong Append(ulong hash, uint value)
    {
        for (var shift = 0; shift < 32; shift += 8)
        {
            hash ^= (byte)(value >> shift);
            hash *= HashPrime;
        }

        return hash;
    }
}
