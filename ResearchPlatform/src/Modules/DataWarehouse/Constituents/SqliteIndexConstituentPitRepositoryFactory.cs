using DataWarehouse.Schema;
using Microsoft.EntityFrameworkCore;
using ResearchPlatform.Contracts.Abstractions;

namespace DataWarehouse.Constituents;

public static class SqliteIndexConstituentPitRepositoryFactory
{
    public static IIndexConstituentPitRepository Create(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var options = new DbContextOptionsBuilder<ResearchWarehouseDbContext>()
            .UseSqlite(connectionString)
            .Options;

        var context = new ResearchWarehouseDbContext(options);
        return new EfIndexConstituentPitRepository(context);
    }
}
