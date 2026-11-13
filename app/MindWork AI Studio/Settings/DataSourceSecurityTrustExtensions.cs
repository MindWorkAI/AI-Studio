using AIStudio.Provider;
using AIStudio.Settings.DataModel;

namespace AIStudio.Settings;

public static class DataSourceSecurityTrustExtensions
{
    extension(Provider provider)
    {
        public bool IsTrustedForDataSourceSecurityChecks(SettingsManager settingsManager)
        {
            if (provider == Provider.NONE)
                return false;

            return provider.IsSelfHosted || provider.IsTrustedByConfiguration(settingsManager);
        }

        public ConfidenceLevel GetConfidenceLevel(SettingsManager settingsManager)
        {
            if (provider == Provider.NONE)
                return ConfidenceLevel.NONE;

            return provider.UsedLLMProvider.GetConfidence(settingsManager).Level;
        }

        public bool AllowsDataSourceAccess(SettingsManager settingsManager, DataSourceSecurity dataSourceSecurity, ConfidenceLevel requiredConfidenceLevel)
        {
            return provider.AllowsDataSourceSecurity(dataSourceSecurity, settingsManager)
                && provider.GetConfidenceLevel(settingsManager).AllowsDataSourceConfidenceLevel(requiredConfidenceLevel);
        }

        public bool AllowsDataSourceSecurity(DataSourceSecurity dataSourceSecurity, SettingsManager settingsManager)
            => provider.IsTrustedForDataSourceSecurityChecks(settingsManager).AllowsDataSourceSecurity(dataSourceSecurity);

        public bool IsTrustedByConfiguration(SettingsManager settingsManager) => IsTrustedProviderId(provider.Id, settingsManager);
    }

    extension(EmbeddingProvider provider)
    {
        public bool IsTrustedForDataSourceSecurityChecks(SettingsManager settingsManager)
        {
            if (provider == EmbeddingProvider.NONE)
                return false;

            return provider.IsSelfHosted || provider.IsTrustedByConfiguration(settingsManager);
        }

        public ConfidenceLevel GetConfidenceLevel(SettingsManager settingsManager)
        {
            if (provider == EmbeddingProvider.NONE)
                return ConfidenceLevel.NONE;

            return provider.UsedLLMProvider.GetConfidence(settingsManager).Level;
        }

        public bool IsTrustedByConfiguration(SettingsManager settingsManager) => IsTrustedProviderId(provider.Id, settingsManager);
    }

    extension(TranscriptionProvider provider)
    {
        public bool IsTrustedForDataSourceSecurityChecks(SettingsManager settingsManager)
        {
            if (provider == TranscriptionProvider.NONE)
                return false;

            return provider.IsSelfHosted || provider.IsTrustedByConfiguration(settingsManager);
        }

        public ConfidenceLevel GetConfidenceLevel(SettingsManager settingsManager)
        {
            if (provider == TranscriptionProvider.NONE)
                return ConfidenceLevel.NONE;

            return provider.UsedLLMProvider.GetConfidence(settingsManager).Level;
        }

        public bool IsTrustedByConfiguration(SettingsManager settingsManager) => IsTrustedProviderId(provider.Id, settingsManager);
    }

    extension(IProvider provider)
    {
        public bool IsTrustedForDataSourceSecurityChecks(SettingsManager settingsManager)
        {
            if (provider is NoProvider)
                return false;

            return provider.Provider is LLMProviders.SELF_HOSTED || IsTrustedProviderId(provider.ConfiguredProviderId, settingsManager);
        }

        public ConfidenceLevel GetConfidenceLevel(SettingsManager settingsManager)
        {
            if (provider is NoProvider)
                return ConfidenceLevel.NONE;

            return provider.Provider.GetConfidence(settingsManager).Level;
        }

        public bool AllowsDataSourceAccess(SettingsManager settingsManager, DataSourceSecurity dataSourceSecurity, ConfidenceLevel requiredConfidenceLevel)
        {
            return provider.AllowsDataSourceSecurity(dataSourceSecurity, settingsManager)
                && provider.GetConfidenceLevel(settingsManager).AllowsDataSourceConfidenceLevel(requiredConfidenceLevel);
        }

