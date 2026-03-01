namespace DataWarehouse.Schema.Entities;

public sealed class IndexConstituentPit
{
    public long Id { get; set; }
    public string IndexCode { get; set; } = string.Empty;
    public long SymbolMasterId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public decimal? Weight { get; set; }
    public string Source { get; set; } = string.Empty;

    public SymbolMaster SymbolMaster { get; set; } = null!;
}
