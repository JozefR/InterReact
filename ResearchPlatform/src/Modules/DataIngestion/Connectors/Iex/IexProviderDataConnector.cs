using System.Globalization;
using System.Text.Json;
using ResearchPlatform.Contracts.Abstractions;
using ResearchPlatform.Contracts.Ingestion;
using ResearchPlatform.Contracts.Universes;

namespace DataIngestion.Connectors.Iex;

public sealed class IexProviderDataConnector : IProviderDataConnector
{
    public const string ProviderCodeValue = "IEX";

    private const int ConstituentsPageSize = 3;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string ProviderCode => ProviderCodeValue;

    public IngestionConnectorCapabilities Capabilities { get; } = new(
        SupportsIndexConstituentSnapshots: true,
        SupportsDailyPrices: true,
        SupportsCorporateActions: true,
        MaxSymbolsPerRequest: 100);

    public Task<ProviderConstituentSnapshotBatch> FetchConstituentSnapshotAsync(
        ProviderConstituentSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var indexCode = request.IndexCode.Trim().ToUpperInvariant();
        var rows = LoadConstituentRows(indexCode);

        var page = ParsePageToken(request.ContinuationToken);
        var offset = (page - 1) * ConstituentsPageSize;
        if (offset >= rows.Count)
        {
            throw new InvalidOperationException(
                $"Constituent continuation token '{request.ContinuationToken}' is out of range for index '{indexCode}'.");
        }

        var pagedRows = rows
            .Skip(offset)
            .Take(ConstituentsPageSize)
            .ToArray();

        var isComplete = offset + pagedRows.Length >= rows.Count;
        var nextToken = isComplete ? null : $"page:{page + 1}";

        var constituents = pagedRows
            .Select(row => new ProviderConstituentRecord(
                ProviderSymbol: row.Symbol,
                Weight: row.Weight,
                SecurityName: row.CompanyName,
                ExchangeMic: MapExchangeToMic(row.PrimaryExchange)))
            .ToArray();

        return Task.FromResult(new ProviderConstituentSnapshotBatch(
            ProviderCode: ProviderCode,
            IndexCode: indexCode,
            EffectiveDate: request.EffectiveDate,
            Constituents: constituents,
            IsComplete: isComplete,
            ContinuationToken: nextToken,
            RetrievedAtUtc: DateTime.UtcNow));
    }

