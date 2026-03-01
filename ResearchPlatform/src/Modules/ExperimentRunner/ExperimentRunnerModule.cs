using ResearchPlatform.Contracts.Abstractions;

namespace ExperimentRunner;

public sealed class ExperimentRunnerModule : IModule
{
    public string Name => "ExperimentRunner";
    public string Description => "Batch orchestration for parameter sweeps and walk-forward experiments.";
}
