namespace AIStudio.Tools.PluginSystem;

public static class PluginTypeExtensions
{
    public static string GetName(this PluginType type) => type switch
    {
        PluginType.LANGUAGE => "Language plugin",
        PluginType.ASSISTANT => "Assistant plugin",
        PluginType.CONFIGURATION => "Configuration plugin",
        PluginType.THEME => "Theme plugin",
        
        _ => "Unknown plugin type",
    };
}