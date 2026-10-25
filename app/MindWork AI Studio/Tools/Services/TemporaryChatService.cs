using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.Services;

public sealed class TemporaryChatService(ILogger<TemporaryChatService> logger, SettingsManager settingsManager) : BackgroundService
{
    private static readonly TimeSpan CHECK_INTERVAL = TimeSpan.FromDays(1);
    private static bool IS_INITIALIZED;

    #region Overrides of BackgroundService

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && !IS_INITIALIZED)
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        logger.LogInformation("The temporary chat maintenance service was initialized.");
        
        if(settingsManager.ConfigurationData.Workspace.StorageTemporaryMaintenancePolicy is WorkspaceStorageTemporaryMaintenancePolicy.NO_AUTOMATIC_MAINTENANCE)
        {
            logger.LogWarning("Automatic maintenance of temporary chat storage is disabled. Exiting maintenance service.");
            return;
        }

        await this.StartMaintenanceAsync();
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CHECK_INTERVAL, stoppingToken);
            await this.StartMaintenanceAsync();
        }
    }

    #endregion

    internal async Task StartMaintenanceAsync()
    {
        logger.LogInformation("Starting maintenance of temporary chat storage.");
        var temporaryDirectories = Path.Join(SettingsManager.DataDirectory, "tempChats");
        if(!Directory.Exists(temporaryDirectories))
        {
            logger.LogWarning("Temporary chat storage directory does not exist. End maintenance.");
            return;
        }

        foreach (var tempChatDirPath in Directory.EnumerateDirectories(temporaryDirectories))
        {
            var chatPath = Path.Join(tempChatDirPath, "thread.json");
            var chatMetadata = new FileInfo(chatPath);
            if (!chatMetadata.Exists)
                continue;

            var lastWriteTime = chatMetadata.LastWriteTimeUtc;
            var deleteChat = settingsManager.ConfigurationData.Workspace.StorageTemporaryMaintenancePolicy switch
            {
                WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_7_DAYS => DateTime.UtcNow - lastWriteTime > TimeSpan.FromDays(7),
                WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_30_DAYS => DateTime.UtcNow - lastWriteTime > TimeSpan.FromDays(30),
                WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_90_DAYS => DateTime.UtcNow - lastWriteTime > TimeSpan.FromDays(90),
                WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_180_DAYS => DateTime.UtcNow - lastWriteTime > TimeSpan.FromDays(180),
                WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_365_DAYS => DateTime.UtcNow - lastWriteTime > TimeSpan.FromDays(365),
                
                _ => false,
            };
            
            if(deleteChat)
            {
                logger.LogInformation($"Deleting temporary chat storage directory '{tempChatDirPath}' due to maintenance policy.");
                await this.DeleteTemporaryChatAsync(tempChatDirPath);
            }
        }

        logger.LogInformation("Finished maintenance of temporary chat storage.");
    }

    /// <summary>
    /// Deletes one temporary chat the way the user deletes a chat, so that what it received goes along.
    /// </summary>
    /// <remarks>
    /// A chat which cannot go now, e.g., since another operation holds it or a file of it is open,
    /// stays until the next maintenance. Its failure must not end this service, nor the app with it.
    /// </remarks>
    /// <param name="tempChatDirPath">The directory of the chat.</param>
    private async Task DeleteTemporaryChatAsync(string tempChatDirPath)
    {
        try
        {
            if (Guid.TryParse(Path.GetFileName(tempChatDirPath), out var chatId))
            {
                if (!await WorkspaceBehaviour.DeleteChatWithoutAskingAsync(Guid.Empty, chatId))
                    logger.LogWarning("The temporary chat '{ChatId}' is busy, so the next maintenance deletes it.", chatId);

                return;
            }

            // Not a chat AI Studio made, so nothing else knows about it:
            Directory.Delete(tempChatDirPath, true);
            WorkspaceBehaviour.InvalidateWorkspaceTreeCache();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not delete the temporary chat storage directory '{Directory}'. The next maintenance tries again.", tempChatDirPath);
        }
    }
    
    public static void Initialize()
    {
        IS_INITIALIZED = true;
    }
}