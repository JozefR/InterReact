using DataWarehouse.Schema.Enums;

namespace DataWarehouse.Schema.Entities;

public sealed class IngestionRun
{
    public long Id { get; set; }
    public Guid RunId { get; set; } = Guid.NewGuid();
    public string Pipeline { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public IngestionRunStatus Status { get; set; } = IngestionRunStatus.Started;
    public DateTime RequestedAtUtc { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public string? RequestParametersJson { get; set; }
    public int RowsRead { get; set; }
    public int RowsInserted { get; set; }
    public int RowsUpdated { get; set; }
    public string? ErrorMessage { get; set; }

    public List<PriceDailyRaw> RawPrices { get; set; } = [];
    public List<PriceDailyAdjusted> AdjustedPrices { get; set; } = [];
    public List<QaResult> QaResults { get; set; } = [];
}
