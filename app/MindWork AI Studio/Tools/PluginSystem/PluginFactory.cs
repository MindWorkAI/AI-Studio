using AIStudio.Settings;

using Lua;

namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger("PluginFactory");
    private static readonly string DATA_DIR = SettingsManager.DataDirectory!;
    private static readonly string PLUGINS_ROOT = Path.Join(DATA_DIR, "plugins");
    
    public static async Task LoadAll()
    {
        
    }
    
    public static async Task<PluginBase> Load(string path, string code, CancellationToken cancellationToken = default)
    {
        if(ForbiddenPlugins.Check(code) is { IsForbidden: true } forbiddenState)
            return new NoPlugin($"This plugin is forbidden: {forbiddenState.Message}");
        
        var state = LuaState.Create();

        try
        {
            await state.DoStringAsync(code, cancellationToken: cancellationToken);
        }
        catch (LuaParseException e)
        {
            return new NoPlugin($"Was not able to parse the plugin: {e.Message}");
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