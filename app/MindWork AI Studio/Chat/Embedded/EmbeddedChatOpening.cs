namespace AIStudio.Chat.Embedded;

/// <summary>
/// Decides when an embedded chat sends the message it opens with, see EmbeddedChat.OpeningMessage.
/// </summary>
public static class EmbeddedChatOpening
{
    /// <summary>
    /// Tells whether an embedded chat sends its opening message now.
    /// </summary>
    /// <remarks>
    /// The message may arrive after the chat loaded, e.g., when the parent learns it only then: it is sent all the
    /// same, as long as the user wrote nothing in the chat meanwhile. It is sent once per chat, also when sending
    /// fails and it waits in the field instead.
    /// </remarks>
    /// <param name="openingMessage">The message the chat opens with, or null without one.</param>
    /// <param name="conversation">The chat shown, or null while it loads.</param>
    /// <param name="wasSent">Whether the chat sent its opening message already.</param>
    /// <returns>True when the chat sends the message now.</returns>
    public static bool SendsNow(ContentText? openingMessage, EmbeddedConversation? conversation, bool wasSent) =>
        openingMessage is not null && !wasSent && conversation is { IsAvailable: true, IsRunning: false, UserMessageCount: 0 };
}