using ResearchPlatform.Contracts.CorporateActions;

namespace ResearchPlatform.Contracts.Abstractions;

public interface ICorporateActionRepository
{
    Task<CorporateActionLoadResult> UpsertActionsAsync(
        CorporateActionLoadRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CorporateActionSnapshot>> GetActionsAsync(
        string canonicalSymbol,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);
}
