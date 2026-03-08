using ResearchPlatform.Contracts.Abstractions;

namespace DataIngestion;

public sealed class DataIngestionModule : IModule
{
    public string Name => "DataIngestion";
    public string Description => "Provider connector implementations (Mock/Massive), pull orchestration, and raw market data ingestion.";
}
