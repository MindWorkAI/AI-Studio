using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.Services;

public class TemporaryChatService(SettingsManager settingsManager) : BackgroundService
{
    private static readonly TimeSpan CHECK_INTERVAL = TimeSpan.FromDays(1);
    private static bool IS_INITIALIZED;

    #region Overrides of BackgroundService

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && !IS_INITIALIZED)
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        await settingsManager.LoadSettings();
        if(settingsManager.ConfigurationData.Workspace.StorageTemporaryMaintenancePolicy is WorkspaceStorageTemporaryMaintenancePolicy.NO_AUTOMATIC_MAINTENANCE)
        {
            Console.WriteLine("Automatic maintenance of temporary chat storage is disabled. Exiting maintenance service.");
            return;
        }

        await this.StartMaintenance();
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CHECK_INTERVAL, stoppingToken);
            await this.StartMaintenance();
        }
    }

    #endregion

    private Task StartMaintenance()
    {
        var temporaryDirectories = Path.Join(SettingsManager.DataDirectory, "tempChats");
        if(!Directory.Exists(temporaryDirectories))
            return Task.CompletedTask;
        
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
                
                WorkspaceStorageTemporaryMaintenancePolicy.NO_AUTOMATIC_MAINTENANCE => false,
                _ => false,
            };
            
            if(deleteChat)
                Directory.Delete(tempChatDirPath, true);
        }

        return Task.CompletedTask;
    }
    
    public static void Initialize()
    {
        IS_INITIALIZED = true;
    }
}