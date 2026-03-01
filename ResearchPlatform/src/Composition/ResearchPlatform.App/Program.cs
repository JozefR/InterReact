using AiResearchAssistant;
using BacktestEngine;
using DataIngestion;
using DataWarehouse;
using DataWarehouse.Schema;
using DataWarehouse.Symbols;
using ExperimentRunner;
using MetricsReporting;
using Microsoft.EntityFrameworkCore;
using ResearchPlatform.App.Configuration;
using ResearchPlatform.Contracts.Abstractions;
using ResearchPlatform.Contracts.Symbols;
using StrategyRegistry;

var command = StartupCommand.Parse(args);
var config = PlatformConfig.Load(command.EnvironmentOverride);

if (command.ValidateConfigOnly)
{
    Console.WriteLine($"Configuration is valid for environment: {config.Runtime.Environment}");
    Console.WriteLine($"- DataIngestion.Provider: {config.DataIngestion.Provider}");
    Console.WriteLine($"- DataIngestion.Universes: {string.Join(", ", config.DataIngestion.Universes)}");
    Console.WriteLine($"- DataWarehouse.ConnectionString: {MaskConnectionString(config.DataWarehouse.ConnectionString)}");
    Console.WriteLine($"- Backtest.InitialCapital: {config.Backtest.InitialCapital}");
    return;
}

if (command.RunSymbolIdentitySmoke)
{
    await RunSymbolIdentitySmokeAsync(config);
    return;
}

var modules = new IModule[]
{
    new DataIngestionModule(),
    new DataWarehouseModule(),
    new BacktestEngineModule(),
    new StrategyRegistryModule(),
    new ExperimentRunnerModule(),
    new MetricsReportingModule(),
    new AiResearchAssistantModule()
};

Console.WriteLine("ResearchPlatform.App bootstrapped modules:");
foreach (var module in modules)
{
    Console.WriteLine($"- {module.Name}: {module.Description}");
}

Console.WriteLine("Runtime configuration:");
Console.WriteLine($"- Environment: {config.Runtime.Environment}");
Console.WriteLine($"- LogLevel: {config.Runtime.LogLevel}");
Console.WriteLine($"- DataIngestion.Provider: {config.DataIngestion.Provider}");
Console.WriteLine($"- DataWarehouse.ConnectionString: {MaskConnectionString(config.DataWarehouse.ConnectionString)}");
Console.WriteLine($"- Backtest.InitialCapital: {config.Backtest.InitialCapital}");

return;

static async Task RunSymbolIdentitySmokeAsync(PlatformConfig config)
{
    var configuredConnection = config.DataWarehouse.ConnectionString;
    var sqliteConnection = configuredConnection.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
        ? configuredConnection
        : "Data Source=researchplatform.db";

    var bootstrapOptions = new DbContextOptionsBuilder<ResearchWarehouseDbContext>()
        .UseSqlite(sqliteConnection)
        .Options;

    await using (var bootstrapContext = new ResearchWarehouseDbContext(bootstrapOptions))
    {
        await bootstrapContext.Database.MigrateAsync();
    }

    var repository = SqliteSymbolIdentityRepositoryFactory.Create(sqliteConnection);
    try
    {
        var asOfDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var result = await repository.UpsertSymbolAsync(new SymbolEnrichmentRequest(
            Provider: "Mock",
            ProviderSymbol: "AAPL",
            EffectiveFrom: new DateOnly(1980, 12, 12),
            CanonicalSymbol: "AAPL",
            SecurityName: "Apple Inc.",
            ExchangeMic: "XNAS",
            AssetType: AssetType.Equity,
            Currency: "USD",
            IsActive: true));

        var resolved = await repository.ResolveProviderSymbolAsync("Mock", "AAPL", asOfDate);
        var mappings = resolved is null
            ? []
            : await repository.ListMappingsAsync(resolved.Id);

        Console.WriteLine("Symbol identity smoke check completed.");
        Console.WriteLine($"- SQLite Connection: {MaskConnectionString(sqliteConnection)}");
        Console.WriteLine($"- SymbolMasterId: {result.SymbolMasterId}");
        Console.WriteLine($"- CreatedSymbolMaster: {result.CreatedSymbolMaster}");
        Console.WriteLine($"- CreatedSymbolMapping: {result.CreatedSymbolMapping}");
        Console.WriteLine($"- ClosedOverlappingMappings: {result.ClosedOverlappingMappings}");
        Console.WriteLine($"- ResolvedCanonicalSymbol: {resolved?.Symbol ?? "<not found>"}");
        Console.WriteLine($"- MappingCountForResolvedSymbol: {mappings.Count}");
    }
    finally
    {
        if (repository is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (repository is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

static string MaskConnectionString(string value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return "<empty>";
    }

    var segments = value.Split(';', StringSplitOptions.RemoveEmptyEntries);
    var masked = segments
        .Select(segment =>
        {
            var pair = segment.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2)
            {
                return segment;
            }

            return pair[0].Equals("Password", StringComparison.OrdinalIgnoreCase)
                ? $"{pair[0]}=***"
                : segment;
        });

    return string.Join(';', masked) + ';';
}

internal sealed record StartupCommand(string? EnvironmentOverride, bool ValidateConfigOnly, bool RunSymbolIdentitySmoke)
{
    public static StartupCommand Parse(string[] args)
    {
        string? environment = null;
        var validateConfigOnly = false;
        var runSymbolIdentitySmoke = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--validate-config":
                    validateConfigOnly = true;
                    break;
                case "--symbol-smoke":
                    runSymbolIdentitySmoke = true;
                    break;
                case "--environment" when i + 1 < args.Length:
                    environment = args[++i];
                    break;
            }
        }

        return new StartupCommand(environment, validateConfigOnly, runSymbolIdentitySmoke);
    }
}