    public Task<ProviderDailyPriceBatch> FetchDailyPricesAsync(
        ProviderDailyPriceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        ValidateDateRange(request.FromDate, request.ToDate, "daily price");

        if (!string.IsNullOrWhiteSpace(request.ContinuationToken))
        {
            throw new NotSupportedException("IEX adapter currently returns full daily price batches without continuation tokens.");
        }

        var symbols = NormalizeSymbols(request.ProviderSymbols);
        var rows = LoadDailyPriceRows()
            .Where(row =>
                symbols.Contains(row.Symbol) &&
                row.TradeDate >= request.FromDate &&
                row.TradeDate <= request.ToDate)
            .Select(row => new ProviderDailyPriceRecord(
                ProviderSymbol: row.Symbol,
                TradeDate: row.TradeDate,
                Open: row.Open,
                High: row.High,
                Low: row.Low,
                Close: row.Close,
                Volume: row.Volume,
                Vwap: row.Vwap,
                Currency: row.Currency))
            .ToArray();

        return Task.FromResult(new ProviderDailyPriceBatch(
            ProviderCode: ProviderCode,
            FromDate: request.FromDate,
            ToDate: request.ToDate,
            Prices: rows,
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

        ValidateDateRange(request.FromDate, request.ToDate, "corporate action");

        if (!string.IsNullOrWhiteSpace(request.ContinuationToken))
        {
            throw new NotSupportedException("IEX adapter currently returns full corporate action batches without continuation tokens.");
        }

        var symbols = NormalizeSymbols(request.ProviderSymbols);
        var rows = LoadCorporateActionRows()
            .Where(row =>
                symbols.Contains(row.Symbol) &&
                row.ActionDate >= request.FromDate &&
                row.ActionDate <= request.ToDate)
            .Select(row => new ProviderCorporateActionRecord(
                ProviderSymbol: row.Symbol,
                ActionDate: row.ActionDate,
                ActionTypeCode: row.ActionTypeCode,
                Value: row.Value,
                Currency: row.Currency,
                ExternalId: row.ExternalId,
                Description: row.Description,
                RelatedProviderSymbol: row.RelatedProviderSymbol))
            .ToArray();

        return Task.FromResult(new ProviderCorporateActionBatch(
            ProviderCode: ProviderCode,
            FromDate: request.FromDate,
            ToDate: request.ToDate,
            Actions: rows,
            IsComplete: true,
            ContinuationToken: null,
            RetrievedAtUtc: DateTime.UtcNow));
    }

    private static IReadOnlyList<ConstituentRow> LoadConstituentRows(string indexCode)
    {
        var json = indexCode switch
        {
            UniverseCodes.Sp500 => IexFixturePayloads.Sp500ConstituentsJson,
            UniverseCodes.Sp100 => IexFixturePayloads.Sp100ConstituentsJson,
            _ => throw new NotSupportedException(
                $"Index '{indexCode}' is not supported by the IEX adapter. Supported indexes: {UniverseCodes.Sp500}, {UniverseCodes.Sp100}.")
        };

        var rows = JsonSerializer.Deserialize<List<ConstituentRaw>>(json, SerializerOptions)
            ?? throw new InvalidOperationException("Failed to parse IEX constituent fixture payload.");

        return rows
            .Select(row => new ConstituentRow(
                Symbol: NormalizeSymbol(row.Symbol),
                CompanyName: row.CompanyName?.Trim() ?? string.Empty,
                Weight: row.Weight,
                PrimaryExchange: row.PrimaryExchange?.Trim() ?? string.Empty))
            .ToArray();
    }

    private static IReadOnlyList<DailyPriceRow> LoadDailyPriceRows()
    {
        var rows = JsonSerializer.Deserialize<List<DailyPriceRaw>>(IexFixturePayloads.DailyPricesJson, SerializerOptions)
            ?? throw new InvalidOperationException("Failed to parse IEX daily price fixture payload.");

        return rows
            .Select(row => new DailyPriceRow(
                Symbol: NormalizeSymbol(row.Symbol),
                TradeDate: ParseDate(row.Date, "daily price"),
                Open: row.Open,
                High: row.High,
                Low: row.Low,
                Close: row.Close,
                Volume: row.Volume,
                Vwap: row.Vwap,
                Currency: string.IsNullOrWhiteSpace(row.Currency) ? "USD" : row.Currency.Trim().ToUpperInvariant()))
            .ToArray();
    }

    private static IReadOnlyList<CorporateActionRow> LoadCorporateActionRows()
    {
        var rows = JsonSerializer.Deserialize<List<CorporateActionRaw>>(IexFixturePayloads.CorporateActionsJson, SerializerOptions)
            ?? throw new InvalidOperationException("Failed to parse IEX corporate action fixture payload.");

        return rows
            .Select(row => new CorporateActionRow(
                Symbol: NormalizeSymbol(row.Symbol),
                ActionDate: ParseDate(row.ExDate, "corporate action"),
                ActionTypeCode: NormalizeActionType(row.Type),
                Value: row.Amount,
                Currency: string.IsNullOrWhiteSpace(row.Currency) ? "USD" : row.Currency.Trim().ToUpperInvariant(),
                ExternalId: string.IsNullOrWhiteSpace(row.RefId) ? null : row.RefId.Trim(),
                Description: string.IsNullOrWhiteSpace(row.Description) ? null : row.Description.Trim(),
                RelatedProviderSymbol: string.IsNullOrWhiteSpace(row.RelatedSymbol) ? null : NormalizeSymbol(row.RelatedSymbol)))
            .ToArray();
    }

    private static int ParsePageToken(string? continuationToken)
    {
        if (string.IsNullOrWhiteSpace(continuationToken))
        {
            return 1;
        }

        var token = continuationToken.Trim();
        if (!token.StartsWith("page:", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(token[5..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var page) ||
            page < 1)
        {
            throw new InvalidOperationException(
                $"Invalid continuation token '{continuationToken}'. Expected format 'page:<positive integer>'.");
        }

        return page;
    }

    private static HashSet<string> NormalizeSymbols(IReadOnlyList<string> providerSymbols)
    {
        if (providerSymbols.Count == 0)
        {
            throw new InvalidOperationException("At least one provider symbol is required.");
        }

        var symbols = providerSymbols
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Select(NormalizeSymbol)
            .ToHashSet(StringComparer.Ordinal);

        if (symbols.Count == 0)
        {
            throw new InvalidOperationException("At least one non-empty provider symbol is required.");
        }

        if (symbols.Count > 100)
        {
            throw new InvalidOperationException("IEX adapter currently supports up to 100 symbols per request.");
        }

        return symbols;
    }

    private static string NormalizeSymbol(string value) =>
        value.Trim().ToUpperInvariant();

    private static void ValidateDateRange(DateOnly fromDate, DateOnly toDate, string label)
    {
        if (fromDate > toDate)
        {
            throw new InvalidOperationException($"Invalid {label} range: {fromDate:yyyy-MM-dd} > {toDate:yyyy-MM-dd}.");
        }
    }

    private static DateOnly ParseDate(string? value, string payloadLabel)
    {
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new InvalidOperationException($"Invalid {payloadLabel} date value '{value ?? "<null>"}'. Expected yyyy-MM-dd.");
        }

        return date;
    }

    private static string NormalizeActionType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "OTHER";
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "DIVIDEND" => "DIVIDEND",
            "SPLIT" => "SPLIT",
            "MERGER" => "MERGER",
            "SPINOFF" => "SPINOFF",
            "DELISTING" => "DELISTING",
            "SYMBOL_CHANGE" => "SYMBOL_CHANGE",
            _ => "OTHER"
        };
    }

    private static string? MapExchangeToMic(string exchange)
    {
        if (string.IsNullOrWhiteSpace(exchange))
        {
            return null;
        }

        return exchange.Trim().ToUpperInvariant() switch
        {
            "NASDAQ" => "XNAS",
            "NYSE" => "XNYS",
            "NYSE ARCA" => "ARCX",
            _ => null
        };
    }

    private sealed record ConstituentRaw(string Symbol, string? CompanyName, decimal? Weight, string? PrimaryExchange);
    private sealed record DailyPriceRaw(string Symbol, string Date, decimal Open, decimal High, decimal Low, decimal Close, long Volume, decimal? Vwap, string? Currency);
    private sealed record CorporateActionRaw(string Symbol, string ExDate, string? Type, decimal Amount, string? Currency, string? RefId, string? Description, string? RelatedSymbol);

    private sealed record ConstituentRow(string Symbol, string CompanyName, decimal? Weight, string PrimaryExchange);
    private sealed record DailyPriceRow(string Symbol, DateOnly TradeDate, decimal Open, decimal High, decimal Low, decimal Close, long Volume, decimal? Vwap, string Currency);
    private sealed record CorporateActionRow(string Symbol, DateOnly ActionDate, string ActionTypeCode, decimal Value, string Currency, string? ExternalId, string? Description, string? RelatedProviderSymbol);
}
