namespace Sea.Performance;

public sealed record ClientEvidence(
    int VisibleShips,
    double FrameP95Milliseconds,
    double FrameP99Milliseconds,
    long IdleBytesPerFrame,
    bool PoolsStable);
