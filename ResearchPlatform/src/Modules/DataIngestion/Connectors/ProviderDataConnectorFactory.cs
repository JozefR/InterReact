using DataIngestion.Connectors.Mock;
using ResearchPlatform.Contracts.Abstractions;

namespace DataIngestion.Connectors;

public static class ProviderDataConnectorFactory
{
    public static IProviderDataConnector Create(string providerCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCode);

        if (providerCode.Equals(MockProviderDataConnector.ProviderCodeValue, StringComparison.OrdinalIgnoreCase))
        {
            return new MockProviderDataConnector();
        }

        throw new NotSupportedException(
            $"Provider '{providerCode}' is not registered yet. " +
            $"Available connectors: {MockProviderDataConnector.ProviderCodeValue}.");
    }
}
