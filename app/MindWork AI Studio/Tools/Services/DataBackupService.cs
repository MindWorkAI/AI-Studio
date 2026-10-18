using System.Reflection;
using System.Text.Json;

using AIStudio.Settings;
using AIStudio.Tools.AIJobs;
using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Databases.VectorStore;
using AIStudio.Tools.DataBackup;
using AIStudio.Tools.Media;
using AIStudio.Tools.Metadata;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

/// <summary>
/// Backs up all data of AI Studio into one file, one backup at a time.
/// </summary>
/// <remarks>
/// The backup runs here rather than in the component which started it, so leaving the page does not
/// stop it, and a schedule can start it later on as well. The components only watch: they read the
/// state and hear about every change through StateChanged.<br/><br/>
///
/// The service enforces what the organization allows on its own, since a component may show an
/// outdated state, and a schedule has no component at all.
/// </remarks>
public sealed class DataBackupService(ILogger<DataBackupService> logger, SettingsManager settingsManager, DataSourceEmbeddingService embeddingService, DatabaseClientProvider databaseClientProvider, AIJobService aiJobService, MediaTranscriptionService mediaTranscriptionService)
{
    private const int BUFFER_SIZE = 64 * 1024;

    /// <summary>
    /// ERROR_FILE_TOO_LARGE of Windows, as an HRESULT.
    /// </summary>
    private const int WINDOWS_FILE_TOO_LARGE = unchecked((int)0x800700DF);

    /// <summary>
    /// ERROR_FILE_SYSTEM_LIMITATION of Windows, as an HRESULT.
    /// </summary>
    private const int WINDOWS_FILE_SYSTEM_LIMITATION = unchecked((int)0x80070299);

