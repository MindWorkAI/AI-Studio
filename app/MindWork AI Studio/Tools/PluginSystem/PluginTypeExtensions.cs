namespace AIStudio.Tools.PluginSystem;

public static class PluginTypeExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PluginTypeExtensions).Namespace, nameof(PluginTypeExtensions));
    
    public static string GetName(this PluginType type) => type switch
    {
        PluginType.LANGUAGE => TB("Language plugin"),
        PluginType.ASSISTANT => TB("Assistant plugin"),
        PluginType.CONFIGURATION => TB("Configuration plugin"),
        PluginType.THEME => TB("Theme plugin"),
        
        _ => TB("Unknown plugin type"),
    };
    
    public static string GetDirectory(this PluginType type) => type switch
    {
        PluginType.LANGUAGE => "languages",
        PluginType.ASSISTANT => "assistants",
        PluginType.CONFIGURATION => "configurations",
        PluginType.THEME => "themes",
        
        _ => "unknown",
    };
}