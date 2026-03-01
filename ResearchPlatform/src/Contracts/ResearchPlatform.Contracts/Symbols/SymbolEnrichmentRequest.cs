namespace ResearchPlatform.Contracts.Symbols;

public sealed record SymbolEnrichmentRequest(
    string Provider,
    string ProviderSymbol,
    DateOnly EffectiveFrom,
    string CanonicalSymbol,
    string SecurityName,
    string ExchangeMic,
    AssetType AssetType = AssetType.Equity,
    string Currency = "USD",
    bool IsActive = true,
    DateOnly? ListedDate = null,
    DateOnly? DelistedDate = null,
    DateOnly? EffectiveTo = null);
