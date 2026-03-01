using ResearchPlatform.Contracts.Symbols;

namespace ResearchPlatform.Contracts.Abstractions;

public interface ISymbolIdentityRepository
{
    Task<SymbolEnrichmentResult> UpsertSymbolAsync(SymbolEnrichmentRequest request, CancellationToken cancellationToken = default);

    Task<SymbolMasterSnapshot?> GetByCanonicalSymbolAsync(string canonicalSymbol, CancellationToken cancellationToken = default);

    Task<SymbolMasterSnapshot?> ResolveProviderSymbolAsync(
        string provider,
        string providerSymbol,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SymbolMasterSnapshot>> ListActiveSymbolsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SymbolMappingSnapshot>> ListMappingsAsync(long symbolMasterId, CancellationToken cancellationToken = default);
}
