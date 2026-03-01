using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DataWarehouse.Schema.Design;

public sealed class ResearchWarehouseDesignTimeFactory : IDesignTimeDbContextFactory<ResearchWarehouseDbContext>
{
    public ResearchWarehouseDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("RP__DataWarehouse__ConnectionString")
                               ?? "Data Source=researchplatform.db";

        var builder = new DbContextOptionsBuilder<ResearchWarehouseDbContext>();
        builder.UseSqlite(connectionString);

        return new ResearchWarehouseDbContext(builder.Options);
    }
}
