using System.ComponentModel;
using System.Reactive.Linq;
using InterReact;
using InterReact.Enums;
using InterReact.Messages;
using InterReact.Messages.Account;
using InterReact.Messages.other;
using NodaTime;
using ModelContextProtocol.Server;

namespace InterReactMCP.Tools;

[McpServerToolType]
public sealed class AccountTools
{
    [McpServerTool, Description("Get managed account IDs from Interactive Brokers.")]
    public static async Task<string> GetManagedAccounts(
        [Description("Request timeout in seconds.")] int timeoutSeconds = 20)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        string[] accounts = await client.Service.GetManagedAccountsAsync(McpToolSupport.Timeout(timeoutSeconds));
        return McpToolSupport.ToJson(accounts);
    }

    [McpServerTool, Description("Get current IB server time.")]
    public static async Task<string> GetCurrentTime(
        [Description("Request timeout in seconds.")] int timeoutSeconds = 10)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        Instant currentTime = await client.Service.CreateCurrentTimeObservable()
            .Timeout(McpToolSupport.Timeout(timeoutSeconds))
            .FirstAsync();
        return McpToolSupport.ToJson(new { currentTime = currentTime.ToString() });
    }

    [McpServerTool, Description("Get account summary rows (AccountSummary).")]
    public static async Task<string> GetAccountSummary(
        [Description("IB account group, usually 'All'.")] string group = "All",
        [Description("Optional comma-separated AccountSummaryTag names or numeric enum values. Empty means all tags.")]
        string tagsCsv = "",
        [Description("Request timeout in seconds.")] int timeoutSeconds = 20)
    {
        List<AccountSummaryTag> tags = McpToolSupport.ParseEnumCsv<AccountSummaryTag>(tagsCsv);
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        AccountSummary[] rows = await McpToolSupport.RequestArrayUntilEndAsync<AccountSummary, AccountSummaryEnd>(
            client,
            (request, requestId) => request.RequestAccountSummary(requestId, group, tags),
            (request, requestId) => request.CancelAccountSummary(requestId),
            timeoutSeconds);
        return McpToolSupport.ToJson(rows);
    }

    [McpServerTool, Description("Get account positions (multi-account endpoint).")]
    public static async Task<string> GetAccountPositionsMulti(
        [Description("Account code, empty for all managed accounts.")] string account = "",
        [Description("Model code filter.")] string modelCode = "",
        [Description("Request timeout in seconds.")] int timeoutSeconds = 25)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        AccountPositionsMulti[] rows = await client.Service.GetAccountPositionsMultiAsync(
            account,
            modelCode,
            McpToolSupport.Timeout(timeoutSeconds));
        return McpToolSupport.ToJson(rows);
    }

    [McpServerTool, Description("Get account updates (multi-account endpoint).")]
    public static async Task<string> GetAccountUpdatesMulti(
        [Description("Account code, empty for all managed accounts.")] string account = "",
        [Description("Model code filter.")] string modelCode = "",
        [Description("Include ledger and NLV values.")] bool ledgerAndNlv = false,
        [Description("Request timeout in seconds.")] int timeoutSeconds = 25)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        AccountUpdateMulti[] rows = await client.Service.GetAccountUpdatesMultiAsync(
            account,
            modelCode,
            ledgerAndNlv,
            McpToolSupport.Timeout(timeoutSeconds));
        return McpToolSupport.ToJson(rows);
    }

    [McpServerTool, Description("Request the next available IB order id.")]
    public static async Task<string> GetNextOrderId(
        [Description("Request timeout in seconds.")] int timeoutSeconds = 10)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        NextOrderId nextId = await McpToolSupport.RequestFirstAsync<NextOrderId>(
            client,
            request => request.RequestNextOrderId(),
            timeoutSeconds);
        return McpToolSupport.ToJson(nextId);
    }

    [McpServerTool, Description("Get IB family codes.")]
    public static async Task<string> GetFamilyCodes(
        [Description("Request timeout in seconds.")] int timeoutSeconds = 10)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        FamilyCodes codes = await McpToolSupport.RequestFirstAsync<FamilyCodes>(
            client,
            request => request.RequestFamilyCodes(),
            timeoutSeconds);
        return McpToolSupport.ToJson(codes);
    }

    [McpServerTool, Description("Get user info (white branding id).")]
    public static async Task<string> GetUserInformation(
        [Description("Request timeout in seconds.")] int timeoutSeconds = 10)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        UserInfo userInfo = await McpToolSupport.RequestSingleByIdAsync<UserInfo>(
            client,
            (request, requestId) => request.RequestUserInformation(requestId),
            timeoutSeconds);
        return McpToolSupport.ToJson(userInfo);
    }
}
