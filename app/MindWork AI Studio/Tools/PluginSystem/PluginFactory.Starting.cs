using System.Text;

namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    private static readonly List<PluginBase> RUNNING_PLUGINS = [];
    
    /// <summary>
    /// A list of all running plugins.
    /// </summary>
    public static IReadOnlyCollection<PluginBase> RunningPlugins => RUNNING_PLUGINS;
    
    private static async Task<List<PluginConfigurationObject>> RestartAllPlugins(CancellationToken cancellationToken = default)
    {
        LOG.LogInformation("Try to start or restart all plugins.");
        var configObjects = new List<PluginConfigurationObject>();
        RUNNING_PLUGINS.Clear();

        //
        // Get the base language plugin. This is the plugin that will be used to fill in missing keys.
        //
        var baseLanguagePluginId = InternalPlugin.LANGUAGE_EN_US.MetaData().Id;
        var baseLanguagePluginMetaData = AVAILABLE_PLUGINS.FirstOrDefault(p => p.Id == baseLanguagePluginId);
        if (baseLanguagePluginMetaData is null)
            LOG.LogError($"Was not able to find the base language plugin: Id='{baseLanguagePluginId}'. Please check your installation.");
        else
        {
            try
            {
                var startedBasePlugin = await Start(baseLanguagePluginMetaData, cancellationToken);
                if (startedBasePlugin is NoPlugin noPlugin)
                    LOG.LogError($"Was not able to start the base language plugin: Id='{baseLanguagePluginId}'. Reason: {noPlugin.Issues.First()}");
        
                if (startedBasePlugin is PluginLanguage languagePlugin)
                {
                    BASE_LANGUAGE_PLUGIN = languagePlugin;
                    RUNNING_PLUGINS.Add(languagePlugin);
                    LOG.LogInformation($"Successfully started the base language plugin: Id='{languagePlugin.Id}', Type='{languagePlugin.Type}', Name='{languagePlugin.Name}', Version='{languagePlugin.Version}'");
                }
                else
                    LOG.LogError($"Was not able to start the base language plugin: Id='{baseLanguagePluginId}'. Reason: {string.Join("; ", startedBasePlugin.Issues)}");
            }
            catch (Exception e)
            {
                LOG.LogError(e, $"An error occurred while starting the base language plugin: Id='{baseLanguagePluginId}'.");
                BASE_LANGUAGE_PLUGIN = NoPluginLanguage.INSTANCE;
            }
        }
        
        //
        // Iterate over all available plugins and try to start them.
        //
        foreach (var availablePlugin in AVAILABLE_PLUGINS)
        {
            if(cancellationToken.IsCancellationRequested)
            {
                LOG.LogWarning("Cancellation requested while starting plugins. Stopping the plugin startup process. Probably due to a timeout.");
                break;
            }

            if (availablePlugin.Id == baseLanguagePluginId)
                continue;

            try
            {
                if (availablePlugin.IsInternal || SETTINGS_MANAGER.IsPluginEnabled(availablePlugin) || availablePlugin.Type == PluginType.CONFIGURATION)
                    if(await Start(availablePlugin, cancellationToken) is { IsValid: true } plugin)
                    {
                        if (plugin is PluginConfiguration configPlugin)
                            configObjects.AddRange(configPlugin.ConfigObjects);
                        
                        RUNNING_PLUGINS.Add(plugin);
                    }
            }
            catch (Exception e)
            {
                LOG.LogError(e, $"An error occurred while starting the plugin: Id='{availablePlugin.Id}', Type='{availablePlugin.Type}', Name='{availablePlugin.Name}', Version='{availablePlugin.Version}'.");
            }
        }
        
        // Inform all components that the plugins have been reloaded or started:
        await MessageBus.INSTANCE.SendMessage<bool>(null, Event.PLUGINS_RELOADED);
        return configObjects;
    }
    
    private static async Task<PluginBase> Start(IAvailablePlugin meta, CancellationToken cancellationToken = default)
    {
        var pluginMainFile = Path.Join(meta.LocalPath, "plugin.lua");
        if(!File.Exists(pluginMainFile))
        {
            LOG.LogError($"Was not able to start plugin: Id='{meta.Id}', Type='{meta.Type}', Name='{meta.Name}', Version='{meta.Version}'. Reason: The plugin file does not exist.");
            return new NoPlugin($"The plugin file does not exist: {pluginMainFile}");
        }

        var code = await File.ReadAllTextAsync(pluginMainFile, Encoding.UTF8, cancellationToken);
        var plugin = await Load(meta.LocalPath, code, cancellationToken);
        if (plugin is NoPlugin noPlugin)
        {
            LOG.LogError($"Was not able to start plugin: Id='{meta.Id}', Type='{meta.Type}', Name='{meta.Name}', Version='{meta.Version}'. Reason: {noPlugin.Issues.First()}");
            return noPlugin;
        }
        
        if (plugin.IsValid)
        {
            //
            // When this is a language plugin, we need to set the base language plugin.
            //
            if (plugin is PluginLanguage languagePlugin && BASE_LANGUAGE_PLUGIN != NoPluginLanguage.INSTANCE)
                languagePlugin.SetBaseLanguage(BASE_LANGUAGE_PLUGIN);
            
            if(plugin is PluginConfiguration configPlugin)
                await configPlugin.InitializeAsync(false);
            
            LOG.LogInformation($"Successfully started plugin: Id='{plugin.Id}', Type='{plugin.Type}', Name='{plugin.Name}', Version='{plugin.Version}'");
            return plugin;
        }

        LOG.LogError($"Was not able to start plugin: Id='{meta.Id}', Type='{meta.Type}', Name='{meta.Name}', Version='{meta.Version}'. Reasons: {string.Join("; ", plugin.Issues)}");
        return new NoPlugin($"Was not able to start plugin: Id='{meta.Id}', Type='{meta.Type}', Name='{meta.Name}', Version='{meta.Version}'. Reasons: {string.Join("; ", plugin.Issues)}");
    }
}