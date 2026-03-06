namespace ResearchPlatform.Contracts.Ingestion;

public sealed record ProviderDailyPriceBatch(
    string ProviderCode,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<ProviderDailyPriceRecord> Prices,
    bool IsComplete,
    string? ContinuationToken,
    DateTime RetrievedAtUtc);
