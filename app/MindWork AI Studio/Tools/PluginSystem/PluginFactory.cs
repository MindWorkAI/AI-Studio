using System.Text;

using Lua;

namespace AIStudio.Tools.PluginSystem;

public static class PluginFactory
{
    public static async Task LoadAll()
    {
        
    }
    
    public static async Task<PluginBase> Load(string code, CancellationToken cancellationToken = default)
    {
        var state = LuaState.Create();

        try
        {
            await state.DoStringAsync(code, cancellationToken: cancellationToken);
        }
        catch (LuaParseException e)
        {
            return new NoPlugin(state, $"Was not able to parse the plugin: {e.Message}");
        }
        
        if (!state.Environment["TYPE"].TryRead<string>(out var typeText))
            return new NoPlugin(state, "TYPE does not exist or is not a valid string.");
        
        if (!Enum.TryParse<PluginType>(typeText, out var type))
            return new NoPlugin(state, $"TYPE is not a valid plugin type. Valid types are: {CommonTools.GetAllEnumValues<PluginType>()}");
        
        return type switch
        {
            PluginType.LANGUAGE => new PluginLanguage(state, type),
            
            _ => new NoPlugin(state, "This plugin type is not supported yet. Please try again with a future version of AI Studio.")
        };
    }
}