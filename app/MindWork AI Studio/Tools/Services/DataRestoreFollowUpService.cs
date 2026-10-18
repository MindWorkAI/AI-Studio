using AIStudio.Settings;

namespace AIStudio.Tools.Services;

/// <summary>
/// One-shot startup service which picks up what a restore or a backup left behind: it learns how the
/// runtime applied a restore, removes what a backup or a preparation left halfway, and removes the data
/// a restore replaced once the restored settings loaded.
/// </summary>
/// <remarks>
/// The report of the runtime waits in DataRestoreService until the main layout shows it, once the
/// plugins started: only then do the providers of the organization exist, and the open points name
/// none of them.
/// </remarks>
public sealed class DataRestoreFollowUpService(ILogger<DataRestoreFollowUpService> logger, SettingsManager settingsManager, DataRestoreService dataRestoreService) : BackgroundService
{
    #region Overrides of BackgroundService

    /// <summary>Waits for the folders and the settings, picks up what the last start left, and then exits.</summary>
    /// <param name="stoppingToken">Host shutdown token.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while ((string.IsNullOrWhiteSpace(SettingsManager.DataDirectory) || string.IsNullOrWhiteSpace(SettingsManager.ConfigDirectory)) && !stoppingToken.IsCancellationRequested)
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

        await dataRestoreService.LoadStartupStateAsync(stoppingToken);

        while (!settingsManager.HasCompletedInitialSettingsLoad && !stoppingToken.IsCancellationRequested)
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

        //
        // Settings which could not be loaded leave the replaced data in place: they hold the settings
        // which worked before the restore.
        //
        if (settingsManager.SettingsWriteBlocked)
        {
            logger.LogWarning("The settings could not be loaded safely. The data which a restore replaced stay where they are.");
            return;
        }

        dataRestoreService.RemoveReplacedData();
    }

    #endregion
}