using DataWarehouse.Schema;
using Microsoft.EntityFrameworkCore;
using ResearchPlatform.Contracts.Abstractions;

namespace DataWarehouse.Quality;

public static class SqliteDataQualityRepositoryFactory
{
    public static IDataQualityRepository Create(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var options = new DbContextOptionsBuilder<ResearchWarehouseDbContext>()
            .UseSqlite(connectionString)
            .Options;

        var context = new ResearchWarehouseDbContext(options);
        return new EfDataQualityRepository(context);
    }
}