        public bool AllowsDataSourceSecurity(DataSourceSecurity dataSourceSecurity, SettingsManager settingsManager)
            => provider.IsTrustedForDataSourceSecurityChecks(settingsManager).AllowsDataSourceSecurity(dataSourceSecurity);
    }

    extension(bool usingTrustedProvider)
    {
        public bool AllowsDataSourceSecurity(DataSourceSecurity dataSourceSecurity) => dataSourceSecurity switch
        {
            DataSourceSecurity.ALLOW_ANY => true,
            DataSourceSecurity.SELF_HOSTED => usingTrustedProvider,
            _ => false,
        };
    }

    extension(ConfidenceLevel providerConfidenceLevel)
    {
        public bool AllowsDataSourceConfidenceLevel(ConfidenceLevel requiredConfidenceLevel)
        {
            if (requiredConfidenceLevel is ConfidenceLevel.NONE)
                return true;

            return providerConfidenceLevel >= requiredConfidenceLevel;
        }

        /// <summary>
        /// Whether a provider of this confidence level may see the content of a mailbox.
        /// </summary>
        /// <remarks>
        /// Unlike AllowsDataSourceConfidenceLevel, this closes a mailbox without an allowed level to every
        /// provider, whether it was never chosen or edited by hand in the settings file. Every provider
        /// which sees mail content has to pass it: the chat provider, the embedding provider, and the
        /// provider of every agent reading mails.
        /// </remarks>
        public bool AllowsMailboxConfidenceLevel(ConfidenceLevel mailboxConfidenceLevel)
        {
            if (!mailboxConfidenceLevel.IsAllowedMailboxConfidence)
                return false;

            return providerConfidenceLevel >= mailboxConfidenceLevel;
        }
    }

    extension(ConfidenceLevel confidenceLevel)
    {
        /// <summary>
        /// Whether a mailbox may require this provider confidence level.
        /// </summary>
        /// <remarks>
        /// Only the levels from very low to high qualify. NONE lets every provider through, cf.
        /// AllowsDataSourceConfidenceLevel, and UNTRUSTED and UNKNOWN do almost the same: both lie at or
        /// below the level of a provider nobody rated, so even such a provider would meet them.
        /// </remarks>
        public bool IsAllowedMailboxConfidence => confidenceLevel is ConfidenceLevel.VERY_LOW or ConfidenceLevel.LOW or ConfidenceLevel.MODERATE or ConfidenceLevel.MEDIUM or ConfidenceLevel.HIGH;
    }

    extension(IEnumerable<IDataSource> dataSources)
    {
        public ConfidenceLevel GetRequiredConfidenceLevel()
        {
            var requiredConfidenceLevel = ConfidenceLevel.NONE;
            foreach (var dataSource in dataSources.OfType<IInternalDataSource>())
                if (dataSource.ConfidenceLevel > requiredConfidenceLevel)
                    requiredConfidenceLevel = dataSource.ConfidenceLevel;

            return requiredConfidenceLevel;
        }

        public DataSourceSecurity GetRequiredSecurityPolicy()
        {
            var requiredSecurityPolicy = DataSourceSecurity.ALLOW_ANY;
            foreach (var dataSource in dataSources.OfType<IExternalDataSource>())
            {
                if (dataSource.SecurityPolicy is DataSourceSecurity.NOT_SPECIFIED)
                    return DataSourceSecurity.NOT_SPECIFIED;

                if (dataSource.SecurityPolicy is DataSourceSecurity.SELF_HOSTED)
                    requiredSecurityPolicy = DataSourceSecurity.SELF_HOSTED;
            }

            return requiredSecurityPolicy;
        }
    }

    private static bool IsTrustedProviderId(string providerId, SettingsManager settingsManager)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            return false;

        return settingsManager.ConfigurationData.DataSourceSecurity.TrustedProviderIds.Any(id => string.Equals(id, providerId, StringComparison.OrdinalIgnoreCase));
    }
}
