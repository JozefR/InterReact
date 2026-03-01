using System.ComponentModel;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using InterReact;
using InterReact.Interfaces;
using InterReact.Messages;
using InterReact.Messages.Order;
using ModelContextProtocol.Server;

namespace InterReactMCP.Tools;

[McpServerToolType]
public sealed class OrderTools
{
    [McpServerTool, Description("Place an order and stream order lifecycle messages for a short window.")]
    public static async Task<string> PlaceOrder(
        [Description("Order JSON payload matching InterReact.Messages.Order.Order.")] string orderJson,
        [Description("Contract JSON payload matching InterReact.Messages.Contract.Contract.")] string contractJson,
        [Description("How long to monitor order messages after placement (seconds).")] int monitorSeconds = 10)
    {
        Order order = McpToolSupport.ParseJson<Order>(orderJson, nameof(orderJson));
        InterReact.Messages.Contract.Contract contract =
            McpToolSupport.ParseJson<InterReact.Messages.Contract.Contract>(contractJson, nameof(contractJson));

        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        using var monitor = client.Service.PlaceOrder(order, contract);

        TimeSpan duration = McpToolSupport.Timeout(monitorSeconds, defaultSeconds: 10);
        IObservable<long> stop = Observable.Timer(duration);
        IHasOrderId[] messages = await monitor.Messages
            .TakeUntil(stop)
            .ToArray()
            .ToTask();

        return McpToolSupport.ToJson(new
        {
            orderId = monitor.OrderId,
            messages
        });
    }

    [McpServerTool, Description("Cancel an order by order id.")]
    public static async Task<string> CancelOrder(
        [Description("IB order id.")] int orderId,
        [Description("Optional manual cancel time in IB format.")] string manualOrderCancelTime = "")
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        client.Request.CancelOrder(orderId, manualOrderCancelTime);
        return McpToolSupport.ToJson(new { status = "ok", orderId });
    }

    [McpServerTool, Description("Request open orders for this client and wait for completion marker.")]
    public static async Task<string> GetOpenOrders(
        [Description("Request timeout in seconds.")] int timeoutSeconds = 20)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        IObservable<OpenOrderEnd> done = client.Response.OfType<OpenOrderEnd>().Take(1);
        IObservable<OpenOrder> stream = client.Response.OfType<OpenOrder>().TakeUntil(done);
        client.Request.RequestOpenOrders();

        OpenOrder[] rows = await stream
            .ToArray()
            .Timeout(McpToolSupport.Timeout(timeoutSeconds))
            .ToTask();

        return McpToolSupport.ToJson(rows);
    }

    [McpServerTool, Description("Request all open orders across clients and wait for completion marker.")]
    public static async Task<string> GetAllOpenOrders(
        [Description("Request timeout in seconds.")] int timeoutSeconds = 20)
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        IObservable<OpenOrderEnd> done = client.Response.OfType<OpenOrderEnd>().Take(1);
        IObservable<OpenOrder> stream = client.Response.OfType<OpenOrder>().TakeUntil(done);
        client.Request.RequestAllOpenOrders();

        OpenOrder[] rows = await stream
            .ToArray()
            .Timeout(McpToolSupport.Timeout(timeoutSeconds))
            .ToTask();

        return McpToolSupport.ToJson(rows);
    }

    [McpServerTool, Description("Request recent executions (last 24h per IB API) with an optional ExecutionFilter JSON payload.")]
    public static async Task<string> GetExecutions(
        [Description("ExecutionFilter JSON payload. Empty means no filter.")] string filterJson = "",
        [Description("Request timeout in seconds.")] int timeoutSeconds = 20)
    {
        ExecutionFilter? filter = string.IsNullOrWhiteSpace(filterJson)
            ? null
            : McpToolSupport.ParseJson<ExecutionFilter>(filterJson, nameof(filterJson));

        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        Execution[] rows = await McpToolSupport.RequestArrayUntilEndAsync<Execution, ExecutionEnd>(
            client,
            (request, requestId) => request.RequestExecutions(requestId, filter),
            null,
            timeoutSeconds);

        return McpToolSupport.ToJson(rows);
    }

    [McpServerTool, Description("Issue a global cancel request to IB.")]
    public static async Task<string> RequestGlobalCancel()
    {
        await using IInterReactClient client = await McpToolSupport.ConnectAsync();
        client.Request.RequestGlobalCancel();
        return McpToolSupport.ToJson(new { status = "ok" });
    }
}
