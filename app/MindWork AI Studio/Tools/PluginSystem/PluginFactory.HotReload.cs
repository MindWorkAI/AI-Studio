using Timer = System.Timers.Timer;

namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    private static readonly SemaphoreSlim HOT_RELOAD_SEMAPHORE = new(1, 1);

    /// <summary>
    /// How long the plugins directory has to stay quiet before we reload.
    /// </summary>
    /// <remarks>
    /// One change never arrives as one event: writing a single file produces several, and moving an
    /// entire plugin directory into place produces dozens. Reloading on each of them would restart
    /// every plugin over and over.
    /// </remarks>
    private static readonly TimeSpan HOT_RELOAD_DEBOUNCE_INTERVAL = TimeSpan.FromSeconds(1);

    private static readonly Timer HOT_RELOAD_DEBOUNCE_TIMER = new(HOT_RELOAD_DEBOUNCE_INTERVAL)
    {
        AutoReset = false,
    };

    public static void SetUpHotReloading()
    {
        if (!IsInitialized)
        {
            LOG.LogError("PluginFactory is not initialized. Please call Setup() before using it.");
            return;
        }

        LOG.LogInformation($"Start hot reloading plugins for path '{HOT_RELOAD_WATCHER.Path}'.");
        try
        {
            HOT_RELOAD_DEBOUNCE_TIMER.Elapsed += (_, _) => _ = ReloadPluginsAsync();

            HOT_RELOAD_WATCHER.IncludeSubdirectories = true;

            //
            // We watch for plugins appearing, disappearing, and changing. We do not watch access
            // times: reading a plugin is not a change, and on Linux our own reads would be
            // reported back to us. Loading the plugins and computing the audit hash of an
            // assistant plugin both read every Lua file in this directory, so such a filter
            // makes each reload cause the next one:
            //
            HOT_RELOAD_WATCHER.NotifyFilter = NotifyFilters.DirectoryName
                                              | NotifyFilters.FileName
                                              | NotifyFilters.LastWrite
                                              | NotifyFilters.Size;

            HOT_RELOAD_WATCHER.Changed += HotReloadEventHandler;
            HOT_RELOAD_WATCHER.Deleted += HotReloadEventHandler;
            HOT_RELOAD_WATCHER.Created += HotReloadEventHandler;
            HOT_RELOAD_WATCHER.Renamed += HotReloadEventHandler;
            HOT_RELOAD_WATCHER.Error += (_, args) =>
            {
                LOG.LogError(args.GetException(), "Error in hot reload watcher.");
            };
            HOT_RELOAD_WATCHER.EnableRaisingEvents = true;
        }
        catch (Exception e)
        {
            LOG.LogError(e, "Error while setting up hot reloading.");
        }
        finally
        {
            LOG.LogInformation("Hot reloading plugins set up.");
        }
    }

    private static void HotReloadEventHandler(object _, FileSystemEventArgs args)
    {
        try
        {
            //
            // Our own lock file lives in the watched directory. Writing and removing it are not
            // plugin changes, and reacting to them would turn every locked operation into a
            // reload of its own:
            //
            if (IsHotReloadLockFile(args.FullPath))
                return;

            var changeType = args.ChangeType.ToString().ToLowerInvariant();
            LOG.LogInformation($"File changed '{args.FullPath}' (event={changeType}). Scheduling a plugin reload.");

            // Restart the debounce window, so that a burst of events results in one reload:
            HOT_RELOAD_DEBOUNCE_TIMER.Stop();
            HOT_RELOAD_DEBOUNCE_TIMER.Start();
        }
        catch (Exception e)
        {
            LOG.LogError(e, $"Error while handling hot reload event for file '{args.FullPath}' with change type '{args.ChangeType}'.");
        }
    }

    private static bool IsHotReloadLockFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(HOT_RELOAD_LOCK_FILE))
            return false;

        return string.Equals(path, HOT_RELOAD_LOCK_FILE, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task ReloadPluginsAsync()
    {
        //
        // Reloads must never overlap. When one is still running, we do not drop this one: the
        // changes which triggered it might have arrived after the running reload had already read
        // them. We try again after another quiet window instead:
        //
        if (!await HOT_RELOAD_SEMAPHORE.WaitAsync(0))
        {
            LOG.LogInformation("A plugin reload is already running. Waiting for it to finish before reloading again.");
            HOT_RELOAD_DEBOUNCE_TIMER.Stop();
            HOT_RELOAD_DEBOUNCE_TIMER.Start();
            return;
        }

        try
        {
            LOG.LogInformation("Reloading plugins...");
            if (File.Exists(HOT_RELOAD_LOCK_FILE))
            {
                LOG.LogInformation("Hot reload lock file exists. Waiting for it to be released before proceeding with the reload.");

                var lockFileCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var token = lockFileCancellationTokenSource.Token;
                var waitTime = TimeSpan.FromSeconds(1);
                while (File.Exists(HOT_RELOAD_LOCK_FILE) && !token.IsCancellationRequested)
                {
                    try
                    {
                        LOG.LogDebug("Waiting for hot reload lock to be released...");
                        await Task.Delay(waitTime, token);
                        waitTime = TimeSpan.FromSeconds(Math.Min(waitTime.TotalSeconds * 2, 120)); // Exponential backoff with a cap
                    }
                    catch (TaskCanceledException)
                    {
                        // Case: The cancellation token was triggered, meaning the lock file is still present.
                        // We expect that something goes wrong. So, we try to delete the lock file:
                        LOG.LogWarning("Hot reload lock file still exists after 30 seconds. Attempting to delete it...");
                        UnlockHotReload();
                        break;
                    }
                }

                LOG.LogInformation("Hot reload lock file released. Proceeding with plugin reload.");
            }

            // LoadAll announces the reload itself, cf. PluginFactory.Starting.RestartAllPlugins:
            await LoadAll();
        }
        catch(Exception e)
        {
            LOG.LogError(e, "Error while reloading plugins after a change in the plugins directory.");
        }
        finally
        {
            HOT_RELOAD_SEMAPHORE.Release();
        }
    }
}