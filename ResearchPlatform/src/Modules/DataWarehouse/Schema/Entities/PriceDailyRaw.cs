namespace DataWarehouse.Schema.Entities;

public sealed class PriceDailyRaw
{
    public long Id { get; set; }
    public long SymbolMasterId { get; set; }
    public DateOnly TradeDate { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public decimal? Vwap { get; set; }
    public string Provider { get; set; } = string.Empty;
    public long? IngestionRunId { get; set; }

    public SymbolMaster SymbolMaster { get; set; } = null!;
    public IngestionRun? IngestionRun { get; set; }
}
