using System.Text;

using AIStudio.Settings;

using Lua;
using Lua.Standard;

namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger("PluginFactory");
    private static readonly string DATA_DIR = SettingsManager.DataDirectory!;
    private static readonly string PLUGINS_ROOT = Path.Join(DATA_DIR, "plugins");
    private static readonly Dictionary<IPluginMetadata, string> AVAILABLE_PLUGINS = new();
    private static readonly SettingsManager SETTINGS = Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();
    
    /// <summary>
    /// A list of all available plugins.
    /// </summary>
    public static IEnumerable<IPluginMetadata> AvailablePlugins => AVAILABLE_PLUGINS.Keys;
    
    /// <summary>
    /// A list of all enabled plugins.
    /// </summary>
    public static IEnumerable<IPluginMetadata> EnabledPlugins => AVAILABLE_PLUGINS.Keys.Where(x => SETTINGS.ConfigurationData.EnabledPlugins.Contains(x.Id));
    
    /// <summary>
    /// A list of all disabled plugins.
    /// </summary>
    public static IEnumerable<IPluginMetadata> DisabledPlugins => AVAILABLE_PLUGINS.Keys.Where(x => !SETTINGS.ConfigurationData.EnabledPlugins.Contains(x.Id));
    
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
        LOG.LogInformation("Start loading plugins.");
        if (!Directory.Exists(PLUGINS_ROOT))
        {
            LOG.LogInformation("No plugins found.");
            return;
        }
        
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
            var plugin = await Load(pluginMainFile, code, cancellationToken);
            
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
                    continue;

                case { IsMaintained: false }:
                    LOG.LogWarning($"The plugin '{pluginMainFile}' is not maintained anymore. Please consider to disable it.");
                    break;
            }
            
            LOG.LogInformation($"Successfully loaded plugin: '{pluginMainFile}' (Id='{plugin.Id}', Type='{plugin.Type}', Name='{plugin.Name}', Version='{plugin.Version}', Authors='{string.Join(", ", plugin.Authors)}')");
            AVAILABLE_PLUGINS.Add(new PluginMetadata(plugin), pluginMainFile);
        }
    }
    
    public static async Task<PluginBase> Load(string path, string code, CancellationToken cancellationToken = default)
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
        
        return type switch
        {
            PluginType.LANGUAGE => new PluginLanguage(path, state, type),
            
            _ => new NoPlugin("This plugin type is not supported yet. Please try again with a future version of AI Studio.")
        };
    }
}