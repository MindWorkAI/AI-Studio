using System.Diagnostics.CodeAnalysis;

using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tools.Services;

internal static class DataSourceEmbeddingProviders
{
    /// <summary>
    /// Finds the embedding provider of a data source, when AI Studio may use it.
    /// </summary>
    /// <remarks>
    /// A provider the organization prohibited counts as missing: nothing may be embedded with it,
    /// neither the documents of the data source nor a query against its index.
    /// </remarks>
    public static bool TryResolve(SettingsManager settingsManager, IDataSourceBase dataSource, [NotNullWhen(true)] out EmbeddingProvider? embeddingProvider)
    {
        embeddingProvider = Find(settingsManager, dataSource);
        return embeddingProvider is { UsedLLMProvider: not LLMProviders.NONE } && !settingsManager.IsProviderProhibited(embeddingProvider.UsedLLMProvider);
    }

    /// <summary>
    /// Checks whether the organization prohibited the embedding provider of a data source.
    /// </summary>
    /// <remarks>
    /// TryResolve fails for such a provider as well, but without the reason. The status of a data
    /// source needs the reason to tell the user what to change.
    /// </remarks>
    public static bool IsProhibited(SettingsManager settingsManager, IDataSourceBase dataSource)
        => Find(settingsManager, dataSource) is { } embeddingProvider && settingsManager.IsProviderProhibited(embeddingProvider.UsedLLMProvider);

    private static EmbeddingProvider? Find(SettingsManager settingsManager, IDataSourceBase dataSource) => settingsManager.ConfigurationData.EmbeddingProviders.FirstOrDefault(provider =>
        dataSource is IIndexedDataSource indexedDataSource &&
        provider.Id.Equals(indexedDataSource.EmbeddingId, StringComparison.OrdinalIgnoreCase));
}