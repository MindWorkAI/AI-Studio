using System.Collections.Concurrent;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.Services;

public sealed partial class DataSourceEmbeddingService
{
    private const int WATCHER_DEBOUNCE_SECONDS = 2;

    private readonly ConcurrentDictionary<string, DataSourceWatcherRegistration> watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> watcherDebounceTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly object watcherDebounceLock = new();
    
    private void RefreshWatchers()
    {
        if (!this.settingsManager.ConfigurationData.DataSourceIndexing.AutomaticRefresh)
        {
            this.RemoveAllWatchers();
            return;
        }

        var supportedSources = this.settingsManager.ConfigurationData.DataSources
            .Where(this.IsSupportedInternalDataSource)
            .ToDictionary(source => source.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var existingWatcherId in this.watchers.Keys.Except(supportedSources.Keys, StringComparer.OrdinalIgnoreCase).ToList())
            this.RemoveWatcher(existingWatcherId);

        foreach (var dataSource in supportedSources.Values)
            this.EnsureWatcher(dataSource);
    }

    private void EnsureWatcher(IDataSource dataSource)
    {
        if (!this.settingsManager.ConfigurationData.DataSourceIndexing.AutomaticRefresh)
            return;

        var configuration = GetWatchConfiguration(dataSource);
        if (configuration is null)
            return;

        if (this.watchers.TryGetValue(dataSource.Id, out var existingRegistration))
        {
            if (IsSameWatchConfiguration(existingRegistration.Configuration, configuration))
                return;

            this.RemoveWatcher(dataSource.Id);
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
                Filter = configuration.Filter,
                IncludeSubdirectories = configuration.IncludeSubdirectories,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
            };

            watcher.Changed += (_, _) => this.OnWatchedDataSourceChanged(dataSourceId);
            watcher.Deleted += (_, _) => this.OnWatchedDataSourceChanged(dataSourceId);
            watcher.Created += (_, _) => this.OnWatchedDataSourceChanged(dataSourceId);
            watcher.Renamed += (_, _) => this.OnWatchedDataSourceChanged(dataSourceId);
            watcher.Error += (_, args) =>
            {
                this.logger.LogWarning(args.GetException(), "The file watcher for data source '{DataSourceId}' failed. Recreating it.", dataSourceId);
                this.RemoveWatcher(dataSourceId);
                this.EnsureWatcher(dataSourceId);
                this.OnWatchedDataSourceChanged(dataSourceId);
            };
            watcher.EnableRaisingEvents = true;
            return watcher;
        }
        catch (Exception exception)
        {
            this.logger.LogWarning(exception, "Failed to create file watcher for data source '{DataSourceId}' at '{RootPath}'.", dataSourceId, configuration.RootPath);
            return null;
        }
    }

    private void RemoveWatcher(string dataSourceId)
    {
        this.CancelPendingWatcherRefresh(dataSourceId);

        if (this.watchers.TryRemove(dataSourceId, out var registration))
            registration.Watcher.Dispose();
    }
    
    private void RemoveAllWatchers()
    {
        foreach (var watcherId in this.watchers.Keys.ToList())
            this.RemoveWatcher(watcherId);
    }

    private void DisposeWatchers()
    {
        this.CancelAllPendingWatcherRefreshes();

        foreach (var registration in this.watchers.Values)
            registration.Watcher.Dispose();

        this.watchers.Clear();
    }

    private void OnWatchedDataSourceChanged(string dataSourceId)
    {
        if (!this.settingsManager.ConfigurationData.DataSourceIndexing.AutomaticRefresh)
            return;

        this.logger.LogDebug("Detected file system change for data source '{DataSourceId}'. Scheduling a debounced embedding run.", dataSourceId);
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

                var dataSource = this.settingsManager.ConfigurationData.DataSources
                    .FirstOrDefault(source => source.Id.Equals(dataSourceId, StringComparison.OrdinalIgnoreCase));

                if (dataSource is not null)
                {
                    this.logger.LogInformation("Queueing data source '{DataSourceName}' ({DataSourceId}) after file system changes settled.", dataSource.Name, dataSource.Id);
                    await this.QueueDataSourceAsync(dataSource);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                this.logger.LogWarning(exception, "Failed to queue watched data source '{DataSourceId}' after a file system change.", dataSourceId);
            }
            finally
            {
                debounceToken.Dispose();
            }
        });
    }

    private void EnsureWatcher(string dataSourceId)
    {
        var dataSource = this.settingsManager.ConfigurationData.DataSources
            .FirstOrDefault(source => source.Id.Equals(dataSourceId, StringComparison.OrdinalIgnoreCase));

        if (dataSource is not null)
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

    private static DataSourceWatcherConfiguration? GetWatchConfiguration(IDataSource dataSource) => dataSource switch
    {
        DataSourceLocalDirectory localDirectory when Directory.Exists(localDirectory.Path) => new DataSourceWatcherConfiguration(
            localDirectory.Path,
            "*.*",
            true),
        DataSourceLocalFile localFile when File.Exists(localFile.FilePath) && !string.IsNullOrWhiteSpace(Path.GetDirectoryName(localFile.FilePath)) => new DataSourceWatcherConfiguration(
            Path.GetDirectoryName(localFile.FilePath)!,
            Path.GetFileName(localFile.FilePath),
            false),
        _ => null,
    };

    private static bool IsSameWatchConfiguration(DataSourceWatcherConfiguration left, DataSourceWatcherConfiguration right)
    {
        return left.IncludeSubdirectories == right.IncludeSubdirectories
               && string.Equals(left.RootPath, right.RootPath, StringComparison.OrdinalIgnoreCase)
               && string.Equals(left.Filter, right.Filter, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record DataSourceWatcherConfiguration(string RootPath, string Filter, bool IncludeSubdirectories);

    private sealed record DataSourceWatcherRegistration(FileSystemWatcher Watcher, DataSourceWatcherConfiguration Configuration);
}
