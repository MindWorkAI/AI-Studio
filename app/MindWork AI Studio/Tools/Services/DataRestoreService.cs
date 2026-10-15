using System.Reflection;

using AIStudio.Settings;
using AIStudio.Tools.AIJobs;
using AIStudio.Tools.DataBackup;
using AIStudio.Tools.Media;
using AIStudio.Tools.Metadata;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

/// <summary>
/// Prepares the restore of a backup, which the next start of AI Studio applies.
/// </summary>
/// <remarks>
/// A restore replaces all data, so it runs only while nothing else changes it: no answer, no
/// transcript, and no backup may be running, and the indexing pauses from the start of the preparation
/// until the restart. Whatever changes after the preparation is lost with the restart, which is why
/// the app asks for the restart right away, and offers to discard the restore instead.<br/><br/>
///
/// The service enforces what the organization allows on its own, since a component may show an
/// outdated state.
/// </remarks>
public sealed class DataRestoreService(ILogger<DataRestoreService> logger, SettingsManager settingsManager, DataSourceEmbeddingService embeddingService, DataBackupService dataBackupService, AIJobService aiJobService, MediaTranscriptionService mediaTranscriptionService)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataRestoreService).Namespace, nameof(DataRestoreService));

    private readonly Lock stateLock = new();

    private CancellationTokenSource? preparing;

    /// <summary>
    /// Holds the indexing back while a restore is prepared or waits for the restart.
    /// </summary>
    private IAsyncDisposable? indexingPause;

    /// <summary>
    /// Fires whenever the state changes: a preparation starts, makes progress, or ends, or a prepared restore is discarded.
    /// </summary>
    /// <remarks>
    /// It fires on whatever thread the preparation runs. A component hands it over to its own context.
    /// </remarks>
    public event Action? StateChanged;

    /// <summary>
    /// Whether a restore is being prepared.
    /// </summary>
    public bool IsPreparing
    {
        get
        {
            lock (this.stateLock)
                return this.preparing is not null;
        }
    }

    /// <summary>
    /// Whether a restore is prepared and waits for the restart.
    /// </summary>
    public bool IsRestorePending { get; private set; }

    /// <summary>
    /// How far the running preparation got.
    /// </summary>
    public DataRestoreProgress Progress { get; private set; }

    /// <summary>
    /// How the last preparation ended, or null when none ended yet.
    /// </summary>
    public DataRestoreResult? LastResult { get; private set; }

    /// <summary>
    /// Prepares the restore of a backup for the next start.
    /// </summary>
    /// <param name="archivePath">The backup.</param>
    /// <returns>Whether the restore is prepared, or why not.</returns>
    public async Task<DataRestoreResult> PrepareRestoreAsync(string archivePath)
    {
        if (!settingsManager.ConfigurationData.App.AllowUserToRestoreData)
            return this.Refuse(TB("Your organization does not allow restoring backups."));

        if (dataBackupService.IsRunning)
            return this.Refuse(TB("A backup is running. Please restore once it is done."));

        if (aiJobService.HasActiveJobs || mediaTranscriptionService.GetSnapshots().Any(snapshot => snapshot.Status is MediaImportStatus.QUEUED or MediaImportStatus.RUNNING or MediaImportStatus.CANCELING))
            return this.Refuse(TB("AI Studio is still writing an answer or a transcript. Please restore once it is done."));

        CancellationTokenSource cancellation;
        lock (this.stateLock)
        {
            if (this.preparing is not null)
                return new(false, false, TB("A restore is being prepared already."));

            cancellation = this.preparing = new();
        }

        this.ReportProgress(new(0, 0));
        var token = cancellation.Token;
        try
        {
            var target = this.CreateTarget();
            this.indexingPause ??= await embeddingService.PauseIndexingAsync(token);

            var progress = new Progress<DataRestoreProgress>(this.ReportProgress);
            var result = await Task.Run(() => DataRestorePreparer.PrepareAsync(archivePath, target, progress, token), token);
            this.IsRestorePending = result.Success;
            if (result.Success)
                logger.LogInformation("Prepared the restore of the backup '{ArchivePath}'. It is applied at the next start.", archivePath);
            else
                logger.LogWarning("Could not prepare the restore of the backup '{ArchivePath}': {Issue}", archivePath, result.Issue);

            return this.End(result);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            logger.LogInformation("The user cancelled preparing the restore of the backup '{ArchivePath}'.", archivePath);
            return this.End(new(false, true, string.Empty));
        }
        catch (Exception exception)
        {
            // The preparation removes what it unpacked when it fails:
            logger.LogError(exception, "Preparing the restore of the backup '{ArchivePath}' failed.", archivePath);
            return this.End(new(false, false, string.Format(TB("The restore could not be prepared: {0}"), exception.Message)));
        }
        finally
        {
            lock (this.stateLock)
                this.preparing = null;

            cancellation.Dispose();
            if (!this.IsRestorePending)
                await this.EndIndexingPauseAsync();

            this.StateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Stops the running preparation. It removes what it unpacked so far.
    /// </summary>
    public void Cancel()
    {
        lock (this.stateLock)
            this.preparing?.Cancel();
    }

    /// <summary>
    /// Discards a prepared restore, so that the next start leaves all data as it is.
    /// </summary>
    public async Task DiscardPendingRestoreAsync()
    {
        if (this.IsPreparing)
            return;

        try
        {
            DataRestorePreparer.Discard(this.CreateTarget());
            this.IsRestorePending = false;
            logger.LogInformation("Discarded the prepared restore.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not discard the prepared restore.");
        }

        await this.EndIndexingPauseAsync();
        this.StateChanged?.Invoke();
    }

    private DataRestoreTarget CreateTarget()
    {
        var dataDirectory = SettingsManager.DataDirectory ?? throw new InvalidOperationException("The data directory is not known yet.");
        var configDirectory = SettingsManager.ConfigDirectory ?? throw new InvalidOperationException("The configuration directory is not known yet.");
        var appVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<MetaDataAttribute>()?.Version ?? string.Empty;
        var organizationPluginFolders = DataBackupContents.FindOrganizationPluginsAmongUserPlugins(PluginFactory.AvailablePlugins.OfType<IAvailablePlugin>(), dataDirectory);

        // Loading the settings brings them to the version this AI Studio writes:
        return new(dataDirectory, configDirectory, appVersion, settingsManager.ConfigurationData.Version, organizationPluginFolders);
    }

    private async Task EndIndexingPauseAsync()
    {
        var pause = this.indexingPause;
        this.indexingPause = null;
        if (pause is not null)
            await pause.DisposeAsync();
    }

    private DataRestoreResult Refuse(string issue)
    {
        logger.LogWarning("Refused to prepare a restore: {Issue}", issue);
        return this.End(new(false, false, issue));
    }

    private DataRestoreResult End(DataRestoreResult result)
    {
        this.LastResult = result;
        this.StateChanged?.Invoke();
        return result;
    }

    private void ReportProgress(DataRestoreProgress progress)
    {
        this.Progress = progress;
        this.StateChanged?.Invoke();
    }
}