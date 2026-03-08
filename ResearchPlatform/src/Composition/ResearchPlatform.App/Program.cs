using AiResearchAssistant;
using BacktestEngine;
using DataIngestion.Connectors;
using DataWarehouse.Constituents;
using DataIngestion;
using DataWarehouse;
using DataWarehouse.Schema;
using DataWarehouse.Symbols;
using ExperimentRunner;
using MetricsReporting;
using Microsoft.EntityFrameworkCore;
using ResearchPlatform.App.Configuration;
using ResearchPlatform.Contracts.Abstractions;
using ResearchPlatform.Contracts.Ingestion;
using ResearchPlatform.Contracts.Symbols;
using ResearchPlatform.Contracts.Universes;
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

if (command.RunPitConstituentSmoke)
{
    await RunPitConstituentSmokeAsync(config);
    return;
}

if (command.RunConnectorSmoke)
{
    await RunConnectorSmokeAsync(config);
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
    var sqliteConnection = ResolveSqliteConnectionString(config);
    await EnsureWarehouseMigratedAsync(sqliteConnection);

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

static async Task RunPitConstituentSmokeAsync(PlatformConfig config)
{
    var sqliteConnection = ResolveSqliteConnectionString(config);
    await EnsureWarehouseMigratedAsync(sqliteConnection);

    var symbolRepository = SqliteSymbolIdentityRepositoryFactory.Create(sqliteConnection);
    var pitRepository = SqliteIndexConstituentPitRepositoryFactory.Create(sqliteConnection);

    try
    {
        await SeedSymbolsForPitSmokeAsync(symbolRepository);

        var sp500Snapshot1 = await pitRepository.UpsertSnapshotAsync(new IndexConstituentSnapshotLoadRequest(
            IndexCode: UniverseCodes.Sp500,
            Source: "MockUniverse",
            EffectiveFrom: new DateOnly(2026, 1, 1),
            Constituents:
            [
                new IndexConstituentInput("AAPL", 0.055m),
                new IndexConstituentInput("MSFT", 0.065m),
                new IndexConstituentInput("NVDA", 0.050m)
            ]));

        var sp500Snapshot2 = await pitRepository.UpsertSnapshotAsync(new IndexConstituentSnapshotLoadRequest(
            IndexCode: UniverseCodes.Sp500,
            Source: "MockUniverse",
            EffectiveFrom: new DateOnly(2026, 2, 1),
            Constituents:
            [
                new IndexConstituentInput("AAPL", 0.054m),
                new IndexConstituentInput("NVDA", 0.052m),
                new IndexConstituentInput("AMZN", 0.038m)
            ]));

        var sp100Snapshot1 = await pitRepository.UpsertSnapshotAsync(new IndexConstituentSnapshotLoadRequest(
            IndexCode: UniverseCodes.Sp100,
            Source: "MockUniverse",
            EffectiveFrom: new DateOnly(2026, 1, 1),
            Constituents:
            [
                new IndexConstituentInput("AAPL", 0.095m),
                new IndexConstituentInput("MSFT", 0.102m)
            ]));

        var sp100Snapshot2 = await pitRepository.UpsertSnapshotAsync(new IndexConstituentSnapshotLoadRequest(
            IndexCode: UniverseCodes.Sp100,
            Source: "MockUniverse",
            EffectiveFrom: new DateOnly(2026, 2, 1),
            Constituents:
            [
                new IndexConstituentInput("AAPL", 0.093m),
                new IndexConstituentInput("AMZN", 0.081m)
            ]));

        var sp500AsOfJanuary = await pitRepository.GetConstituentsAsOfAsync(UniverseCodes.Sp500, new DateOnly(2026, 1, 15));
        var sp500AsOfFebruary = await pitRepository.GetConstituentsAsOfAsync(UniverseCodes.Sp500, new DateOnly(2026, 2, 15));
        var sp100AsOfFebruary = await pitRepository.GetConstituentsAsOfAsync(UniverseCodes.Sp100, new DateOnly(2026, 2, 15));
        var msftSp500History = await pitRepository.GetConstituentHistoryAsync(UniverseCodes.Sp500, "MSFT");

        Console.WriteLine("PIT constituent smoke check completed.");
        Console.WriteLine($"- SQLite Connection: {MaskConnectionString(sqliteConnection)}");
        Console.WriteLine($"- SP500 Snapshot #1: inserted={sp500Snapshot1.InsertedMembershipRows}, closed={sp500Snapshot1.ClosedMembershipRows}, requested={sp500Snapshot1.RequestedConstituentCount}");
        Console.WriteLine($"- SP500 Snapshot #2: inserted={sp500Snapshot2.InsertedMembershipRows}, closed={sp500Snapshot2.ClosedMembershipRows}, requested={sp500Snapshot2.RequestedConstituentCount}");
        Console.WriteLine($"- SP100 Snapshot #1: inserted={sp100Snapshot1.InsertedMembershipRows}, closed={sp100Snapshot1.ClosedMembershipRows}, requested={sp100Snapshot1.RequestedConstituentCount}");
        Console.WriteLine($"- SP100 Snapshot #2: inserted={sp100Snapshot2.InsertedMembershipRows}, closed={sp100Snapshot2.ClosedMembershipRows}, requested={sp100Snapshot2.RequestedConstituentCount}");
        Console.WriteLine($"- SP500 as-of 2026-01-15: {string.Join(", ", sp500AsOfJanuary.Select(x => x.CanonicalSymbol))}");
        Console.WriteLine($"- SP500 as-of 2026-02-15: {string.Join(", ", sp500AsOfFebruary.Select(x => x.CanonicalSymbol))}");
        Console.WriteLine($"- SP100 as-of 2026-02-15: {string.Join(", ", sp100AsOfFebruary.Select(x => x.CanonicalSymbol))}");
        Console.WriteLine(
            $"- SP500 history windows for MSFT: {string.Join(", ", msftSp500History.Select(x => $"[{x.EffectiveFrom:yyyy-MM-dd}..{(x.EffectiveTo?.ToString("yyyy-MM-dd") ?? "open")}]"))}");
    }
    finally
    {
        if (pitRepository is IAsyncDisposable pitAsyncDisposable)
        {
            await pitAsyncDisposable.DisposeAsync();
        }
        else if (pitRepository is IDisposable pitDisposable)
        {
            pitDisposable.Dispose();
        }

        if (symbolRepository is IAsyncDisposable symbolAsyncDisposable)
        {
            await symbolAsyncDisposable.DisposeAsync();
        }
        else if (symbolRepository is IDisposable symbolDisposable)
        {
            symbolDisposable.Dispose();
        }
    }
}

static async Task RunConnectorSmokeAsync(PlatformConfig config)
{
    var connector = ProviderDataConnectorFactory.Create(
        config.DataIngestion.Provider,
        new ProviderDataConnectorFactoryOptions(
            RequestTimeoutSeconds: config.DataIngestion.RequestTimeoutSeconds,
            MassiveApiBaseUrl: config.DataIngestion.MassiveApiBaseUrl,
            MassiveApiKey: config.DataIngestion.MassiveApiKey,
            MassiveUseFixtureFallbackWhenApiKeyMissing: config.DataIngestion.MassiveUseFixtureFallbackWhenApiKeyMissing));

    var indexCode = config.DataIngestion.Universes.FirstOrDefault() ?? UniverseCodes.Sp500;
    ProviderConstituentSnapshotBatch? firstConstituentSnapshot = null;
    ProviderConstituentSnapshotBatch? secondConstituentSnapshot = null;
    var allConstituents = new List<ProviderConstituentRecord>();

    if (connector.Capabilities.SupportsIndexConstituentSnapshots)
    {
        firstConstituentSnapshot = await connector.FetchConstituentSnapshotAsync(new ProviderConstituentSnapshotRequest(
            IndexCode: indexCode,
            EffectiveDate: new DateOnly(2026, 2, 15)));

        allConstituents.AddRange(firstConstituentSnapshot.Constituents);

        if (!firstConstituentSnapshot.IsComplete && !string.IsNullOrWhiteSpace(firstConstituentSnapshot.ContinuationToken))
        {
            secondConstituentSnapshot = await connector.FetchConstituentSnapshotAsync(new ProviderConstituentSnapshotRequest(
                IndexCode: indexCode,
                EffectiveDate: firstConstituentSnapshot.EffectiveDate,
                ContinuationToken: firstConstituentSnapshot.ContinuationToken));

            allConstituents.AddRange(secondConstituentSnapshot.Constituents);
        }
    }

    var sampleSymbols = allConstituents
        .Select(x => x.ProviderSymbol)
        .Take(3)
        .ToArray();

    if (sampleSymbols.Length == 0)
    {
        sampleSymbols = ["AAPL", "MSFT", "NVDA"];
    }

    ProviderDailyPriceBatch? prices = null;
    if (connector.Capabilities.SupportsDailyPrices)
    {
        prices = await connector.FetchDailyPricesAsync(new ProviderDailyPriceRequest(
            ProviderSymbols: sampleSymbols,
            FromDate: new DateOnly(2026, 2, 10),
            ToDate: new DateOnly(2026, 2, 20)));
    }

    ProviderCorporateActionBatch? actions = null;
    if (connector.Capabilities.SupportsCorporateActions)
    {
        actions = await connector.FetchCorporateActionsAsync(new ProviderCorporateActionRequest(
            ProviderSymbols: sampleSymbols,
            FromDate: new DateOnly(2026, 2, 1),
            ToDate: new DateOnly(2026, 2, 28)));
    }

    Console.WriteLine("Connector smoke check completed.");
    Console.WriteLine($"- Provider: {connector.ProviderCode}");
    Console.WriteLine($"- Capabilities: index={connector.Capabilities.SupportsIndexConstituentSnapshots}, daily={connector.Capabilities.SupportsDailyPrices}, actions={connector.Capabilities.SupportsCorporateActions}");
    if (connector.ProviderCode.Equals("MASSIVE", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"- Massive config: api_base={config.DataIngestion.MassiveApiBaseUrl}, has_api_key={!string.IsNullOrWhiteSpace(config.DataIngestion.MassiveApiKey)}, fixture_fallback_if_missing_key={config.DataIngestion.MassiveUseFixtureFallbackWhenApiKeyMissing}");
    }
    if (firstConstituentSnapshot is not null)
    {
        Console.WriteLine($"- Universe snapshot page #1: {firstConstituentSnapshot.IndexCode} as-of {firstConstituentSnapshot.EffectiveDate:yyyy-MM-dd} -> {firstConstituentSnapshot.Constituents.Count} symbols, complete={firstConstituentSnapshot.IsComplete}, next={firstConstituentSnapshot.ContinuationToken ?? "<none>"}");
    }
    else
    {
        Console.WriteLine("- Universe snapshot: <not supported by connector>");
    }

    if (secondConstituentSnapshot is not null)
    {
        Console.WriteLine($"- Universe snapshot page #2: {secondConstituentSnapshot.Constituents.Count} symbols, complete={secondConstituentSnapshot.IsComplete}, next={secondConstituentSnapshot.ContinuationToken ?? "<none>"}");
    }

    Console.WriteLine($"- Universe snapshot combined symbols: {allConstituents.Count}");
    if (prices is not null)
    {
        Console.WriteLine($"- Daily prices fetched: {prices.Prices.Count} rows ({prices.FromDate:yyyy-MM-dd}..{prices.ToDate:yyyy-MM-dd})");
    }
    else
    {
        Console.WriteLine("- Daily prices: <not supported by connector>");
    }

    if (actions is not null)
    {
        Console.WriteLine($"- Corporate actions fetched: {actions.Actions.Count} rows ({actions.FromDate:yyyy-MM-dd}..{actions.ToDate:yyyy-MM-dd})");
    }
    else
    {
        Console.WriteLine("- Corporate actions: <not supported by connector>");
    }

    Console.WriteLine($"- Sample symbols: {string.Join(", ", sampleSymbols)}");
}

static async Task SeedSymbolsForPitSmokeAsync(ISymbolIdentityRepository repository)
{
    var symbols = new[]
    {
        new SymbolEnrichmentRequest("Mock", "AAPL", new DateOnly(1980, 12, 12), "AAPL", "Apple Inc.", "XNAS"),
        new SymbolEnrichmentRequest("Mock", "MSFT", new DateOnly(1986, 3, 13), "MSFT", "Microsoft Corporation", "XNAS"),
        new SymbolEnrichmentRequest("Mock", "NVDA", new DateOnly(1999, 1, 22), "NVDA", "NVIDIA Corporation", "XNAS"),
        new SymbolEnrichmentRequest("Mock", "AMZN", new DateOnly(1997, 5, 15), "AMZN", "Amazon.com, Inc.", "XNAS")
    };

    foreach (var symbol in symbols)
    {
        await repository.UpsertSymbolAsync(symbol);
    }
}

static string ResolveSqliteConnectionString(PlatformConfig config)
{
    var configuredConnection = config.DataWarehouse.ConnectionString;
    return configuredConnection.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
        ? configuredConnection
        : "Data Source=researchplatform.db";
}

static async Task EnsureWarehouseMigratedAsync(string sqliteConnection)
{
    var bootstrapOptions = new DbContextOptionsBuilder<ResearchWarehouseDbContext>()
        .UseSqlite(sqliteConnection)
        .Options;

    await using var bootstrapContext = new ResearchWarehouseDbContext(bootstrapOptions);
    await bootstrapContext.Database.MigrateAsync();
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

internal sealed record StartupCommand(
    string? EnvironmentOverride,
    bool ValidateConfigOnly,
    bool RunSymbolIdentitySmoke,
    bool RunPitConstituentSmoke,
    bool RunConnectorSmoke)
{
    public static StartupCommand Parse(string[] args)
    {
        string? environment = null;
        var validateConfigOnly = false;
        var runSymbolIdentitySmoke = false;
        var runPitConstituentSmoke = false;
        var runConnectorSmoke = false;

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
                case "--pit-smoke":
                    runPitConstituentSmoke = true;
                    break;
                case "--connector-smoke":
                    runConnectorSmoke = true;
                    break;
                case "--environment" when i + 1 < args.Length:
                    environment = args[++i];
                    break;
            }
        }

        return new StartupCommand(environment, validateConfigOnly, runSymbolIdentitySmoke, runPitConstituentSmoke, runConnectorSmoke);
    }
}
