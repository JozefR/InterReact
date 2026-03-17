using ResearchPlatform.Contracts.Ingestion;

namespace ResearchPlatform.Contracts.CorporateActions;

public sealed record CorporateActionLoadRequest(
    string Provider,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<ProviderCorporateActionRecord> Actions,
    string Pipeline = "CorporateActions",
    string? RequestParametersJson = null);
