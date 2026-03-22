using System.Globalization;
using System.Text.Json;
using ResearchPlatform.Contracts.Abstractions;
using ResearchPlatform.Contracts.Ingestion;

namespace DataIngestion.Connectors.Massive;

public sealed class MassiveEodProviderDataConnector : IProviderDataConnector
{
    public const string ProviderCodeValue = "MASSIVE";

    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;
    private readonly string? _apiKey;
    private readonly bool _useFixtureFallback;

    public MassiveEodProviderDataConnector(MassiveEodConnectorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ApiBaseUrl))
        {
            throw new InvalidOperationException("Massive connector requires a non-empty API base URL.");
        }

        if (options.RequestTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Massive connector requires RequestTimeoutSeconds greater than zero.");
        }

        _apiBaseUrl = options.ApiBaseUrl.TrimEnd('/');
        _apiKey = string.IsNullOrWhiteSpace(options.ApiKey) ? null : options.ApiKey.Trim();
        _useFixtureFallback = options.UseFixtureFallbackWhenApiKeyMissing && string.IsNullOrWhiteSpace(_apiKey);

        var handler = new SocketsHttpHandler
        {
            UseCookies = false
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds)
        };
    }

    public string ProviderCode => ProviderCodeValue;

    public IngestionConnectorCapabilities Capabilities { get; } = new(
        SupportsIndexConstituentSnapshots: false,
        SupportsDailyPrices: true,
        SupportsCorporateActions: true,
        MaxSymbolsPerRequest: 100);

    public Task<ProviderConstituentSnapshotBatch> FetchConstituentSnapshotAsync(
        ProviderConstituentSnapshotRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "Massive EOD connector does not provide index constituent snapshots. " +
            "Use PIT repository snapshots loaded from dedicated universe sources.");

    public async Task<ProviderDailyPriceBatch> FetchDailyPricesAsync(
        ProviderDailyPriceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.FromDate > request.ToDate)
        {
            throw new InvalidOperationException($"Invalid daily price range: {request.FromDate:yyyy-MM-dd} > {request.ToDate:yyyy-MM-dd}.");
        }

        if (!string.IsNullOrWhiteSpace(request.ContinuationToken))
        {
            throw new NotSupportedException("Massive EOD connector currently returns complete daily batches without continuation tokens.");
        }

        var symbols = NormalizeSymbols(request.ProviderSymbols);
        var prices = new List<ProviderDailyPriceRecord>(symbols.Count * 16);

        foreach (var symbol in symbols)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rows = _useFixtureFallback
                ? ParseAggRowsFromJson(GetDailyAggFixturePayload(symbol), symbol)
                : await FetchAggRowsFromApiAsync(symbol, request.FromDate, request.ToDate, cancellationToken);

            prices.AddRange(rows
                .Where(row => row.TradeDate >= request.FromDate && row.TradeDate <= request.ToDate)
                .Select(row => new ProviderDailyPriceRecord(
                    ProviderSymbol: symbol,
                    TradeDate: row.TradeDate,
                    Open: row.Open,
                    High: row.High,
                    Low: row.Low,
                    Close: row.Close,
                    Volume: row.Volume,
                    Vwap: row.Vwap,
                    Currency: "USD")));
        }

        var orderedPrices = prices
            .OrderBy(x => x.ProviderSymbol, StringComparer.Ordinal)
            .ThenBy(x => x.TradeDate)
            .ToArray();

        return new ProviderDailyPriceBatch(
            ProviderCode: ProviderCode,
            FromDate: request.FromDate,
            ToDate: request.ToDate,
            Prices: orderedPrices,
            IsComplete: true,
            ContinuationToken: null,
            RetrievedAtUtc: DateTime.UtcNow);
    }

    public async Task<ProviderCorporateActionBatch> FetchCorporateActionsAsync(
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
            throw new NotSupportedException("Massive connector returns complete corporate action batches without continuation tokens.");
        }

        var symbols = NormalizeSymbols(request.ProviderSymbols);
        var actions = new List<ProviderCorporateActionRecord>(32);

        if (_useFixtureFallback)
        {
            actions.AddRange(ParseDividendActions(MassiveFixturePayloads.Dividends, symbols, request.FromDate, request.ToDate));
            actions.AddRange(ParseSplitActions(MassiveFixturePayloads.Splits, symbols, request.FromDate, request.ToDate));
        }
        else
        {
            actions.AddRange(await FetchDividendActionsFromApiAsync(symbols, request.FromDate, request.ToDate, cancellationToken));
            actions.AddRange(await FetchSplitActionsFromApiAsync(symbols, request.FromDate, request.ToDate, cancellationToken));
        }

        var orderedActions = actions
            .OrderBy(x => x.ProviderSymbol, StringComparer.Ordinal)
            .ThenBy(x => x.ActionDate)
            .ThenBy(x => x.ActionTypeCode, StringComparer.Ordinal)
            .ToArray();

        return new ProviderCorporateActionBatch(
            ProviderCode: ProviderCode,
            FromDate: request.FromDate,
            ToDate: request.ToDate,
            Actions: orderedActions,
            IsComplete: true,
            ContinuationToken: null,
            RetrievedAtUtc: DateTime.UtcNow);
    }

    private async Task<IReadOnlyList<AggRow>> FetchAggRowsFromApiAsync(
        string symbol,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        EnsureLiveApiKeyConfigured();

        var rows = new List<AggRow>(64);
        var nextUrl = BuildAggUrl(symbol, fromDate, toDate);

        while (!string.IsNullOrWhiteSpace(nextUrl))
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var response = await _httpClient.GetAsync(nextUrl, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Massive aggregate request failed for {symbol} ({response.StatusCode}): {payload}");
            }

            var parseResult = ParseAggResponse(payload, symbol);
            rows.AddRange(parseResult.Rows);
            nextUrl = parseResult.NextUrl;
        }

        return rows;
    }

    private async Task<IReadOnlyList<ProviderCorporateActionRecord>> FetchDividendActionsFromApiAsync(
        HashSet<string> symbols,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        EnsureLiveApiKeyConfigured();

        var actions = new List<ProviderCorporateActionRecord>(32);
        var nextUrl = BuildDividendsUrl(symbols, fromDate, toDate);

        while (!string.IsNullOrWhiteSpace(nextUrl))
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var response = await _httpClient.GetAsync(nextUrl, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Massive dividends request failed ({response.StatusCode}): {payload}");
            }

            var parseResult = ParseDividendResponse(payload, symbols, fromDate, toDate);
            actions.AddRange(parseResult.Actions);
            nextUrl = parseResult.NextUrl;
        }

        return actions;
    }

    private async Task<IReadOnlyList<ProviderCorporateActionRecord>> FetchSplitActionsFromApiAsync(
        HashSet<string> symbols,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        EnsureLiveApiKeyConfigured();

        var actions = new List<ProviderCorporateActionRecord>(16);
        var nextUrl = BuildSplitsUrl(symbols, fromDate, toDate);

        while (!string.IsNullOrWhiteSpace(nextUrl))
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var response = await _httpClient.GetAsync(nextUrl, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Massive splits request failed ({response.StatusCode}): {payload}");
            }

            var parseResult = ParseSplitResponse(payload, symbols, fromDate, toDate);
            actions.AddRange(parseResult.Actions);
            nextUrl = parseResult.NextUrl;
        }

        return actions;
    }

    private void EnsureLiveApiKeyConfigured()
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException(
                "Massive API key is required for live calls. Set DataIngestion.MassiveApiKey or enable fixture fallback.");
        }
    }

    private string BuildAggUrl(string symbol, DateOnly fromDate, DateOnly toDate)
    {
        var url =
            $"{_apiBaseUrl}/v2/aggs/ticker/{Uri.EscapeDataString(symbol)}/range/1/day/{fromDate:yyyy-MM-dd}/{toDate:yyyy-MM-dd}" +
            "?adjusted=false&sort=asc&limit=50000";

        return AppendApiKey(url);
    }

    private string BuildDividendsUrl(HashSet<string> symbols, DateOnly fromDate, DateOnly toDate)
    {
        var tickers = string.Join(',', symbols.OrderBy(x => x, StringComparer.Ordinal).Select(Uri.EscapeDataString));
        var url =
            $"{_apiBaseUrl}/stocks/v1/dividends" +
            $"?ticker.any_of={tickers}" +
            $"&ex_dividend_date.gte={fromDate:yyyy-MM-dd}" +
            $"&ex_dividend_date.lte={toDate:yyyy-MM-dd}" +
            "&sort=ex_dividend_date.asc,ticker.asc" +
            "&limit=5000";

        return AppendApiKey(url);
    }

    private string BuildSplitsUrl(HashSet<string> symbols, DateOnly fromDate, DateOnly toDate)
    {
        var tickers = string.Join(',', symbols.OrderBy(x => x, StringComparer.Ordinal).Select(Uri.EscapeDataString));
        var url =
            $"{_apiBaseUrl}/stocks/v1/splits" +
            $"?ticker.any_of={tickers}" +
            $"&execution_date.gte={fromDate:yyyy-MM-dd}" +
            $"&execution_date.lte={toDate:yyyy-MM-dd}" +
            "&sort=execution_date.asc,ticker.asc" +
            "&limit=5000";

        return AppendApiKey(url);
    }

    private string AppendApiKey(string url)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return url;
        }

        var separator = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{url}{separator}apiKey={Uri.EscapeDataString(_apiKey)}";
    }

    private static string GetDailyAggFixturePayload(string symbol)
    {
        if (!MassiveFixturePayloads.DailyAggsBySymbol.TryGetValue(symbol, out var payload))
        {
            throw new InvalidOperationException(
                $"No Massive fixture payload exists for symbol '{symbol}'. " +
                $"Available fixture symbols: {string.Join(", ", MassiveFixturePayloads.DailyAggsBySymbol.Keys)}.");
        }

        return payload;
    }

    private ParseResult ParseAggResponse(string payload, string requestedSymbol)
    {
        try
        {
            var result = ParseAggDocument(payload, requestedSymbol);
            var normalizedNext = NormalizeNextUrl(result.NextUrl);
            return new ParseResult(result.Rows, normalizedNext is null ? null : AppendApiKey(normalizedNext));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            var preview = payload.Length <= 280 ? payload : payload[..280];
            throw new InvalidOperationException(
                $"Failed to parse Massive aggregate response for '{requestedSymbol}'. Payload preview: {preview}",
                ex);
        }
    }

    private CorporateActionParseResult ParseDividendResponse(
        string payload,
        HashSet<string> requestedSymbols,
        DateOnly fromDate,
        DateOnly toDate)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            EnsureSuccessStatus(root, "dividends");

            var actions = ParseDividendActions(root, requestedSymbols, fromDate, toDate);
            var normalizedNext = NormalizeNextUrl(GetNextUrl(root));
            return new CorporateActionParseResult(actions, normalizedNext is null ? null : AppendApiKey(normalizedNext));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            var preview = payload.Length <= 280 ? payload : payload[..280];
            throw new InvalidOperationException(
                $"Failed to parse Massive dividends response. Payload preview: {preview}",
                ex);
        }
    }

    private CorporateActionParseResult ParseSplitResponse(
        string payload,
        HashSet<string> requestedSymbols,
        DateOnly fromDate,
        DateOnly toDate)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            EnsureSuccessStatus(root, "splits");

            var actions = ParseSplitActions(root, requestedSymbols, fromDate, toDate);
            var normalizedNext = NormalizeNextUrl(GetNextUrl(root));
            return new CorporateActionParseResult(actions, normalizedNext is null ? null : AppendApiKey(normalizedNext));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            var preview = payload.Length <= 280 ? payload : payload[..280];
            throw new InvalidOperationException(
                $"Failed to parse Massive splits response. Payload preview: {preview}",
                ex);
        }
    }

    private static IReadOnlyList<AggRow> ParseAggRowsFromJson(string payload, string requestedSymbol) =>
        ParseAggDocument(payload, requestedSymbol).Rows;

    private static IReadOnlyList<ProviderCorporateActionRecord> ParseDividendActions(
        string payload,
        HashSet<string> requestedSymbols,
        DateOnly fromDate,
        DateOnly toDate)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        EnsureSuccessStatus(root, "dividends");
        return ParseDividendActions(root, requestedSymbols, fromDate, toDate);
    }

    private static IReadOnlyList<ProviderCorporateActionRecord> ParseSplitActions(
        string payload,
        HashSet<string> requestedSymbols,
        DateOnly fromDate,
        DateOnly toDate)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        EnsureSuccessStatus(root, "splits");
        return ParseSplitActions(root, requestedSymbols, fromDate, toDate);
    }

    private static ParseResult ParseAggDocument(string payload, string requestedSymbol)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        EnsureSuccessStatus(root, "aggregate bars");

        var symbol = TryGetString(root, out var tickerText, "ticker")
            ? NormalizeSymbol(tickerText)
            : requestedSymbol;

        var rows = new List<AggRow>(64);
        if (TryGetProperty(root, out var resultsElement, "results") && resultsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in resultsElement.EnumerateArray())
            {
                var tradeDate = ParseTradeDate(item);
                var open = GetDecimal(item, "o", "open", "O");
                var high = GetDecimal(item, "h", "high", "H");
                var low = GetDecimal(item, "l", "low", "L");
                var close = GetDecimal(item, "c", "close", "C");
                var volume = GetLong(item, "v", "volume", "V");
                var vwap = TryGetDecimal(item, "vw", "vwap", "VW");

                rows.Add(new AggRow(symbol, tradeDate, open, high, low, close, volume, vwap));
            }
        }

        return new ParseResult(rows, GetNextUrl(root));
    }

    private static IReadOnlyList<ProviderCorporateActionRecord> ParseDividendActions(
        JsonElement root,
        HashSet<string> requestedSymbols,
        DateOnly fromDate,
        DateOnly toDate)
    {
        var actions = new List<ProviderCorporateActionRecord>(32);
        if (!TryGetProperty(root, out var resultsElement, "results") || resultsElement.ValueKind != JsonValueKind.Array)
        {
            return actions;
        }

        foreach (var item in resultsElement.EnumerateArray())
        {
            var providerSymbol = NormalizeSymbol(GetRequiredString(item, "ticker"));
            if (!requestedSymbols.Contains(providerSymbol))
            {
                continue;
            }

            var actionDate = ParseRequiredDate(item, "ex_dividend_date");
            if (actionDate < fromDate || actionDate > toDate)
            {
                continue;
            }

            var distributionType = TryGetString(item, out var distributionTypeText, "distribution_type")
                ? NormalizeCode(distributionTypeText)
                : "RECURRING";

            var actionTypeCode = distributionType switch
            {
                "SPECIAL" => "SPECIAL_DIVIDEND",
                "SUPPLEMENTAL" => "SUPPLEMENTAL_DIVIDEND",
                "IRREGULAR" => "IRREGULAR_DIVIDEND",
                _ => "DIVIDEND"
            };

            var externalId = TryGetString(item, out var externalIdText, "id") ? externalIdText : null;
            var currency = TryGetString(item, out var currencyText, "currency") ? currencyText : null;
            var description = BuildDividendDescription(item, distributionType);
            var adjustmentFactor = TryGetDecimal(item, "historical_adjustment_factor");
            var value = GetDecimal(item, "cash_amount");

            actions.Add(new ProviderCorporateActionRecord(
                ProviderSymbol: providerSymbol,
                ActionDate: actionDate,
                ActionTypeCode: actionTypeCode,
                Value: value,
                Currency: currency,
                ExternalId: externalId,
                Description: description,
                RelatedProviderSymbol: null,
                AdjustmentFactor: adjustmentFactor,
                AttributesJson: item.GetRawText()));
        }

        return actions;
    }

    private static IReadOnlyList<ProviderCorporateActionRecord> ParseSplitActions(
        JsonElement root,
        HashSet<string> requestedSymbols,
        DateOnly fromDate,
        DateOnly toDate)
    {
        var actions = new List<ProviderCorporateActionRecord>(16);
        if (!TryGetProperty(root, out var resultsElement, "results") || resultsElement.ValueKind != JsonValueKind.Array)
        {
            return actions;
        }

        foreach (var item in resultsElement.EnumerateArray())
        {
            var providerSymbol = NormalizeSymbol(GetRequiredString(item, "ticker"));
            if (!requestedSymbols.Contains(providerSymbol))
            {
                continue;
            }

            var actionDate = ParseRequiredDate(item, "execution_date");
            if (actionDate < fromDate || actionDate > toDate)
            {
                continue;
            }

            var splitFrom = GetDecimal(item, "split_from");
            var splitTo = GetDecimal(item, "split_to");
            if (splitFrom == 0m)
            {
                throw new InvalidOperationException("Massive split payload contains split_from=0, which is invalid.");
            }

            var adjustmentType = TryGetString(item, out var adjustmentTypeText, "adjustment_type")
                ? NormalizeCode(adjustmentTypeText)
                : "SPLIT";

            var actionTypeCode = adjustmentType switch
            {
                "FORWARD_SPLIT" => "FORWARD_SPLIT",
                "REVERSE_SPLIT" => "REVERSE_SPLIT",
                "STOCK_DIVIDEND" => "STOCK_DIVIDEND",
                _ => "SPLIT"
            };

            var externalId = TryGetString(item, out var externalIdText, "id") ? externalIdText : null;
            var description = $"{adjustmentType.ToLowerInvariant().Replace('_', ' ')} {splitTo:0.########}-for-{splitFrom:0.########}";
            var adjustmentFactor = TryGetDecimal(item, "historical_adjustment_factor");
            var value = decimal.Round(splitTo / splitFrom, 8, MidpointRounding.AwayFromZero);

            actions.Add(new ProviderCorporateActionRecord(
                ProviderSymbol: providerSymbol,
                ActionDate: actionDate,
                ActionTypeCode: actionTypeCode,
                Value: value,
                Currency: null,
                ExternalId: externalId,
                Description: description,
                RelatedProviderSymbol: null,
                AdjustmentFactor: adjustmentFactor,
                AttributesJson: item.GetRawText()));
        }

        return actions;
    }

    private string? NormalizeNextUrl(string? nextUrl)
    {
        if (string.IsNullOrWhiteSpace(nextUrl))
        {
            return null;
        }

        if (nextUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return nextUrl;
        }

        return $"{_apiBaseUrl.TrimEnd('/')}/{nextUrl.TrimStart('/')}";
    }

    private static void EnsureSuccessStatus(JsonElement root, string operationName)
    {
        if (TryGetString(root, out var statusText, "status") &&
            !string.Equals(statusText, "OK", StringComparison.OrdinalIgnoreCase))
        {
            var errorText = TryGetString(root, out var directError, "error", "message")
                ? directError
                : $"Unknown Massive API error while loading {operationName}.";

            throw new InvalidOperationException(
                $"Massive API returned non-OK status '{statusText}' for {operationName}. Details: {errorText}");
        }
    }

    private static string? GetNextUrl(JsonElement root) =>
        TryGetString(root, out var nextUrl, "next_url") ? nextUrl : null;

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
            throw new InvalidOperationException("Massive EOD connector currently supports up to 100 symbols per request.");
        }

        return symbols;
    }

    private static string NormalizeSymbol(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static string NormalizeCode(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant().Replace('-', '_').Replace(' ', '_');

    private static DateOnly ParseTradeDate(JsonElement element)
    {
        if (TryGetLong(element, out var epochMilliseconds, "t", "timestamp", "T"))
        {
            return DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(epochMilliseconds).UtcDateTime);
        }

        if (TryGetString(element, out var dateText, "date", "d") &&
            DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            return parsedDate;
        }

        throw new InvalidOperationException("Massive aggregate payload is missing a valid trade date (expected `t` or `date`).");
    }

    private static DateOnly ParseRequiredDate(JsonElement element, params string[] names)
    {
        if (TryGetString(element, out var dateText, names) &&
            DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            return parsedDate;
        }

        throw new InvalidOperationException($"Massive payload is missing a valid date field. Tried: {string.Join(", ", names)}.");
    }

    private static string GetRequiredString(JsonElement element, params string[] names)
    {
        if (TryGetString(element, out var value, names) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"Massive payload is missing a required string field. Tried: {string.Join(", ", names)}.");
    }

    private static string BuildDividendDescription(JsonElement item, string distributionType)
    {
        var fragments = new List<string>(3)
        {
            $"{distributionType.ToLowerInvariant().Replace('_', ' ')} dividend"
        };

        if (TryGetString(item, out var declarationDate, "declaration_date"))
        {
            fragments.Add($"declared {declarationDate}");
        }

        if (TryGetString(item, out var payDate, "pay_date"))
        {
            fragments.Add($"pay {payDate}");
        }

        return string.Join("; ", fragments);
    }

    private static decimal GetDecimal(JsonElement element, params string[] names)
    {
        if (TryGetDecimal(element, out var value, names))
        {
            return value;
        }

        throw new InvalidOperationException($"Massive payload is missing required numeric field. Tried: {string.Join(", ", names)}.");
    }

    private static long GetLong(JsonElement element, params string[] names)
    {
        if (TryGetLong(element, out var value, names))
        {
            return value;
        }

        throw new InvalidOperationException($"Massive payload is missing required integer field. Tried: {string.Join(", ", names)}.");
    }

    private static decimal? TryGetDecimal(JsonElement element, params string[] names) =>
        TryGetDecimal(element, out var value, names) ? value : null;

    private static bool TryGetDecimal(JsonElement element, out decimal value, params string[] names)
    {
        value = default;
        if (!TryGetProperty(element, out var property, names))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out value))
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.String &&
            decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetLong(JsonElement element, out long value, params string[] names)
    {
        value = default;
        if (!TryGetProperty(element, out var property, names))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out value))
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var decimalNumber))
        {
            value = decimal.ToInt64(decimal.Truncate(decimalNumber));
            return true;
        }

        if (property.ValueKind == JsonValueKind.String &&
            long.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.String &&
            decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDecimal))
        {
            value = decimal.ToInt64(decimal.Truncate(parsedDecimal));
            return true;
        }

        return false;
    }

    private static bool TryGetString(JsonElement element, out string? value, params string[] names)
    {
        value = default;
        if (!TryGetProperty(element, out var property, names) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement property, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out property))
            {
                return true;
            }
        }

        foreach (var candidate in element.EnumerateObject())
        {
            if (names.Any(name => candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    private sealed record AggRow(string Symbol, DateOnly TradeDate, decimal Open, decimal High, decimal Low, decimal Close, long Volume, decimal? Vwap);
    private sealed record ParseResult(IReadOnlyList<AggRow> Rows, string? NextUrl);
    private sealed record CorporateActionParseResult(IReadOnlyList<ProviderCorporateActionRecord> Actions, string? NextUrl);
}
