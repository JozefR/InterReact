namespace ResearchPlatform.Contracts.Quality;

public sealed record DataQualityRunResult(
    Guid RunId,
    string Pipeline,
    string Provider,
    DateOnly FromDate,
    DateOnly ToDate,
    int RowsRead,
    int ChecksEvaluated,
    int ChecksPassed,
    int ChecksFailed,
    int ErrorCount,
    int WarningCount,
    int SymbolsProcessed);
