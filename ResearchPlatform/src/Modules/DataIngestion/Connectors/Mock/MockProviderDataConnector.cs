using ResearchPlatform.Contracts.Abstractions;
using ResearchPlatform.Contracts.Ingestion;
using ResearchPlatform.Contracts.Universes;

namespace DataIngestion.Connectors.Mock;

public sealed class MockProviderDataConnector : IProviderDataConnector
{
    public const string ProviderCodeValue = "Mock";

    private static readonly DateOnly MembershipShiftDate = new(2026, 2, 1);

    private static readonly IReadOnlyList<ProviderConstituentRecord> Sp500SnapshotA =
    [
        new("AAPL", 0.055m, "Apple Inc.", "XNAS"),
        new("MSFT", 0.065m, "Microsoft Corporation", "XNAS"),
        new("NVDA", 0.050m, "NVIDIA Corporation", "XNAS")
    ];

    private static readonly IReadOnlyList<ProviderConstituentRecord> Sp500SnapshotB =
    [
        new("AAPL", 0.054m, "Apple Inc.", "XNAS"),
        new("NVDA", 0.052m, "NVIDIA Corporation", "XNAS"),
        new("AMZN", 0.038m, "Amazon.com, Inc.", "XNAS")
    ];

    private static readonly IReadOnlyList<ProviderConstituentRecord> Sp100SnapshotA =
    [
        new("AAPL", 0.095m, "Apple Inc.", "XNAS"),
        new("MSFT", 0.102m, "Microsoft Corporation", "XNAS")
    ];

    private static readonly IReadOnlyList<ProviderConstituentRecord> Sp100SnapshotB =
    [
        new("AAPL", 0.093m, "Apple Inc.", "XNAS"),
        new("AMZN", 0.081m, "Amazon.com, Inc.", "XNAS")
    ];

    private static readonly IReadOnlyList<ProviderCorporateActionRecord> CorporateActions =
    [
        new("AAPL", new DateOnly(2026, 2, 16), "SPLIT", 2.0m, null, "AAPL-20260216-SPLIT", "2-for-1 stock split"),
        new("MSFT", new DateOnly(2026, 2, 19), "DIVIDEND", 0.78m, "USD", "MSFT-20260219-DIV", "Quarterly cash dividend"),
        new("AMZN", new DateOnly(2026, 2, 24), "SPECIAL_DIVIDEND", 1.25m, "USD", "AMZN-20260224-SPDIV", "Special distribution")
    ];

    public string ProviderCode => ProviderCodeValue;

    public IngestionConnectorCapabilities Capabilities { get; } = new(
        SupportsIndexConstituentSnapshots: true,
        SupportsDailyPrices: true,
        SupportsCorporateActions: true,
        MaxSymbolsPerRequest: 200);

    public Task<ProviderConstituentSnapshotBatch> FetchConstituentSnapshotAsync(
        ProviderConstituentSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(request.ContinuationToken))
        {
            throw new NotSupportedException("Mock connector returns full constituent snapshots and does not use continuation tokens.");
        }

        var indexCode = request.IndexCode.Trim().ToUpperInvariant();
        var records = indexCode switch
        {
            UniverseCodes.Sp500 => request.EffectiveDate < MembershipShiftDate ? Sp500SnapshotA : Sp500SnapshotB,
            UniverseCodes.Sp100 => request.EffectiveDate < MembershipShiftDate ? Sp100SnapshotA : Sp100SnapshotB,
            _ => throw new NotSupportedException(
                $"Index '{request.IndexCode}' is not supported by the mock connector. Supported indexes: {UniverseCodes.Sp500}, {UniverseCodes.Sp100}.")
        };

