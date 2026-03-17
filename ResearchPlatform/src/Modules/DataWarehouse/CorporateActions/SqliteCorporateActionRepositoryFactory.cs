using DataWarehouse.Schema;
using Microsoft.EntityFrameworkCore;
using ResearchPlatform.Contracts.Abstractions;

namespace DataWarehouse.CorporateActions;

public static class SqliteCorporateActionRepositoryFactory
{
    public static ICorporateActionRepository Create(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var options = new DbContextOptionsBuilder<ResearchWarehouseDbContext>()
            .UseSqlite(connectionString)
            .Options;

        var context = new ResearchWarehouseDbContext(options);
        return new EfCorporateActionRepository(context);
    }
}
