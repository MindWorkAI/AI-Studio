namespace AIStudio.Settings.DataModel;

public sealed class DataChat
{
    /// <summary>
    /// Shortcuts to send the input to the AI.
    /// </summary>
    public SendBehavior ShortcutSendBehavior { get; set; } = SendBehavior.ENTER_IS_SENDING;

    /// <summary>
    /// Defines the provider behavior for loading a chat.
    /// </summary>
    public LoadingChatProviderBehavior LoadingProviderBehavior { get; set; } = LoadingChatProviderBehavior.USE_CHAT_PROVIDER_IF_AVAILABLE;

    /// <summary>
    /// Defines the provider behavior when adding a chat.
    /// </summary>
    public AddChatProviderBehavior AddChatProviderBehavior { get; set; } = AddChatProviderBehavior.ADDED_CHATS_USE_LATEST_PROVIDER;

    /// <summary>
    /// Defines the data source behavior when sending assistant results to a chat.
    /// </summary>
    public SendToChatDataSourceBehavior SendToChatDataSourceBehavior { get; set; } = SendToChatDataSourceBehavior.NO_DATA_SOURCES;

    /// <summary>
    /// Preselect any chat options?
    /// </summary>
    public bool PreselectOptions { get; set; }

    /// <summary>
    /// Should we preselect a provider for the chat?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect a profile?
    /// </summary>
    public string PreselectedProfile { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect a chat template?
    /// </summary>
    public string PreselectedChatTemplate { get; set; } = string.Empty;
    
    /// <summary>
    /// Should we preselect data sources options for a created chat?
    /// </summary>
    public DataSourceOptions PreselectedDataSourceOptions { get; set; } = new();

    /// <summary>
    /// Should we show the latest message after loading? When false, we show the first (aka oldest) message.
    /// </summary>
    public bool ShowLatestMessageAfterLoading { get; set; } = true;
}