using AIStudio.Provider;
using AIStudio.Settings.DataModel;

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

    public static ConfidenceLevel GetConfidenceLevel(this Provider provider, SettingsManager settingsManager)
    {
        if (provider == Provider.NONE)
            return ConfidenceLevel.NONE;

        return provider.UsedLLMProvider.GetConfidence(settingsManager).Level;
    }

    public static ConfidenceLevel GetConfidenceLevel(this EmbeddingProvider provider, SettingsManager settingsManager)
    {
        if (provider == EmbeddingProvider.NONE)
            return ConfidenceLevel.NONE;

        return provider.UsedLLMProvider.GetConfidence(settingsManager).Level;
    }

    public static ConfidenceLevel GetConfidenceLevel(this IProvider provider, SettingsManager settingsManager)
    {
        if (provider is NoProvider)
            return ConfidenceLevel.NONE;

        return provider.Provider.GetConfidence(settingsManager).Level;
    }

    public static bool AllowsDataSourceAccess(this Provider provider, SettingsManager settingsManager, DataSourceSecurity dataSourceSecurity, ConfidenceLevel dataSourceComplianceLevel)
    {
        return provider.AllowsDataSourceSecurity(dataSourceSecurity, settingsManager)
            && provider.GetConfidenceLevel(settingsManager).AllowsDataSourceComplianceLevel(dataSourceComplianceLevel);
    }

    public static bool AllowsDataSourceAccess(this EmbeddingProvider provider, SettingsManager settingsManager, DataSourceSecurity dataSourceSecurity, ConfidenceLevel dataSourceComplianceLevel)
    {
        return provider.AllowsDataSourceSecurity(dataSourceSecurity, settingsManager)
            && provider.GetConfidenceLevel(settingsManager).AllowsDataSourceComplianceLevel(dataSourceComplianceLevel);
    }

    public static bool AllowsDataSourceAccess(this IProvider provider, SettingsManager settingsManager, DataSourceSecurity dataSourceSecurity, ConfidenceLevel dataSourceComplianceLevel)
    {
        return provider.AllowsDataSourceSecurity(dataSourceSecurity, settingsManager)
            && provider.GetConfidenceLevel(settingsManager).AllowsDataSourceComplianceLevel(dataSourceComplianceLevel);
    }

    public static bool AllowsDataSourceSecurity(this Provider provider, DataSourceSecurity dataSourceSecurity, SettingsManager settingsManager)
        => provider.IsTrustedForDataSourceSecurityChecks(settingsManager).AllowsDataSourceSecurity(dataSourceSecurity);

    public static bool AllowsDataSourceSecurity(this EmbeddingProvider provider, DataSourceSecurity dataSourceSecurity, SettingsManager settingsManager)
        => provider.IsTrustedForDataSourceSecurityChecks(settingsManager).AllowsDataSourceSecurity(dataSourceSecurity);

    public static bool AllowsDataSourceSecurity(this IProvider provider, DataSourceSecurity dataSourceSecurity, SettingsManager settingsManager)
        => provider.IsTrustedForDataSourceSecurityChecks(settingsManager).AllowsDataSourceSecurity(dataSourceSecurity);

    public static bool AllowsDataSourceSecurity(this bool usingTrustedProvider, DataSourceSecurity dataSourceSecurity) => dataSourceSecurity switch
    {
        DataSourceSecurity.ALLOW_ANY => true,
        DataSourceSecurity.SELF_HOSTED => usingTrustedProvider,
        _ => false,
    };

    public static bool AllowsDataSourceComplianceLevel(this ConfidenceLevel providerConfidenceLevel, ConfidenceLevel dataSourceComplianceLevel)
    {
        if (dataSourceComplianceLevel is ConfidenceLevel.NONE)
            return true;

        return providerConfidenceLevel >= dataSourceComplianceLevel;
    }

    public static ConfidenceLevel GetRequiredComplianceLevel(this IEnumerable<IDataSource> dataSources)
    {
        var requiredComplianceLevel = ConfidenceLevel.NONE;
        foreach (var dataSource in dataSources)
            if (dataSource.ComplianceLevel > requiredComplianceLevel)
                requiredComplianceLevel = dataSource.ComplianceLevel;

        return requiredComplianceLevel;
    }

    public static DataSourceSecurity GetRequiredSecurityPolicy(this IEnumerable<IDataSource> dataSources)
    {
        var requiredSecurityPolicy = DataSourceSecurity.ALLOW_ANY;
        foreach (var dataSource in dataSources)
        {
            if (dataSource.SecurityPolicy is DataSourceSecurity.NOT_SPECIFIED)
                return DataSourceSecurity.NOT_SPECIFIED;

            if (dataSource.SecurityPolicy is DataSourceSecurity.SELF_HOSTED)
                requiredSecurityPolicy = DataSourceSecurity.SELF_HOSTED;
        }

        return requiredSecurityPolicy;
    }

    public static bool IsTrustedByConfiguration(this Provider provider, SettingsManager settingsManager) => IsTrustedProviderId(provider.Id, settingsManager);

    public static bool IsTrustedByConfiguration(this EmbeddingProvider provider, SettingsManager settingsManager) => IsTrustedProviderId(provider.Id, settingsManager);

    public static bool IsTrustedByConfiguration(this TranscriptionProvider provider, SettingsManager settingsManager) => IsTrustedProviderId(provider.Id, settingsManager);

    private static bool IsTrustedProviderId(string providerId, SettingsManager settingsManager)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            return false;

        return settingsManager.ConfigurationData.DataSourceSecurity.TrustedProviderIds.Any(id => string.Equals(id, providerId, StringComparison.OrdinalIgnoreCase));
    }
}