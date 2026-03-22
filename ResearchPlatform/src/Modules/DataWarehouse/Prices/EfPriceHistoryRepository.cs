using DataWarehouse.Schema;
using DataWarehouse.Schema.Entities;
using DataWarehouse.Schema.Enums;
using Microsoft.EntityFrameworkCore;
using ResearchPlatform.Contracts.Abstractions;
using ResearchPlatform.Contracts.Ingestion;
using ResearchPlatform.Contracts.Prices;

namespace DataWarehouse.Prices;

public sealed class EfPriceHistoryRepository(ResearchWarehouseDbContext dbContext)
    : IPriceHistoryRepository, IDisposable, IAsyncDisposable
{
    public async Task<DailyPriceLoadResult> UpsertRawPricesAsync(
        DailyPriceLoadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalized = NormalizedRawPriceLoadRequest.From(request);
        ValidateRawPriceLoad(normalized);

        var nowUtc = DateTime.UtcNow;
        var ingestionRun = new IngestionRun
        {
            Pipeline = normalized.Pipeline,
            Provider = normalized.Provider,
            Status = IngestionRunStatus.Started,
            RequestedAtUtc = nowUtc,
            StartedAtUtc = nowUtc,
            RequestParametersJson = normalized.RequestParametersJson,
            RowsRead = normalized.Prices.Count
        };

        dbContext.IngestionRuns.Add(ingestionRun);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var resolvedPrices = await ResolveRawPricesAsync(normalized, cancellationToken);
            var rowsInserted = 0;
            var rowsUpdated = 0;

            if (resolvedPrices.Count > 0)
            {
                var symbolMasterIds = resolvedPrices.Select(x => x.SymbolMasterId).Distinct().ToArray();
                var existingRows = await dbContext.PricesDailyRaw
                    .Where(x => x.Provider == normalized.Provider)
                    .Where(x => symbolMasterIds.Contains(x.SymbolMasterId))
                    .Where(x => x.TradeDate >= normalized.FromDate && x.TradeDate <= normalized.ToDate)
                    .ToListAsync(cancellationToken);

                var existingByKey = existingRows.ToDictionary(
                    x => new RawPriceKey(x.SymbolMasterId, x.TradeDate, x.Provider),
                    EqualityComparer<RawPriceKey>.Default);

                foreach (var price in resolvedPrices.OrderBy(x => x.TradeDate).ThenBy(x => x.CanonicalSymbol, StringComparer.Ordinal))
                {
                    var key = new RawPriceKey(price.SymbolMasterId, price.TradeDate, price.Provider);
                    if (!existingByKey.TryGetValue(key, out var existing))
                    {
                        var entity = new PriceDailyRaw
                        {
                            SymbolMasterId = price.SymbolMasterId,
                            TradeDate = price.TradeDate,
                            Open = price.Open,
                            High = price.High,
                            Low = price.Low,
                            Close = price.Close,
                            Volume = price.Volume,
                            Vwap = price.Vwap,
                            Provider = price.Provider,
                            IngestionRunId = ingestionRun.Id
                        };

                        dbContext.PricesDailyRaw.Add(entity);
                        existingByKey[key] = entity;
                        rowsInserted++;
                        continue;
                    }

                    if (ApplyRawPriceUpdates(existing, price, ingestionRun.Id))
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

            return new DailyPriceLoadResult(
                RunId: ingestionRun.RunId,
                Pipeline: ingestionRun.Pipeline,
                Provider: ingestionRun.Provider,
                FromDate: normalized.FromDate,
                ToDate: normalized.ToDate,
                RowsRead: normalized.Prices.Count,
                RowsInserted: rowsInserted,
                RowsUpdated: rowsUpdated,
                ResolvedSymbolCount: resolvedPrices.Select(x => x.SymbolMasterId).Distinct().Count());
        }
        catch (OperationCanceledException)
        {
            await MarkRunAsync(
                ingestionRun.Id,
                IngestionRunStatus.Cancelled,
                "Daily raw price load was cancelled.",
                normalized.Prices.Count,
                CancellationToken.None);
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            await MarkRunAsync(
                ingestionRun.Id,
                IngestionRunStatus.Failed,
                ex.Message,
                normalized.Prices.Count,
                CancellationToken.None);
            throw;
        }
    }

    public async Task<AdjustedPriceBuildResult> RebuildAdjustedPricesAsync(
        AdjustedPriceBuildRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalized = NormalizedAdjustedPriceBuildRequest.From(request);
        ValidateAdjustedPriceBuild(normalized);

        var nowUtc = DateTime.UtcNow;
        var ingestionRun = new IngestionRun
        {
            Pipeline = normalized.Pipeline,
            Provider = normalized.Provider,
            Status = IngestionRunStatus.Started,
            RequestedAtUtc = nowUtc,
            StartedAtUtc = nowUtc,
            RequestParametersJson = normalized.RequestParametersJson
        };

        dbContext.IngestionRuns.Add(ingestionRun);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var symbolMasters = await dbContext.SymbolMasters
                .AsNoTracking()
                .Where(x => normalized.CanonicalSymbols.Contains(x.Symbol))
                .Select(x => new { x.Id, x.Symbol })
                .ToListAsync(cancellationToken);

            var symbolByCanonical = symbolMasters.ToDictionary(x => x.Symbol, x => x.Id, StringComparer.Ordinal);
            var unresolvedSymbols = normalized.CanonicalSymbols
                .Where(x => !symbolByCanonical.ContainsKey(x))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();

            if (unresolvedSymbols.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Cannot rebuild adjusted prices for provider '{normalized.Provider}'. Missing canonical symbols: {string.Join(", ", unresolvedSymbols)}.");
            }

            var symbolMasterIds = symbolMasters.Select(x => x.Id).ToArray();
            if (symbolMasterIds.Length == 0)
            {
                ingestionRun.Status = IngestionRunStatus.Succeeded;
                ingestionRun.FinishedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                return new AdjustedPriceBuildResult(
                    RunId: ingestionRun.RunId,
                    Pipeline: ingestionRun.Pipeline,
                    Provider: ingestionRun.Provider,
                    AdjustmentBasis: normalized.AdjustmentBasis,
                    FromDate: normalized.FromDate,
                    ToDate: normalized.ToDate,
                    RowsRead: 0,
                    RowsInserted: 0,
                    RowsUpdated: 0,
                    SymbolsProcessed: 0);
            }

            var latestTradeDates = await dbContext.PricesDailyRaw
                .AsNoTracking()
                .Where(x => x.Provider == normalized.Provider)
                .Where(x => symbolMasterIds.Contains(x.SymbolMasterId))
                .GroupBy(x => x.SymbolMasterId)
                .Select(x => new { SymbolMasterId = x.Key, MaxTradeDate = x.Max(y => y.TradeDate) })
                .ToListAsync(cancellationToken);

            if (latestTradeDates.Count == 0)
            {
                ingestionRun.Status = IngestionRunStatus.Succeeded;
                ingestionRun.FinishedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                return new AdjustedPriceBuildResult(
                    RunId: ingestionRun.RunId,
                    Pipeline: ingestionRun.Pipeline,
                    Provider: ingestionRun.Provider,
                    AdjustmentBasis: normalized.AdjustmentBasis,
                    FromDate: normalized.FromDate,
                    ToDate: normalized.ToDate,
                    RowsRead: 0,
                    RowsInserted: 0,
                    RowsUpdated: 0,
                    SymbolsProcessed: 0);
            }

            var globalMaxTradeDate = latestTradeDates.Max(x => x.MaxTradeDate);

            var rawRows = await dbContext.PricesDailyRaw
                .Where(x => x.Provider == normalized.Provider)
                .Where(x => symbolMasterIds.Contains(x.SymbolMasterId))
                .Where(x => x.TradeDate >= normalized.FromDate && x.TradeDate <= globalMaxTradeDate)
                .OrderBy(x => x.SymbolMasterId)
                .ThenByDescending(x => x.TradeDate)
                .ToListAsync(cancellationToken);

            var corporateActions = await dbContext.CorporateActions
                .AsNoTracking()
                .Where(x => x.Provider == normalized.Provider)
                .Where(x => symbolMasterIds.Contains(x.SymbolMasterId))
                .Where(x => x.ActionDate >= normalized.FromDate && x.ActionDate <= globalMaxTradeDate)
                .OrderBy(x => x.SymbolMasterId)
                .ThenByDescending(x => x.ActionDate)
                .ToListAsync(cancellationToken);

            var existingAdjustedRows = await dbContext.PricesDailyAdjusted
                .Where(x => x.Provider == normalized.Provider)
                .Where(x => x.AdjustmentBasis == normalized.AdjustmentBasis)
                .Where(x => symbolMasterIds.Contains(x.SymbolMasterId))
                .Where(x => x.TradeDate >= normalized.FromDate && x.TradeDate <= normalized.ToDate)
                .ToListAsync(cancellationToken);

            var existingByKey = existingAdjustedRows.ToDictionary(
                x => new AdjustedPriceKey(x.SymbolMasterId, x.TradeDate, x.Provider, x.AdjustmentBasis),
                EqualityComparer<AdjustedPriceKey>.Default);

            var rawRowsBySymbol = rawRows
                .GroupBy(x => x.SymbolMasterId)
                .ToDictionary(x => x.Key, x => x.ToList(), EqualityComparer<long>.Default);

            var actionsBySymbol = corporateActions
                .GroupBy(x => x.SymbolMasterId)
                .ToDictionary(
                    x => x.Key,
                    x => x.OrderByDescending(y => y.ActionDate)
                        .ThenBy(y => y.ActionType == CorporateActionType.Split ? 0 : 1)
                        .ToList(),
                    EqualityComparer<long>.Default);

            var rowsRead = 0;
            var rowsInserted = 0;
            var rowsUpdated = 0;
            var symbolsProcessed = 0;

            foreach (var symbolMaster in symbolMasters.OrderBy(x => x.Symbol, StringComparer.Ordinal))
            {
                if (!rawRowsBySymbol.TryGetValue(symbolMaster.Id, out var symbolRawRows) || symbolRawRows.Count == 0)
                {
                    continue;
                }

                rowsRead += symbolRawRows.Count;
                symbolsProcessed++;

                var symbolActions = actionsBySymbol.TryGetValue(symbolMaster.Id, out var groupedActions)
                    ? groupedActions
                    : [];

                var priceFactor = 1m;
                var volumeFactor = 1m;
                var actionIndex = 0;

                for (var index = 0; index < symbolRawRows.Count; index++)
                {
                    var rawRow = symbolRawRows[index];

                    if (rawRow.TradeDate <= normalized.ToDate)
                    {
                        var adjusted = BuildAdjustedRow(rawRow, normalized.AdjustmentBasis, priceFactor, volumeFactor, ingestionRun.Id);
                        var key = new AdjustedPriceKey(
                            adjusted.SymbolMasterId,
                            adjusted.TradeDate,
                            adjusted.Provider,
                            adjusted.AdjustmentBasis);

                        if (!existingByKey.TryGetValue(key, out var existing))
                        {
                            dbContext.PricesDailyAdjusted.Add(adjusted);
                            existingByKey[key] = adjusted;
                            rowsInserted++;
                        }
                        else if (ApplyAdjustedPriceUpdates(existing, adjusted))
                        {
                            rowsUpdated++;
                        }
                    }

                    if (index == symbolRawRows.Count - 1)
                    {
                        continue;
                    }

                    while (actionIndex < symbolActions.Count && symbolActions[actionIndex].ActionDate > rawRow.TradeDate)
                    {
                        actionIndex++;
                    }

                    var previousTradingDayRow = symbolRawRows[index + 1];
                    var applicableActions = new List<CorporateAction>(2);

                    while (actionIndex < symbolActions.Count &&
                           symbolActions[actionIndex].ActionDate <= rawRow.TradeDate &&
                           symbolActions[actionIndex].ActionDate > previousTradingDayRow.TradeDate)
                    {
                        applicableActions.Add(symbolActions[actionIndex]);
                        actionIndex++;
                    }

                    if (applicableActions.Count == 0)
                    {
                        continue;
                    }

                    ApplyCorporateActionFactors(
                        applicableActions,
                        previousTradingDayRow,
                        normalized.AdjustmentBasis,
                        ref priceFactor,
                        ref volumeFactor);
                }
            }

            ingestionRun.Status = IngestionRunStatus.Succeeded;
            ingestionRun.FinishedAtUtc = DateTime.UtcNow;
            ingestionRun.RowsRead = rowsRead;
            ingestionRun.RowsInserted = rowsInserted;
            ingestionRun.RowsUpdated = rowsUpdated;

            ValidatePendingAdjustedRows();
            await dbContext.SaveChangesAsync(cancellationToken);

            return new AdjustedPriceBuildResult(
                RunId: ingestionRun.RunId,
                Pipeline: ingestionRun.Pipeline,
                Provider: ingestionRun.Provider,
                AdjustmentBasis: normalized.AdjustmentBasis,
                FromDate: normalized.FromDate,
                ToDate: normalized.ToDate,
                RowsRead: rowsRead,
                RowsInserted: rowsInserted,
                RowsUpdated: rowsUpdated,
                SymbolsProcessed: symbolsProcessed);
        }
        catch (OperationCanceledException)
        {
            await MarkRunAsync(
                ingestionRun.Id,
                IngestionRunStatus.Cancelled,
                "Adjusted price rebuild was cancelled.",
                ingestionRun.RowsRead,
                CancellationToken.None);
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            await MarkRunAsync(
                ingestionRun.Id,
                IngestionRunStatus.Failed,
                ex.Message,
                ingestionRun.RowsRead,
                CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<RawDailyPriceSnapshot>> GetRawPricesAsync(
        string canonicalSymbol,
        string provider,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var normalizedSymbol = NormalizeRequired(canonicalSymbol, nameof(canonicalSymbol), toUpper: true);
        var normalizedProvider = NormalizeRequired(provider, nameof(provider), toUpper: true);

        if (fromDate > toDate)
        {
            throw new ArgumentException($"Invalid raw price range: {fromDate:yyyy-MM-dd} > {toDate:yyyy-MM-dd}.");
        }

        return await dbContext.PricesDailyRaw
            .AsNoTracking()
            .Where(x => x.SymbolMaster.Symbol == normalizedSymbol)
            .Where(x => x.Provider == normalizedProvider)
            .Where(x => x.TradeDate >= fromDate && x.TradeDate <= toDate)
            .OrderBy(x => x.TradeDate)
            .Select(x => new RawDailyPriceSnapshot(
                x.Id,
                x.SymbolMasterId,
                x.SymbolMaster.Symbol,
                x.TradeDate,
                x.Open,
                x.High,
                x.Low,
                x.Close,
                x.Volume,
                x.Vwap,
                x.Provider,
                x.IngestionRun != null ? x.IngestionRun.RunId : null))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdjustedDailyPriceSnapshot>> GetAdjustedPricesAsync(
        string canonicalSymbol,
        string provider,
        string adjustmentBasis,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var normalizedSymbol = NormalizeRequired(canonicalSymbol, nameof(canonicalSymbol), toUpper: true);
        var normalizedProvider = NormalizeRequired(provider, nameof(provider), toUpper: true);
        var normalizedBasis = NormalizeAdjustmentBasis(adjustmentBasis);

        if (fromDate > toDate)
        {
            throw new ArgumentException($"Invalid adjusted price range: {fromDate:yyyy-MM-dd} > {toDate:yyyy-MM-dd}.");
        }

        return await dbContext.PricesDailyAdjusted
            .AsNoTracking()
            .Where(x => x.SymbolMaster.Symbol == normalizedSymbol)
            .Where(x => x.Provider == normalizedProvider)
            .Where(x => x.AdjustmentBasis == normalizedBasis)
            .Where(x => x.TradeDate >= fromDate && x.TradeDate <= toDate)
            .OrderBy(x => x.TradeDate)
            .Select(x => new AdjustedDailyPriceSnapshot(
                x.Id,
                x.SymbolMasterId,
                x.SymbolMaster.Symbol,
                x.TradeDate,
                x.Open,
                x.High,
                x.Low,
                x.Close,
                x.AdjustedClose,
                x.AdjustmentFactor,
                x.Volume,
                x.AdjustmentBasis,
                x.Provider,
                x.IngestionRun != null ? x.IngestionRun.RunId : null))
            .ToListAsync(cancellationToken);
    }

    public void Dispose() => dbContext.Dispose();

    public ValueTask DisposeAsync() => dbContext.DisposeAsync();

    private async Task<IReadOnlyList<ResolvedRawPrice>> ResolveRawPricesAsync(
        NormalizedRawPriceLoadRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Prices.Count == 0)
        {
            return [];
        }

        var distinctProviderSymbols = request.Prices.Select(x => x.ProviderSymbol).Distinct(StringComparer.Ordinal).ToArray();
        var minTradeDate = request.Prices.Min(x => x.TradeDate);
        var maxTradeDate = request.Prices.Max(x => x.TradeDate);

        var mappings = await dbContext.SymbolMappings
            .AsNoTracking()
            .Include(x => x.SymbolMaster)
            .Where(x => x.Provider == request.Provider)
            .Where(x => distinctProviderSymbols.Contains(x.ProviderSymbol))
            .Where(x => x.EffectiveFrom <= maxTradeDate)
            .Where(x => x.EffectiveTo == null || x.EffectiveTo >= minTradeDate)
            .OrderBy(x => x.ProviderSymbol)
            .ThenByDescending(x => x.EffectiveFrom)
            .ToListAsync(cancellationToken);

        var mappingsByProviderSymbol = mappings
            .GroupBy(x => x.ProviderSymbol, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);

        var resolved = new List<ResolvedRawPrice>(request.Prices.Count);
        var seenKeys = new HashSet<RawPriceKey>(EqualityComparer<RawPriceKey>.Default);

        foreach (var price in request.Prices)
        {
            if (!mappingsByProviderSymbol.TryGetValue(price.ProviderSymbol, out var candidates))
            {
                throw new InvalidOperationException(
                    $"Cannot load raw daily price for provider '{request.Provider}'. " +
                    $"No canonical mapping exists for provider symbol '{price.ProviderSymbol}'.");
            }

            var mapping = candidates.FirstOrDefault(x =>
                x.EffectiveFrom <= price.TradeDate &&
                (x.EffectiveTo == null || x.EffectiveTo >= price.TradeDate));

            if (mapping is null)
            {
                throw new InvalidOperationException(
                    $"Cannot load raw daily price for provider '{request.Provider}'. " +
                    $"Provider symbol '{price.ProviderSymbol}' is not mapped on {price.TradeDate:yyyy-MM-dd}.");
            }

            var resolvedPrice = new ResolvedRawPrice(
                SymbolMasterId: mapping.SymbolMasterId,
                CanonicalSymbol: mapping.SymbolMaster.Symbol,
                TradeDate: price.TradeDate,
                Open: price.Open,
                High: price.High,
                Low: price.Low,
                Close: price.Close,
                Volume: price.Volume,
                Vwap: price.Vwap,
                Provider: request.Provider);

            var key = new RawPriceKey(resolvedPrice.SymbolMasterId, resolvedPrice.TradeDate, resolvedPrice.Provider);
            if (!seenKeys.Add(key))
            {
                throw new InvalidOperationException(
                    $"Duplicate raw daily price detected in batch for canonical symbol '{resolvedPrice.CanonicalSymbol}' " +
                    $"on {resolvedPrice.TradeDate:yyyy-MM-dd}.");
            }

            resolved.Add(resolvedPrice);
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

    private static void ApplyCorporateActionFactors(
        IReadOnlyList<CorporateAction> actionsOnDate,
        PriceDailyRaw previousTradingDayRow,
        string adjustmentBasis,
        ref decimal priceFactor,
        ref decimal volumeFactor)
    {
        foreach (var action in actionsOnDate)
        {
            switch (action.ActionType)
            {
                case CorporateActionType.Split:
                {
                    if (action.Value <= 0m)
                    {
                        throw new InvalidOperationException(
                            $"Invalid split ratio for symbolMasterId={action.SymbolMasterId} on {action.ActionDate:yyyy-MM-dd}: {action.Value}.");
                    }

                    var splitPriceFactor = decimal.Round(1m / action.Value, 8, MidpointRounding.AwayFromZero);
                    priceFactor = decimal.Round(priceFactor * splitPriceFactor, 8, MidpointRounding.AwayFromZero);
                    volumeFactor = decimal.Round(volumeFactor * action.Value, 8, MidpointRounding.AwayFromZero);
                    break;
                }
                case CorporateActionType.Dividend when adjustmentBasis == AdjustmentBasisCodes.SplitAndDividend:
                {
                    if (previousTradingDayRow.Close <= 0m)
                    {
                        throw new InvalidOperationException(
                            $"Cannot compute dividend adjustment because prior close is not positive for symbolMasterId={action.SymbolMasterId} " +
                            $"before {action.ActionDate:yyyy-MM-dd}.");
                    }

                    var dividendFactor = decimal.Round(
                        (previousTradingDayRow.Close - action.Value) / previousTradingDayRow.Close,
                        8,
                        MidpointRounding.AwayFromZero);

                    if (dividendFactor <= 0m || dividendFactor > 1m)
                    {
                        throw new InvalidOperationException(
                            $"Computed dividend adjustment factor {dividendFactor} is invalid for symbolMasterId={action.SymbolMasterId} " +
                            $"on {action.ActionDate:yyyy-MM-dd}. Prior close={previousTradingDayRow.Close}, cash={action.Value}.");
                    }

                    priceFactor = decimal.Round(priceFactor * dividendFactor, 8, MidpointRounding.AwayFromZero);
                    break;
                }
            }
        }
    }

    private static PriceDailyAdjusted BuildAdjustedRow(
        PriceDailyRaw rawRow,
        string adjustmentBasis,
        decimal priceFactor,
        decimal volumeFactor,
        long ingestionRunId)
    {
        var adjustedOpen = RoundPrice(rawRow.Open * priceFactor);
        var adjustedHigh = RoundPrice(rawRow.High * priceFactor);
        var adjustedLow = RoundPrice(rawRow.Low * priceFactor);
        var adjustedClose = RoundPrice(rawRow.Close * priceFactor);

        return new PriceDailyAdjusted
        {
            SymbolMasterId = rawRow.SymbolMasterId,
            TradeDate = rawRow.TradeDate,
            Open = adjustedOpen,
            High = adjustedHigh,
            Low = adjustedLow,
            Close = adjustedClose,
            AdjustedClose = adjustedClose,
            AdjustmentFactor = decimal.Round(priceFactor, 8, MidpointRounding.AwayFromZero),
            Volume = RoundVolume(rawRow.Volume, volumeFactor),
            AdjustmentBasis = adjustmentBasis,
            Provider = rawRow.Provider,
            IngestionRunId = ingestionRunId
        };
    }

    private static bool ApplyRawPriceUpdates(PriceDailyRaw existing, ResolvedRawPrice price, long ingestionRunId)
    {
        var changed = false;

        if (existing.Open != price.Open)
        {
            existing.Open = price.Open;
            changed = true;
        }

        if (existing.High != price.High)
        {
            existing.High = price.High;
            changed = true;
        }

        if (existing.Low != price.Low)
        {
            existing.Low = price.Low;
            changed = true;
        }

        if (existing.Close != price.Close)
        {
            existing.Close = price.Close;
            changed = true;
        }

        if (existing.Volume != price.Volume)
        {
            existing.Volume = price.Volume;
            changed = true;
        }

        if (existing.Vwap != price.Vwap)
        {
            existing.Vwap = price.Vwap;
            changed = true;
        }

        if (existing.IngestionRunId != ingestionRunId)
        {
            existing.IngestionRunId = ingestionRunId;
            changed = true;
        }

        return changed;
    }

    private static bool ApplyAdjustedPriceUpdates(PriceDailyAdjusted existing, PriceDailyAdjusted candidate)
    {
        var changed = false;

        if (existing.Open != candidate.Open)
        {
            existing.Open = candidate.Open;
            changed = true;
        }

        if (existing.High != candidate.High)
        {
            existing.High = candidate.High;
            changed = true;
        }

        if (existing.Low != candidate.Low)
        {
            existing.Low = candidate.Low;
            changed = true;
        }

        if (existing.Close != candidate.Close)
        {
            existing.Close = candidate.Close;
            changed = true;
        }

        if (existing.AdjustedClose != candidate.AdjustedClose)
        {
            existing.AdjustedClose = candidate.AdjustedClose;
            changed = true;
        }

        if (existing.AdjustmentFactor != candidate.AdjustmentFactor)
        {
            existing.AdjustmentFactor = candidate.AdjustmentFactor;
            changed = true;
        }

        if (existing.Volume != candidate.Volume)
        {
            existing.Volume = candidate.Volume;
            changed = true;
        }

        if (existing.IngestionRunId != candidate.IngestionRunId)
        {
            existing.IngestionRunId = candidate.IngestionRunId;
            changed = true;
        }

        return changed;
    }

    private void ValidatePendingAdjustedRows()
    {
        var invalidRows = dbContext.ChangeTracker.Entries<PriceDailyAdjusted>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity)
            .Where(row =>
                row.High < row.Low ||
                row.Open < row.Low ||
                row.Open > row.High ||
                row.Close < row.Low ||
                row.Close > row.High ||
                row.AdjustedClose < 0m)
            .Select(row => new
            {
                row.SymbolMasterId,
                row.TradeDate,
                row.Provider,
                row.AdjustmentBasis,
                row.Open,
                row.High,
                row.Low,
                row.Close,
                row.AdjustedClose,
                row.AdjustmentFactor
            })
            .ToArray();

        if (invalidRows.Length == 0)
        {
            return;
        }

        var details = string.Join(
            " | ",
            invalidRows.Select(row =>
                $"symbolMasterId={row.SymbolMasterId}, tradeDate={row.TradeDate:yyyy-MM-dd}, provider={row.Provider}, basis={row.AdjustmentBasis}, " +
                $"open={row.Open}, high={row.High}, low={row.Low}, close={row.Close}, adjustedClose={row.AdjustedClose}, factor={row.AdjustmentFactor}"));

        throw new InvalidOperationException($"Adjusted price validation failed before save: {details}");
    }

    private static void ValidateRawPriceLoad(NormalizedRawPriceLoadRequest request)
    {
        if (request.FromDate > request.ToDate)
        {
            throw new ArgumentException($"Invalid raw price range: {request.FromDate:yyyy-MM-dd} > {request.ToDate:yyyy-MM-dd}.");
        }
    }

    private static void ValidateAdjustedPriceBuild(NormalizedAdjustedPriceBuildRequest request)
    {
        if (request.FromDate > request.ToDate)
        {
            throw new ArgumentException($"Invalid adjusted price range: {request.FromDate:yyyy-MM-dd} > {request.ToDate:yyyy-MM-dd}.");
        }

        if (request.CanonicalSymbols.Count == 0)
        {
            throw new ArgumentException("At least one canonical symbol is required for adjusted price rebuild.");
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

    private static string NormalizeAdjustmentBasis(string value)
    {
        var normalized = NormalizeRequired(value, nameof(value), toUpper: false);

        return AdjustmentBasisCodes.Supported.FirstOrDefault(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase))
               ?? throw new ArgumentException(
                   $"Unsupported adjustment basis '{value}'. Supported values: {string.Join(", ", AdjustmentBasisCodes.Supported)}.",
                   nameof(value));
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

    private static decimal RoundPrice(decimal value) =>
        decimal.Round(value, 6, MidpointRounding.AwayFromZero);

    private static long RoundVolume(long volume, decimal factor) =>
        decimal.ToInt64(decimal.Round(volume * factor, 0, MidpointRounding.AwayFromZero));

    private sealed record NormalizedRawPriceLoadRequest(
        string Provider,
        string Pipeline,
        DateOnly FromDate,
        DateOnly ToDate,
        IReadOnlyList<NormalizedRawPrice> Prices,
        string? RequestParametersJson)
    {
        public static NormalizedRawPriceLoadRequest From(DailyPriceLoadRequest request)
        {
            var provider = NormalizeRequired(request.Provider, nameof(request.Provider), toUpper: true);
            var pipeline = NormalizeRequired(request.Pipeline, nameof(request.Pipeline), toUpper: false);
            var pricesInput = request.Prices ?? throw new ArgumentException("Price list must not be null.", nameof(request.Prices));

            var normalizedPrices = new List<NormalizedRawPrice>(pricesInput.Count);
            foreach (var price in pricesInput)
            {
                ArgumentNullException.ThrowIfNull(price);

                normalizedPrices.Add(new NormalizedRawPrice(
                    ProviderSymbol: NormalizeRequired(price.ProviderSymbol, nameof(price.ProviderSymbol), toUpper: true),
                    TradeDate: price.TradeDate,
                    Open: price.Open,
                    High: price.High,
                    Low: price.Low,
                    Close: price.Close,
                    Volume: price.Volume,
                    Vwap: price.Vwap));
            }

            return new NormalizedRawPriceLoadRequest(
                Provider: provider,
                Pipeline: pipeline,
                FromDate: request.FromDate,
                ToDate: request.ToDate,
                Prices: normalizedPrices,
                RequestParametersJson: TrimOptional(request.RequestParametersJson, 4000));
        }
    }

    private sealed record NormalizedAdjustedPriceBuildRequest(
        string Provider,
        string AdjustmentBasis,
        IReadOnlyList<string> CanonicalSymbols,
        string Pipeline,
        DateOnly FromDate,
        DateOnly ToDate,
        string? RequestParametersJson)
    {
        public static NormalizedAdjustedPriceBuildRequest From(AdjustedPriceBuildRequest request)
        {
            var provider = NormalizeRequired(request.Provider, nameof(request.Provider), toUpper: true);
            var basis = NormalizeAdjustmentBasis(request.AdjustmentBasis);
            var pipeline = NormalizeRequired(request.Pipeline, nameof(request.Pipeline), toUpper: false);
            var symbolsInput = request.CanonicalSymbols ?? throw new ArgumentException("Canonical symbol list must not be null.", nameof(request.CanonicalSymbols));

            var canonicalSymbols = symbolsInput
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => NormalizeRequired(x, nameof(request.CanonicalSymbols), toUpper: true))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return new NormalizedAdjustedPriceBuildRequest(
                Provider: provider,
                AdjustmentBasis: basis,
                CanonicalSymbols: canonicalSymbols,
                Pipeline: pipeline,
                FromDate: request.FromDate,
                ToDate: request.ToDate,
                RequestParametersJson: TrimOptional(request.RequestParametersJson, 4000));
        }
    }

    private sealed record NormalizedRawPrice(
        string ProviderSymbol,
        DateOnly TradeDate,
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close,
        long Volume,
        decimal? Vwap);

    private sealed record ResolvedRawPrice(
        long SymbolMasterId,
        string CanonicalSymbol,
        DateOnly TradeDate,
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close,
        long Volume,
        decimal? Vwap,
        string Provider);

    private readonly record struct RawPriceKey(long SymbolMasterId, DateOnly TradeDate, string Provider);
    private readonly record struct AdjustedPriceKey(long SymbolMasterId, DateOnly TradeDate, string Provider, string AdjustmentBasis);
}
