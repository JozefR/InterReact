namespace ResearchPlatform.Contracts.Ingestion;

public sealed record ProviderDailyPriceRequest(
    IReadOnlyList<string> ProviderSymbols,
    DateOnly FromDate,
    DateOnly ToDate,
    string? ContinuationToken = null);
