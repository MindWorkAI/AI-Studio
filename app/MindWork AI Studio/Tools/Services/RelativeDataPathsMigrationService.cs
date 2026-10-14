using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

/// <summary>
/// One-shot startup service which stores the chats and the settings once more, so that every path
/// below the data directory in them becomes relative.
/// </summary>
/// <remarks>
/// Compatibility shim, see documentation/compatibility-shims/2026-10-relative-data-paths.md. Remove
/// after 2027-04-14.<br/><br/>
///
/// AI Studio stores such a path relative whenever it stores a chat or the settings. A chat which is
/// never stored again would keep its absolute paths, though, and a backup of the data directory
/// brings them to another computer, where they lead nowhere.
/// </remarks>
public sealed class RelativeDataPathsMigrationService(ILogger<RelativeDataPathsMigrationService> logger, SettingsManager settingsManager) : BackgroundService
{
    /// <summary>
    /// How long to wait for the plugins to load for the first time, before storing the settings anyway.
    /// </summary>
    private static readonly TimeSpan FIRST_PLUGIN_LOAD_TIMEOUT = TimeSpan.FromMinutes(2);

    #region Overrides of BackgroundService

    /// <summary>Waits for the settings, stores every chat and the settings once more, and then exits.</summary>
    /// <param name="stoppingToken">Host shutdown token.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!settingsManager.HasCompletedInitialSettingsLoad && !stoppingToken.IsCancellationRequested)
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

        if (settingsManager.ConfigurationData.StoredDataPathsAreRelative)
            return;

        try
        {
            if (!await WorkspaceBehaviour.StoreChatsWithRelativePathsAsync(stoppingToken))
            {
                logger.LogWarning("Not every chat could be stored with relative paths yet. The next start tries again.");
                return;
            }

            //
            // While the plugins load for the first time, they bring their providers and tokenizers into
            // the settings. Storing the settings meanwhile could meet a list in the middle of a change:
            //
            try
            {
                await PluginFactory.FirstLoadCompleted.WaitAsync(FIRST_PLUGIN_LOAD_TIMEOUT, stoppingToken);
            }
            catch (TimeoutException)
            {
                logger.LogWarning("The plugins did not finish loading within {Timeout} minutes. The settings are stored with relative paths anyway.", FIRST_PLUGIN_LOAD_TIMEOUT.TotalMinutes);
            }

            settingsManager.ConfigurationData.StoredDataPathsAreRelative = true;
            await settingsManager.StoreSettings();
            logger.LogInformation("Every chat and the settings are stored with paths relative to the data directory now.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Storing the chats and the settings with relative paths failed. The next start tries again.");
        }
    }

    #endregion
}