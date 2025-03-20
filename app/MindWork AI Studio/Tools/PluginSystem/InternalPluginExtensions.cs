namespace AIStudio.Tools.PluginSystem;

public static class InternalPluginExtensions
{
    public static InternalPluginData MetaData(this InternalPlugin plugin) => plugin switch
    {
        InternalPlugin.LANGUAGE_EN_US => new (PluginType.LANGUAGE, new("97dfb1ba-50c4-4440-8dfa-6575daf543c8"), "en-us"),
        InternalPlugin.LANGUAGE_DE_DE => new(PluginType.LANGUAGE, new("43065dbc-78d0-45b7-92be-f14c2926e2dc"), "de-de"),
        
        _ => new InternalPluginData(PluginType.NONE, Guid.Empty, "unknown")
    };
}