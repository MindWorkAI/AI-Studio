using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.Services;

/// <summary>
/// Deletes stored chats once they stayed unchanged longer than the user chose: the disappearing chats
/// outside of workspaces, and the chats in workspaces, each by a choice of their own.
/// </summary>
/// <remarks>
/// Which chats go is decided by DataWorkspace.GetDeletionPolicy: a chat the user saved on purpose stays,
/// and a duration applies to all chats of its place, however they were stored back then.
/// </remarks>
public sealed class TemporaryChatService(ILogger<TemporaryChatService> logger, SettingsManager settingsManager) : BackgroundService
{
    private static readonly TimeSpan CHECK_INTERVAL = TimeSpan.FromDays(1);
    private static bool IS_INITIALIZED;

    #region Overrides of BackgroundService

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && !IS_INITIALIZED)
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        logger.LogInformation("The chat maintenance service was initialized.");

        //
        // The service keeps running while no chat is due for deletion: the user may choose a duration at
        // any time, and the next maintenance takes it into account.
        //
        while (!stoppingToken.IsCancellationRequested)
        {
            await this.StartMaintenanceAsync();
            await Task.Delay(CHECK_INTERVAL, stoppingToken);
        }
    }

    #endregion

    internal async Task StartMaintenanceAsync()
    {
        var workspace = settingsManager.ConfigurationData.Workspace;
        await this.DeleteExpiredChatsAsync(isInWorkspace: false, workspace.GetDeletionPolicy(isInWorkspace: false));
        await this.DeleteExpiredChatsAsync(isInWorkspace: true, workspace.GetDeletionPolicy(isInWorkspace: true));
    }

    /// <summary>
    /// Deletes the chats of one place which stayed unchanged longer than its duration.
    /// </summary>
    /// <param name="isInWorkspace">Whether to look at the chats in workspaces, or at the disappearing chats.</param>
    /// <param name="policy">When the chats of the place are deleted.</param>
    private async Task DeleteExpiredChatsAsync(bool isInWorkspace, WorkspaceStorageTemporaryMaintenancePolicy policy)
    {
        if (GetMaximumAge(policy) is not { } maximumAge)
            return;

        var rootDirectory = Path.Join(SettingsManager.DataDirectory, isInWorkspace ? "workspaces" : "tempChats");
        if (!Directory.Exists(rootDirectory))
            return;

        logger.LogInformation("Starting maintenance of the {Place}.", isInWorkspace ? "chats in workspaces" : "disappearing chats");

        // The disappearing chats lie right in their directory, the chats of a workspace in its directory:
        var places = isInWorkspace ? Directory.EnumerateDirectories(rootDirectory).ToList() : [rootDirectory];
        foreach (var placeDirectory in places)
        {
            var workspaceId = Guid.Empty;
            if (isInWorkspace && !Guid.TryParse(Path.GetFileName(placeDirectory), out workspaceId))
                continue;

            foreach (var chatDirectory in Directory.EnumerateDirectories(placeDirectory).ToList())
            {
                var chatFile = new FileInfo(Path.Join(chatDirectory, "thread.json"));
                if (!chatFile.Exists || DateTime.UtcNow - chatFile.LastWriteTimeUtc <= maximumAge)
                    continue;

                logger.LogInformation("Deleting the chat directory '{Directory}', which stayed unchanged longer than the chosen duration.", chatDirectory);
                await this.DeleteChatAsync(workspaceId, chatDirectory);
            }
        }

        logger.LogInformation("Finished maintenance of the {Place}.", isInWorkspace ? "chats in workspaces" : "disappearing chats");
    }

    /// <summary>
    /// How long a chat may stay unchanged under a policy, or null when it stays until the user deletes it.
    /// </summary>
    private static TimeSpan? GetMaximumAge(WorkspaceStorageTemporaryMaintenancePolicy policy) => policy switch
    {
        WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_7_DAYS => TimeSpan.FromDays(7),
        WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_30_DAYS => TimeSpan.FromDays(30),
        WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_90_DAYS => TimeSpan.FromDays(90),
        WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_180_DAYS => TimeSpan.FromDays(180),
        WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_365_DAYS => TimeSpan.FromDays(365),

        _ => null,
    };

    /// <summary>
    /// Deletes one chat the way the user deletes a chat, so that what it received goes along.
    /// </summary>
    /// <remarks>
    /// A chat which cannot go now, e.g., since another operation holds it or a file of it is open,
    /// stays until the next maintenance. Its failure must not end this service, nor the app with it.
    /// </remarks>
    /// <param name="workspaceId">The workspace the chat lies in, or <see cref="Guid.Empty"/> for a disappearing chat.</param>
    /// <param name="chatDirectory">The directory of the chat.</param>
    private async Task DeleteChatAsync(Guid workspaceId, string chatDirectory)
    {
        try
        {
            if (Guid.TryParse(Path.GetFileName(chatDirectory), out var chatId))
            {
                if (!await WorkspaceBehaviour.DeleteChatWithoutAskingAsync(workspaceId, chatId))
                    logger.LogWarning("The chat '{ChatId}' is busy, so the next maintenance deletes it.", chatId);

                return;
            }

            // A directory in a workspace which is no chat of AI Studio stays, since nobody chose to delete it:
            if (workspaceId != Guid.Empty)
                return;

            // Not a chat AI Studio made, so nothing else knows about it:
            Directory.Delete(chatDirectory, true);
            WorkspaceBehaviour.InvalidateWorkspaceTreeCache();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not delete the chat directory '{Directory}'. The next maintenance tries again.", chatDirectory);
        }
    }

    public static void Initialize()
    {
        IS_INITIALIZED = true;
    }
}