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
            var messageBus = Program.SERVICE_PROVIDER.GetRequiredService<MessageBus>();
        
            HOT_RELOAD_WATCHER.IncludeSubdirectories = true;
            HOT_RELOAD_WATCHER.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            HOT_RELOAD_WATCHER.Filter = "*.lua";
            HOT_RELOAD_WATCHER.Changed += async (_, args) =>
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
                    await messageBus.SendMessage<bool>(null, Event.PLUGINS_RELOADED);
                }
                finally
                {
                    HOT_RELOAD_SEMAPHORE.Release();
                }
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
}