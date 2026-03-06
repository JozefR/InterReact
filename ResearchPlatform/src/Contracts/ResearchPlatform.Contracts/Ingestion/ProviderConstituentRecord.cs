namespace ResearchPlatform.Contracts.Ingestion;

public sealed record ProviderConstituentRecord(
    string ProviderSymbol,
    decimal? Weight = null,
    string? SecurityName = null,
    string? ExchangeMic = null);
