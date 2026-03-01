using System.ComponentModel;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using InterReact;
using InterReact.Consts;
using InterReact.Enums;
using InterReact.Interfaces;
using InterReact.Messages;
using InterReact.Messages.Contract;
using InterReact.Messages.HistoricalTicks;
using InterReact.Messages.MarketData;
using InterReact.Messages.MarketDepth;
using InterReact.Messages.other;
using ModelContextProtocol.Server;

namespace InterReactMCP.Tools;

[McpServerToolType]
public sealed class MarketTools
{
    [McpServerTool, Description("Resolve IB contract details for a contract selector JSON payload.")]
    public static async Task<string> GetContractDetails(
        [Description("Contract JSON payload matching InterReact.Messages.Contract.Contract.")]
        string contractJson,
        [Description("Include expired contracts.")] bool includeExpired = false,
        [Description("Request timeout in seconds.")] int timeoutSeconds = 30)
    {
        Contract contract = McpToolSupport.ParseJson<Contract>(contractJson, nameof(contractJson));
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        ContractDetails[] details = await client.Service.GetContractDetailsAsync(
            contract,
            includeExpired,
            McpToolSupport.Timeout(timeoutSeconds));
        return McpToolSupport.ToJson(details);
    }

    [McpServerTool, Description("Search matching symbols by text pattern.")]
    public static async Task<string> SearchSymbols(
        [Description("Pattern, e.g. 'AAPL'.")] string pattern,
        [Description("Request timeout in seconds.")] int timeoutSeconds = 15)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        SymbolSamples samples = await McpToolSupport.RequestSingleByIdAsync<SymbolSamples>(
            client,
            (request, requestId) => request.RequestMatchingSymbols(requestId, pattern),
            timeoutSeconds);
        return McpToolSupport.ToJson(samples);
    }

    [McpServerTool, Description("Get option security definition parameters.")]
    public static async Task<string> GetSecurityDefinitionOptionParameters(
        [Description("Underlying symbol, e.g. AAPL.")] string underlyingSymbol,
        [Description("Future/FOP exchange or empty string.")] string futFopExchange = "",
        [Description("Underlying security type, e.g. STK.")] string underlyingSecType = "STK",
        [Description("Underlying contract ID.")] int underlyingConId = 0,
        [Description("Request timeout in seconds.")] int timeoutSeconds = 20)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        SecurityDefinitionOptionParameter[] rows =
            await McpToolSupport.RequestArrayUntilEndAsync<SecurityDefinitionOptionParameter, SecurityDefinitionOptionParameterEnd>(
                client,
                (request, requestId) => request.RequestSecDefOptParams(
                    requestId,
                    underlyingSymbol,
                    futFopExchange,
                    underlyingSecType,
                    underlyingConId),
                null,
                timeoutSeconds);
        return McpToolSupport.ToJson(rows);
    }

    [McpServerTool, Description("Set requested IB market data type (Realtime/Frozen/Delayed/DelayedFrozen).")]
    public static async Task<string> SetMarketDataType(
        [Description("MarketDataType name or numeric enum value.")] string marketDataType = "Delayed")
    {
        MarketDataType parsed = McpToolSupport.ParseEnumToken<MarketDataType>(marketDataType);
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        client.Request.RequestMarketDataType(parsed);
        return McpToolSupport.ToJson(new { status = "ok", marketDataType = parsed.ToString() });
    }

    [McpServerTool, Description("Get market data snapshot ticks for a contract.")]
    public static async Task<string> GetMarketDataSnapshot(
        [Description("Contract JSON payload.")] string contractJson,
        [Description("Optional GenericTickType values as comma-separated names or numeric values.")] string genericTickTypesCsv = "",
        [Description("Use regulatory snapshot request mode.")] bool isRegulatorySnapshot = false,
        [Description("Request timeout in seconds.")] int timeoutSeconds = 20)
    {
        Contract contract = McpToolSupport.ParseJson<Contract>(contractJson, nameof(contractJson));
        List<GenericTickType> genericTickTypes = McpToolSupport.ParseEnumCsv<GenericTickType>(genericTickTypesCsv);

        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        IHasRequestId[] ticks = await client.Service.GetMarketDataSnapshotAsync(
            contract,
            genericTickTypes,
            isRegulatorySnapshot,
            null,
            McpToolSupport.Timeout(timeoutSeconds));
        return McpToolSupport.ToJson(ticks);
    }

    [McpServerTool, Description("Get historical bar data for a contract.")]
    public static async Task<string> GetHistoricalData(
        [Description("Contract JSON payload.")] string contractJson,
        [Description("End datetime in IB format, empty for now.")] string endDateTime = "",
        [Description("IB duration string, e.g. '1 M'.")] string duration = HistoricalDataDuration.OneMonth,
        [Description("IB bar size string, e.g. '1 hour'.")] string barSize = HistoricalDataBarSize.OneHour,
        [Description("IB whatToShow string, e.g. TRADES.")] string whatToShow = HistoricalDataWhatToShow.Trades,
        [Description("Restrict to regular trading hours.")] bool regularTradingHoursOnly = true,
        [Description("Date format: 1 string, 2 epoch seconds.")] int dateFormat = 1,
        [Description("Request timeout in seconds.")] int timeoutSeconds = 30)
    {
        try
        {
            Contract contract = McpToolSupport.ParseJson<Contract>(contractJson, nameof(contractJson));
            await using IInterReactClient client = await McpToolSupport.ConnectAsync();
            HistoricalData data = await McpToolSupport.RequestSingleByIdAsync<HistoricalData>(
                client,
                (request, requestId) => request.RequestHistoricalData(
                    requestId,
                    contract,
                    endDateTime,
                    duration,
                    barSize,
                    whatToShow,
                    regularTradingHoursOnly,
                    dateFormat,
                    false,
                    null),
                timeoutSeconds);
            return McpToolSupport.ToJson(data);
        }
        catch (Exception ex)
        {
            return McpToolSupport.ToJson(new
            {
                error = new
                {
                    type = ex.GetType().Name,
                    message = ex.Message,
                    inner = ex.InnerException?.Message
                }
            });
        }
    }

    [McpServerTool, Description("Get earliest available timestamp for historical data for a contract.")]
    public static async Task<string> GetHeadTimestamp(
        [Description("Contract JSON payload.")] string contractJson,
        [Description("IB whatToShow string.")] string whatToShow = HistoricalDataWhatToShow.Trades,
        [Description("1 for RTH only, 0 for all sessions.")] int useRth = 1,
        [Description("Date format: 1 string, 2 epoch seconds.")] int formatDate = 1,
        [Description("Request timeout in seconds.")] int timeoutSeconds = 20)
    {
        Contract contract = McpToolSupport.ParseJson<Contract>(contractJson, nameof(contractJson));
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        HeadTimestamp headTimestamp = await McpToolSupport.RequestSingleByIdAsync<HeadTimestamp>(
            client,
            (request, requestId) => request.RequestHeadTimestamp(requestId, contract, whatToShow, useRth, formatDate),
            timeoutSeconds);
        return McpToolSupport.ToJson(headTimestamp);
    }

    [McpServerTool, Description("Get histogram distribution for a contract.")]
    public static async Task<string> GetHistogramData(
        [Description("Contract JSON payload.")] string contractJson,
        [Description("Use regular trading hours only.")] bool useRth = true,
        [Description("Period string, e.g. '3 days'.")] string period = "3 days",
        [Description("Request timeout in seconds.")] int timeoutSeconds = 20)
    {
        Contract contract = McpToolSupport.ParseJson<Contract>(contractJson, nameof(contractJson));
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        HistogramData histogram = await McpToolSupport.RequestSingleByIdAsync<HistogramData>(
            client,
            (request, requestId) => request.RequestHistogramData(requestId, contract, useRth, period),
            timeoutSeconds);
        return McpToolSupport.ToJson(histogram);
    }

    [McpServerTool, Description("Get historical ticks (TRADES/BID_ASK/MIDPOINT) for a contract.")]
    public static async Task<string> GetHistoricalTicks(
        [Description("Contract JSON payload.")] string contractJson,
        [Description("Start datetime (IB format).")] string startDateTime = "",
        [Description("End datetime (IB format).")] string endDateTime = "",
        [Description("Max number of ticks per response batch.")] int numberOfTicks = 1000,
        [Description("Tick type: TRADES, BID_ASK, MIDPOINT.")] string whatToShow = HistoricalDataWhatToShow.Trades,
        [Description("1 for RTH only, 0 for all sessions.")] int useRth = 1,
        [Description("Ignore size where applicable.")] bool ignoreSize = false,
        [Description("Request timeout in seconds.")] int timeoutSeconds = 30)
    {
        Contract contract = McpToolSupport.ParseJson<Contract>(contractJson, nameof(contractJson));
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        int requestId = client.Request.GetNextId();

        IObservable<object> stream = client.Response
            .OfType<object>()
            .Where(message => IsHistoricalTicksMessage(message, requestId));

        IObservable<object> done = stream
            .Where(message => IsHistoricalTicksDone(message))
            .Take(1);

        client.Request.RequestHistoricalTicks(
            requestId,
            contract,
            startDateTime,
            endDateTime,
            numberOfTicks,
            whatToShow,
            useRth,
            ignoreSize,
            null);

        object[] rows = await stream
            .TakeUntil(done)
            .ToArray()
            .Timeout(McpToolSupport.Timeout(timeoutSeconds))
            .ToTask();

        return McpToolSupport.ToJson(rows);
    }

    [McpServerTool, Description("Get all exchange capabilities for market depth.")]
    public static async Task<string> GetMarketDepthExchanges(
        [Description("Request timeout in seconds.")] int timeoutSeconds = 15)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        MarketDepthExchanges exchanges = await McpToolSupport.RequestFirstAsync<MarketDepthExchanges>(
            client,
            request => request.RequestMarketDepthExchanges(),
            timeoutSeconds);
        return McpToolSupport.ToJson(exchanges);
    }

    [McpServerTool, Description("Get SMART exchange map from bit-number to exchange letter.")]
    public static async Task<string> GetSmartComponents(
        [Description("BBO exchange code, e.g. 'a6'.")] string bboExchange,
        [Description("Request timeout in seconds.")] int timeoutSeconds = 15)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        SmartComponents components = await McpToolSupport.RequestSingleByIdAsync<SmartComponents>(
            client,
            (request, requestId) => request.RequestSmartComponents(requestId, bboExchange),
            timeoutSeconds);
        return McpToolSupport.ToJson(components);
    }

    [McpServerTool, Description("Get market rule increments by marketRuleId.")]
    public static async Task<string> GetMarketRule(
        [Description("Market rule id from contract details.")] int marketRuleId,
        [Description("Request timeout in seconds.")] int timeoutSeconds = 15)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        MarketRule rule = await McpToolSupport.RequestFirstAsync<MarketRule>(
            client,
            request => request.RequestMarketRule(marketRuleId),
            timeoutSeconds);
        return McpToolSupport.ToJson(rule);
    }

    private static bool IsHistoricalTicksMessage(object message, int requestId) =>
        message switch
        {
            HistoricalTicks ticks => ticks.RequestId == requestId,
            HistoricalBidAskTicks ticks => ticks.RequestId == requestId,
            HistoricalLastTicks ticks => ticks.RequestId == requestId,
            _ => false
        };

    private static bool IsHistoricalTicksDone(object message) =>
        message switch
        {
            HistoricalTicks ticks => ticks.Done,
            HistoricalBidAskTicks ticks => ticks.Done,
            HistoricalLastTicks ticks => ticks.Done,
            _ => false
        };
}
