using System.ComponentModel;
using ModelContextProtocol.Server;

namespace InterReactMCP.Tools;

[McpServerToolType]
public class AccountTools
{
    [McpServerTool, Description("Get Account Summary.")]
    public static async Task<string> GetAccountSummary()
    {
        return "";
    }
    
    [McpServerTool, Description("Get Portfolio Value.")]
    public static async Task<string> GetPortfolioValue()
    {
        return "";
    }
}