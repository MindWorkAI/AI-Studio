using System.Text;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Lua;
using Lua.Standard;

namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    private static readonly List<IAvailablePlugin> AVAILABLE_PLUGINS = [];
    private static readonly SemaphoreSlim PLUGIN_LOAD_SEMAPHORE = new(1, 1);
    
    /// <summary>
    /// A list of all available plugins.
    /// </summary>
    public static IReadOnlyCollection<IPluginMetadata> AvailablePlugins => AVAILABLE_PLUGINS;
    
    /// <summary>
    /// Try to load all plugins from the plugins directory.
    /// </summary>
    /// <remarks>
    /// Loading plugins means:<br/>
    /// - Parsing and checking the plugin code<br/>
    /// - Check for forbidden plugins<br/>
    /// - Creating a new instance of the allowed plugin<br/>
    /// - Read the plugin metadata<br/>
    /// - Start the plugin<br/>
    /// </remarks>
    public static async Task LoadAll(CancellationToken cancellationToken = default)
    {
        if (!IS_INITIALIZED)
        {
            LOG.LogError("PluginFactory is not initialized. Please call Setup() before using it.");
            return;
        }
        
        if (!await PLUGIN_LOAD_SEMAPHORE.WaitAsync(0, cancellationToken))
            return;

        var configObjectList = new List<PluginConfigurationObject>();
        
        try
        {
            LOG.LogInformation("Start loading plugins.");
            if (!Directory.Exists(PLUGINS_ROOT))
            {
                LOG.LogInformation("No plugins found.");
                return;
            }
        
            AVAILABLE_PLUGINS.Clear();
        
            //
            // The easiest way to load all plugins is to find all `plugin.lua` files and load them.
            // By convention, each plugin is enforced to have a `plugin.lua` file.
            //
            var pluginMainFiles = Directory.EnumerateFiles(PLUGINS_ROOT, "plugin.lua", SearchOption.AllDirectories);
            foreach (var pluginMainFile in pluginMainFiles)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        LOG.LogWarning("Was not able to load all plugins, because the operation was cancelled. It seems to be a timeout.");
                        break;
                    }

                    LOG.LogInformation($"Try to load plugin: {pluginMainFile}");
                    var fileInfo = new FileInfo(pluginMainFile);
                    string code;
                    await using(var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using var reader = new StreamReader(fileStream, Encoding.UTF8);
                        code = await reader.ReadToEndAsync(cancellationToken);
                    }
                    
                    var pluginPath = Path.GetDirectoryName(pluginMainFile)!;
                    var plugin = await Load(pluginPath, code, cancellationToken);
            
                    switch (plugin)
                    {
                        case NoPlugin noPlugin when noPlugin.Issues.Any():
                            LOG.LogError($"Was not able to load plugin: '{pluginMainFile}'. Reason: {noPlugin.Issues.First()}");
                            continue;
                
                        case NoPlugin:
                            LOG.LogError($"Was not able to load plugin: '{pluginMainFile}'. Reason: Unknown.");
                            continue;
                
                        case { IsValid: false }:
                            LOG.LogError($"Was not able to load plugin '{pluginMainFile}', because the Lua code is not a valid AI Studio plugin. There are {plugin.Issues.Count()} issues to fix. First issue is: {plugin.Issues.FirstOrDefault()}");
                            #if DEBUG
                            foreach (var pluginIssue in plugin.Issues)
                                LOG.LogError($"Plugin issue: {pluginIssue}");
                            #endif
                            continue;

                        case { IsMaintained: false }:
                            LOG.LogWarning($"The plugin '{pluginMainFile}' is not maintained anymore. Please consider to disable it.");
                            break;
                    }
            
                    LOG.LogInformation($"Successfully loaded plugin: '{pluginMainFile}' (Id='{plugin.Id}', Type='{plugin.Type}', Name='{plugin.Name}', Version='{plugin.Version}', Authors='{string.Join(", ", plugin.Authors)}')");
                    AVAILABLE_PLUGINS.Add(new PluginMetadata(plugin, pluginPath));
                }
                catch (Exception e)
                {
                    LOG.LogError($"Was not able to load plugin '{pluginMainFile}'. Issue: {e.Message}");
                    LOG.LogDebug(e.StackTrace);
                }
            }
        
            // Start or restart all plugins:
            var configObjects = await RestartAllPlugins(cancellationToken);
            configObjectList.AddRange(configObjects);
        }
        finally
        {
            PLUGIN_LOAD_SEMAPHORE.Release();
            LOG.LogInformation("Finished loading plugins.");
        }
        
        //
        // =========================================================
        // Next, we have to clean up our settings. It is possible that a configuration plugin was removed.
        // We have to remove the related settings as well:
        // =========================================================
        //
        var wasConfigurationChanged = false;
        
        //
        // Check LLM providers:
        //
        #pragma warning disable MWAIS0001
        var configuredProviders = SETTINGS_MANAGER.ConfigurationData.Providers.ToList();
        foreach (var configuredProvider in configuredProviders)
        {
            if(!configuredProvider.IsEnterpriseConfiguration)
                continue;

            var providerSourcePluginId = configuredProvider.EnterpriseConfigurationPluginId;
            if(providerSourcePluginId == Guid.Empty)
                continue;
            
            var providerSourcePlugin = AVAILABLE_PLUGINS.FirstOrDefault(plugin => plugin.Id == providerSourcePluginId);
            if(providerSourcePlugin is null)
            {
                LOG.LogWarning($"The configured LLM provider '{configuredProvider.InstanceName}' (id={configuredProvider.Id}) is based on a plugin that is not available anymore. Removing the provider from the settings.");
                SETTINGS_MANAGER.ConfigurationData.Providers.Remove(configuredProvider);
                wasConfigurationChanged = true;
            }
            
            if(!configObjectList.Any(configObject =>
                configObject.Type is PluginConfigurationObjectType.LLM_PROVIDER &&
                configObject.ConfigPluginId == providerSourcePluginId &&
                configObject.Id.ToString() == configuredProvider.Id))
            {
                LOG.LogWarning($"The configured LLM provider '{configuredProvider.InstanceName}' (id={configuredProvider.Id}) is not present in the configuration plugin anymore. Removing the provider from the settings.");
                SETTINGS_MANAGER.ConfigurationData.Providers.Remove(configuredProvider);
                wasConfigurationChanged = true;
            }
        }
        #pragma warning restore MWAIS0001
        
        //
        // Check chat templates:
        //
        var configuredTemplates = SETTINGS_MANAGER.ConfigurationData.ChatTemplates.ToList();
        foreach (var configuredTemplate in configuredTemplates)
        {
            if(!configuredTemplate.IsEnterpriseConfiguration)
                continue;

            var templateSourcePluginId = configuredTemplate.EnterpriseConfigurationPluginId;
            if(templateSourcePluginId == Guid.Empty)
                continue;
            
            var templateSourcePlugin = AVAILABLE_PLUGINS.FirstOrDefault(plugin => plugin.Id == templateSourcePluginId);
            if(templateSourcePlugin is null)
            {
                LOG.LogWarning($"The configured chat template '{configuredTemplate.Name}' (id={configuredTemplate.Id}) is based on a plugin that is not available anymore. Removing the chat template from the settings.");
                SETTINGS_MANAGER.ConfigurationData.ChatTemplates.Remove(configuredTemplate);
                wasConfigurationChanged = true;
            }
            
            if(!configObjectList.Any(configObject =>
                configObject.Type is PluginConfigurationObjectType.CHAT_TEMPLATE &&
                configObject.ConfigPluginId == templateSourcePluginId &&
                configObject.Id.ToString() == configuredTemplate.Id))
            {
                LOG.LogWarning($"The configured chat template '{configuredTemplate.Name}' (id={configuredTemplate.Id}) is not present in the configuration plugin anymore. Removing the chat template from the settings.");
                SETTINGS_MANAGER.ConfigurationData.ChatTemplates.Remove(configuredTemplate);
                wasConfigurationChanged = true;
            }
        }
        
        //
        // ==========================================================
        // Check all possible settings:
        // ==========================================================
        //
        
        // Check for updates, and if so, how often?
        if(ManagedConfiguration.IsConfigurationLeftOver<DataApp, UpdateBehavior>(x => x.App, x => x.UpdateBehavior, AVAILABLE_PLUGINS))
            wasConfigurationChanged = true;
        
        // Allow the user to add providers?
        if(ManagedConfiguration.IsConfigurationLeftOver<DataApp, bool>(x => x.App, x => x.AllowUserToAddProvider, AVAILABLE_PLUGINS))
            wasConfigurationChanged = true;
        
        if (wasConfigurationChanged)
        {
            await SETTINGS_MANAGER.StoreSettings();
            await MessageBus.INSTANCE.SendMessage<bool>(null, Event.CONFIGURATION_CHANGED);
        }
    }

    public static async Task<PluginBase> Load(string? pluginPath, string code, CancellationToken cancellationToken = default)
    {
        if(ForbiddenPlugins.Check(code) is { IsForbidden: true } forbiddenState)
            return new NoPlugin($"This plugin is forbidden: {forbiddenState.Message}");
        
        var state = LuaState.Create();
        if (!string.IsNullOrWhiteSpace(pluginPath))
        {
            // Add the module loader so that the plugin can load other Lua modules:
            state.ModuleLoader = new PluginLoader(pluginPath);
        }

        // Add some useful libraries:
        state.OpenModuleLibrary();
        state.OpenStringLibrary();
        state.OpenTableLibrary();
        state.OpenMathLibrary();
        state.OpenBitwiseLibrary();
        state.OpenCoroutineLibrary();

        try
        {
            await state.DoStringAsync(code, cancellationToken: cancellationToken);
        }
        catch (LuaParseException e)
        {
            return new NoPlugin($"Was not able to parse the plugin: {e.Message}");
        }
        catch (LuaRuntimeException e)
        {
            return new NoPlugin($"Was not able to run the plugin: {e.Message}");
        }
        
        if (!state.Environment["TYPE"].TryRead<string>(out var typeText))
            return new NoPlugin("TYPE does not exist or is not a valid string.");
        
        if (!Enum.TryParse<PluginType>(typeText, out var type))
            return new NoPlugin($"TYPE is not a valid plugin type. Valid types are: {CommonTools.GetAllEnumValues<PluginType>()}");
        
        if(type is PluginType.NONE)
            return new NoPlugin($"TYPE is not a valid plugin type. Valid types are: {CommonTools.GetAllEnumValues<PluginType>()}");
        
        var isInternal = !string.IsNullOrWhiteSpace(pluginPath) && pluginPath.StartsWith(INTERNAL_PLUGINS_ROOT, StringComparison.OrdinalIgnoreCase);
        switch (type)
        {
            case PluginType.LANGUAGE:
                return new PluginLanguage(isInternal, state, type);
            
            case PluginType.CONFIGURATION:
                var configPlug = new PluginConfiguration(isInternal, state, type);
                await configPlug.InitializeAsync(true);
                return configPlug;
            
            default:
                return new NoPlugin("This plugin type is not supported yet. Please try again with a future version of AI Studio.");
        }
    }
}