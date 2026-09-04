using AIStudio.Provider;

namespace AIStudio.Settings;

public static class DataSourceSecurityTrustExtensions
{
    public static bool IsTrustedForDataSourceSecurityChecks(this Provider provider, SettingsManager settingsManager)
    {
        if (provider == Provider.NONE)
            return false;

        return provider.IsSelfHosted || provider.IsTrustedByConfiguration(settingsManager);
    }

    public static bool IsTrustedForDataSourceSecurityChecks(this EmbeddingProvider provider, SettingsManager settingsManager)
    {
        if (provider == EmbeddingProvider.NONE)
            return false;

        return provider.IsSelfHosted || provider.IsTrustedByConfiguration(settingsManager);
    }

    public static bool IsTrustedForDataSourceSecurityChecks(this TranscriptionProvider provider, SettingsManager settingsManager)
    {
        if (provider == TranscriptionProvider.NONE)
            return false;

        return provider.IsSelfHosted || provider.IsTrustedByConfiguration(settingsManager);
    }

    public static bool IsTrustedForDataSourceSecurityChecks(this IProvider provider, SettingsManager settingsManager)
    {
        if (provider is NoProvider)
            return false;

        return provider.Provider is LLMProviders.SELF_HOSTED || IsTrustedProviderId(provider.ConfiguredProviderId, settingsManager);
    }

    public static bool IsTrustedByConfiguration(this Provider provider, SettingsManager settingsManager) => IsTrustedProviderId(provider.Id, settingsManager);

    public static bool IsTrustedByConfiguration(this EmbeddingProvider provider, SettingsManager settingsManager) => IsTrustedProviderId(provider.Id, settingsManager);

    public static bool IsTrustedByConfiguration(this TranscriptionProvider provider, SettingsManager settingsManager) => IsTrustedProviderId(provider.Id, settingsManager);

    public static bool IsTrustedByConfiguration(this IProvider provider, SettingsManager settingsManager) => IsTrustedProviderId(provider.ConfiguredProviderId, settingsManager);

    private static bool IsTrustedProviderId(string providerId, SettingsManager settingsManager)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            return false;

        return settingsManager.ConfigurationData.DataSourceSecurity.TrustedProviderIds.Any(id => string.Equals(id, providerId, StringComparison.OrdinalIgnoreCase));
    }
}
