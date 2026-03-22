using DataWarehouse.Schema;
using Microsoft.EntityFrameworkCore;
using ResearchPlatform.Contracts.Abstractions;

namespace DataWarehouse.Prices;

public static class SqlitePriceHistoryRepositoryFactory
{
    public static IPriceHistoryRepository Create(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var options = new DbContextOptionsBuilder<ResearchWarehouseDbContext>()
            .UseSqlite(connectionString)
            .Options;

        var context = new ResearchWarehouseDbContext(options);
        return new EfPriceHistoryRepository(context);
    }
}
