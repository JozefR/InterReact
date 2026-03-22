namespace ResearchPlatform.Contracts.Prices;

public sealed record AdjustedPriceBuildRequest(
    string Provider,
    string AdjustmentBasis,
    IReadOnlyList<string> CanonicalSymbols,
    DateOnly FromDate,
    DateOnly ToDate,
    string Pipeline = "AdjustedPrices",
    string? RequestParametersJson = null);
