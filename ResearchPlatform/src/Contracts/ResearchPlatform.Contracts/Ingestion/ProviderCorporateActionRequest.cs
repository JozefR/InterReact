namespace ResearchPlatform.Contracts.Ingestion;

public sealed record ProviderCorporateActionRequest(
    IReadOnlyList<string> ProviderSymbols,
    DateOnly FromDate,
    DateOnly ToDate,
    string? ContinuationToken = null);