        return Task.FromResult(new ProviderConstituentSnapshotBatch(
            ProviderCode: ProviderCode,
            IndexCode: indexCode,
            EffectiveDate: request.EffectiveDate,
            Constituents: records,
            IsComplete: true,
            ContinuationToken: null,
            RetrievedAtUtc: DateTime.UtcNow));
    }

    public Task<ProviderDailyPriceBatch> FetchDailyPricesAsync(
        ProviderDailyPriceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ProviderSymbols.Count == 0)
        {
            throw new InvalidOperationException("At least one provider symbol is required.");
        }

        if (request.FromDate > request.ToDate)
        {
            throw new InvalidOperationException($"Invalid daily price range: {request.FromDate:yyyy-MM-dd} > {request.ToDate:yyyy-MM-dd}.");
        }

        if (!string.IsNullOrWhiteSpace(request.ContinuationToken))
        {
            throw new NotSupportedException("Mock connector returns full price batches and does not use continuation tokens.");
        }

        var requestedSymbols = request.ProviderSymbols
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Select(symbol => symbol.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (requestedSymbols.Length == 0)
        {
            throw new InvalidOperationException("At least one non-empty provider symbol is required.");
        }

        var bars = new List<ProviderDailyPriceRecord>(requestedSymbols.Length * 16);
        for (var date = request.FromDate; date <= request.ToDate; date = date.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsUsTradingDay(date))
            {
                continue;
            }

            foreach (var symbol in requestedSymbols)
            {
                bars.Add(GenerateBar(symbol, date));
            }
        }

        return Task.FromResult(new ProviderDailyPriceBatch(
            ProviderCode: ProviderCode,
            FromDate: request.FromDate,
            ToDate: request.ToDate,
            Prices: bars,
            IsComplete: true,
            ContinuationToken: null,
            RetrievedAtUtc: DateTime.UtcNow));
    }

    public Task<ProviderCorporateActionBatch> FetchCorporateActionsAsync(
        ProviderCorporateActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.FromDate > request.ToDate)
        {
            throw new InvalidOperationException($"Invalid corporate action range: {request.FromDate:yyyy-MM-dd} > {request.ToDate:yyyy-MM-dd}.");
        }

        if (!string.IsNullOrWhiteSpace(request.ContinuationToken))
        {
            throw new NotSupportedException("Mock connector returns full corporate action batches and does not use continuation tokens.");
        }

        var requestedSymbols = request.ProviderSymbols
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Select(symbol => symbol.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        if (requestedSymbols.Count == 0)
        {
            throw new InvalidOperationException("At least one non-empty provider symbol is required.");
        }

        var actions = CorporateActions
            .Where(action =>
                requestedSymbols.Contains(action.ProviderSymbol) &&
                action.ActionDate >= request.FromDate &&
                action.ActionDate <= request.ToDate)
            .ToArray();

        return Task.FromResult(new ProviderCorporateActionBatch(
            ProviderCode: ProviderCode,
            FromDate: request.FromDate,
            ToDate: request.ToDate,
            Actions: actions,
            IsComplete: true,
            ContinuationToken: null,
            RetrievedAtUtc: DateTime.UtcNow));
    }

    private static ProviderDailyPriceRecord GenerateBar(string providerSymbol, DateOnly tradeDate)
    {
        var symbolSeed = providerSymbol.Sum(ch => ch);
        var daySeed = (tradeDate.DayNumber % 29) + 1;

        var close = RoundPrice(75m + (symbolSeed % 95) + daySeed * 0.45m);
        var open = RoundPrice(close * 0.996m);
        var high = RoundPrice(close * 1.007m);
        var low = RoundPrice(close * 0.991m);
        var volume = 900_000L + (symbolSeed * 250L) + (daySeed * 40_000L);
        var vwap = RoundPrice((open + high + low + close) / 4m);

        return new ProviderDailyPriceRecord(
            ProviderSymbol: providerSymbol,
            TradeDate: tradeDate,
            Open: open,
            High: high,
            Low: low,
            Close: close,
            Volume: volume,
            Vwap: vwap,
            Currency: "USD");
    }

    private static bool IsUsTradingDay(DateOnly value) =>
        value.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;

    private static decimal RoundPrice(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
