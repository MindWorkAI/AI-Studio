using System.Collections.Concurrent;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.Services.Indexing;

internal sealed partial class FileSourceIndexer
{
    private const int WATCHER_DEBOUNCE_SECONDS = 2;

    private readonly ConcurrentDictionary<string, DataSourceWatcherRegistration> watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> watcherDebounceTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly object watcherDebounceLock = new();

    /// <summary>
    /// The data sources handed in last, by their id. A watcher which failed is created anew from here.
    /// </summary>
    private IReadOnlyDictionary<string, IIndexedDataSource> trackedSources = new Dictionary<string, IIndexedDataSource>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// How to ask the embedding service for a run, as handed in last.
    /// </summary>
    private Func<string, DataSourceEmbeddingRefreshMode, Task> requestRunCallback = (_, _) => Task.CompletedTask;

    /// <inheritdoc />
    public void TrackChanges(IReadOnlyCollection<IIndexedDataSource> dataSources, Func<string, DataSourceEmbeddingRefreshMode, Task> requestRun)
    {
        this.requestRunCallback = requestRun;

        var supportedSources = dataSources
            .Where(this.Supports)
            .ToDictionary(source => source.Id, StringComparer.OrdinalIgnoreCase);

        this.trackedSources = supportedSources;

        foreach (var existingWatcherId in this.watchers.Keys.Except(supportedSources.Keys, StringComparer.OrdinalIgnoreCase).ToList())
            this.StopTracking(existingWatcherId);

        foreach (var dataSource in supportedSources.Values)
            this.EnsureWatcher(dataSource);
    }

    /// <inheritdoc />
    public void StopTracking(string dataSourceId)
    {
        this.CancelPendingWatcherRefresh(dataSourceId);

        if (this.watchers.TryRemove(dataSourceId, out var registration))
            registration.Watcher.Dispose();
    }

    /// <inheritdoc />
    public void StopTrackingAll()
    {
        foreach (var watcherId in this.watchers.Keys.ToList())
            this.StopTracking(watcherId);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.CancelAllPendingWatcherRefreshes();

        foreach (var registration in this.watchers.Values)
            registration.Watcher.Dispose();

        this.watchers.Clear();
    }

    private void EnsureWatcher(IDataSourceBase dataSource)
    {
        if (!settingsManager.ConfigurationData.App.DataSourceIndexing.AutomaticRefresh)
            return;

        var configuration = GetWatchConfiguration(dataSource);
        if (configuration is null)
            return;

        if (this.watchers.TryGetValue(dataSource.Id, out var existingRegistration))
        {
            if (IsSameWatchConfiguration(existingRegistration.Configuration, configuration))
                return;

            this.StopTracking(dataSource.Id);
        }

        var watcher = this.CreateWatcher(dataSource.Id, configuration);
        if (watcher is null)
            return;

        if (!this.watchers.TryAdd(dataSource.Id, new DataSourceWatcherRegistration(watcher, configuration)))
            watcher.Dispose();
    }

    private FileSystemWatcher? CreateWatcher(string dataSourceId, DataSourceWatcherConfiguration configuration)
    {
        try
        {
            var watcher = new FileSystemWatcher(configuration.RootPath)
            {
                // Filtering in managed code keeps rename events reliable: a native filter for one
                // file may suppress the event when that file is renamed to a different name.
                Filter = "*",
                IncludeSubdirectories = configuration.IncludeSubdirectories,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
            };

            watcher.Changed += (_, args) => this.OnWatchedDataSourceChanged(dataSourceId, configuration, args);
            watcher.Deleted += (_, args) => this.OnWatchedDataSourceChanged(dataSourceId, configuration, args);
            watcher.Created += (_, args) => this.OnWatchedDataSourceChanged(dataSourceId, configuration, args);
            watcher.Renamed += (_, args) => this.OnWatchedDataSourceChanged(dataSourceId, configuration, args);
            watcher.Error += (_, args) =>
            {
                logger.LogWarning(args.GetException(), "The file watcher for data source '{DataSourceId}' failed. Recreating it.", dataSourceId);
                this.StopTracking(dataSourceId);
                this.EnsureWatcher(dataSourceId);
                this.ScheduleWatchedDataSourceRefresh(dataSourceId);
            };
            watcher.EnableRaisingEvents = true;
            return watcher;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to create file watcher for data source '{DataSourceId}' at '{RootPath}'.", dataSourceId, configuration.RootPath);
            return null;
        }
    }

