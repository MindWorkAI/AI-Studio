using System.Diagnostics.CodeAnalysis;

using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tools.Services;

internal static class DataSourceEmbeddingProviders
{
    public static bool TryResolve(SettingsManager settingsManager, IDataSource dataSource, [NotNullWhen(true)] out EmbeddingProvider? embeddingProvider)
    {
        embeddingProvider = settingsManager.ConfigurationData.EmbeddingProviders.FirstOrDefault(provider =>
            dataSource is IInternalDataSource internalDataSource &&
            provider.Id.Equals(internalDataSource.EmbeddingId, StringComparison.OrdinalIgnoreCase));

        return embeddingProvider != default && embeddingProvider.UsedLLMProvider is not LLMProviders.NONE;
    }
}
