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
            HOT_RELOAD_WATCHER.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            HOT_RELOAD_WATCHER.Filter = "*.lua";
            HOT_RELOAD_WATCHER.Changed += HotReloadEventHandler;
            HOT_RELOAD_WATCHER.Deleted += HotReloadEventHandler;
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
                LOG.LogInformation($"File changed ({changeType}): {args.FullPath}. Already processing another change.");
                return;
            }

            try
            {
                LOG.LogInformation($"File changed ({changeType}): {args.FullPath}. Reloading plugins...");
                await LoadAll();
                await MessageBus.INSTANCE.SendMessage<bool>(null, Event.PLUGINS_RELOADED);
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