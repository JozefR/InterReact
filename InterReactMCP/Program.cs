using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

// Do not attach console logging for stdio MCP servers.
// Any non-protocol stdout output can break JSON-RPC framing.
builder.Logging.ClearProviders();

// Configure the MCP server with standard input/output transport
builder.Services.AddMcpServer()
    .WithStdioServerTransport() // Ensure Claude Desktop supports this transport
    .WithToolsFromAssembly();   // Ensure tools are in the same assembly

var app = builder.Build();

try
{
    // Run the application
    await app.RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    throw;
}
