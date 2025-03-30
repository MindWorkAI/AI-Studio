namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    public static void SetUpHotReloading()
    {
        LOG.LogInformation($"Start hot reloading plugins for path '{HOT_RELOAD_WATCHER.Path}'.");
        try
        {
            var messageBus = Program.SERVICE_PROVIDER.GetRequiredService<MessageBus>();
        
            HOT_RELOAD_WATCHER.IncludeSubdirectories = true;
            HOT_RELOAD_WATCHER.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            HOT_RELOAD_WATCHER.Filter = "*.lua";
            HOT_RELOAD_WATCHER.Changed += async (_, args) =>
            {
                LOG.LogInformation($"File changed: {args.FullPath}");
                await LoadAll();
                await messageBus.SendMessage<bool>(null, Event.PLUGINS_RELOADED);
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