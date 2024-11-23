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
    /// Should we show the latest message after loading? When false, we show the first (aka oldest) message.
    /// </summary>
    public bool ShowLatestMessageAfterLoading { get; set; } = true;
}