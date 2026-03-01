namespace ResearchPlatform.Contracts.Symbols;

public sealed record SymbolMasterSnapshot(
    long Id,
    string Symbol,
    string Name,
    string ExchangeMic,
    AssetType AssetType,
    string Currency,
    bool IsActive,
    DateOnly? ListedDate,
    DateOnly? DelistedDate,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
