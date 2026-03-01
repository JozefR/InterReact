using DataWarehouse.Schema;
using DataWarehouse.Schema.Entities;
using Microsoft.EntityFrameworkCore;
using ResearchPlatform.Contracts.Abstractions;
using ResearchPlatform.Contracts.Symbols;
using SchemaAssetType = DataWarehouse.Schema.Enums.AssetType;
using ContractAssetType = ResearchPlatform.Contracts.Symbols.AssetType;

namespace DataWarehouse.Symbols;

public sealed class EfSymbolIdentityRepository(ResearchWarehouseDbContext dbContext)
    : ISymbolIdentityRepository, IDisposable, IAsyncDisposable
{
    public async Task<SymbolEnrichmentResult> UpsertSymbolAsync(SymbolEnrichmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalized = NormalizedRequest.From(request);
        ValidateRequest(normalized);

        var nowUtc = DateTime.UtcNow;

        var createdSymbolMaster = false;
        var updatedSymbolMasterMetadata = false;
        var createdSymbolMapping = false;
        var reassignedExistingMapping = false;
        var closedOverlappingMappings = 0;

        var symbolMaster = await dbContext.SymbolMasters
            .SingleOrDefaultAsync(x => x.Symbol == normalized.CanonicalSymbol, cancellationToken);

        if (symbolMaster is null)
        {
            symbolMaster = new SymbolMaster
            {
                Symbol = normalized.CanonicalSymbol,
                Name = normalized.SecurityName,
                ExchangeMic = normalized.ExchangeMic,
                AssetType = ToSchemaAssetType(normalized.AssetType),
                Currency = normalized.Currency,
                IsActive = normalized.IsActive,
                ListedDate = normalized.ListedDate,
                DelistedDate = normalized.DelistedDate,
                CreatedUtc = nowUtc,
                UpdatedUtc = nowUtc
            };

            dbContext.SymbolMasters.Add(symbolMaster);
            createdSymbolMaster = true;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            updatedSymbolMasterMetadata = ApplyMetadataUpdates(symbolMaster, normalized, nowUtc);
        }

        var mappingByExactKey = await dbContext.SymbolMappings
            .SingleOrDefaultAsync(
                x => x.Provider == normalized.Provider
                     && x.ProviderSymbol == normalized.ProviderSymbol
                     && x.EffectiveFrom == normalized.EffectiveFrom,
                cancellationToken);

        SymbolMapping? resolvedMapping = mappingByExactKey;

        if (mappingByExactKey is null)
        {
            var overlappingProviderMappings = await dbContext.SymbolMappings
                .Where(x => x.Provider == normalized.Provider && x.ProviderSymbol == normalized.ProviderSymbol)
                .Where(x => x.EffectiveFrom <= normalized.EffectiveFrom)
                .Where(x => x.EffectiveTo == null || x.EffectiveTo >= normalized.EffectiveFrom)
                .OrderByDescending(x => x.EffectiveFrom)
                .ToListAsync(cancellationToken);

            foreach (var candidate in overlappingProviderMappings)
            {
                if (candidate.SymbolMasterId == symbolMaster.Id)
                {
                    resolvedMapping = candidate;
                    break;
                }

                if (candidate.EffectiveFrom == normalized.EffectiveFrom)
                {
                    candidate.SymbolMasterId = symbolMaster.Id;
                    candidate.EffectiveTo = normalized.EffectiveTo;
                    reassignedExistingMapping = true;
                    resolvedMapping = candidate;
                    break;
                }

                if (TryCloseMapping(candidate, normalized.EffectiveFrom))
                {
                    closedOverlappingMappings++;
                }
            }

            if (resolvedMapping is null)
            {
                var symbolAliasMappings = await dbContext.SymbolMappings
                    .Where(x => x.SymbolMasterId == symbolMaster.Id)
                    .Where(x => x.Provider == normalized.Provider)
                    .Where(x => x.ProviderSymbol != normalized.ProviderSymbol)
                    .Where(x => x.EffectiveFrom < normalized.EffectiveFrom)
                    .Where(x => x.EffectiveTo == null || x.EffectiveTo >= normalized.EffectiveFrom)
                    .ToListAsync(cancellationToken);

                foreach (var candidate in symbolAliasMappings)
                {
                    if (TryCloseMapping(candidate, normalized.EffectiveFrom))
                    {
                        closedOverlappingMappings++;
                    }
                }

                resolvedMapping = new SymbolMapping
                {
                    SymbolMasterId = symbolMaster.Id,
                    Provider = normalized.Provider,
                    ProviderSymbol = normalized.ProviderSymbol,
                    EffectiveFrom = normalized.EffectiveFrom,
                    EffectiveTo = normalized.EffectiveTo
                };

                dbContext.SymbolMappings.Add(resolvedMapping);
                createdSymbolMapping = true;
            }
        }
        else
        {
            if (mappingByExactKey.SymbolMasterId != symbolMaster.Id)
            {
                mappingByExactKey.SymbolMasterId = symbolMaster.Id;
                reassignedExistingMapping = true;
            }

            if (mappingByExactKey.EffectiveTo != normalized.EffectiveTo)
            {
                mappingByExactKey.EffectiveTo = normalized.EffectiveTo;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new SymbolEnrichmentResult(
            SymbolMasterId: symbolMaster.Id,
            CanonicalSymbol: symbolMaster.Symbol,
            CreatedSymbolMaster: createdSymbolMaster,
            UpdatedSymbolMasterMetadata: updatedSymbolMasterMetadata,
            CreatedSymbolMapping: createdSymbolMapping,
            ReassignedExistingMapping: reassignedExistingMapping,
            ClosedOverlappingMappings: closedOverlappingMappings);
    }

    public async Task<SymbolMasterSnapshot?> GetByCanonicalSymbolAsync(string canonicalSymbol, CancellationToken cancellationToken = default)
    {
        var normalizedSymbol = NormalizeRequired(canonicalSymbol, nameof(canonicalSymbol), toUpper: true);

        var entity = await dbContext.SymbolMasters
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Symbol == normalizedSymbol, cancellationToken);

        return entity is null ? null : ToSnapshot(entity);
    }

    public async Task<SymbolMasterSnapshot?> ResolveProviderSymbolAsync(
        string provider,
        string providerSymbol,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var normalizedProvider = NormalizeRequired(provider, nameof(provider), toUpper: true);
        var normalizedProviderSymbol = NormalizeRequired(providerSymbol, nameof(providerSymbol), toUpper: true);

        var mapping = await dbContext.SymbolMappings
            .AsNoTracking()
            .Include(x => x.SymbolMaster)
            .Where(x => x.Provider == normalizedProvider && x.ProviderSymbol == normalizedProviderSymbol)
            .Where(x => x.EffectiveFrom <= asOfDate)
            .Where(x => x.EffectiveTo == null || x.EffectiveTo >= asOfDate)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        return mapping is null ? null : ToSnapshot(mapping.SymbolMaster);
    }

    public async Task<IReadOnlyList<SymbolMasterSnapshot>> ListActiveSymbolsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.SymbolMasters
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Symbol)
            .ToListAsync(cancellationToken);

        return entities.Select(ToSnapshot).ToList();
    }

    public async Task<IReadOnlyList<SymbolMappingSnapshot>> ListMappingsAsync(long symbolMasterId, CancellationToken cancellationToken = default)
    {
        if (symbolMasterId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(symbolMasterId), "Symbol master id must be greater than zero.");
        }

        var mappings = await dbContext.SymbolMappings
            .AsNoTracking()
            .Where(x => x.SymbolMasterId == symbolMasterId)
            .OrderBy(x => x.Provider)
            .ThenBy(x => x.ProviderSymbol)
            .ThenBy(x => x.EffectiveFrom)
            .Select(x => new SymbolMappingSnapshot(
                x.Id,
                x.SymbolMasterId,
                x.Provider,
                x.ProviderSymbol,
                x.EffectiveFrom,
                x.EffectiveTo))
            .ToListAsync(cancellationToken);

        return mappings;
    }

    private static bool ApplyMetadataUpdates(SymbolMaster symbolMaster, NormalizedRequest request, DateTime nowUtc)
    {
        var changed = false;

        if (!string.Equals(symbolMaster.Name, request.SecurityName, StringComparison.Ordinal))
        {
            symbolMaster.Name = request.SecurityName;
            changed = true;
        }

        if (!string.Equals(symbolMaster.ExchangeMic, request.ExchangeMic, StringComparison.Ordinal))
        {
            symbolMaster.ExchangeMic = request.ExchangeMic;
            changed = true;
        }

        var requestedAssetType = ToSchemaAssetType(request.AssetType);
        if (symbolMaster.AssetType != requestedAssetType)
        {
            symbolMaster.AssetType = requestedAssetType;
            changed = true;
        }

        if (!string.Equals(symbolMaster.Currency, request.Currency, StringComparison.Ordinal))
        {
            symbolMaster.Currency = request.Currency;
            changed = true;
        }

        if (symbolMaster.IsActive != request.IsActive)
        {
            symbolMaster.IsActive = request.IsActive;
            changed = true;
        }

        if (symbolMaster.ListedDate != request.ListedDate)
        {
            symbolMaster.ListedDate = request.ListedDate;
            changed = true;
        }

        if (symbolMaster.DelistedDate != request.DelistedDate)
        {
            symbolMaster.DelistedDate = request.DelistedDate;
            changed = true;
        }

        if (changed)
        {
            symbolMaster.UpdatedUtc = nowUtc;
        }

        return changed;
    }

    private static bool TryCloseMapping(SymbolMapping mapping, DateOnly newEffectiveFrom)
    {
        if (newEffectiveFrom == DateOnly.MinValue)
        {
            return false;
        }

        var closeTo = newEffectiveFrom.AddDays(-1);
        if (closeTo < mapping.EffectiveFrom)
        {
            return false;
        }

        if (mapping.EffectiveTo == closeTo)
        {
            return false;
        }

        mapping.EffectiveTo = closeTo;
        return true;
    }

    private static SymbolMasterSnapshot ToSnapshot(SymbolMaster entity)
    {
        return new SymbolMasterSnapshot(
            Id: entity.Id,
            Symbol: entity.Symbol,
            Name: entity.Name,
            ExchangeMic: entity.ExchangeMic,
            AssetType: ToContractAssetType(entity.AssetType),
            Currency: entity.Currency,
            IsActive: entity.IsActive,
            ListedDate: entity.ListedDate,
            DelistedDate: entity.DelistedDate,
            CreatedUtc: entity.CreatedUtc,
            UpdatedUtc: entity.UpdatedUtc);
    }

    private static SchemaAssetType ToSchemaAssetType(ContractAssetType assetType)
    {
        return assetType switch
        {
            ContractAssetType.Equity => SchemaAssetType.Equity,
            ContractAssetType.Etf => SchemaAssetType.Etf,
            _ => SchemaAssetType.Other
        };
    }

    private static ContractAssetType ToContractAssetType(SchemaAssetType assetType)
    {
        return assetType switch
        {
            SchemaAssetType.Equity => ContractAssetType.Equity,
            SchemaAssetType.Etf => ContractAssetType.Etf,
            SchemaAssetType.Other => ContractAssetType.Unknown,
            _ => ContractAssetType.Unknown
        };
    }

    private static void ValidateRequest(NormalizedRequest request)
    {
        if (request.EffectiveTo is not null && request.EffectiveTo < request.EffectiveFrom)
        {
            throw new ArgumentException("EffectiveTo must be null or greater than or equal to EffectiveFrom.");
        }

        if (request.DelistedDate is not null && request.ListedDate is not null && request.DelistedDate < request.ListedDate)
        {
            throw new ArgumentException("DelistedDate must be greater than or equal to ListedDate when both are provided.");
        }
    }

    private static string NormalizeRequired(string value, string fieldName, bool toUpper)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} must not be empty.", fieldName);
        }

        var normalized = value.Trim();
        return toUpper ? normalized.ToUpperInvariant() : normalized;
    }

    private sealed record NormalizedRequest(
        string Provider,
        string ProviderSymbol,
        DateOnly EffectiveFrom,
        string CanonicalSymbol,
        string SecurityName,
        string ExchangeMic,
        ContractAssetType AssetType,
        string Currency,
        bool IsActive,
        DateOnly? ListedDate,
        DateOnly? DelistedDate,
        DateOnly? EffectiveTo)
    {
        public static NormalizedRequest From(SymbolEnrichmentRequest request)
        {
            return new NormalizedRequest(
                Provider: NormalizeRequired(request.Provider, nameof(request.Provider), toUpper: true),
                ProviderSymbol: NormalizeRequired(request.ProviderSymbol, nameof(request.ProviderSymbol), toUpper: true),
                EffectiveFrom: request.EffectiveFrom,
                CanonicalSymbol: NormalizeRequired(request.CanonicalSymbol, nameof(request.CanonicalSymbol), toUpper: true),
                SecurityName: NormalizeRequired(request.SecurityName, nameof(request.SecurityName), toUpper: false),
                ExchangeMic: NormalizeRequired(request.ExchangeMic, nameof(request.ExchangeMic), toUpper: true),
                AssetType: request.AssetType,
                Currency: NormalizeRequired(request.Currency, nameof(request.Currency), toUpper: true),
                IsActive: request.IsActive,
                ListedDate: request.ListedDate,
                DelistedDate: request.DelistedDate,
                EffectiveTo: request.EffectiveTo);
        }
    }

    public void Dispose()
    {
        dbContext.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return dbContext.DisposeAsync();
    }
}
