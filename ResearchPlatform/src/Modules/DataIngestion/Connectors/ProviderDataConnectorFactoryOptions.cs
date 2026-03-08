namespace DataIngestion.Connectors;

public sealed record ProviderDataConnectorFactoryOptions(
    int RequestTimeoutSeconds,
    string MassiveApiBaseUrl,
    string? MassiveApiKey,
    bool MassiveUseFixtureFallbackWhenApiKeyMissing);
