using System.Text;

using AIStudio.Settings;

using Lua;
using Lua.Standard;

namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger(nameof(PluginFactory));
    private static readonly SettingsManager SETTINGS_MANAGER = Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();
    private static readonly List<IAvailablePlugin> AVAILABLE_PLUGINS = [];
    private static readonly List<PluginBase> RUNNING_PLUGINS = [];

    private static bool IS_INITIALIZED;
    private static string DATA_DIR = string.Empty;
    private static string PLUGINS_ROOT = string.Empty;
    private static string INTERNAL_PLUGINS_ROOT = string.Empty;
    private static FileSystemWatcher HOT_RELOAD_WATCHER = null!;
    private static ILanguagePlugin BASE_LANGUAGE_PLUGIN = NoPluginLanguage.INSTANCE;
    
    /// <summary>
    /// A list of all available plugins.
    /// </summary>
    public static IReadOnlyCollection<IPluginMetadata> AvailablePlugins => AVAILABLE_PLUGINS;
    
    /// <summary>
    /// A list of all running plugins.
    /// </summary>
    public static IReadOnlyCollection<PluginBase> RunningPlugins => RUNNING_PLUGINS;

    public static ILanguagePlugin BaseLanguage => BASE_LANGUAGE_PLUGIN;

    /// <summary>
    /// Set up the plugin factory. We will read the data directory from the settings manager.
    /// Afterward, we will create the plugins directory and the internal plugin directory.
    /// </summary>
    public static void Setup()
    {
        DATA_DIR = SettingsManager.DataDirectory!;
        PLUGINS_ROOT = Path.Join(DATA_DIR, "plugins");
        INTERNAL_PLUGINS_ROOT = Path.Join(PLUGINS_ROOT, ".internal");
        
        if (!Directory.Exists(PLUGINS_ROOT))
            Directory.CreateDirectory(PLUGINS_ROOT);
        
        HOT_RELOAD_WATCHER = new(PLUGINS_ROOT);
        IS_INITIALIZED = true;
    }
    
    /// <summary>
    /// Try to load all plugins from the plugins directory.
    /// </summary>
    /// <remarks>
    /// Loading plugins means:<br/>
    /// - Parsing and checking the plugin code<br/>
    /// - Check for forbidden plugins<br/>
    /// - Creating a new instance of the allowed plugin<br/>
    /// - Read the plugin metadata<br/>
    /// <br/>
    /// Loading a plugin does not mean to start the plugin, though.
    /// </remarks>
    public static async Task LoadAll(CancellationToken cancellationToken = default)
    {
        if (!IS_INITIALIZED)
        {
            LOG.LogError("PluginFactory is not initialized. Please call Setup() before using it.");
            return;
        }
        
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
            if (cancellationToken.IsCancellationRequested)
                break;
            
            LOG.LogInformation($"Try to load plugin: {pluginMainFile}");
            var code = await File.ReadAllTextAsync(pluginMainFile, Encoding.UTF8, cancellationToken);
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
                    LOG.LogError($"Was not able to load plugin '{pluginMainFile}', because the Lua code is not a valid AI Studio plugin. There are {plugin.Issues.Count()} issues to fix.");
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
        
        // Start or restart all plugins:
        await RestartAllPlugins(cancellationToken);
    }

    private static async Task<PluginBase> Load(string pluginPath, string code, CancellationToken cancellationToken = default)
    {
        if(ForbiddenPlugins.Check(code) is { IsForbidden: true } forbiddenState)
            return new NoPlugin($"This plugin is forbidden: {forbiddenState.Message}");
        
        var state = LuaState.Create();
        
        // Add the module loader so that the plugin can load other Lua modules:
        state.ModuleLoader = new PluginLoader(pluginPath);
        
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
        
        var isInternal = pluginPath.StartsWith(INTERNAL_PLUGINS_ROOT, StringComparison.OrdinalIgnoreCase);
        return type switch
        {
            PluginType.LANGUAGE => new PluginLanguage(isInternal, state, type),
            
            _ => new NoPlugin("This plugin type is not supported yet. Please try again with a future version of AI Studio.")
        };
    }

    private static async Task RestartAllPlugins(CancellationToken cancellationToken = default)
    {
        LOG.LogInformation("Try to start or restart all plugins.");
        RUNNING_PLUGINS.Clear();

        //
        // Get the base language plugin. This is the plugin that will be used to fill in missing keys.
        //
        var baseLanguagePluginId = InternalPlugin.LANGUAGE_EN_US.MetaData().Id;
        var baseLanguagePluginMetaData = AVAILABLE_PLUGINS.FirstOrDefault(p => p.Id == baseLanguagePluginId);
        if (baseLanguagePluginMetaData is null)
        {
            LOG.LogError($"Was not able to find the base language plugin: Id='{baseLanguagePluginId}'. Please check your installation.");
            return;
        }

        var startedBasePlugin = await Start(baseLanguagePluginMetaData, cancellationToken);
        if (startedBasePlugin is NoPlugin noPlugin)
        {
            LOG.LogError($"Was not able to start the base language plugin: Id='{baseLanguagePluginId}'. Reason: {noPlugin.Issues.First()}");
            return;
        }
        
        if (startedBasePlugin is PluginLanguage languagePlugin)
        {
            BASE_LANGUAGE_PLUGIN = languagePlugin;
            LOG.LogInformation($"Successfully started the base language plugin: Id='{languagePlugin.Id}', Type='{languagePlugin.Type}', Name='{languagePlugin.Name}', Version='{languagePlugin.Version}'");
        }
        else
        {
            LOG.LogError($"Was not able to start the base language plugin: Id='{baseLanguagePluginId}'. Reason: {string.Join("; ", startedBasePlugin.Issues)}");
            return;
        }

        //
        // Iterate over all available plugins and try to start them.
        //
        foreach (var availablePlugin in AVAILABLE_PLUGINS)
        {
            if(cancellationToken.IsCancellationRequested)
                break;
            
            if (availablePlugin.Id == baseLanguagePluginId)
                continue;
            
            if (availablePlugin.IsInternal || SETTINGS_MANAGER.IsPluginEnabled(availablePlugin))
                if(await Start(availablePlugin, cancellationToken) is { IsValid: true } plugin)
                    RUNNING_PLUGINS.Add(plugin);

            // Inform all components that the plugins have been reloaded or started:
            await MessageBus.INSTANCE.SendMessage<bool>(null, Event.PLUGINS_RELOADED);
        }
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
            
            LOG.LogInformation($"Successfully started plugin: Id='{plugin.Id}', Type='{plugin.Type}', Name='{plugin.Name}', Version='{plugin.Version}'");
            return plugin;
        }

        LOG.LogError($"Was not able to start plugin: Id='{meta.Id}', Type='{meta.Type}', Name='{meta.Name}', Version='{meta.Version}'. Reasons: {string.Join("; ", plugin.Issues)}");
        return new NoPlugin($"Was not able to start plugin: Id='{meta.Id}', Type='{meta.Type}', Name='{meta.Name}', Version='{meta.Version}'. Reasons: {string.Join("; ", plugin.Issues)}");
    }
    
    public static void Dispose()
    {
        if(!IS_INITIALIZED)
            return;
        
        HOT_RELOAD_WATCHER.Dispose();
    }
}