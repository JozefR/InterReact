namespace ResearchPlatform.Contracts.Symbols;

public sealed record SymbolMappingSnapshot(
    long Id,
    long SymbolMasterId,
    string Provider,
    string ProviderSymbol,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);
