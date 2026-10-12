using AIStudio.Tools.Databases.IndexStore;

using Microsoft.AspNetCore.Components;

using static AIStudio.Tools.Services.Indexing.IndexingLogFormat;

namespace AIStudio.Tools.Services;

/// <remarks>
/// Changes to the embedding settings which do not pass through the queue: the configuration of an
/// organization, which may change an embedding provider whenever its plugin is applied, and the
/// confidence level of a provider, which is set for all of its uses at once.
/// </remarks>
public sealed partial class DataSourceEmbeddingService
{
    #region Implementation of IMessageBusReceiver

    public Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
    {
        if (triggeredEvent is not (Event.PLUGINS_RELOADED or Event.CONFIGURATION_CHANGED))
            return Task.CompletedTask;

        //
        // Not awaited: the plugins start, and the settings save, while this may wait for the index
        // database.
        //
        var queueIdleDataSources = triggeredEvent is Event.PLUGINS_RELOADED;
        _ = Task.Run(async () =>
        {
            try
            {
                await this.StopOutdatedRunsAsync();
                if (queueIdleDataSources)
                    await this.QueueOutdatedIdleDataSourcesAsync();
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to check the data sources against the changed configuration.");
            }
        });

        return Task.CompletedTask;
    }

    public Task<MessageBusAnswer<TResult>> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data) => Task.FromResult(MessageBusAnswer<TResult>.NONE);

    #endregion

    /// <summary>
    /// Stops every active run which works for embedding settings which no longer apply.
    /// </summary>
    /// <remarks>
    /// Reads no database, because it runs on every change of a setting anywhere in the app. A run
    /// which has not recorded its signature yet checks for itself once it does.
    /// </remarks>
    private async Task StopOutdatedRunsAsync()
    {
        foreach (var (dataSourceId, activeRun) in this.activeRuns)
        {
            // Stopping already, e.g. because the change was saved through the queue:
            if (activeRun.TokenSource.IsCancellationRequested)
                continue;

            DataSourceRunSignature? runSignature;
            lock (this.runSignatureLock)
                runSignature = activeRun.RunSignature;

            if (IsRunOutdated(runSignature, this.GetCurrentRunSignature(dataSourceId)))
                await this.StopOutdatedDataSourceRunAsync(dataSourceId);
        }
    }

    /// <summary>
    /// Queues every data source at rest whose stored index belongs to embedding settings which no longer apply.
    /// </summary>
    /// <remarks>
    /// Such a data source stays out of the selection until its index is built anew, cf.
    /// IsAwaitingReindexAsync, which so far happened only once one of its documents changed or the
    /// app started again. The rebuild is the one the next run would do anyway, decided by the same
    /// signature. It only comes sooner, and only while the data sources refresh on their own.
    ///
    /// A data source with a run, or with one queued, is left alone: StopOutdatedRunsAsync takes care
    /// of the run, and a queued run reads the current settings when it starts. The same holds for the
    /// data sources the startup check has yet to reach, which wait as queued as well.
    /// </remarks>
    private async Task QueueOutdatedIdleDataSourcesAsync()
    {
        if (!settingsManager.ConfigurationData.App.DataSourceIndexing.AutomaticRefresh)
            return;

        // The startup check has yet to come, and it reads the current settings for every data source:
        if (Volatile.Read(ref this.startupHashCheckStarted) == 0)
            return;

        IndexStoreClient? indexStore = null;
        foreach (var dataSource in this.GetConfiguredIndexedSources())
        {
            if (this.activeRuns.ContainsKey(dataSource.Id))
                continue;

            if (this.statuses.TryGetValue(dataSource.Id, out var status) && status.State is DataSourceEmbeddingState.QUEUED or DataSourceEmbeddingState.RUNNING)
                continue;

            // No run would start, and its status says why already:
            var currentRunSignature = this.GetCurrentRunSignature(dataSource.Id);
            if (currentRunSignature == DataSourceRunSignature.NONE)
                continue;

            indexStore ??= await databaseClientProvider.GetIndexStoreAsync(CancellationToken.None);
            if (!indexStore.IsAvailable)
                return;

            var indexState = await indexStore.GetDataSourceStateAsync(dataSource.Id, CancellationToken.None);
            if (string.Equals(indexState?.EmbeddingSignature, currentRunSignature.EmbeddingSignature, StringComparison.Ordinal))
                continue;

            logger.LogInformation(
                "Queueing data source '{DataSourceName}' ({DataSourceId}) because the changed configuration changed its embedding settings. StoredSignaturePrefix={StoredSignaturePrefix}, CurrentSignaturePrefix={CurrentSignaturePrefix}.",
                dataSource.Name,
                dataSource.Id,
                ShortHash(indexState?.EmbeddingSignature ?? string.Empty),
                ShortHash(currentRunSignature.EmbeddingSignature));

            await this.QueueDataSourceAsync(dataSource);
        }
    }
}