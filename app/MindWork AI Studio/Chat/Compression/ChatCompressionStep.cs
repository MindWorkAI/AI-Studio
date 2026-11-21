namespace AIStudio.Chat.Compression;

/// <summary>
/// What compressing a chat is busy with, see ChatCompressionService.SummarizeAsync.
/// </summary>
public enum ChatCompressionStep
{
    /// <summary>
    /// AI Studio reads the messages to summarize and the documents attached to them.
    /// </summary>
    READING_FILES,

    /// <summary>
    /// The AI summarizes the conversation, one part after the other.
    /// </summary>
    SUMMARIZING,

    /// <summary>
    /// The AI shortens a summary which grew too long, see ChatCompressionService.IsTooLong.
    /// </summary>
    CONDENSING,
}