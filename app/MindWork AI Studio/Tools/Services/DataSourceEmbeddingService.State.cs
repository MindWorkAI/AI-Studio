using AIStudio.Tools.Databases.EmbeddingState;
using AIStudio.Tools.Databases.VectorStore;

namespace AIStudio.Tools.Services;

public sealed partial class DataSourceEmbeddingService
{
    private async Task ResetPersistedStateAsync(
        string dataSourceId,
        VectorStoreClient? vectorStore,
        EmbeddingStateClient? embeddingState,
        CancellationToken token)
    {
        await this.DeleteCollectionAsync(this.GetCollectionName(dataSourceId), vectorStore, token);

        embeddingState ??= await databaseClientProvider.GetEmbeddingStateAsync(token);
        if (!embeddingState.IsAvailable)
        {
            logger.LogWarning("Could not delete local RAG index state for data source '{DataSourceId}' because the database '{DatabaseName}' is unavailable.", dataSourceId, embeddingState.Name);
            return;
        }

        await embeddingState.DeleteDataSourceAsync(dataSourceId, token);
        logger.LogInformation("Reset persisted local RAG index state for data source '{DataSourceId}'.", dataSourceId);
    }
}
