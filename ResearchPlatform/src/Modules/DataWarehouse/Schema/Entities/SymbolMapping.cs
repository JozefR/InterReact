namespace DataWarehouse.Schema.Entities;

public sealed class SymbolMapping
{
    public long Id { get; set; }
    public long SymbolMasterId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderSymbol { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    public SymbolMaster SymbolMaster { get; set; } = null!;
}
