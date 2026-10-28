using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Chat;

public static class ChatRestrictionsExtensions
{
    /// <summary>
    /// Checks if the specified provider may see content with these restrictions.
    /// </summary>
    /// <remarks>
    /// A provider the organization prohibited is never allowed, whatever the restrictions.<br/><br/>
    ///
    /// We don't check if the provider is allowed to use the data sources the content came from.
    /// That kind of check is done when the available data sources are resolved. What counts here is
    /// what the content demands since it holds data of them, see ChatThreadExtensions.IsLLMProviderAllowed.
    /// </remarks>
    /// <param name="restrictions">What the content demands.</param>
    /// <param name="provider">The provider to check.</param>
    /// <returns>True, when the provider may see the content. False, otherwise.</returns>
    public static bool IsLLMProviderAllowed<T>(this ChatRestrictions restrictions, T provider)
    {
        var settingsManager = Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();

        // A provider the organization prohibited is allowed nowhere, not even for content which demands nothing:
        if (settingsManager.IsProviderProhibited(GetLLMProvider(provider)))
            return false;

        var providerConfidence = provider switch
        {
            IProvider p => p.GetConfidenceLevel(settingsManager),
            AIStudio.Settings.Provider p => p.GetConfidenceLevel(settingsManager),

            _ => ConfidenceLevel.UNKNOWN,
        };

        //
        // The confidence axis is checked on its own: a provider trusted by configuration counts as
        // self-hosted for data-source security, which is the check further down, but that trust
        // says nothing about how confidential the provider is. An organization which wants its
        // contractually covered cloud provider to pass here raises its level through the custom
        // confidence scheme instead.
        //
        if (providerConfidence < restrictions.ProviderConfidence)
            return false;

        // The data security is not specified. Means, the content never came from RAG, or RAG was
        // enabled, but no data sources were selected. That's fine:
        if (restrictions.DataSecurity is DataSourceSecurity.NOT_SPECIFIED)
            return true;

        //
        // Is the provider trusted for data-source security checks?
        //
        var isTrustedProvider = provider switch
        {
            IProvider p => p.IsTrustedForDataSourceSecurityChecks(settingsManager),
            AIStudio.Settings.Provider p => p.IsTrustedForDataSourceSecurityChecks(settingsManager),

            _ => false,
        };

        //
        // Check the data security of the content against the selected provider:
        //
        return isTrustedProvider switch
        {
            // The provider is trusted -- it may see data of any data source:
            true => true,

            // The provider is not trusted -- it depends on the data security of the content:
            false => restrictions.DataSecurity is not DataSourceSecurity.SELF_HOSTED,
        };
    }

    private static LLMProviders GetLLMProvider<T>(T provider) => provider switch
    {
        IProvider p => p.Provider,
        AIStudio.Settings.Provider p => p.UsedLLMProvider,

        _ => LLMProviders.NONE,
    };
}
