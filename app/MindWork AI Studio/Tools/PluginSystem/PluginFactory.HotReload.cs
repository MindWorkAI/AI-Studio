namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    private static readonly SemaphoreSlim HOT_RELOAD_SEMAPHORE = new(1, 1);
    
    public static void SetUpHotReloading()
    {
        if (!IS_INITIALIZED)
        {
            LOG.LogError("PluginFactory is not initialized. Please call Setup() before using it.");
            return;
        }
        
        LOG.LogInformation($"Start hot reloading plugins for path '{HOT_RELOAD_WATCHER.Path}'.");
        try
        {
            HOT_RELOAD_WATCHER.IncludeSubdirectories = true;
            HOT_RELOAD_WATCHER.NotifyFilter = NotifyFilters.CreationTime
                                              | NotifyFilters.DirectoryName
                                              | NotifyFilters.FileName
                                              | NotifyFilters.LastAccess
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
    
    private static async void HotReloadEventHandler(object _, FileSystemEventArgs args)
    {
        try
        {
            var changeType = args.ChangeType.ToString().ToLowerInvariant();
            if (!await HOT_RELOAD_SEMAPHORE.WaitAsync(0))
            {
                LOG.LogInformation($"File changed '{args.FullPath}' (event={changeType}). Already processing another change.");
                return;
            }

            try
            {
                LOG.LogInformation($"File changed '{args.FullPath}' (event={changeType}). Reloading plugins...");
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

                await LoadAll();
                await MessageBus.INSTANCE.SendMessage<bool>(null, Event.PLUGINS_RELOADED);
            }
            catch(Exception e)
            {
                LOG.LogError(e, $"Error while reloading plugins after change in file '{args.FullPath}' with change type '{changeType}'.");
            }
            finally
            {
                HOT_RELOAD_SEMAPHORE.Release();
            }
        }
        catch (Exception e)
        {
            LOG.LogError(e, $"Error while handling hot reload event for file '{args.FullPath}' with change type '{args.ChangeType}'.");
        }
    }
}