using System.Text.Json;
using DataWarehouse.Schema;
using DataWarehouse.Schema.Entities;
using DataWarehouse.Schema.Enums;
using Microsoft.EntityFrameworkCore;
using ResearchPlatform.Contracts.Abstractions;
using ResearchPlatform.Contracts.Prices;
using ResearchPlatform.Contracts.Quality;

namespace DataWarehouse.Quality;

public sealed class EfDataQualityRepository(ResearchWarehouseDbContext dbContext)
    : IDataQualityRepository, IDisposable, IAsyncDisposable
{
    private const string RawPricePresenceCheck = "RawPricePresence";
    private const string RawPriceShapeCheck = "RawPriceShape";
    private const string CorporateActionValuesCheck = "CorporateActionValues";
    private const string UnexplainedPriceJumpCheck = "UnexplainedPriceJump";
    private const string AdjustedRowCoverageCheck = "AdjustedRowCoverage";
    private const string AdjustedSeriesShapeCheck = "AdjustedSeriesShape";
    private const int MaxDetailSamples = 5;

    public async Task<DataQualityRunResult> RunChecksAsync(
        DataQualityRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalized = NormalizedRunRequest.From(request);
        Validate(normalized);

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
                    $"Cannot run data-quality checks for provider '{normalized.Provider}'. Missing canonical symbols: {string.Join(", ", unresolvedSymbols)}.");
            }

            var symbolMasterIds = symbolMasters.Select(x => x.Id).ToArray();
            var rawRows = await dbContext.PricesDailyRaw
                .AsNoTracking()
                .Where(x => x.Provider == normalized.Provider)
                .Where(x => symbolMasterIds.Contains(x.SymbolMasterId))
                .Where(x => x.TradeDate >= normalized.FromDate && x.TradeDate <= normalized.ToDate)
                .OrderBy(x => x.SymbolMasterId)
                .ThenBy(x => x.TradeDate)
                .ToListAsync(cancellationToken);

            var corporateActions = await dbContext.CorporateActions
                .AsNoTracking()
                .Where(x => x.Provider == normalized.Provider)
                .Where(x => symbolMasterIds.Contains(x.SymbolMasterId))
                .Where(x => x.ActionDate >= normalized.FromDate && x.ActionDate <= normalized.ToDate)
                .OrderBy(x => x.SymbolMasterId)
                .ThenBy(x => x.ActionDate)
                .ToListAsync(cancellationToken);

            var adjustedRows = await dbContext.PricesDailyAdjusted
                .AsNoTracking()
                .Where(x => x.Provider == normalized.Provider)
                .Where(x => symbolMasterIds.Contains(x.SymbolMasterId))
                .Where(x => normalized.AdjustmentBases.Contains(x.AdjustmentBasis))
                .Where(x => x.TradeDate >= normalized.FromDate && x.TradeDate <= normalized.ToDate)
                .OrderBy(x => x.SymbolMasterId)
                .ThenBy(x => x.AdjustmentBasis)
                .ThenBy(x => x.TradeDate)
                .ToListAsync(cancellationToken);

            var rawRowsBySymbol = rawRows
                .GroupBy(x => x.SymbolMasterId)
                .ToDictionary(x => x.Key, x => (IReadOnlyList<PriceDailyRaw>)x.ToList());

            var actionsBySymbol = corporateActions
                .GroupBy(x => x.SymbolMasterId)
                .ToDictionary(x => x.Key, x => (IReadOnlyList<CorporateAction>)x.ToList());

            var adjustedBySymbolAndBasis = adjustedRows
                .GroupBy(x => new SymbolBasisKey(x.SymbolMasterId, x.AdjustmentBasis))
                .ToDictionary(x => x.Key, x => (IReadOnlyList<PriceDailyAdjusted>)x.ToList());

            var qaResults = new List<QaResult>(normalized.CanonicalSymbols.Count * (4 + normalized.AdjustmentBases.Count * 2));

            foreach (var symbolMaster in symbolMasters.OrderBy(x => x.Symbol, StringComparer.Ordinal))
            {
                var symbolRawRows = rawRowsBySymbol.TryGetValue(symbolMaster.Id, out var rawForSymbol)
                    ? rawForSymbol
                    : [];

                var symbolActions = actionsBySymbol.TryGetValue(symbolMaster.Id, out var actionsForSymbol)
                    ? actionsForSymbol
                    : [];

                qaResults.Add(EvaluateRawPricePresence(normalized.Provider, symbolMaster.Symbol, normalized.FromDate, normalized.ToDate, symbolRawRows, nowUtc, ingestionRun.Id));
                qaResults.Add(EvaluateRawPriceShape(normalized.Provider, symbolMaster.Symbol, symbolRawRows, nowUtc, ingestionRun.Id));
                qaResults.Add(EvaluateCorporateActionValues(normalized.Provider, symbolMaster.Symbol, symbolActions, nowUtc, ingestionRun.Id));
                qaResults.Add(EvaluateUnexplainedPriceJumps(
                    normalized.Provider,
                    symbolMaster.Symbol,
                    normalized.UnexplainedJumpThreshold,
                    symbolRawRows,
                    symbolActions,
                    nowUtc,
                    ingestionRun.Id));

                foreach (var adjustmentBasis in normalized.AdjustmentBases)
                {
                    var adjustedKey = new SymbolBasisKey(symbolMaster.Id, adjustmentBasis);
                    var symbolAdjustedRows = adjustedBySymbolAndBasis.TryGetValue(adjustedKey, out var adjustedForSymbol)
                        ? adjustedForSymbol
                        : [];

                    qaResults.Add(EvaluateAdjustedRowCoverage(
                        normalized.Provider,
                        symbolMaster.Symbol,
                        adjustmentBasis,
                        symbolRawRows,
                        symbolAdjustedRows,
                        nowUtc,
                        ingestionRun.Id));

                    qaResults.Add(EvaluateAdjustedSeriesShape(
                        normalized.Provider,
                        symbolMaster.Symbol,
                        adjustmentBasis,
                        symbolAdjustedRows,
                        nowUtc,
                        ingestionRun.Id));
                }
            }

            dbContext.QaResults.AddRange(qaResults);

            ingestionRun.Status = IngestionRunStatus.Succeeded;
            ingestionRun.FinishedAtUtc = DateTime.UtcNow;
            ingestionRun.RowsRead = rawRows.Count + corporateActions.Count + adjustedRows.Count;
            ingestionRun.RowsInserted = qaResults.Count;

            await dbContext.SaveChangesAsync(cancellationToken);

            var checksFailed = qaResults.Count(x => x.Status == QaStatus.Fail);
            var checksPassed = qaResults.Count - checksFailed;
            var errorCount = qaResults.Count(x => x.Status == QaStatus.Fail && x.Severity == QaSeverity.Error);
            var warningCount = qaResults.Count(x => x.Status == QaStatus.Fail && x.Severity == QaSeverity.Warning);

            return new DataQualityRunResult(
                RunId: ingestionRun.RunId,
                Pipeline: ingestionRun.Pipeline,
                Provider: ingestionRun.Provider,
                FromDate: normalized.FromDate,
                ToDate: normalized.ToDate,
                RowsRead: ingestionRun.RowsRead,
                ChecksEvaluated: qaResults.Count,
                ChecksPassed: checksPassed,
                ChecksFailed: checksFailed,
                ErrorCount: errorCount,
                WarningCount: warningCount,
                SymbolsProcessed: symbolMasters.Count);
        }
        catch (OperationCanceledException)
        {
            await MarkRunAsync(
                ingestionRun.Id,
                IngestionRunStatus.Cancelled,
                "Data-quality suite was cancelled.",
                CancellationToken.None);
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            await MarkRunAsync(
                ingestionRun.Id,
                IngestionRunStatus.Failed,
                ex.Message,
                CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<DataQualityResultSnapshot>> GetResultsAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.QaResults
            .AsNoTracking()
            .Where(x => x.IngestionRun != null && x.IngestionRun.RunId == runId)
            .Select(x => new
            {
                x.Id,
                RunId = x.IngestionRun != null ? x.IngestionRun.RunId : (Guid?)null,
                x.CheckName,
                x.Scope,
                x.Severity,
                x.Status,
                x.AffectedRows,
                x.DetailsJson,
                x.CreatedUtc
            })
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(x => x.Status == QaStatus.Fail)
            .ThenByDescending(x => x.Severity)
            .ThenBy(x => x.CheckName, StringComparer.Ordinal)
            .ThenBy(x => x.Scope, StringComparer.Ordinal)
            .Select(x => new DataQualityResultSnapshot(
                x.Id,
                x.RunId,
                x.CheckName,
                x.Scope,
                MapSeverity(x.Severity),
                MapStatus(x.Status),
                x.AffectedRows,
                x.DetailsJson,
                x.CreatedUtc))
            .ToList();
    }

    public void Dispose() => dbContext.Dispose();

    public ValueTask DisposeAsync() => dbContext.DisposeAsync();

    private async Task MarkRunAsync(
        long ingestionRunId,
        IngestionRunStatus status,
        string message,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        var run = await dbContext.IngestionRuns.SingleAsync(x => x.Id == ingestionRunId, cancellationToken);
        run.Status = status;
        run.FinishedAtUtc = DateTime.UtcNow;
        run.ErrorMessage = TrimOptional(message, 4000);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static QaResult EvaluateRawPricePresence(
        string provider,
        string canonicalSymbol,
        DateOnly fromDate,
        DateOnly toDate,
        IReadOnlyList<PriceDailyRaw> rawRows,
        DateTime createdUtc,
        long ingestionRunId)
    {
        if (rawRows.Count > 0)
        {
            return CreateResult(
                RawPricePresenceCheck,
                BuildScope(provider, canonicalSymbol),
                QaSeverity.Info,
                QaStatus.Pass,
                0,
                new
                {
                    RowCount = rawRows.Count,
                    FromDate = fromDate,
                    ToDate = toDate,
                    FirstTradeDate = rawRows.First().TradeDate,
                    LastTradeDate = rawRows.Last().TradeDate
                },
                createdUtc,
                ingestionRunId);
        }

        return CreateResult(
            RawPricePresenceCheck,
            BuildScope(provider, canonicalSymbol),
            QaSeverity.Error,
            QaStatus.Fail,
            1,
            new
            {
                Message = "No raw daily prices found for requested symbol and range.",
                FromDate = fromDate,
                ToDate = toDate
            },
            createdUtc,
            ingestionRunId);
    }

    private static QaResult EvaluateRawPriceShape(
        string provider,
        string canonicalSymbol,
        IReadOnlyList<PriceDailyRaw> rawRows,
        DateTime createdUtc,
        long ingestionRunId)
    {
        var issues = rawRows
            .Where(IsInvalidRawRow)
            .Select(row => new
            {
                row.TradeDate,
                row.Open,
                row.High,
                row.Low,
                row.Close,
                row.Volume
            })
            .Take(MaxDetailSamples)
            .ToArray();

        if (issues.Length == 0)
        {
            return CreateResult(
                RawPriceShapeCheck,
                BuildScope(provider, canonicalSymbol),
                QaSeverity.Info,
                QaStatus.Pass,
                0,
                new { ValidatedRows = rawRows.Count },
                createdUtc,
                ingestionRunId);
        }

        return CreateResult(
            RawPriceShapeCheck,
            BuildScope(provider, canonicalSymbol),
            QaSeverity.Error,
            QaStatus.Fail,
            issues.Length,
            new
            {
                Message = "Raw rows violate OHLC positivity/order or volume rules.",
                Samples = issues
            },
            createdUtc,
            ingestionRunId);
    }

    private static QaResult EvaluateCorporateActionValues(
        string provider,
        string canonicalSymbol,
        IReadOnlyList<CorporateAction> actions,
        DateTime createdUtc,
        long ingestionRunId)
    {
        var errorIssues = new List<object>();
        var warningIssues = new List<object>();

        foreach (var action in actions)
        {
            if (action.ActionType == CorporateActionType.Split && action.Value <= 0m)
            {
                errorIssues.Add(new
                {
                    action.ActionDate,
                    ActionType = action.ActionType.ToString(),
                    action.Value,
                    Message = "Split ratio must be positive."
                });
            }

            if (action.ActionType == CorporateActionType.Dividend && action.Value < 0m)
            {
                errorIssues.Add(new
                {
                    action.ActionDate,
                    ActionType = action.ActionType.ToString(),
                    action.Value,
                    Message = "Dividend cash amount must not be negative."
                });
            }

            if (action.AdjustmentFactor.HasValue && action.AdjustmentFactor.Value <= 0m)
            {
                warningIssues.Add(new
                {
                    action.ActionDate,
                    ActionType = action.ActionType.ToString(),
                    action.AdjustmentFactor,
                    Message = "Adjustment factor is present but not positive."
                });
            }

            if (action.ActionType == CorporateActionType.Dividend && string.IsNullOrWhiteSpace(action.Currency))
            {
                warningIssues.Add(new
                {
                    action.ActionDate,
                    ActionType = action.ActionType.ToString(),
                    Message = "Dividend action is missing currency."
                });
            }
        }

        if (errorIssues.Count == 0 && warningIssues.Count == 0)
        {
            return CreateResult(
                CorporateActionValuesCheck,
                BuildScope(provider, canonicalSymbol),
                QaSeverity.Info,
                QaStatus.Pass,
                0,
                new { ActionCount = actions.Count },
                createdUtc,
                ingestionRunId);
        }

        var severity = errorIssues.Count > 0 ? QaSeverity.Error : QaSeverity.Warning;
        var samples = errorIssues.Concat(warningIssues).Take(MaxDetailSamples).ToArray();

        return CreateResult(
            CorporateActionValuesCheck,
            BuildScope(provider, canonicalSymbol),
            severity,
            QaStatus.Fail,
            errorIssues.Count + warningIssues.Count,
            new
            {
                ErrorCount = errorIssues.Count,
                WarningCount = warningIssues.Count,
                Samples = samples
            },
            createdUtc,
            ingestionRunId);
    }

    private static QaResult EvaluateUnexplainedPriceJumps(
        string provider,
        string canonicalSymbol,
        decimal threshold,
        IReadOnlyList<PriceDailyRaw> rawRows,
        IReadOnlyList<CorporateAction> actions,
        DateTime createdUtc,
        long ingestionRunId)
    {
        var issues = new List<object>();

        for (var index = 1; index < rawRows.Count; index++)
        {
            var previous = rawRows[index - 1];
            var current = rawRows[index];

            if (previous.Close <= 0m)
            {
                continue;
            }

            var absoluteMove = Math.Abs((current.Close / previous.Close) - 1m);
            var hasSplitBetween = actions.Any(action =>
                action.ActionType == CorporateActionType.Split &&
                action.ActionDate > previous.TradeDate &&
                action.ActionDate <= current.TradeDate);

            if (absoluteMove >= threshold && !hasSplitBetween)
            {
                issues.Add(new
                {
                    PreviousTradeDate = previous.TradeDate,
                    PreviousClose = previous.Close,
                    CurrentTradeDate = current.TradeDate,
                    CurrentClose = current.Close,
                    Move = decimal.Round(absoluteMove, 8, MidpointRounding.AwayFromZero)
                });
            }
        }

        if (issues.Count == 0)
        {
            return CreateResult(
                UnexplainedPriceJumpCheck,
                BuildScope(provider, canonicalSymbol),
                QaSeverity.Info,
                QaStatus.Pass,
                0,
                new { Threshold = threshold, ComparedPairs = Math.Max(rawRows.Count - 1, 0) },
                createdUtc,
                ingestionRunId);
        }

        return CreateResult(
            UnexplainedPriceJumpCheck,
            BuildScope(provider, canonicalSymbol),
            QaSeverity.Warning,
            QaStatus.Fail,
            issues.Count,
            new
            {
                Threshold = threshold,
                Samples = issues.Take(MaxDetailSamples).ToArray()
            },
            createdUtc,
            ingestionRunId);
    }

    private static QaResult EvaluateAdjustedRowCoverage(
        string provider,
        string canonicalSymbol,
        string adjustmentBasis,
        IReadOnlyList<PriceDailyRaw> rawRows,
        IReadOnlyList<PriceDailyAdjusted> adjustedRows,
        DateTime createdUtc,
        long ingestionRunId)
    {
        var rawDates = rawRows.Select(x => x.TradeDate).ToHashSet();
        var adjustedDates = adjustedRows.Select(x => x.TradeDate).ToHashSet();

        var missingDates = rawDates
            .Where(date => !adjustedDates.Contains(date))
            .OrderBy(date => date)
            .Take(MaxDetailSamples)
            .ToArray();

        var extraDates = adjustedDates
            .Where(date => !rawDates.Contains(date))
            .OrderBy(date => date)
            .Take(MaxDetailSamples)
            .ToArray();

        if (missingDates.Length == 0 && extraDates.Length == 0)
        {
            return CreateResult(
                AdjustedRowCoverageCheck,
                BuildScope(provider, canonicalSymbol, adjustmentBasis),
                QaSeverity.Info,
                QaStatus.Pass,
                0,
                new { RawRows = rawRows.Count, AdjustedRows = adjustedRows.Count },
                createdUtc,
                ingestionRunId);
        }

        return CreateResult(
            AdjustedRowCoverageCheck,
            BuildScope(provider, canonicalSymbol, adjustmentBasis),
            QaSeverity.Error,
            QaStatus.Fail,
            missingDates.Length + extraDates.Length,
            new
            {
                RawRows = rawRows.Count,
                AdjustedRows = adjustedRows.Count,
                MissingDates = missingDates,
                ExtraDates = extraDates
            },
            createdUtc,
            ingestionRunId);
    }

    private static QaResult EvaluateAdjustedSeriesShape(
        string provider,
        string canonicalSymbol,
        string adjustmentBasis,
        IReadOnlyList<PriceDailyAdjusted> adjustedRows,
        DateTime createdUtc,
        long ingestionRunId)
    {
        var issues = adjustedRows
            .Where(IsInvalidAdjustedRow)
            .Select(row => new
            {
                row.TradeDate,
                row.Open,
                row.High,
                row.Low,
                row.Close,
                row.AdjustedClose,
                row.AdjustmentFactor,
                row.Volume
            })
            .Take(MaxDetailSamples)
            .ToArray();

        if (issues.Length == 0)
        {
            return CreateResult(
                AdjustedSeriesShapeCheck,
                BuildScope(provider, canonicalSymbol, adjustmentBasis),
                QaSeverity.Info,
                QaStatus.Pass,
                0,
                new { ValidatedRows = adjustedRows.Count },
                createdUtc,
                ingestionRunId);
        }

        return CreateResult(
            AdjustedSeriesShapeCheck,
            BuildScope(provider, canonicalSymbol, adjustmentBasis),
            QaSeverity.Error,
            QaStatus.Fail,
            issues.Length,
            new
            {
                Message = "Adjusted rows violate positivity/order/factor rules.",
                Samples = issues
            },
            createdUtc,
            ingestionRunId);
    }

    private static bool IsInvalidRawRow(PriceDailyRaw row) =>
        row.Open <= 0m ||
        row.High <= 0m ||
        row.Low <= 0m ||
        row.Close <= 0m ||
        row.Volume < 0 ||
        row.High < row.Low ||
        row.Open < row.Low ||
        row.Open > row.High ||
        row.Close < row.Low ||
        row.Close > row.High;

    private static bool IsInvalidAdjustedRow(PriceDailyAdjusted row) =>
        row.Open <= 0m ||
        row.High <= 0m ||
        row.Low <= 0m ||
        row.Close <= 0m ||
        row.AdjustedClose <= 0m ||
        row.AdjustmentFactor <= 0m ||
        row.Volume < 0 ||
        row.High < row.Low ||
        row.Open < row.Low ||
        row.Open > row.High ||
        row.Close < row.Low ||
        row.Close > row.High;

    private static QaResult CreateResult(
        string checkName,
        string scope,
        QaSeverity severity,
        QaStatus status,
        int affectedRows,
        object details,
        DateTime createdUtc,
        long ingestionRunId) =>
        new()
        {
            CheckName = checkName,
            Scope = scope,
            Severity = severity,
            Status = status,
            AffectedRows = affectedRows,
            DetailsJson = SerializeDetails(details),
            CreatedUtc = createdUtc,
            IngestionRunId = ingestionRunId
        };

    private static string BuildScope(string provider, string canonicalSymbol, string? adjustmentBasis = null) =>
        adjustmentBasis is null
            ? $"provider={provider};symbol={canonicalSymbol}"
            : $"provider={provider};symbol={canonicalSymbol};basis={adjustmentBasis}";

    private static string? SerializeDetails(object details)
    {
        var serialized = JsonSerializer.Serialize(details);
        return TrimOptional(serialized, 4000);
    }

    private static DataQualitySeverity MapSeverity(QaSeverity severity) => severity switch
    {
        QaSeverity.Info => DataQualitySeverity.Info,
        QaSeverity.Warning => DataQualitySeverity.Warning,
        QaSeverity.Error => DataQualitySeverity.Error,
        _ => throw new InvalidOperationException($"Unsupported QA severity '{severity}'.")
    };

    private static DataQualityStatus MapStatus(QaStatus status) => status switch
    {
        QaStatus.Pass => DataQualityStatus.Pass,
        QaStatus.Fail => DataQualityStatus.Fail,
        _ => throw new InvalidOperationException($"Unsupported QA status '{status}'.")
    };

    private static void Validate(NormalizedRunRequest request)
    {
        if (request.FromDate > request.ToDate)
        {
            throw new ArgumentException($"Invalid data-quality range: {request.FromDate:yyyy-MM-dd} > {request.ToDate:yyyy-MM-dd}.");
        }

        if (request.CanonicalSymbols.Count == 0)
        {
            throw new ArgumentException("At least one canonical symbol is required for data-quality checks.");
        }

        if (request.AdjustmentBases.Count == 0)
        {
            throw new ArgumentException("At least one adjustment basis is required for data-quality checks.");
        }

        if (request.UnexplainedJumpThreshold <= 0m)
        {
            throw new ArgumentException("UnexplainedJumpThreshold must be greater than zero.");
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
            return normalized[..maxLength];
        }

        return normalized;
    }

    private sealed record NormalizedRunRequest(
        string Provider,
        IReadOnlyList<string> CanonicalSymbols,
        IReadOnlyList<string> AdjustmentBases,
        DateOnly FromDate,
        DateOnly ToDate,
        decimal UnexplainedJumpThreshold,
        string Pipeline,
        string? RequestParametersJson)
    {
        public static NormalizedRunRequest From(DataQualityRunRequest request)
        {
            var provider = NormalizeRequired(request.Provider, nameof(request.Provider), toUpper: true);
            var pipeline = NormalizeRequired(request.Pipeline, nameof(request.Pipeline), toUpper: false);

            var canonicalSymbols = (request.CanonicalSymbols ?? throw new ArgumentException("Canonical symbol list must not be null.", nameof(request.CanonicalSymbols)))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => NormalizeRequired(x, nameof(request.CanonicalSymbols), toUpper: true))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var adjustmentBases = (request.AdjustmentBases ?? throw new ArgumentException("Adjustment basis list must not be null.", nameof(request.AdjustmentBases)))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizeAdjustmentBasis)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return new NormalizedRunRequest(
                Provider: provider,
                CanonicalSymbols: canonicalSymbols,
                AdjustmentBases: adjustmentBases,
                FromDate: request.FromDate,
                ToDate: request.ToDate,
                UnexplainedJumpThreshold: request.UnexplainedJumpThreshold,
                Pipeline: pipeline,
                RequestParametersJson: TrimOptional(request.RequestParametersJson, 4000));
        }
    }

    private readonly record struct SymbolBasisKey(long SymbolMasterId, string AdjustmentBasis);
}
