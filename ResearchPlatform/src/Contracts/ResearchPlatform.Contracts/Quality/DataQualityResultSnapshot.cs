namespace ResearchPlatform.Contracts.Quality;

public sealed record DataQualityResultSnapshot(
    long Id,
    Guid? RunId,
    string CheckName,
    string Scope,
    DataQualitySeverity Severity,
    DataQualityStatus Status,
    int AffectedRows,
    string? DetailsJson,
    DateTime CreatedUtc);
