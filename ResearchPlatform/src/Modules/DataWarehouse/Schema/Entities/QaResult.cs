using DataWarehouse.Schema.Enums;

namespace DataWarehouse.Schema.Entities;

public sealed class QaResult
{
    public long Id { get; set; }
    public long? IngestionRunId { get; set; }
    public string CheckName { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public QaSeverity Severity { get; set; }
    public QaStatus Status { get; set; }
    public int AffectedRows { get; set; }
    public string? DetailsJson { get; set; }
    public DateTime CreatedUtc { get; set; }

    public IngestionRun? IngestionRun { get; set; }
}
