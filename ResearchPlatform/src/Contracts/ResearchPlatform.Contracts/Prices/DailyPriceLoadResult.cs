namespace ResearchPlatform.Contracts.Prices;

public sealed record DailyPriceLoadResult(
    Guid RunId,
    string Pipeline,
    string Provider,
    DateOnly FromDate,
    DateOnly ToDate,
    int RowsRead,
    int RowsInserted,
    int RowsUpdated,
    int ResolvedSymbolCount);
