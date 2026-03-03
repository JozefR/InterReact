using ResearchPlatform.Contracts.Universes;

namespace ResearchPlatform.Contracts.Abstractions;

public interface IIndexConstituentPitRepository
{
    Task<IndexConstituentSnapshotLoadResult> UpsertSnapshotAsync(
        IndexConstituentSnapshotLoadRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IndexConstituentMembershipSnapshot>> GetConstituentsAsOfAsync(
        string indexCode,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IndexConstituentMembershipSnapshot>> GetConstituentHistoryAsync(
        string indexCode,
        string canonicalSymbol,
        CancellationToken cancellationToken = default);
}
