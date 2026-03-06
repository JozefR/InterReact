namespace ResearchPlatform.Contracts.Ingestion;

public sealed record ProviderCorporateActionRecord(
    string ProviderSymbol,
    DateOnly ActionDate,
    string ActionTypeCode,
    decimal Value,
    string? Currency = null,
    string? ExternalId = null,
    string? Description = null,
    string? RelatedProviderSymbol = null);
