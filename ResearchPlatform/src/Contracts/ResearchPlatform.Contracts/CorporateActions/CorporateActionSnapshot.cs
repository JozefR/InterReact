namespace ResearchPlatform.Contracts.CorporateActions;

public sealed record CorporateActionSnapshot(
    long Id,
    long SymbolMasterId,
    string CanonicalSymbol,
    DateOnly ActionDate,
    string ActionTypeCode,
    decimal Value,
    decimal? AdjustmentFactor,
    string? Currency,
    string Provider,
    string? ExternalId,
    string? Description,
    string? RelatedProviderSymbol,
    string? AttributesJson,
    Guid? LastIngestionRunId);
