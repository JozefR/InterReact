namespace ResearchPlatform.Contracts.Ingestion;

public sealed record ProviderConstituentSnapshotRequest(
    string IndexCode,
    DateOnly EffectiveDate,
    string? ContinuationToken = null);
