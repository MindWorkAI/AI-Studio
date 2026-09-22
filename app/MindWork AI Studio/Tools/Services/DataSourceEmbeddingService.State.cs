using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Databases.VectorStore;

namespace AIStudio.Tools.Services;

public sealed partial class DataSourceEmbeddingService
{
    /// <summary>
    /// Throws away everything stored for one data source and starts a fresh indexing run.
    /// </summary>
    /// <remarks>
    /// The one way out of an index which cannot be read, and nothing in the app takes it by itself:
    /// a rebuild sends every document of the data source to the embedding provider once more, which
    /// costs money with a cloud provider and hours with a large data source. It happens because the
    /// user asked for it, after being told both.
    ///
    /// An active run is stopped first, the same way deleting a data source does it. The repair is
    /// offered for a failed data source only, so there should be none -- but a file watcher may
    /// well have queued one between the click and this call, and discarding the index next to a
    /// live run would leave it half thrown away.
    /// </remarks>
    /// <param name="dataSourceId">The data source to build anew.</param>
    public async Task RepairDataSourceAsync(string dataSourceId)
    {
        if (!this.TryGetConfiguredDataSource(dataSourceId, out var dataSource) || !this.IsSupportedInternalDataSource(dataSource))
            return;

        logger.LogWarning(
            "Repairing data source '{DataSourceName}' ({DataSourceId}) on the user's request: the stored index is discarded and built anew.",
            dataSource.Name,
            dataSource.Id);

        var activeRun = this.CancelActiveDataSourceRun(dataSource);
        this.ClearQueuedDataSourceState(dataSourceId);
        if (activeRun is not null)
            await activeRun.Completion.Task;

        await this.ResetPersistedStateAsync(dataSourceId, null, null, CancellationToken.None);
        this.statuses.TryRemove(dataSourceId, out _);
        this.PublishStatusChanged();

        await this.QueueDataSourceAsync(dataSource, true, DataSourceEmbeddingRefreshMode.MANUAL_RETRY);
    }

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
