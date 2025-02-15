namespace AIStudio.Settings.DataModel;

public static class SendToChatDataSourceBehaviorExtensions
{
    public static string Description(this SendToChatDataSourceBehavior behavior) => behavior switch
    {
        SendToChatDataSourceBehavior.NO_DATA_SOURCES => "Use no data sources, when sending an assistant result to a chat",
        SendToChatDataSourceBehavior.APPLY_STANDARD_CHAT_DATA_SOURCE_OPTIONS => "Apply standard chat data source options, when sending an assistant result to a chat",
        
        _ => "Unknown behavior",
    };
}