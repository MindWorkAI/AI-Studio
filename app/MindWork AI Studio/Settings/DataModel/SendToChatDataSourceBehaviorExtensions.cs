using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

public static class SendToChatDataSourceBehaviorExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(SendToChatDataSourceBehaviorExtensions).Namespace, nameof(SendToChatDataSourceBehaviorExtensions));
    
    public static string Description(this SendToChatDataSourceBehavior behavior) => behavior switch
    {
        SendToChatDataSourceBehavior.NO_DATA_SOURCES => TB("Use no data sources, when sending an assistant result to a chat"),
        SendToChatDataSourceBehavior.APPLY_STANDARD_CHAT_DATA_SOURCE_OPTIONS => TB("Apply standard chat data source options, when sending an assistant result to a chat"),
        
        _ => TB("Unknown behavior"),
    };
}