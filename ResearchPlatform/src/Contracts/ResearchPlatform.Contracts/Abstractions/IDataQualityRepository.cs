using ResearchPlatform.Contracts.Quality;

namespace ResearchPlatform.Contracts.Abstractions;

public interface IDataQualityRepository
{
    Task<DataQualityRunResult> RunChecksAsync(
        DataQualityRunRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DataQualityResultSnapshot>> GetResultsAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}
