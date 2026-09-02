namespace Sea.Performance;

public sealed record QualityEvidence(
    double LineCoveragePercent,
    double BranchCoveragePercent,
    double MutationScorePercent,
    int CriticalSurvivingMutations);
