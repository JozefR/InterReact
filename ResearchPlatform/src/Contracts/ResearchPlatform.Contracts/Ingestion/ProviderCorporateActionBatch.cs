namespace ResearchPlatform.Contracts.Ingestion;

public sealed record ProviderCorporateActionBatch(
    string ProviderCode,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<ProviderCorporateActionRecord> Actions,
    bool IsComplete,
    string? ContinuationToken,
    DateTime RetrievedAtUtc);
