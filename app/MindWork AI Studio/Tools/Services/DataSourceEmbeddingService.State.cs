using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Databases.VectorStore;

namespace AIStudio.Tools.Services;

public sealed partial class DataSourceEmbeddingService
{
    private async Task ResetPersistedStateAsync(
        string dataSourceId,
        VectorStoreClient? vectorStore,
        IndexStoreClient? indexStore,
        CancellationToken token)
    {
        await this.DeleteCollectionAsync(DataSourceEmbeddingNames.GetCollectionName(dataSourceId), vectorStore, token);

        indexStore ??= await databaseClientProvider.GetIndexStoreAsync(token);
        if (!indexStore.IsAvailable)
        {
            logger.LogWarning("Could not delete local RAG embedding state for data source '{DataSourceId}' because the database '{DatabaseName}' is unavailable.", dataSourceId, indexStore.Name);
            return;
        }

        await indexStore.DeleteDataSourceAsync(dataSourceId, token);
        logger.LogInformation("Reset persisted local RAG embedding state for data source '{DataSourceId}'.", dataSourceId);
    }
}
