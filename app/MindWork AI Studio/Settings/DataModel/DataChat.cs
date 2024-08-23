namespace AIStudio.Settings.DataModel;

public sealed class DataChat
{
    /// <summary>
    /// Shortcuts to send the input to the AI.
    /// </summary>
    public SendBehavior ShortcutSendBehavior { get; set; } = SendBehavior.ENTER_IS_SENDING;

    /// <summary>
    /// Preselect any chat options?
    /// </summary>
    public bool PreselectOptions { get; set; }

    /// <summary>
    /// Should we preselect a provider for the chat?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;

    /// <summary>
    /// Should we show the latest message after loading? When false, we show the first (aka oldest) message.
    /// </summary>
    public bool ShowLatestMessageAfterLoading { get; set; } = true;
}