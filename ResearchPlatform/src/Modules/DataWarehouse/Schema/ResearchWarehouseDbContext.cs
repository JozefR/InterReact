using DataWarehouse.Schema.Entities;
using DataWarehouse.Schema.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataWarehouse.Schema;

public sealed class ResearchWarehouseDbContext(DbContextOptions<ResearchWarehouseDbContext> options) : DbContext(options)
{
    public DbSet<SymbolMaster> SymbolMasters => Set<SymbolMaster>();
    public DbSet<SymbolMapping> SymbolMappings => Set<SymbolMapping>();
    public DbSet<IndexConstituentPit> IndexConstituentsPit => Set<IndexConstituentPit>();
    public DbSet<PriceDailyRaw> PricesDailyRaw => Set<PriceDailyRaw>();
    public DbSet<CorporateAction> CorporateActions => Set<CorporateAction>();
    public DbSet<PriceDailyAdjusted> PricesDailyAdjusted => Set<PriceDailyAdjusted>();
    public DbSet<IngestionRun> IngestionRuns => Set<IngestionRun>();
    public DbSet<QaResult> QaResults => Set<QaResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureSymbolMaster(modelBuilder.Entity<SymbolMaster>());
        ConfigureSymbolMapping(modelBuilder.Entity<SymbolMapping>());
        ConfigureIndexConstituent(modelBuilder.Entity<IndexConstituentPit>());
        ConfigurePriceDailyRaw(modelBuilder.Entity<PriceDailyRaw>());
        ConfigureCorporateAction(modelBuilder.Entity<CorporateAction>());
        ConfigurePriceDailyAdjusted(modelBuilder.Entity<PriceDailyAdjusted>());
        ConfigureIngestionRun(modelBuilder.Entity<IngestionRun>());
        ConfigureQaResult(modelBuilder.Entity<QaResult>());
    }

    private static void ConfigureSymbolMaster(EntityTypeBuilder<SymbolMaster> e)
    {
        e.ToTable("symbol_master");
        e.HasKey(x => x.Id);

        e.Property(x => x.Symbol).HasMaxLength(16).IsRequired();
        e.Property(x => x.Name).HasMaxLength(256).IsRequired();
        e.Property(x => x.ExchangeMic).HasMaxLength(16).IsRequired();
        e.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        e.Property(x => x.AssetType).HasConversion<string>().HasMaxLength(32).IsRequired();

        e.HasIndex(x => x.Symbol).IsUnique();
        e.HasIndex(x => x.IsActive);

        e.Property(x => x.CreatedUtc).IsRequired();
        e.Property(x => x.UpdatedUtc).IsRequired();
    }

    private static void ConfigureSymbolMapping(EntityTypeBuilder<SymbolMapping> e)
    {
        e.ToTable("symbol_mapping", t =>
        {
            t.HasCheckConstraint("CK_symbol_mapping_effective_range", "EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom");
        });
        e.HasKey(x => x.Id);

        e.Property(x => x.Provider).HasMaxLength(64).IsRequired();
        e.Property(x => x.ProviderSymbol).HasMaxLength(64).IsRequired();
        e.Property(x => x.EffectiveFrom).IsRequired();

        e.HasIndex(x => new { x.Provider, x.ProviderSymbol, x.EffectiveFrom }).IsUnique();
        e.HasIndex(x => new { x.SymbolMasterId, x.EffectiveFrom });

        e.HasOne(x => x.SymbolMaster)
            .WithMany(x => x.SymbolMappings)
            .HasForeignKey(x => x.SymbolMasterId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureIndexConstituent(EntityTypeBuilder<IndexConstituentPit> e)
    {
        e.ToTable("index_constituents_pit", t =>
        {
            t.HasCheckConstraint("CK_index_constituents_pit_effective_range", "EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom");
        });
        e.HasKey(x => x.Id);

        e.Property(x => x.IndexCode).HasMaxLength(32).IsRequired();
        e.Property(x => x.Source).HasMaxLength(64).IsRequired();
        e.Property(x => x.Weight).HasPrecision(12, 8);

        e.HasIndex(x => new { x.IndexCode, x.SymbolMasterId, x.EffectiveFrom }).IsUnique();
        e.HasIndex(x => new { x.IndexCode, x.EffectiveFrom });

        e.HasOne(x => x.SymbolMaster)
            .WithMany(x => x.IndexConstituents)
            .HasForeignKey(x => x.SymbolMasterId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigurePriceDailyRaw(EntityTypeBuilder<PriceDailyRaw> e)
    {
        e.ToTable("prices_daily_raw", t =>
        {
            t.HasCheckConstraint("CK_prices_daily_raw_price_order", "High >= Low AND Open >= Low AND Open <= High AND Close >= Low AND Close <= High");
            t.HasCheckConstraint("CK_prices_daily_raw_positive_volume", "Volume >= 0");
        });
        e.HasKey(x => x.Id);

        e.Property(x => x.Open).HasPrecision(18, 6).IsRequired();
        e.Property(x => x.High).HasPrecision(18, 6).IsRequired();
        e.Property(x => x.Low).HasPrecision(18, 6).IsRequired();
        e.Property(x => x.Close).HasPrecision(18, 6).IsRequired();
        e.Property(x => x.Vwap).HasPrecision(18, 6);
        e.Property(x => x.Provider).HasMaxLength(64).IsRequired();

        e.HasIndex(x => new { x.SymbolMasterId, x.TradeDate, x.Provider }).IsUnique();
        e.HasIndex(x => new { x.SymbolMasterId, x.TradeDate });

        e.HasOne(x => x.SymbolMaster)
            .WithMany(x => x.RawPrices)
            .HasForeignKey(x => x.SymbolMasterId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.IngestionRun)
            .WithMany(x => x.RawPrices)
            .HasForeignKey(x => x.IngestionRunId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureCorporateAction(EntityTypeBuilder<CorporateAction> e)
    {
        e.ToTable("corporate_actions");
        e.HasKey(x => x.Id);

        e.Property(x => x.ActionType).HasConversion<string>().HasMaxLength(32).IsRequired();
        e.Property(x => x.Value).HasPrecision(18, 8).IsRequired();
        e.Property(x => x.Currency).HasMaxLength(8);
        e.Property(x => x.Provider).HasMaxLength(64).IsRequired();
        e.Property(x => x.ExternalId).HasMaxLength(128);
        e.Property(x => x.Description).HasMaxLength(512);

        e.HasIndex(x => new { x.SymbolMasterId, x.ActionDate, x.ActionType, x.Provider, x.Value }).IsUnique();
        e.HasIndex(x => new { x.SymbolMasterId, x.ActionDate });

        e.HasOne(x => x.SymbolMaster)
            .WithMany(x => x.CorporateActions)
            .HasForeignKey(x => x.SymbolMasterId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigurePriceDailyAdjusted(EntityTypeBuilder<PriceDailyAdjusted> e)
    {
        e.ToTable("prices_daily_adjusted", t =>
        {
            t.HasCheckConstraint("CK_prices_daily_adjusted_price_order", "High >= Low AND Open >= Low AND Open <= High AND Close >= Low AND Close <= High AND AdjustedClose >= 0");
            t.HasCheckConstraint("CK_prices_daily_adjusted_positive_volume", "Volume >= 0");
            t.HasCheckConstraint("CK_prices_daily_adjusted_factor_non_negative", "AdjustmentFactor >= 0");
        });
        e.HasKey(x => x.Id);

        e.Property(x => x.Open).HasPrecision(18, 6).IsRequired();
        e.Property(x => x.High).HasPrecision(18, 6).IsRequired();
        e.Property(x => x.Low).HasPrecision(18, 6).IsRequired();
        e.Property(x => x.Close).HasPrecision(18, 6).IsRequired();
        e.Property(x => x.AdjustedClose).HasPrecision(18, 6).IsRequired();
        e.Property(x => x.AdjustmentFactor).HasPrecision(18, 8).IsRequired();
        e.Property(x => x.AdjustmentBasis).HasMaxLength(64).IsRequired();
        e.Property(x => x.Provider).HasMaxLength(64).IsRequired();

        e.HasIndex(x => new { x.SymbolMasterId, x.TradeDate, x.Provider }).IsUnique();
        e.HasIndex(x => new { x.SymbolMasterId, x.TradeDate });

        e.HasOne(x => x.SymbolMaster)
            .WithMany(x => x.AdjustedPrices)
            .HasForeignKey(x => x.SymbolMasterId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.IngestionRun)
            .WithMany(x => x.AdjustedPrices)
            .HasForeignKey(x => x.IngestionRunId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureIngestionRun(EntityTypeBuilder<IngestionRun> e)
    {
        e.ToTable("ingestion_runs");
        e.HasKey(x => x.Id);

        e.Property(x => x.RunId).IsRequired();
        e.Property(x => x.Pipeline).HasMaxLength(128).IsRequired();
        e.Property(x => x.Provider).HasMaxLength(64).IsRequired();
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        e.Property(x => x.RequestParametersJson).HasMaxLength(4000);
        e.Property(x => x.ErrorMessage).HasMaxLength(4000);

        e.HasIndex(x => x.RunId).IsUnique();
        e.HasIndex(x => new { x.Pipeline, x.StartedAtUtc });
        e.HasIndex(x => x.Status);
    }

    private static void ConfigureQaResult(EntityTypeBuilder<QaResult> e)
    {
        e.ToTable("qa_results");
        e.HasKey(x => x.Id);

        e.Property(x => x.CheckName).HasMaxLength(128).IsRequired();
        e.Property(x => x.Scope).HasMaxLength(128).IsRequired();
        e.Property(x => x.Severity).HasConversion<string>().HasMaxLength(16).IsRequired();
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        e.Property(x => x.DetailsJson).HasMaxLength(4000);
        e.Property(x => x.CreatedUtc).IsRequired();

        e.HasIndex(x => new { x.Severity, x.Status, x.CreatedUtc });

        e.HasOne(x => x.IngestionRun)
            .WithMany(x => x.QaResults)
            .HasForeignKey(x => x.IngestionRunId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
