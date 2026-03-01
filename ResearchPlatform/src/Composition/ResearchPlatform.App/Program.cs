using AiResearchAssistant;
using BacktestEngine;
using DataIngestion;
using DataWarehouse;
using ExperimentRunner;
using MetricsReporting;
using ResearchPlatform.App.Configuration;
using ResearchPlatform.Contracts.Abstractions;
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

internal sealed record StartupCommand(string? EnvironmentOverride, bool ValidateConfigOnly)
{
    public static StartupCommand Parse(string[] args)
    {
        string? environment = null;
        var validateConfigOnly = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--validate-config":
                    validateConfigOnly = true;
                    break;
                case "--environment" when i + 1 < args.Length:
                    environment = args[++i];
                    break;
            }
        }

        return new StartupCommand(environment, validateConfigOnly);
    }
}
