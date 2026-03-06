namespace ResearchPlatform.Contracts.Ingestion;

public sealed record IngestionConnectorCapabilities(
    bool SupportsIndexConstituentSnapshots = true,
    bool SupportsDailyPrices = true,
    bool SupportsCorporateActions = true,
    int? MaxSymbolsPerRequest = null);
