using DataWarehouse.Schema.Enums;

namespace DataWarehouse.Schema.Entities;

public sealed class SymbolMaster
{
    public long Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ExchangeMic { get; set; } = string.Empty;
    public AssetType AssetType { get; set; } = AssetType.Equity;
    public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; } = true;
    public DateOnly? ListedDate { get; set; }
    public DateOnly? DelistedDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public List<SymbolMapping> SymbolMappings { get; set; } = [];
    public List<IndexConstituentPit> IndexConstituents { get; set; } = [];
    public List<PriceDailyRaw> RawPrices { get; set; } = [];
    public List<CorporateAction> CorporateActions { get; set; } = [];
    public List<PriceDailyAdjusted> AdjustedPrices { get; set; } = [];
}
