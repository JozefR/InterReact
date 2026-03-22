using ResearchPlatform.Contracts.Ingestion;

namespace ResearchPlatform.Contracts.Prices;

public sealed record DailyPriceLoadRequest(
    string Provider,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<ProviderDailyPriceRecord> Prices,
    string Pipeline = "DailyRawPrices",
    string? RequestParametersJson = null);
