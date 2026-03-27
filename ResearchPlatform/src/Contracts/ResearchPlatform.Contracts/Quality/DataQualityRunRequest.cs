namespace ResearchPlatform.Contracts.Quality;

public sealed record DataQualityRunRequest(
    string Provider,
    IReadOnlyList<string> CanonicalSymbols,
    IReadOnlyList<string> AdjustmentBases,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal UnexplainedJumpThreshold = 0.35m,
    string Pipeline = "DataQualitySuite",
    string? RequestParametersJson = null);
