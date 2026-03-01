namespace ResearchPlatform.Contracts.Symbols;

public sealed record SymbolEnrichmentResult(
    long SymbolMasterId,
    string CanonicalSymbol,
    bool CreatedSymbolMaster,
    bool UpdatedSymbolMasterMetadata,
    bool CreatedSymbolMapping,
    bool ReassignedExistingMapping,
    int ClosedOverlappingMappings);
