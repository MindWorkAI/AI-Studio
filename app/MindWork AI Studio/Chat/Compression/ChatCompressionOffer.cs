namespace AIStudio.Chat.Compression;

/// <summary>
/// Whether the notice before sending offers to compress the chat, or why compressing would not help, see
/// ChatCompressionService.OfferBeforeSendingAsync.
/// </summary>
/// <remarks>
/// The reasons are named, so that the notice can tell them: the user guide recommends compressing, and a notice which
/// leaves it out without a word looks like a mistake.
/// </remarks>
public enum ChatCompressionOffer
{
    /// <summary>
    /// The place compresses nothing at all, e.g., an assistant, a chat without workspaces, or a provider which the chat
    /// may not use; the notice says nothing about compressing.
    /// </summary>
    NONE,

    /// <summary>
    /// Compressing makes room for the message about to be sent.
    /// </summary>
    OFFERED,

    /// <summary>
    /// The model is too small to write a summary, see ConversationSummaryBudget.MIN_CONTEXT_WINDOW.
    /// </summary>
    WINDOW_TOO_SMALL,

    /// <summary>
    /// The message about to be sent nearly fills the context window by itself, and it goes along unchanged.
    /// </summary>
    MESSAGE_TOO_LONG,

    /// <summary>
    /// Before the last message of the user and the last answer of the AI, there is nothing to summarize.
    /// </summary>
    NOTHING_TO_SUMMARIZE,

    /// <summary>
    /// What compressing keeps word for word, such as the last message of the user with its files, the last answer of
    /// the AI, and the system prompt, still nearly fills the context window together with the message about to be sent.
    /// </summary>
    KEEPS_TOO_MUCH,
}
