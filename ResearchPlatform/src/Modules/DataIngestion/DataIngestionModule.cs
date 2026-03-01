using ResearchPlatform.Contracts.Abstractions;

namespace DataIngestion;

public sealed class DataIngestionModule : IModule
{
    public string Name => "DataIngestion";
    public string Description => "Provider adapters, scheduled pulls, and raw market data ingestion.";
}
