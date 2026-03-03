namespace ResearchPlatform.Contracts.Universes;

public sealed record IndexConstituentSnapshotLoadRequest(
    string IndexCode,
    string Source,
    DateOnly EffectiveFrom,
    IReadOnlyList<IndexConstituentInput> Constituents);
