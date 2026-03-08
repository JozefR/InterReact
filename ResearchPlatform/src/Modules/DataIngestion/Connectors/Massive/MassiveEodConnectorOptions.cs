namespace DataIngestion.Connectors.Massive;

public sealed record MassiveEodConnectorOptions(
    string ApiBaseUrl,
    string? ApiKey,
    int RequestTimeoutSeconds,
    bool UseFixtureFallbackWhenApiKeyMissing);
