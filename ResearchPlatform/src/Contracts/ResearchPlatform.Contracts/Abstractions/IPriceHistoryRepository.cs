using ResearchPlatform.Contracts.Prices;

namespace ResearchPlatform.Contracts.Abstractions;

public interface IPriceHistoryRepository
{
    Task<DailyPriceLoadResult> UpsertRawPricesAsync(
        DailyPriceLoadRequest request,
        CancellationToken cancellationToken = default);

    Task<AdjustedPriceBuildResult> RebuildAdjustedPricesAsync(
        AdjustedPriceBuildRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RawDailyPriceSnapshot>> GetRawPricesAsync(
        string canonicalSymbol,
        string provider,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdjustedDailyPriceSnapshot>> GetAdjustedPricesAsync(
        string canonicalSymbol,
        string provider,
        string adjustmentBasis,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);
}
