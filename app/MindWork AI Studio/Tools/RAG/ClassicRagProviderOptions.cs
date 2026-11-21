using AIStudio.AIOperations;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.RAG;

/// <summary>
/// What the classic RAG process adds to the choice of a provider for its AI operations.
/// </summary>
/// <remarks>
/// The data source selection and the validation of retrieval contexts run with the same fallbacks, and
/// DataSourceService asks with them in advance which providers would see the data. All three build their
/// options here, so that the providers a check counts are the ones which run.
/// </remarks>
public static class ClassicRagProviderOptions
{
    /// <summary>
    /// Builds the options for an AI operation of the classic RAG process.
    /// </summary>
    /// <remarks>
    /// Without a provider of its own, an operation falls back on the provider of the chat, and then on the
    /// app-wide default. Its minimum confidence applies to both of them.
    /// </remarks>
    /// <param name="settingsManager">The settings, for the app-wide default.</param>
    /// <param name="chatProviderId">The ID of the provider of the chat.</param>
    /// <param name="checkProvider">An optional check of the caller, e.g., whether a provider may see the data sources.</param>
    /// <returns>The options for the provider resolver.</returns>
    public static AIOperationProviderOptions For(SettingsManager settingsManager, string chatProviderId, Func<AIStudio.Settings.Provider, AIOperationProviderIssue>? checkProvider = null) => new()
    {
        FallbackProviderIds = [chatProviderId, settingsManager.AppDefaultProviderId],
        OwnMinimumConfidenceAppliesToFallbacks = true,
        CheckProvider = checkProvider,
    };

    /// <summary>
    /// Builds the options for an AI operation of the classic RAG process which reads the data of data sources.
    /// </summary>
    /// <remarks>
    /// Besides the fallbacks, every candidate has to be allowed to see the data, by the security and the
    /// confidence it requires. A candidate which is not reports NOT_ALLOWED_FOR_CONTENT.
    /// </remarks>
    /// <param name="settingsManager">The settings, for the app-wide default.</param>
    /// <param name="chatProviderId">The ID of the provider of the chat.</param>
    /// <param name="requiredDataSecurity">The data security the data requires.</param>
    /// <param name="requiredConfidenceLevel">The provider confidence the data requires.</param>
    /// <returns>The options for the provider resolver.</returns>
    public static AIOperationProviderOptions ForDataAccess(SettingsManager settingsManager, string chatProviderId, DataSourceSecurity requiredDataSecurity, ConfidenceLevel requiredConfidenceLevel) => For(settingsManager, chatProviderId, candidate => candidate.AllowsDataSourceAccess(settingsManager, requiredDataSecurity, requiredConfidenceLevel)
        ? AIOperationProviderIssue.NONE
        : AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT);
}