using DataWarehouse.Schema.Enums;

namespace DataWarehouse.Schema.Entities;

public sealed class CorporateAction
{
    public long Id { get; set; }
    public long SymbolMasterId { get; set; }
    public DateOnly ActionDate { get; set; }
    public CorporateActionType ActionType { get; set; }
    public decimal Value { get; set; }
    public decimal? AdjustmentFactor { get; set; }
    public string? Currency { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string? Description { get; set; }
    public string? RelatedProviderSymbol { get; set; }
    public string? AttributesJson { get; set; }
    public long? IngestionRunId { get; set; }

    public SymbolMaster SymbolMaster { get; set; } = null!;
    public IngestionRun? IngestionRun { get; set; }
}
