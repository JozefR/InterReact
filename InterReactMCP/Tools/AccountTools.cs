using System.ComponentModel;
using System.Text.Json;
using InterReact;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace InterReactMCP.Tools;

[McpServerToolType]
public sealed class AccountTools
{
    [McpServerTool, Description("Get Account Summary.")]
    public static async Task<string> GetAccountSummary()
    {
        // ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder
        //     .AddSimpleConsole(c => c.SingleLine = true)
        //     .SetMinimumLevel(LogLevel.Information));
        //
        // IInterReactClient client = await InterReactClient.ConnectAsync(options => options.LogFactory = loggerFactory);
        //
        // var positions = client.Service.GetAccountPositionsMultiAsync();
        
        var result = new
        {
            Event = "test",
            Status = "ok"
        };
        return await Task.FromResult(JsonSerializer.Serialize(result));
    }
    
    [McpServerTool, Description("Get Portfolio Value.")]
    public static async Task<string> GetPortfolioValue()
    {
        var result = new
        {
            Event = "test",
            Status = "ok"
        };
        return await Task.FromResult(JsonSerializer.Serialize(result));
    }
}