    private void OnWatchedDataSourceChanged(string dataSourceId, DataSourceWatcherConfiguration configuration, FileSystemEventArgs args)
    {
        if (!IsRelevantWatcherEvent(configuration, args))
        {
            logger.LogDebug(
                "Ignoring file system change for data source '{DataSourceId}' at '{Path}' (event={ChangeType}) because the path is not part of the RAG index.",
                dataSourceId,
                args.FullPath,
                args.ChangeType);
            return;
        }

        logger.LogDebug(
            "Detected relevant file system change for data source '{DataSourceId}' at '{Path}' (event={ChangeType}). Scheduling a debounced embedding run.",
            dataSourceId,
            args.FullPath,
            args.ChangeType);

        this.ScheduleWatchedDataSourceRefresh(dataSourceId);
    }

    private void ScheduleWatchedDataSourceRefresh(string dataSourceId)
    {
        if (!settingsManager.ConfigurationData.App.DataSourceIndexing.AutomaticRefresh)
            return;

        var debounceToken = new CancellationTokenSource();

        lock (this.watcherDebounceLock)
        {
            if (this.watcherDebounceTokens.Remove(dataSourceId, out var existingToken))
                existingToken.Cancel();

            this.watcherDebounceTokens[dataSourceId] = debounceToken;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(WATCHER_DEBOUNCE_SECONDS), debounceToken.Token);
                if (!this.TryCompletePendingWatcherRefresh(dataSourceId, debounceToken))
                    return;

                if (this.trackedSources.TryGetValue(dataSourceId, out var dataSource))
                {
                    logger.LogInformation("Queueing data source '{DataSourceName}' ({DataSourceId}) after file system changes settled. The hash pipeline will reindex only changed files.", dataSource.Name, dataSource.Id);
                    await this.requestRunCallback(dataSource.Id, DataSourceEmbeddingRefreshMode.WATCHER_HASH_CHECK);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to queue watched data source '{DataSourceId}' after a file system change.", dataSourceId);
            }
            finally
            {
                debounceToken.Dispose();
            }
        });
    }

    private void EnsureWatcher(string dataSourceId)
    {
        if (this.trackedSources.TryGetValue(dataSourceId, out var dataSource))
            this.EnsureWatcher(dataSource);
    }

    private void CancelPendingWatcherRefresh(string dataSourceId)
    {
        lock (this.watcherDebounceLock)
        {
            if (this.watcherDebounceTokens.Remove(dataSourceId, out var token))
                token.Cancel();
        }
    }

    private void CancelAllPendingWatcherRefreshes()
    {
        lock (this.watcherDebounceLock)
        {
            foreach (var token in this.watcherDebounceTokens.Values)
                token.Cancel();

            this.watcherDebounceTokens.Clear();
        }
    }

    private bool TryCompletePendingWatcherRefresh(string dataSourceId, CancellationTokenSource debounceToken)
    {
        lock (this.watcherDebounceLock)
        {
            if (!this.watcherDebounceTokens.TryGetValue(dataSourceId, out var currentToken) || !ReferenceEquals(currentToken, debounceToken))
                return false;

            this.watcherDebounceTokens.Remove(dataSourceId);
            return true;
        }
    }

    internal static bool IsRelevantWatcherEvent(DataSourceWatcherConfiguration configuration, FileSystemEventArgs args)
    {
        if (args is RenamedEventArgs renamedArgs)
        {
            return IsRelevantWatcherPath(configuration, renamedArgs.FullPath, args.ChangeType)
                   || IsRelevantWatcherPath(configuration, renamedArgs.OldFullPath, args.ChangeType);
        }

        return IsRelevantWatcherPath(configuration, args.FullPath, args.ChangeType);
    }

    internal static bool IsRelevantWatcherPath(DataSourceWatcherConfiguration configuration, string path, WatcherChangeTypes changeType)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var fileName = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(fileName))
            return true;

        if (configuration.TargetFileName is { } targetFileName)
            return string.Equals(fileName, targetFileName, StringComparison.OrdinalIgnoreCase);

        if (configuration.IncludeSubdirectories
            && !configuration.IncludeHiddenFilesAndFolders
            && changeType is not WatcherChangeTypes.Deleted and not WatcherChangeTypes.Renamed
            && DirectoryFileTraversal.IsInHiddenSubdirectory(configuration.RootPath, path))
            return false;

        if (!configuration.IncludeHiddenFilesAndFolders && IsLeftOutAsHidden(configuration, path))
            return false;

        if (Directory.Exists(path))
            return true;

        if (FileExclusions.IsAlwaysExcludedFile(fileName))
            return false;

        if (configuration.FilePatterns is { } patterns)
        {
            if (FileNamePatterns.Matches(path, patterns))
                return true;

            // A newly found file which lies outside the patterns belongs in the exclusion summary.
            // A deleted path can have been a directory, but FileSystemWatcher does not tell us
            // that after it is gone. Refresh conservatively so the index and that list stay current.
            return changeType is WatcherChangeTypes.Created or WatcherChangeTypes.Deleted or WatcherChangeTypes.Renamed;
        }

        if (IsSupportedRagFilePath(path))
            return true;

        return changeType is WatcherChangeTypes.Deleted or WatcherChangeTypes.Renamed
               && string.IsNullOrWhiteSpace(Path.GetExtension(path));
    }

    /// <summary>
    /// Whether a path is a hidden file or folder the data source leaves out, cf. DirectoryFileTraversal.
    /// </summary>
    /// <remarks>
    /// A hidden folder is never entered, while a file pattern which begins with a dot, such as
    /// .gitignore, asks for a hidden file by itself.
    /// </remarks>
    private static bool IsLeftOutAsHidden(DataSourceWatcherConfiguration configuration, string path)
    {
        if (!DirectoryFileTraversal.IsHiddenPath(path))
            return false;

        return Directory.Exists(path) || !FileNamePatterns.NamesHiddenFile(path, configuration.FilePatterns ?? []);
    }

    private static DataSourceWatcherConfiguration? GetWatchConfiguration(IDataSourceBase dataSource) => dataSource switch
    {
        DataSourceLocalDirectory localDirectory when Directory.Exists(localDirectory.Path) => new DataSourceWatcherConfiguration(
            localDirectory.Path,
            localDirectory.IncludeSubdirectories,
            localDirectory.IncludeHiddenFilesAndFolders,
            FilePatterns: FileNamePatterns.Parse(localDirectory.EffectiveFilePatterns)),
        DataSourceLocalFile localFile when File.Exists(localFile.FilePath) && !string.IsNullOrWhiteSpace(Path.GetDirectoryName(localFile.FilePath)) => new DataSourceWatcherConfiguration(
            Path.GetDirectoryName(localFile.FilePath)!,
            false,
            TargetFileName: Path.GetFileName(localFile.FilePath)),
        _ => null,
    };

    internal static bool IsSameWatchConfiguration(DataSourceWatcherConfiguration left, DataSourceWatcherConfiguration right)
    {
        return left.IncludeSubdirectories == right.IncludeSubdirectories
               && left.IncludeHiddenFilesAndFolders == right.IncludeHiddenFilesAndFolders
               && string.Equals(left.RootPath, right.RootPath, StringComparison.OrdinalIgnoreCase)
               && string.Equals(left.TargetFileName, right.TargetFileName, StringComparison.OrdinalIgnoreCase)
               && FileNamePatterns.AreEquivalent(left.FilePatterns, right.FilePatterns);
    }

    internal sealed record DataSourceWatcherConfiguration(
        string RootPath,
        bool IncludeSubdirectories,
        bool IncludeHiddenFilesAndFolders = false,
        string? TargetFileName = null,
        IReadOnlyList<string>? FilePatterns = null);

    private sealed record DataSourceWatcherRegistration(FileSystemWatcher Watcher, DataSourceWatcherConfiguration Configuration);
}
