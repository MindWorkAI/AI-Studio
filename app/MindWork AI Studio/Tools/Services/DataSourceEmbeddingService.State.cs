using AIStudio.Tools.Databases.EmbeddingState;
using AIStudio.Tools.Databases.VectorStore;

namespace AIStudio.Tools.Services;

public sealed partial class DataSourceEmbeddingService
{
    private async Task ResetPersistedStateAsync(
        string dataSourceName,
        string dataSourceId,
        VectorStoreClient? vectorStore,
        EmbeddingStateClient? embeddingState,
        CancellationToken token)
    {
        await this.DeleteCollectionAsync(this.GetCollectionName(dataSourceName, dataSourceId), vectorStore, token);

        embeddingState ??= await databaseClientProvider.GetEmbeddingStateAsync(token);
        if (!embeddingState.IsAvailable)
        {
            logger.LogWarning("Could not delete SQLite embedding state for data source '{DataSourceId}' because the embedding state database '{DatabaseName}' is unavailable.", dataSourceId, embeddingState.Name);
            return;
        }

        await embeddingState.DeleteDataSourceAsync(dataSourceId, token);
        logger.LogInformation("Reset persisted embedding state for data source '{DataSourceId}'.", dataSourceId);
    }
}
