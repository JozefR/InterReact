using System;
using AiResearchAssistant;
using BacktestEngine;
using DataIngestion;
using DataWarehouse;
using ExperimentRunner;
using MetricsReporting;
using ResearchPlatform.Contracts.Abstractions;
using StrategyRegistry;

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
