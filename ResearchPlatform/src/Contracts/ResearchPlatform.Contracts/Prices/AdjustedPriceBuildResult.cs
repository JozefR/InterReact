namespace ResearchPlatform.Contracts.Prices;

public sealed record AdjustedPriceBuildResult(
    Guid RunId,
    string Pipeline,
    string Provider,
    string AdjustmentBasis,
    DateOnly FromDate,
    DateOnly ToDate,
    int RowsRead,
    int RowsInserted,
    int RowsUpdated,
    int SymbolsProcessed);
