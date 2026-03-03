using DataWarehouse.Schema;
using DataWarehouse.Schema.Entities;
using Microsoft.EntityFrameworkCore;
using ResearchPlatform.Contracts.Abstractions;
using ResearchPlatform.Contracts.Universes;

namespace DataWarehouse.Constituents;

public sealed class EfIndexConstituentPitRepository(ResearchWarehouseDbContext dbContext)
    : IIndexConstituentPitRepository, IDisposable, IAsyncDisposable
{
    public async Task<IndexConstituentSnapshotLoadResult> UpsertSnapshotAsync(
        IndexConstituentSnapshotLoadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalized = NormalizedSnapshotRequest.From(request);
        Validate(normalized);

        var requestedSymbols = normalized.Constituents.Select(x => x.CanonicalSymbol).ToHashSet(StringComparer.Ordinal);

        var symbolMappings = await dbContext.SymbolMasters
            .AsNoTracking()
            .Where(x => requestedSymbols.Contains(x.Symbol))
            .Select(x => new { x.Id, x.Symbol })
            .ToListAsync(cancellationToken);

        var symbolByCanonical = symbolMappings.ToDictionary(x => x.Symbol, x => x.Id, StringComparer.Ordinal);
        var unresolvedSymbols = requestedSymbols.Where(x => !symbolByCanonical.ContainsKey(x)).OrderBy(x => x).ToArray();
        if (unresolvedSymbols.Length > 0)
        {
            throw new InvalidOperationException(
                $"Cannot load PIT constituents for index '{normalized.IndexCode}'. Missing canonical symbols: {string.Join(", ", unresolvedSymbols)}.");
        }

        var requestedBySymbolMasterId = normalized.Constituents
            .ToDictionary(
                x => symbolByCanonical[x.CanonicalSymbol],
                x => x,
                EqualityComparer<long>.Default);

        var activeRows = await dbContext.IndexConstituentsPit
            .Where(x => x.IndexCode == normalized.IndexCode)
            .Where(x => x.EffectiveFrom <= normalized.EffectiveFrom)
            .Where(x => x.EffectiveTo == null || x.EffectiveTo >= normalized.EffectiveFrom)
            .ToListAsync(cancellationToken);

        var activeBySymbolMasterId = activeRows.ToDictionary(x => x.SymbolMasterId, EqualityComparer<long>.Default);

        var insertedRows = 0;
        var closedRows = 0;
        var removedSameDayRows = 0;
        var updatedRows = 0;
        var unchangedRows = 0;

        foreach (var active in activeRows)
        {
            if (!requestedBySymbolMasterId.TryGetValue(active.SymbolMasterId, out var requested))
            {
                if (active.EffectiveFrom == normalized.EffectiveFrom)
                {
                    dbContext.IndexConstituentsPit.Remove(active);
                    removedSameDayRows++;
                    continue;
                }

                var closeTo = normalized.EffectiveFrom.AddDays(-1);
                if (closeTo >= active.EffectiveFrom && active.EffectiveTo != closeTo)
                {
                    active.EffectiveTo = closeTo;
                    closedRows++;
                }

                continue;
            }

            var changed = false;
            if (!string.Equals(active.Source, normalized.Source, StringComparison.Ordinal))
            {
                active.Source = normalized.Source;
                changed = true;
            }

            if (active.Weight != requested.Weight)
            {
                active.Weight = requested.Weight;
                changed = true;
            }

            if (changed)
            {
                updatedRows++;
            }
            else
            {
                unchangedRows++;
            }
        }

        foreach (var requested in requestedBySymbolMasterId)
        {
            if (activeBySymbolMasterId.ContainsKey(requested.Key))
            {
                continue;
            }

            dbContext.IndexConstituentsPit.Add(new IndexConstituentPit
            {
                IndexCode = normalized.IndexCode,
                SymbolMasterId = requested.Key,
                EffectiveFrom = normalized.EffectiveFrom,
                EffectiveTo = null,
                Weight = requested.Value.Weight,
                Source = normalized.Source
            });

            insertedRows++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new IndexConstituentSnapshotLoadResult(
            IndexCode: normalized.IndexCode,
            EffectiveFrom: normalized.EffectiveFrom,
            InsertedMembershipRows: insertedRows,
            ClosedMembershipRows: closedRows,
            RemovedSameDayRows: removedSameDayRows,
            UpdatedActiveRows: updatedRows,
            UnchangedActiveRows: unchangedRows,
            RequestedConstituentCount: normalized.Constituents.Count);
    }

    public async Task<IReadOnlyList<IndexConstituentMembershipSnapshot>> GetConstituentsAsOfAsync(
        string indexCode,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var normalizedIndexCode = NormalizeIndexCode(indexCode);

        var rows = await dbContext.IndexConstituentsPit
            .AsNoTracking()
            .Include(x => x.SymbolMaster)
            .Where(x => x.IndexCode == normalizedIndexCode)
            .Where(x => x.EffectiveFrom <= asOfDate)
            .Where(x => x.EffectiveTo == null || x.EffectiveTo >= asOfDate)
            .OrderBy(x => x.SymbolMaster.Symbol)
            .Select(x => new IndexConstituentMembershipSnapshot(
                x.IndexCode,
                x.SymbolMasterId,
                x.SymbolMaster.Symbol,
                x.EffectiveFrom,
                x.EffectiveTo,
                x.Weight,
                x.Source))
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<IReadOnlyList<IndexConstituentMembershipSnapshot>> GetConstituentHistoryAsync(
        string indexCode,
        string canonicalSymbol,
        CancellationToken cancellationToken = default)
    {
        var normalizedIndexCode = NormalizeIndexCode(indexCode);
        var normalizedCanonicalSymbol = NormalizeRequired(canonicalSymbol, nameof(canonicalSymbol), toUpper: true);

        var rows = await dbContext.IndexConstituentsPit
            .AsNoTracking()
            .Include(x => x.SymbolMaster)
            .Where(x => x.IndexCode == normalizedIndexCode)
            .Where(x => x.SymbolMaster.Symbol == normalizedCanonicalSymbol)
            .OrderBy(x => x.EffectiveFrom)
            .Select(x => new IndexConstituentMembershipSnapshot(
                x.IndexCode,
                x.SymbolMasterId,
                x.SymbolMaster.Symbol,
                x.EffectiveFrom,
                x.EffectiveTo,
                x.Weight,
                x.Source))
            .ToListAsync(cancellationToken);

        return rows;
    }

    private static void Validate(NormalizedSnapshotRequest request)
    {
        if (request.Constituents.Count == 0)
        {
            throw new ArgumentException("Constituents list must not be empty.");
        }

        foreach (var constituent in request.Constituents)
        {
            if (constituent.Weight is null)
            {
                continue;
            }

            if (constituent.Weight < 0m || constituent.Weight > 1m)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "Constituent weight must be between 0 and 1 when provided.");
            }
        }
    }

    private static string NormalizeIndexCode(string indexCode)
    {
        var normalized = NormalizeRequired(indexCode, nameof(indexCode), toUpper: true);

        if (!UniverseCodes.Supported.Contains(normalized, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Unsupported index code '{normalized}'. Supported values: {string.Join(", ", UniverseCodes.Supported)}.",
                nameof(indexCode));
        }

        return normalized;
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

    private sealed record NormalizedSnapshotRequest(
        string IndexCode,
        string Source,
        DateOnly EffectiveFrom,
        IReadOnlyList<NormalizedConstituent> Constituents)
    {
        public static NormalizedSnapshotRequest From(IndexConstituentSnapshotLoadRequest request)
        {
            var indexCode = NormalizeIndexCode(request.IndexCode);
            var source = NormalizeRequired(request.Source, nameof(request.Source), toUpper: false);
            var inputConstituents = request.Constituents ?? throw new ArgumentException("Constituents list must not be null.", nameof(request.Constituents));

            var constituents = new List<NormalizedConstituent>(inputConstituents.Count);
            var duplicateCheck = new HashSet<string>(StringComparer.Ordinal);

            foreach (var constituent in inputConstituents)
            {
                ArgumentNullException.ThrowIfNull(constituent);

                var canonicalSymbol = NormalizeRequired(constituent.CanonicalSymbol, nameof(constituent.CanonicalSymbol), toUpper: true);
                if (!duplicateCheck.Add(canonicalSymbol))
                {
                    throw new ArgumentException($"Duplicate constituent symbol detected: {canonicalSymbol}.", nameof(request.Constituents));
                }

                constituents.Add(new NormalizedConstituent(canonicalSymbol, constituent.Weight));
            }

            return new NormalizedSnapshotRequest(indexCode, source, request.EffectiveFrom, constituents);
        }
    }

    private sealed record NormalizedConstituent(string CanonicalSymbol, decimal? Weight);

    public void Dispose()
    {
        dbContext.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return dbContext.DisposeAsync();
    }
}
