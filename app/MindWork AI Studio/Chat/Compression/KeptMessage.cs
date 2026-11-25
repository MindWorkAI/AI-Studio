namespace AIStudio.Chat.Compression;

/// <summary>
/// Whether a message the AI reads for the summary follows the summary in the new chat word for word.
/// </summary>
/// <remarks>
/// The AI reads the last exchange for the summary as well, so that the summary leads up to it, but
/// it must neither summarize nor repeat it, see ChatCompressionPlan.
/// </remarks>
public enum KeptMessage
{
    /// <summary>
    /// The summary replaces the message.
    /// </summary>
    NONE,

    /// <summary>
    /// The last message of the user, which follows the summary with its files.
    /// </summary>
    LAST_QUESTION,

    /// <summary>
    /// The last answer of the AI.
    /// </summary>
    LAST_ANSWER,
}