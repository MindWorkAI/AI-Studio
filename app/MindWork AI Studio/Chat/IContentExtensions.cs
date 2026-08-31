namespace AIStudio.Chat;

public static class IContentExtensions
{
    /// <summary>
    /// Detaches whoever listens to the stream of this content.
    /// </summary>
    /// <remarks>
    /// The streaming handlers are closures over the component which registered them. A content
    /// object belongs to the chat thread and therefore outlives every component which renders it,
    /// so handlers left behind would keep those components alive for as long as the thread exists.
    /// Whoever registers a handler calls this when it is no longer needed.
    /// </remarks>
    /// <param name="content">The content whose streaming handlers you want to detach.</param>
    public static void ResetStreamingHandlers(this IContent content)
    {
        content.StreamingEvent = IContent.NO_STREAMING_HANDLER;
        content.StreamingDone = IContent.NO_STREAMING_HANDLER;
    }

    /// <summary>
    /// Reads this content as the Markdown text the AI produced.
    /// </summary>
    /// <remarks>
    /// Only text content carries Markdown. Everything else, an image for example, has no text
    /// representation at all, which is why this reports failure instead of returning a placeholder:
    /// a caller which writes files must not put an excuse into the file it writes.
    /// </remarks>
    /// <param name="content">The content to read.</param>
    /// <param name="markdown">The Markdown text, or an empty string when there is none.</param>
    /// <returns>True, when this content carries Markdown text.</returns>
    public static bool TryGetMarkdownText(this IContent content, out string markdown)
    {
        if (content is ContentText text)
        {
            markdown = text.Text;
            return true;
        }

        markdown = string.Empty;
        return false;
    }
}