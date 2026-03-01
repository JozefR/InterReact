namespace DataWarehouse.Schema.Entities;

public sealed class PriceDailyAdjusted
{
    public long Id { get; set; }
    public long SymbolMasterId { get; set; }
    public DateOnly TradeDate { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal AdjustedClose { get; set; }
    public decimal AdjustmentFactor { get; set; }
    public long Volume { get; set; }
    public string AdjustmentBasis { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public long? IngestionRunId { get; set; }

    public SymbolMaster SymbolMaster { get; set; } = null!;
    public IngestionRun? IngestionRun { get; set; }
}