    /// <summary>
    /// EFBIG of Linux and macOS, which .NET passes on as the HResult.
    /// </summary>
    private const int UNIX_FILE_TOO_LARGE = 27;

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataBackupService).Namespace, nameof(DataBackupService));

    private readonly Lock stateLock = new();

    private CancellationTokenSource? running;

    /// <summary>
    /// Fires whenever the state changes: a backup starts, makes progress, or ends.
    /// </summary>
    /// <remarks>
    /// It fires on whatever thread the backup runs. A component hands it over to its own context.
    /// </remarks>
    public event Action? StateChanged;

    /// <summary>
    /// Whether a backup runs.
    /// </summary>
    public bool IsRunning
    {
        get
        {
            lock (this.stateLock)
                return this.running is not null;
        }
    }

    /// <summary>
    /// How far the running backup got.
    /// </summary>
    public DataBackupProgress Progress { get; private set; }

    /// <summary>
    /// How the last backup ended, or null when none ended yet.
    /// </summary>
    public DataBackupResult? LastResult { get; private set; }

    /// <summary>
    /// Backs up all data into the given file.
    /// </summary>
    /// <remarks>
    /// The file is written in place; a file which was there before is replaced. A backup which fails
    /// or is cancelled removes what it wrote.
    /// </remarks>
    /// <param name="archivePath">The file of the backup, as the user chose it.</param>
    /// <returns>How the backup ended.</returns>
    public async Task<DataBackupResult> CreateBackupAsync(string archivePath)
    {
        if (!settingsManager.ConfigurationData.App.AllowUserToBackUpData)
            return this.Refuse(archivePath, TB("Your organization does not allow backups."));

        //
        // A chat which is being written while it is copied may end up broken in the backup. An
        // answer or a transcript stores its chat once it is done, so the backup waits for them:
        //
        if (aiJobService.HasActiveJobs || mediaTranscriptionService.GetSnapshots().Any(snapshot => snapshot.Status is MediaImportStatus.QUEUED or MediaImportStatus.RUNNING or MediaImportStatus.CANCELING))
            return this.Refuse(archivePath, TB("AI Studio is still writing an answer or a transcript. Please start the backup once it is done."));

        CancellationTokenSource cancellation;
        lock (this.stateLock)
        {
            if (this.running is not null)
                return new(false, false, archivePath, 0, 0, false, TB("A backup is running already."));

            cancellation = this.running = new();
        }

        this.ReportProgress(new(DataBackupPhase.PREPARING, 0, 0));
        var token = cancellation.Token;
        try
        {
            //
            // Copying the settings and the index of the data sources takes a while, and SQLite does
            // its work synchronously, even behind its async methods. The click which starts a backup,
            // though, arrives on the thread of the user interface, which would stand still meanwhile:
            //
            var progress = new Progress<DataBackupProgress>(this.ReportProgress);
            var manifest = await Task.Run(() => this.WriteBackupAsync(archivePath, progress, token), token);

            logger.LogInformation("Backed up {FileCount} files with {Megabytes:F1} MB into '{ArchivePath}'. The indexes of the data sources are included: {IncludesIndexes}.", manifest.Entries.Count, manifest.Entries.Sum(entry => entry.Size) / (1024d * 1024d), archivePath, manifest.IncludesDataSourceIndexes);
            return this.End(new(true, false, archivePath, manifest.Entries.Count, manifest.Entries.Sum(entry => entry.Size), manifest.IncludesDataSourceIndexes, string.Empty));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            this.TryDeleteArchive(archivePath);
            logger.LogInformation("The user cancelled the backup into '{ArchivePath}'. The incomplete backup was removed.", archivePath);
            return this.End(new(false, true, archivePath, 0, 0, false, string.Empty));
        }
        catch (IOException exception) when (IsFileTooLarge(exception))
        {
            this.TryDeleteArchive(archivePath);
            logger.LogError(exception, "The backup into '{ArchivePath}' failed, since the file system takes no file this large. The incomplete backup was removed.", archivePath);
            return this.End(new(false, false, archivePath, 0, 0, false, TB("The backup is too large for the chosen drive, which takes no files beyond 4 GB. USB sticks formatted with FAT32 are such drives. Please choose another drive.")));
        }
        catch (Exception exception)
        {
            this.TryDeleteArchive(archivePath);
            logger.LogError(exception, "The backup into '{ArchivePath}' failed. The incomplete backup was removed.", archivePath);
            return this.End(new(false, false, archivePath, 0, 0, false, string.Format(TB("The backup failed: {0}"), exception.Message)));
        }
        finally
        {
            lock (this.stateLock)
                this.running = null;

            cancellation.Dispose();
            this.StateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Copies the settings and the indexes, and writes them into the backup together with the rest of the data.
    /// </summary>
    /// <param name="archivePath">The file of the backup.</param>
    /// <param name="progress">Receives how far writing got.</param>
    /// <param name="token">Cancels the backup.</param>
    /// <returns>The manifest of the backup, as written into it.</returns>
    private async Task<DataBackupManifest> WriteBackupAsync(string archivePath, IProgress<DataBackupProgress> progress, CancellationToken token)
    {
        var indexCopiesDirectory = string.Empty;
        try
        {
            var dataDirectory = SettingsManager.DataDirectory ?? throw new InvalidOperationException("The data directory is not known yet.");
            var configDirectory = SettingsManager.ConfigDirectory ?? throw new InvalidOperationException("The configuration directory is not known yet.");
            var settings = DataBackupSettings.CopyWithoutPluginObjects(settingsManager.ConfigurationData);
            var settingsJson = JsonSerializer.Serialize(settings, SettingsManager.JSON_OPTIONS);
            var organizationPluginFolders = DataBackupContents.FindOrganizationPluginsAmongUserPlugins(PluginFactory.AvailablePlugins.OfType<IAvailablePlugin>(), dataDirectory);

            indexCopiesDirectory = await this.CopyIndexesAsync(dataDirectory, token);

            var sources = new DataBackupSources(dataDirectory, configDirectory, settingsJson, indexCopiesDirectory, organizationPluginFolders);
            var manifest = new DataBackupManifest(
                DataBackupFormat.FORMAT_VERSION,
                Assembly.GetExecutingAssembly().GetCustomAttribute<MetaDataAttribute>()?.Version ?? "unknown",
                settings.Version.ToString(),
                DateTimeOffset.UtcNow,
                dataDirectory,
                configDirectory,
                !string.IsNullOrWhiteSpace(indexCopiesDirectory),
                []);

            await using var archiveStream = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None, BUFFER_SIZE, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await DataBackupWriter.WriteAsync(archiveStream, sources, manifest, progress, token);
        }
        finally
        {
            this.TryDeleteIndexCopies(indexCopiesDirectory);
        }
    }

    /// <summary>
    /// Whether writing failed since the file system takes no file this large, as FAT32 does beyond 4 GiB.
    /// </summary>
    private static bool IsFileTooLarge(IOException exception) => exception.HResult is WINDOWS_FILE_TOO_LARGE or WINDOWS_FILE_SYSTEM_LIMITATION or UNIX_FILE_TOO_LARGE;

    /// <summary>
    /// Stops the running backup. It removes what it wrote so far.
    /// </summary>
    public void Cancel()
    {
        lock (this.stateLock)
            this.running?.Cancel();
    }

    /// <summary>
    /// Copies both indexes of the data sources and the transcripts of their media files, while the indexing pauses.
    /// </summary>
    /// <remarks>
    /// Both copies are made during one pause: the vector stores have to fit to the index, or a
    /// restore would leave embeddings which belong to nothing, or files which count as embedded
    /// without their embeddings. Writing the backup afterward takes far longer, so the indexing
    /// goes on meanwhile.<br/><br/>
    ///
    /// Without either database, the backup leaves both indexes out, and its manifest says so.
    /// </remarks>
    /// <param name="dataDirectory">The data folder, which holds the transcripts.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The directory of the copies, in the layout of the data folder, or an empty string when the backup leaves the indexes out.</returns>
    private async Task<string> CopyIndexesAsync(string dataDirectory, CancellationToken token)
    {
        var indexStore = await databaseClientProvider.GetIndexStoreAsync(token);
        var vectorStore = await databaseClientProvider.GetVectorStoreAsync(token);
        if (indexStore is NoIndexStoreClient || vectorStore is NoVectorStoreClient)
        {
            logger.LogWarning("The backup leaves the indexes of the data sources out, because the index store or the vector store is not available.");
            return string.Empty;
        }

        await using var pause = await embeddingService.PauseIndexingAsync(token);
        var preparation = await vectorStore.PrepareBackupAsync(token);
        try
        {
            var indexEntry = DataBackupContents.ENTRIES.First(entry => entry.Kind is DataBackupEntryKind.DATA_SOURCE_INDEX);
            await indexStore.BackupToDirectoryAsync(Path.Join(preparation.StagingPath, indexEntry.RelativePath.Replace(DataBackupContents.SEPARATOR, Path.DirectorySeparatorChar)), token);

            for (var index = 0; index < preparation.Stores.Count; index++)
            {
                this.ReportProgress(new(DataBackupPhase.COPYING_INDEXES, index, preparation.Stores.Count));
                await vectorStore.CopyStoreForBackupAsync(preparation.BackupId, preparation.Stores[index].StoreName, token);
            }

            //
            // The index knows which transcript belongs to which media file, so the transcripts are
            // copied while the indexing pauses as well, and the backup holds both as they fit together:
            //
            await DataSourceTranscriptStore.CopyAllAsync(dataDirectory, preparation.StagingPath, token);

            return preparation.StagingPath;
        }
        catch
        {
            this.TryDeleteIndexCopies(preparation.StagingPath);
            throw;
        }
    }

    private DataBackupResult Refuse(string archivePath, string issue)
    {
        logger.LogWarning("Refused to start a backup into '{ArchivePath}': {Issue}", archivePath, issue);
        return this.End(new(false, false, archivePath, 0, 0, false, issue));
    }

    private DataBackupResult End(DataBackupResult result)
    {
        this.LastResult = result;
        this.StateChanged?.Invoke();
        return result;
    }

    private void ReportProgress(DataBackupProgress progress)
    {
        this.Progress = progress;
        this.StateChanged?.Invoke();
    }

    private void TryDeleteArchive(string archivePath)
    {
        try
        {
            if (File.Exists(archivePath))
                File.Delete(archivePath);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not remove the incomplete backup '{ArchivePath}'.", archivePath);
        }
    }

    /// <summary>
    /// Removes the copies of the indexes. Should that fail, the next start removes them.
    /// </summary>
    private void TryDeleteIndexCopies(string indexCopiesDirectory)
    {
        if (string.IsNullOrWhiteSpace(indexCopiesDirectory))
            return;

        try
        {
            if (Directory.Exists(indexCopiesDirectory))
                Directory.Delete(indexCopiesDirectory, true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not remove the copies of the indexes at '{IndexCopiesDirectory}'.", indexCopiesDirectory);
        }
    }
}