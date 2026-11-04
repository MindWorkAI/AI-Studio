namespace AIStudio.Tools.PluginSystem;

public static class PluginTypeExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PluginTypeExtensions).Namespace, nameof(PluginTypeExtensions));
    
    extension(PluginType type)
    {
        public string GetName() => type switch
        {
            PluginType.LANGUAGE => TB("Language plugin"),
            PluginType.ASSISTANT => TB("Assistant plugin"),
            PluginType.CONFIGURATION => TB("Configuration plugin"),
            PluginType.THEME => TB("Theme plugin"),
            PluginType.MODEL => TB("Model plugin"),

            _ => TB("Unknown plugin type"),
        };

        public string GetDirectory() => type switch
        {
            PluginType.LANGUAGE => "languages",
            PluginType.ASSISTANT => "assistants",
            PluginType.CONFIGURATION => "configurations",
            PluginType.THEME => "themes",
            PluginType.MODEL => "models",

            _ => "unknown",
        };
    }
}