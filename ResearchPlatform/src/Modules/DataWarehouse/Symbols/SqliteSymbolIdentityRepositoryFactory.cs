using DataWarehouse.Schema;
using Microsoft.EntityFrameworkCore;
using ResearchPlatform.Contracts.Abstractions;

namespace DataWarehouse.Symbols;

public static class SqliteSymbolIdentityRepositoryFactory
{
    public static ISymbolIdentityRepository Create(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var options = new DbContextOptionsBuilder<ResearchWarehouseDbContext>()
            .UseSqlite(connectionString)
            .Options;

        var context = new ResearchWarehouseDbContext(options);
        return new EfSymbolIdentityRepository(context);
    }
}
