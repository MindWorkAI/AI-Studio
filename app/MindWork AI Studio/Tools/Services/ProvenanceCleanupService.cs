using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.ProvenanceStore;

namespace AIStudio.Tools.Services;

/// <summary>
/// One-shot startup service that removes from the provenance store what no stored chat refers to, see ProvenanceCleanup.
/// </summary>
public sealed class ProvenanceCleanupService(DatabaseClientProvider databaseClientProvider, ILogger<ProvenanceCleanupService> logger) : BackgroundService
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

        try
        {
            var store = await databaseClientProvider.GetProvenanceStoreAsync(stoppingToken);
            if (store is NoProvenanceStoreClient)
            {
                logger.LogInformation("The provenance store is not available, so its startup cleanup has nothing to do.");
                return;
            }

            //
            // SQLite works synchronously behind its async methods, and every chat with entries is read
            // once, so the whole pass runs on a thread of its own:
            //
            var (orphaned, unreferenced) = await Task.Run(() => ProvenanceCleanup.CleanUpAsync(
                store,
                WorkspaceBehaviour.ReadAllChatLocations(),
                (workspaceId, chatId) => WorkspaceBehaviour.LoadChatAsync(new(workspaceId, chatId)),
                this.startedAt,
                stoppingToken), stoppingToken);

            logger.LogInformation("The provenance startup cleanup removed {OrphanedCount} entries of chats which no longer exist and {UnreferencedCount} entries their chats no longer refer to.", orphaned, unreferenced);
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