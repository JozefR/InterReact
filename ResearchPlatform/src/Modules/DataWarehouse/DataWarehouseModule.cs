using ResearchPlatform.Contracts.Abstractions;

namespace DataWarehouse;

public sealed class DataWarehouseModule : IModule
{
    public string Name => "DataWarehouse";
    public string Description => "Persistence models, schema lifecycle, symbol identity/PIT repositories, and curated research datasets.";
}
