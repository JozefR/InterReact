using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

// Configure logging to output to the console
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
});

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
    // Log any unhandled exceptions
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while running the application.");
    throw;
}