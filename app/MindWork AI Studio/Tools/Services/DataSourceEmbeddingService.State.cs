using AIStudio.Settings.DataModel;
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
        if (!this.TryGetConfiguredIndexedSource(dataSourceId, out var dataSource))
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
        PublishStatusChanged();

        await this.QueueDataSourceAsync(dataSource, true, DataSourceEmbeddingRefreshMode.MANUAL_RETRY);
    }

    /// <summary>
    /// Records that the user agreed to the removal a mailbox held back, and syncs the mailbox to carry it out.
    /// </summary>
    /// <remarks>
    /// The count is the one the status showed the user, cf. DataSourceEmbeddingStatus.PendingRemovalCount.
    /// The agreement holds for exactly that number: should a sync have come up with another one in the
    /// meantime, nothing is agreed to, and the user is asked anew. The sync which carries the removal
    /// out signs in like any other, so a recorded sign-in failure still keeps it from the server.
    /// </remarks>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="removalCount">How many mails the user agreed to remove from the index.</param>
    /// <returns>True when the agreement was recorded. False when the number changed in the meantime, or the index cannot be reached.</returns>
    public async Task<bool> ApprovePendingMailRemovalAsync(string dataSourceId, int removalCount)
    {
        if (!this.TryGetConfiguredIndexedSource(dataSourceId, out var dataSource) || dataSource is not DataSourceMailbox)
            return false;

        var indexStore = await databaseClientProvider.GetIndexStoreAsync(CancellationToken.None);
        if (!await indexStore.ApprovePendingMailRemovalAsync(dataSourceId, removalCount, CancellationToken.None))
        {
            logger.LogInformation("The removal held back for mailbox '{DataSourceId}' was not approved, since another number is held back by now or the index store is unavailable.", dataSourceId);
            return false;
        }

        logger.LogInformation("The user approved removing {RemovalCount} mails from the index of mailbox '{DataSourceId}'.", removalCount, dataSourceId);
        await this.QueueDataSourceAsync(dataSource, true, DataSourceEmbeddingRefreshMode.HASH_CHECK);
        return true;
    }

    /// <summary>
    /// Deletes what the index keeps of mailboxes which are no longer configured.
    /// </summary>
    /// <remarks>
    /// Such leftovers come about in two ways. An older version of AI Studio does not know mailboxes
    /// and saves the settings without them. Or the stores could not be reached while the user
    /// deleted a mailbox. Either way, nothing would ever delete the index then, nor a refused
    /// sign-in kept next to it.
    ///
    /// Only with settings which were loaded and may be written. Settings which could not be read
    /// look like settings without a single mailbox, and every index would be deleted for that. Both
    /// stores have to be there as well: the row in the index store is all that leads to the
    /// collection in the vector store, so deleting the one without the other would leave the other
    /// behind for good.
    ///
    /// Runs before the first run, so none of the mailboxes is in use. The password stays in the OS
    /// keyring: its entry carries the name of the mailbox, which nothing here knows anymore.
    /// </remarks>
    /// <param name="token">The cancellation token.</param>
    private async Task DeleteOrphanedMailboxIndexesAsync(CancellationToken token)
    {
        if (!settingsManager.HasCompletedInitialSettingsLoad || settingsManager.SettingsWriteBlocked)
        {
            logger.LogInformation("Leaving the indexes of mailboxes alone, since the settings could not be loaded completely. SettingsWriteBlockReason={SettingsWriteBlockReason}.", settingsManager.SettingsWriteBlockReason);
            return;
        }

        try
        {
            var vectorStore = await databaseClientProvider.GetVectorStoreAsync(token);
            var indexStore = await databaseClientProvider.GetIndexStoreAsync(token);
            if (!vectorStore.IsAvailable || !indexStore.IsAvailable)
                return;

            var configuredIds = settingsManager.ConfigurationData.Mailboxes
                .Select(mailbox => mailbox.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var mailboxId in await indexStore.GetStoredMailboxIdsAsync(token))
            {
                if (configuredIds.Contains(mailboxId))
                    continue;

                await this.ResetPersistedStateAsync(mailboxId, vectorStore, indexStore, token);
                await indexStore.ClearMailboxAuthFailureAsync(mailboxId, token);
                logger.LogInformation("Deleted the index of mailbox '{DataSourceId}', which is no longer configured.", mailboxId);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Nothing depends on it: whatever is left over is tried again on the next start.
            logger.LogWarning(exception, "Could not delete the indexes of mailboxes which are no longer configured.");
        }
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
