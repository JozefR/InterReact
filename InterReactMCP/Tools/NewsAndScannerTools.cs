using System.ComponentModel;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using InterReact;
using InterReact.Interfaces;
using InterReact.Messages.News;
using InterReact.Messages.Scanner;
using InterReact.Messages.other;
using ModelContextProtocol.Server;

namespace InterReactMCP.Tools;

[McpServerToolType]
public sealed class NewsAndScannerTools
{
    [McpServerTool, Description("Get XML schema of available scanner parameters.")]
    public static async Task<string> GetScannerParameters(
        [Description("Request timeout in seconds.")] int timeoutSeconds = 15)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        ScannerParameters parameters = await McpToolSupport.RequestFirstAsync<ScannerParameters>(
            client,
            request => request.RequestScannerParameters(),
            timeoutSeconds);
        return McpToolSupport.ToJson(parameters);
    }

    [McpServerTool, Description("Run a scanner subscription for a short window and return collected scanner snapshots.")]
    public static async Task<string> RunScanner(
        [Description("ScannerSubscription JSON payload.")] string subscriptionJson,
        [Description("Scanner subscription options string.")] string subscriptionOptions = "",
        [Description("Scanner filter options string.")] string subscriptionFilterOptions = "",
        [Description("How long to stream scanner updates before cancel (seconds).")] int streamSeconds = 10)
    {
        ScannerSubscription subscription = McpToolSupport.ParseJson<ScannerSubscription>(subscriptionJson, nameof(subscriptionJson));
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();

        int requestId = client.Request.GetNextId();
        TimeSpan duration = McpToolSupport.Timeout(streamSeconds, defaultSeconds: 10);
        IObservable<long> stop = Observable.Timer(duration);
        IObservable<ScannerData> stream = client.Response.OfType<ScannerData>()
            .Where(x => x.RequestId == requestId)
            .TakeUntil(stop);

        try
        {
            client.Request.RequestScannerSubscription(requestId, subscription, subscriptionOptions, subscriptionFilterOptions);
            ScannerData[] updates = await stream.ToArray().ToTask();
            return McpToolSupport.ToJson(updates);
        }
        finally
        {
            client.Request.CancelScannerSubscription(requestId);
        }
    }

    [McpServerTool, Description("List available IB news providers.")]
    public static async Task<string> GetNewsProviders(
        [Description("Request timeout in seconds.")] int timeoutSeconds = 15)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        NewsProviders providers = await McpToolSupport.RequestFirstAsync<NewsProviders>(
            client,
            request => request.RequestNewsProviders(),
            timeoutSeconds);
        return McpToolSupport.ToJson(providers);
    }

    [McpServerTool, Description("Get historical news headlines for a contract.")]
    public static async Task<string> GetHistoricalNews(
        [Description("Contract id.")] int contractId,
        [Description("Provider codes CSV, e.g. BRFG+DJNL.")] string providerCodes,
        [Description("Start time (IB format).")] string startTime = "",
        [Description("End time (IB format).")] string endTime = "",
        [Description("Max number of headlines.")] int totalResults = 50,
        [Description("Request timeout in seconds.")] int timeoutSeconds = 20)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        HistoricalNews[] rows = await McpToolSupport.RequestArrayUntilEndAsync<HistoricalNews, HistoricalNewsEnd>(
            client,
            (request, requestId) => request.RequestHistoricalNews(
                requestId,
                contractId,
                providerCodes,
                startTime,
                endTime,
                totalResults,
                null),
            null,
            timeoutSeconds);
        return McpToolSupport.ToJson(rows);
    }

    [McpServerTool, Description("Get a specific news article by provider code and article id.")]
    public static async Task<string> GetNewsArticle(
        [Description("News provider code.")] string providerCode,
        [Description("Provider-specific article id.")] string articleId,
        [Description("Request timeout in seconds.")] int timeoutSeconds = 20)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        NewsArticle article = await McpToolSupport.RequestSingleByIdAsync<NewsArticle>(
            client,
            (request, requestId) => request.RequestNewsArticle(requestId, providerCode, articleId, null),
            timeoutSeconds);
        return McpToolSupport.ToJson(article);
    }

    [McpServerTool, Description("Get fundamental data report XML for a contract.")]
    public static async Task<string> GetFundamentalData(
        [Description("Contract JSON payload.")] string contractJson,
        [Description("Report type, e.g. ReportsFinSummary, ReportSnapshot, ReportRatios.")] string reportType = "ReportSnapshot",
        [Description("Request timeout in seconds.")] int timeoutSeconds = 20)
    {
        InterReact.Messages.Contract.Contract contract = McpToolSupport.ParseJson<InterReact.Messages.Contract.Contract>(
            contractJson,
            nameof(contractJson));

        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        FundamentalData data = await McpToolSupport.RequestSingleByIdAsync<FundamentalData>(
            client,
            (request, requestId) => request.RequestFundamentalData(requestId, contract, reportType, null),
            timeoutSeconds);
        return McpToolSupport.ToJson(data);
    }

    [McpServerTool, Description("Get soft-dollar tiers for the connected account.")]
    public static async Task<string> GetSoftDollarTiers(
        [Description("Request timeout in seconds.")] int timeoutSeconds = 15)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        SoftDollarTiers tiers = await McpToolSupport.RequestSingleByIdAsync<SoftDollarTiers>(
            client,
            (request, requestId) => request.RequestSoftDollarTiers(requestId),
            timeoutSeconds);
        return McpToolSupport.ToJson(tiers);
    }
}
