namespace Sea.Server;

public enum ReplayCommandKind : byte
{
    SetCourse = 1,
    StopCourse = 2,
}

public readonly record struct ReplayCommand(
    uint Tick,
    ReplayCommandKind Kind,
    float X,
    float Y);

public readonly record struct ReplayResult(SailingState State, ulong StateHash);

public static class ReplayRules
{
    private const ulong HashOffset = 14695981039346656037UL;
    private const ulong HashPrime = 1099511628211UL;

    public static ReplayResult Run(
        uint tickCount,
        SailingState initial,
        IReadOnlyList<ReplayCommand> commands,
        SailingParameters parameters,
        float deltaSeconds)
    {
        var state = initial;
        var destinationX = initial.PositionX;
        var destinationY = initial.PositionY;
        var stopping = true;
        var commandIndex = 0;
        var hash = HashOffset;

        for (uint tick = 0; tick < tickCount; tick++)
        {
            while (commandIndex < commands.Count && commands[commandIndex].Tick == tick)
            {
                var command = commands[commandIndex++];
                stopping = command.Kind == ReplayCommandKind.StopCourse;
                if (!stopping)
                {
                    destinationX = command.X;
                    destinationY = command.Y;
                }
            }

            var step = SailingRules.Step(
                state,
                destinationX,
                destinationY,
                stopping,
                parameters,
                deltaSeconds);
            state = new SailingState(
                step.PositionX,
                step.PositionY,
                step.HeadingDegrees,
                step.Speed);
            hash = Append(hash, tick);
            hash = Append(hash, BitConverter.SingleToUInt32Bits(state.PositionX));
            hash = Append(hash, BitConverter.SingleToUInt32Bits(state.PositionY));
            hash = Append(hash, BitConverter.SingleToUInt32Bits(state.HeadingDegrees));
            hash = Append(hash, BitConverter.SingleToUInt32Bits(state.Speed));
        }

        return new ReplayResult(state, hash);
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
