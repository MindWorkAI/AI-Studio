using AIStudio.Settings.DataModel;

namespace AIStudio.Chat.Compression;

/// <summary>
/// Decides what happens with a message which would fill the context window of the model, as the settings say.
/// </summary>
/// <remarks>
/// Pure functions, shared by the chat, the embedded chat of an assistant, and the assistants, so that all of them
/// follow the same rules, see DataChatCompression.ContextWindowFullBehavior.
/// </remarks>
public static class ContextWindowFullHandling
{
    /// <summary>
    /// Decides whether a chat asks, sends, or compresses before it sends a message which would fill the context window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sending without asking needs a place which may send the message at all: the embedded chat of an assistant
    /// sends no message which no longer fits, so it asks there.
    /// </para>
    /// <para>
    /// Compressing without asking needs a compressing which helps, see ChatCompressionService.OfferBeforeSendingAsync.
    /// Otherwise, the chat asks, and the dialog tells why compressing would not help. An assistant compresses
    /// nothing, so it asks as well. After compressing, the chat decides once more without compressing again, so
    /// that a summary which leaves the chat too full ends in a question instead of a loop.
    /// </para>
    /// </remarks>
    /// <param name="behavior">What the settings say, read once for this decision.</param>
    /// <param name="offer">Whether compressing helps, or why not; NONE where nothing compresses.</param>
    /// <param name="canSendAnyway">Whether the place may send the message although it fills the window.</param>
    /// <param name="mayCompressAutomatically">False when the chat was compressed for this message already.</param>
    /// <returns>What the chat does with the message.</returns>
    public static ContextWindowFullAction Decide(ContextWindowFullBehavior behavior, ChatCompressionOffer offer, bool canSendAnyway, bool mayCompressAutomatically) => behavior switch
    {
        ContextWindowFullBehavior.SEND_WITHOUT_ASKING when canSendAnyway => ContextWindowFullAction.SEND,
        ContextWindowFullBehavior.COMPRESS_INTO_NEW_CHAT or ContextWindowFullBehavior.COMPRESS_AND_REPLACE when mayCompressAutomatically && offer is ChatCompressionOffer.OFFERED => ContextWindowFullAction.COMPRESS,

        _ => ContextWindowFullAction.ASK,
    };

    /// <summary>
    /// Decides where the conversation of the chat page goes on once it is compressed before sending.
    /// </summary>
    /// <remarks>
    /// Without workspaces, the chat is never stored, so there is no new chat to switch to, and the summary takes the
    /// place of the earlier messages. Otherwise, the settings decide; asking continues in a new chat, as the button
    /// of the chat does. The embedded chat of an assistant always compresses in place, see EmbeddedChat.
    /// </remarks>
    /// <param name="behavior">What the settings say, read once for this decision.</param>
    /// <param name="workspacesDisabled">Whether the user switched off the workspaces.</param>
    /// <returns>Where the conversation goes on.</returns>
    public static ChatCompressionTarget TargetFor(ContextWindowFullBehavior behavior, bool workspacesDisabled) => workspacesDisabled || behavior is ContextWindowFullBehavior.COMPRESS_AND_REPLACE
        ? ChatCompressionTarget.SAME_CHAT
        : ChatCompressionTarget.NEW_CHAT;
}