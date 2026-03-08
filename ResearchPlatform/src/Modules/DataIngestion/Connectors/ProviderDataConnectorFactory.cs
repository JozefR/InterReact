using DataIngestion.Connectors.Massive;
using DataIngestion.Connectors.Mock;
using ResearchPlatform.Contracts.Abstractions;

namespace DataIngestion.Connectors;

public static class ProviderDataConnectorFactory
{
    public static IProviderDataConnector Create(
        string providerCode,
        ProviderDataConnectorFactoryOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCode);
        options ??= new ProviderDataConnectorFactoryOptions(
            RequestTimeoutSeconds: 30,
            MassiveApiBaseUrl: "https://api.massive.com",
            MassiveApiKey: null,
            MassiveUseFixtureFallbackWhenApiKeyMissing: true);

        if (providerCode.Equals(MockProviderDataConnector.ProviderCodeValue, StringComparison.OrdinalIgnoreCase))
        {
            return new MockProviderDataConnector();
        }

        if (providerCode.Equals(MassiveEodProviderDataConnector.ProviderCodeValue, StringComparison.OrdinalIgnoreCase))
        {
            return new MassiveEodProviderDataConnector(new MassiveEodConnectorOptions(
                ApiBaseUrl: options.MassiveApiBaseUrl,
                ApiKey: options.MassiveApiKey,
                RequestTimeoutSeconds: options.RequestTimeoutSeconds,
                UseFixtureFallbackWhenApiKeyMissing: options.MassiveUseFixtureFallbackWhenApiKeyMissing));
        }

        if (providerCode.Equals("IEX", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                "Provider 'IEX' is retired. Please switch to provider 'MASSIVE'.");
        }

        throw new NotSupportedException(
            $"Provider '{providerCode}' is not registered yet. " +
            $"Available connectors: {MockProviderDataConnector.ProviderCodeValue}, {MassiveEodProviderDataConnector.ProviderCodeValue}.");
    }
}
