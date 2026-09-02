namespace Sea.Performance;

public sealed record CorrectnessEvidence(
    int IdentityDelta,
    int RuntimeErrors,
    int UnhandledReducerErrors,
    int MissingAssets,
    int DuplicateRewards,
    long DormantMovementWork,
    long DormantAiWork);
