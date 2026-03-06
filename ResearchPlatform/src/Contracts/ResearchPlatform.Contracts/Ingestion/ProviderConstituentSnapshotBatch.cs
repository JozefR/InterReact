namespace ResearchPlatform.Contracts.Ingestion;

public sealed record ProviderConstituentSnapshotBatch(
    string ProviderCode,
    string IndexCode,
    DateOnly EffectiveDate,
    IReadOnlyList<ProviderConstituentRecord> Constituents,
    bool IsComplete,
    string? ContinuationToken,
    DateTime RetrievedAtUtc);
