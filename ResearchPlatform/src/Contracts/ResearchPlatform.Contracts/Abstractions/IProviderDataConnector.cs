using ResearchPlatform.Contracts.Ingestion;

namespace ResearchPlatform.Contracts.Abstractions;

public interface IProviderDataConnector
{
    string ProviderCode { get; }
    IngestionConnectorCapabilities Capabilities { get; }

    Task<ProviderConstituentSnapshotBatch> FetchConstituentSnapshotAsync(
        ProviderConstituentSnapshotRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderDailyPriceBatch> FetchDailyPricesAsync(
        ProviderDailyPriceRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderCorporateActionBatch> FetchCorporateActionsAsync(
        ProviderCorporateActionRequest request,
        CancellationToken cancellationToken = default);
}
