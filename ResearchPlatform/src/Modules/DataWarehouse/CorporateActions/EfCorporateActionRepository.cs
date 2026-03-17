using DataWarehouse.Schema;
using DataWarehouse.Schema.Entities;
using DataWarehouse.Schema.Enums;
using Microsoft.EntityFrameworkCore;
using ResearchPlatform.Contracts.Abstractions;
using ResearchPlatform.Contracts.CorporateActions;
using ResearchPlatform.Contracts.Ingestion;

namespace DataWarehouse.CorporateActions;

public sealed class EfCorporateActionRepository(ResearchWarehouseDbContext dbContext)
    : ICorporateActionRepository, IDisposable, IAsyncDisposable
{
    public async Task<CorporateActionLoadResult> UpsertActionsAsync(
        CorporateActionLoadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalized = NormalizedLoadRequest.From(request);
        Validate(normalized);

        var nowUtc = DateTime.UtcNow;
        var ingestionRun = new IngestionRun
        {
            Pipeline = normalized.Pipeline,
            Provider = normalized.Provider,
            Status = IngestionRunStatus.Started,
            RequestedAtUtc = nowUtc,
            StartedAtUtc = nowUtc,
            RequestParametersJson = normalized.RequestParametersJson,
            RowsRead = normalized.Actions.Count
        };

        dbContext.IngestionRuns.Add(ingestionRun);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var resolvedActions = await ResolveActionsAsync(normalized, cancellationToken);
            var rowsInserted = 0;
            var rowsUpdated = 0;

            if (resolvedActions.Count > 0)
            {
                var symbolMasterIds = resolvedActions
                    .Select(x => x.SymbolMasterId)
                    .Distinct()
                    .ToArray();

                var existingRows = await dbContext.CorporateActions
                    .Where(x => x.Provider == normalized.Provider)
                    .Where(x => symbolMasterIds.Contains(x.SymbolMasterId))
                    .Where(x => x.ActionDate >= normalized.FromDate && x.ActionDate <= normalized.ToDate)
                    .ToListAsync(cancellationToken);

                var existingByExternalId = existingRows
                    .Where(x => !string.IsNullOrWhiteSpace(x.ExternalId))
                    .ToDictionary(x => x.ExternalId!, StringComparer.Ordinal);

                var existingByNaturalKey = existingRows.ToDictionary(
                    x => new NaturalKey(x.SymbolMasterId, x.ActionDate, x.ActionType, x.Provider, x.Value),
                    EqualityComparer<NaturalKey>.Default);

                foreach (var action in resolvedActions.OrderBy(x => x.ActionDate).ThenBy(x => x.CanonicalSymbol, StringComparer.Ordinal))
                {
                    CorporateAction? existing = null;
                    if (!string.IsNullOrWhiteSpace(action.ExternalId) &&
                        existingByExternalId.TryGetValue(action.ExternalId, out var existingById))
                    {
                        existing = existingById;
                    }
                    else
                    {
                        existingByNaturalKey.TryGetValue(
                            new NaturalKey(action.SymbolMasterId, action.ActionDate, action.ActionType, action.Provider, action.Value),
                            out existing);
                    }

                    if (existing is null)
                    {
                        var entity = new CorporateAction
                        {
                            SymbolMasterId = action.SymbolMasterId,
                            ActionDate = action.ActionDate,
                            ActionType = action.ActionType,
                            Value = action.Value,
                            AdjustmentFactor = action.AdjustmentFactor,
                            Currency = action.Currency,
                            Provider = action.Provider,
                            ExternalId = action.ExternalId,
                            Description = action.Description,
                            RelatedProviderSymbol = action.RelatedProviderSymbol,
                            AttributesJson = action.AttributesJson,
                            IngestionRunId = ingestionRun.Id
                        };

                        dbContext.CorporateActions.Add(entity);
                        rowsInserted++;

                        if (!string.IsNullOrWhiteSpace(entity.ExternalId))
                        {
                            existingByExternalId[entity.ExternalId] = entity;
                        }

                        existingByNaturalKey[new NaturalKey(entity.SymbolMasterId, entity.ActionDate, entity.ActionType, entity.Provider, entity.Value)] = entity;
                        continue;
                    }

                    if (ApplyUpdates(existing, action, ingestionRun.Id))
                    {
                        rowsUpdated++;
                    }
                }
            }

            ingestionRun.Status = IngestionRunStatus.Succeeded;
            ingestionRun.FinishedAtUtc = DateTime.UtcNow;
            ingestionRun.RowsInserted = rowsInserted;
            ingestionRun.RowsUpdated = rowsUpdated;

            await dbContext.SaveChangesAsync(cancellationToken);

            return new CorporateActionLoadResult(
                RunId: ingestionRun.RunId,
                Pipeline: ingestionRun.Pipeline,
                Provider: ingestionRun.Provider,
                FromDate: normalized.FromDate,
                ToDate: normalized.ToDate,
                RowsRead: normalized.Actions.Count,
                RowsInserted: rowsInserted,
                RowsUpdated: rowsUpdated,
                ResolvedSymbolCount: resolvedActions.Select(x => x.SymbolMasterId).Distinct().Count());
        }
        catch (OperationCanceledException)
        {
            await MarkRunAsync(
                ingestionRun.Id,
                IngestionRunStatus.Cancelled,
                "Corporate action load was cancelled.",
                normalized.Actions.Count,
                CancellationToken.None);
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            await MarkRunAsync(
                ingestionRun.Id,
                IngestionRunStatus.Failed,
                ex.Message,
                normalized.Actions.Count,
                CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<CorporateActionSnapshot>> GetActionsAsync(
        string canonicalSymbol,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var normalizedSymbol = NormalizeRequired(canonicalSymbol, nameof(canonicalSymbol), toUpper: true);
        if (fromDate > toDate)
        {
            throw new ArgumentException($"Invalid corporate action range: {fromDate:yyyy-MM-dd} > {toDate:yyyy-MM-dd}.");
        }

        var rows = await dbContext.CorporateActions
            .AsNoTracking()
            .Where(x => x.SymbolMaster.Symbol == normalizedSymbol)
            .Where(x => x.ActionDate >= fromDate && x.ActionDate <= toDate)
            .OrderBy(x => x.ActionDate)
            .ThenBy(x => x.ActionType)
            .ThenBy(x => x.Provider)
            .Select(x => new CorporateActionSnapshot(
                x.Id,
                x.SymbolMasterId,
                x.SymbolMaster.Symbol,
                x.ActionDate,
                x.ActionType.ToString().ToUpperInvariant(),
                x.Value,
                x.AdjustmentFactor,
                x.Currency,
                x.Provider,
                x.ExternalId,
                x.Description,
                x.RelatedProviderSymbol,
                x.AttributesJson,
                x.IngestionRun != null ? x.IngestionRun.RunId : null))
            .ToListAsync(cancellationToken);

        return rows;
    }

    public void Dispose() => dbContext.Dispose();

    public ValueTask DisposeAsync() => dbContext.DisposeAsync();

    private async Task<IReadOnlyList<ResolvedCorporateAction>> ResolveActionsAsync(
        NormalizedLoadRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Actions.Count == 0)
        {
            return [];
        }

        var distinctProviderSymbols = request.Actions
            .Select(x => x.ProviderSymbol)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var minActionDate = request.Actions.Min(x => x.ActionDate);
        var maxActionDate = request.Actions.Max(x => x.ActionDate);

        var mappings = await dbContext.SymbolMappings
            .AsNoTracking()
            .Include(x => x.SymbolMaster)
            .Where(x => x.Provider == request.Provider)
            .Where(x => distinctProviderSymbols.Contains(x.ProviderSymbol))
            .Where(x => x.EffectiveFrom <= maxActionDate)
            .Where(x => x.EffectiveTo == null || x.EffectiveTo >= minActionDate)
            .OrderBy(x => x.ProviderSymbol)
            .ThenByDescending(x => x.EffectiveFrom)
            .ToListAsync(cancellationToken);

        var mappingsByProviderSymbol = mappings
            .GroupBy(x => x.ProviderSymbol, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);

        var resolved = new List<ResolvedCorporateAction>(request.Actions.Count);
        var seenExternalIds = new HashSet<string>(StringComparer.Ordinal);
        var seenNaturalKeys = new HashSet<NaturalKey>(EqualityComparer<NaturalKey>.Default);

        foreach (var action in request.Actions)
        {
            if (!mappingsByProviderSymbol.TryGetValue(action.ProviderSymbol, out var candidates))
            {
                throw new InvalidOperationException(
                    $"Cannot load corporate action for provider '{request.Provider}'. " +
                    $"No canonical mapping exists for provider symbol '{action.ProviderSymbol}'.");
            }

            var mapping = candidates.FirstOrDefault(x =>
                x.EffectiveFrom <= action.ActionDate &&
                (x.EffectiveTo == null || x.EffectiveTo >= action.ActionDate));

            if (mapping is null)
            {
                throw new InvalidOperationException(
                    $"Cannot load corporate action for provider '{request.Provider}'. " +
                    $"Provider symbol '{action.ProviderSymbol}' is not mapped on {action.ActionDate:yyyy-MM-dd}.");
            }

            var actionType = MapActionType(action.ActionTypeCode);
            var resolvedAction = new ResolvedCorporateAction(
                SymbolMasterId: mapping.SymbolMasterId,
                CanonicalSymbol: mapping.SymbolMaster.Symbol,
                ActionDate: action.ActionDate,
                ActionType: actionType,
                Value: action.Value,
                AdjustmentFactor: action.AdjustmentFactor,
                Currency: action.Currency,
                Provider: request.Provider,
                ExternalId: action.ExternalId,
                Description: action.Description,
                RelatedProviderSymbol: action.RelatedProviderSymbol,
                AttributesJson: action.AttributesJson);

            if (!string.IsNullOrWhiteSpace(resolvedAction.ExternalId) &&
                !seenExternalIds.Add(resolvedAction.ExternalId))
            {
                throw new InvalidOperationException(
                    $"Duplicate corporate action external id detected in batch: {resolvedAction.ExternalId}.");
            }

            var naturalKey = new NaturalKey(
                resolvedAction.SymbolMasterId,
                resolvedAction.ActionDate,
                resolvedAction.ActionType,
                resolvedAction.Provider,
                resolvedAction.Value);

            if (!seenNaturalKeys.Add(naturalKey))
            {
                throw new InvalidOperationException(
                    $"Duplicate corporate action detected in batch for canonical symbol '{resolvedAction.CanonicalSymbol}' " +
                    $"on {resolvedAction.ActionDate:yyyy-MM-dd}.");
            }

            resolved.Add(resolvedAction);
        }

        return resolved;
    }

    private async Task MarkRunAsync(
        long ingestionRunId,
        IngestionRunStatus status,
        string message,
        int rowsRead,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        var run = await dbContext.IngestionRuns.SingleAsync(x => x.Id == ingestionRunId, cancellationToken);
        run.Status = status;
        run.FinishedAtUtc = DateTime.UtcNow;
        run.RowsRead = rowsRead;
        run.ErrorMessage = TrimOptional(message, 4000);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool ApplyUpdates(CorporateAction existing, ResolvedCorporateAction action, long ingestionRunId)
    {
        var changed = false;

        if (existing.SymbolMasterId != action.SymbolMasterId)
        {
            existing.SymbolMasterId = action.SymbolMasterId;
            changed = true;
        }

        if (existing.ActionDate != action.ActionDate)
        {
            existing.ActionDate = action.ActionDate;
            changed = true;
        }

        if (existing.ActionType != action.ActionType)
        {
            existing.ActionType = action.ActionType;
            changed = true;
        }

        if (existing.Value != action.Value)
        {
            existing.Value = action.Value;
            changed = true;
        }

        if (existing.AdjustmentFactor != action.AdjustmentFactor)
        {
            existing.AdjustmentFactor = action.AdjustmentFactor;
            changed = true;
        }

        if (!string.Equals(existing.Currency, action.Currency, StringComparison.Ordinal))
        {
            existing.Currency = action.Currency;
            changed = true;
        }

        if (!string.Equals(existing.ExternalId, action.ExternalId, StringComparison.Ordinal))
        {
            existing.ExternalId = action.ExternalId;
            changed = true;
        }

        if (!string.Equals(existing.Description, action.Description, StringComparison.Ordinal))
        {
            existing.Description = action.Description;
            changed = true;
        }

        if (!string.Equals(existing.RelatedProviderSymbol, action.RelatedProviderSymbol, StringComparison.Ordinal))
        {
            existing.RelatedProviderSymbol = action.RelatedProviderSymbol;
            changed = true;
        }

        if (!string.Equals(existing.AttributesJson, action.AttributesJson, StringComparison.Ordinal))
        {
            existing.AttributesJson = action.AttributesJson;
            changed = true;
        }

        if (existing.IngestionRunId != ingestionRunId)
        {
            existing.IngestionRunId = ingestionRunId;
            changed = true;
        }

        return changed;
    }

    private static void Validate(NormalizedLoadRequest request)
    {
        if (request.FromDate > request.ToDate)
        {
            throw new ArgumentException($"Invalid corporate action range: {request.FromDate:yyyy-MM-dd} > {request.ToDate:yyyy-MM-dd}.");
        }
    }

    private static CorporateActionType MapActionType(string actionTypeCode)
    {
        var normalized = NormalizeRequired(actionTypeCode, nameof(actionTypeCode), toUpper: true)
            .Replace('-', '_')
            .Replace(' ', '_');

        if (normalized.Contains("SPLIT", StringComparison.Ordinal) ||
            normalized.Equals("STOCK_DIVIDEND", StringComparison.Ordinal))
        {
            return CorporateActionType.Split;
        }

        if (normalized.Contains("DIVIDEND", StringComparison.Ordinal))
        {
            return CorporateActionType.Dividend;
        }

        if (normalized.Contains("MERGER", StringComparison.Ordinal))
        {
            return CorporateActionType.Merger;
        }

        if (normalized.Contains("SPINOFF", StringComparison.Ordinal) ||
            normalized.Contains("SPIN_OFF", StringComparison.Ordinal))
        {
            return CorporateActionType.Spinoff;
        }

        if (normalized.Contains("DELIST", StringComparison.Ordinal))
        {
            return CorporateActionType.Delisting;
        }

        if (normalized.Contains("SYMBOL", StringComparison.Ordinal) && normalized.Contains("CHANGE", StringComparison.Ordinal))
        {
            return CorporateActionType.SymbolChange;
        }

        return CorporateActionType.Other;
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

    private static string? TrimOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value exceeds the maximum supported length of {maxLength} characters.");
        }

        return normalized;
    }

    private sealed record NormalizedLoadRequest(
        string Provider,
        string Pipeline,
        DateOnly FromDate,
        DateOnly ToDate,
        IReadOnlyList<NormalizedCorporateAction> Actions,
        string? RequestParametersJson)
    {
        public static NormalizedLoadRequest From(CorporateActionLoadRequest request)
        {
            var provider = NormalizeRequired(request.Provider, nameof(request.Provider), toUpper: true);
            var pipeline = NormalizeRequired(request.Pipeline, nameof(request.Pipeline), toUpper: false);
            var actionsInput = request.Actions ?? throw new ArgumentException("Corporate action list must not be null.", nameof(request.Actions));

            var normalizedActions = new List<NormalizedCorporateAction>(actionsInput.Count);
            foreach (var action in actionsInput)
            {
                ArgumentNullException.ThrowIfNull(action);

                normalizedActions.Add(new NormalizedCorporateAction(
                    ProviderSymbol: NormalizeRequired(action.ProviderSymbol, nameof(action.ProviderSymbol), toUpper: true),
                    ActionDate: action.ActionDate,
                    ActionTypeCode: NormalizeRequired(action.ActionTypeCode, nameof(action.ActionTypeCode), toUpper: true),
                    Value: action.Value,
                    Currency: TrimOptional(action.Currency, 8),
                    ExternalId: TrimOptional(action.ExternalId, 128),
                    Description: TrimOptional(action.Description, 512),
                    RelatedProviderSymbol: TrimOptional(
                        string.IsNullOrWhiteSpace(action.RelatedProviderSymbol) ? null : action.RelatedProviderSymbol.ToUpperInvariant(),
                        64),
                    AdjustmentFactor: action.AdjustmentFactor,
                    AttributesJson: TrimOptional(action.AttributesJson, 4000)));
            }

            return new NormalizedLoadRequest(
                Provider: provider,
                Pipeline: pipeline,
                FromDate: request.FromDate,
                ToDate: request.ToDate,
                Actions: normalizedActions,
                RequestParametersJson: TrimOptional(request.RequestParametersJson, 4000));
        }
    }

    private sealed record NormalizedCorporateAction(
        string ProviderSymbol,
        DateOnly ActionDate,
        string ActionTypeCode,
        decimal Value,
        string? Currency,
        string? ExternalId,
        string? Description,
        string? RelatedProviderSymbol,
        decimal? AdjustmentFactor,
        string? AttributesJson);

    private sealed record ResolvedCorporateAction(
        long SymbolMasterId,
        string CanonicalSymbol,
        DateOnly ActionDate,
        CorporateActionType ActionType,
        decimal Value,
        decimal? AdjustmentFactor,
        string? Currency,
        string Provider,
        string? ExternalId,
        string? Description,
        string? RelatedProviderSymbol,
        string? AttributesJson);

    private readonly record struct NaturalKey(
        long SymbolMasterId,
        DateOnly ActionDate,
        CorporateActionType ActionType,
        string Provider,
        decimal Value);
}
