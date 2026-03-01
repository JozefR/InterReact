using ResearchPlatform.Contracts.Abstractions;

namespace DataWarehouse;

public sealed class DataWarehouseModule : IModule
{
    public string Name => "DataWarehouse";
    public string Description => "Persistence models, schema lifecycle, and curated research datasets.";
}
