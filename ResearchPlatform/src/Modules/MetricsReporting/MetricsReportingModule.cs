using ResearchPlatform.Contracts.Abstractions;

namespace MetricsReporting;

public sealed class MetricsReportingModule : IModule
{
    public string Name => "MetricsReporting";
    public string Description => "Metric computation, benchmark comparisons, and report artifact generation.";
}
