using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools.DataBackup;
using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.ProvenanceStore;

namespace AIStudio.Tools.Services;

/// <summary>
/// One-shot startup service that removes from the provenance store what no stored chat refers to, see ProvenanceCleanup.
/// </summary>
public sealed class ProvenanceCleanupService(DatabaseClientProvider databaseClientProvider, DataRestoreService dataRestoreService, ILogger<ProvenanceCleanupService> logger) : BackgroundService
{
    /// <summary>
    /// When this run of the app started. Entries stored since belong to it and stay.
    /// </summary>
    private readonly DateTimeOffset startedAt = DateTimeOffset.UtcNow;

    /// <summary>Waits for the data directory, performs one cleanup pass, and then exits.</summary>
    /// <param name="stoppingToken">Host shutdown token.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (string.IsNullOrWhiteSpace(SettingsManager.DataDirectory) && !stoppingToken.IsCancellationRequested)
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

        if (stoppingToken.IsCancellationRequested)
            return;

        //
        // A restore which the runtime could not finish leaves chats and store of two different states
        // in the data folder, while the chats it replaced wait elsewhere to be moved back. Nothing
        // tells then which chat refers to what:
        //
        if (SettingsManager.DataDirectory is { } dataDirectory && DataRestoreFollowUp.IsRestoreUnfinished(dataDirectory))
        {
            logger.LogWarning("A restore is not finished yet, so the provenance startup cleanup waits for a later start.");
            return;
        }

        try
        {
            var store = await databaseClientProvider.GetProvenanceStoreAsync(stoppingToken);
            if (store is NoProvenanceStoreClient)
            {
                logger.LogInformation("The provenance store is not available, so its startup cleanup has nothing to do.");
                return;
            }

            //
            // A restore at this start may have brought back the chats of one moment and kept the store
            // of another, e.g., from a backup without the store. Then the time of the last cleanup
            // tells nothing about the chats, and every chat is read:
            //
            await dataRestoreService.StartupStateLoaded.WaitAsync(stoppingToken);
            var lastCleanup = dataRestoreService.Report is null ? await store.GetLastCleanupAsync(stoppingToken) : null;

            //
            // SQLite works synchronously behind its async methods, and every changed chat with entries
            // is read once, so the whole pass runs on a thread of its own:
            //
            var result = await Task.Run(() => ProvenanceCleanup.CleanUpAsync(
                store,
                WorkspaceBehaviour.ReadAllChatLocations(),
                (workspaceId, chatId) => WorkspaceBehaviour.LoadChatAsync(new(workspaceId, chatId)),
                WorkspaceBehaviour.GetChatWrittenAt,
                this.startedAt,
                lastCleanup,
                stoppingToken), stoppingToken);

            logger.LogInformation("The provenance startup cleanup removed {OrphanedCount} entries of chats which no longer exist and {UnreferencedCount} entries their chats no longer refer to. It skipped {SkippedCount} unchanged chats.", result.Orphaned, result.Unreferenced, result.Skipped);

            // A chat which could not be read is read again at the next start, as long as the time of this cleanup is not noted:
            if (result.Unreadable is 0)
                await store.SetLastCleanupAsync(this.startedAt, stoppingToken);
            else
                logger.LogWarning("The provenance startup cleanup could not read {UnreadableCount} chats, which keep all their entries. The next start reads them again.", result.Unreadable);
        }
        catch (OperationCanceledException)
        {
            // The app is shutting down; the next start cleans up.
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The provenance startup cleanup failed. The next start tries again.");
        }
    }
}