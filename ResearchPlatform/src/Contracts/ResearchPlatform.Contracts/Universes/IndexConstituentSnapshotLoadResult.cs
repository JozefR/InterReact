namespace ResearchPlatform.Contracts.Universes;

public sealed record IndexConstituentSnapshotLoadResult(
    string IndexCode,
    DateOnly EffectiveFrom,
    int InsertedMembershipRows,
    int ClosedMembershipRows,
    int RemovedSameDayRows,
    int UpdatedActiveRows,
    int UnchangedActiveRows,
    int RequestedConstituentCount);
