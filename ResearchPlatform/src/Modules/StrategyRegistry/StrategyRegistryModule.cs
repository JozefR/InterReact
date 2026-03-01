using ResearchPlatform.Contracts.Abstractions;

namespace StrategyRegistry;

public sealed class StrategyRegistryModule : IModule
{
    public string Name => "StrategyRegistry";
    public string Description => "Strategy contracts and pluggable strategy discovery/registration.";
}
