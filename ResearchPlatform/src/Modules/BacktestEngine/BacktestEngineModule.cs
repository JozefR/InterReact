using ResearchPlatform.Contracts.Abstractions;

namespace BacktestEngine;

public sealed class BacktestEngineModule : IModule
{
    public string Name => "BacktestEngine";
    public string Description => "Deterministic event loop, fills, portfolio accounting, and trade simulation.";
}
