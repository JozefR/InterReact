namespace ResearchPlatform.Contracts.CorporateActions;

public sealed record CorporateActionLoadResult(
    Guid RunId,
    string Pipeline,
    string Provider,
    DateOnly FromDate,
    DateOnly ToDate,
    int RowsRead,
    int RowsInserted,
    int RowsUpdated,
    int ResolvedSymbolCount);
