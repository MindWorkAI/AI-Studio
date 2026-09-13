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
    /// a caller which writes files must not put an excuse into the file it writes. This is the text
    /// the model wrote and nothing else: whoever reads a table out of a message wants exactly that,
    /// while whoever writes a file wants the sources along with it and asks for the export reading.
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

    /// <summary>
    /// Reads this content the way it leaves AI Studio, as a file or through the clipboard.
    /// </summary>
    /// <remarks>
    /// What the user sees is the answer together with the sources AI Studio collected for it, and
    /// that is what a document has to hold as well: an answer built on a web page a tool read, or on
    /// a document of the user, is worth little when the reader cannot tell which one it was. Those
    /// sources are not part of the text the model wrote, they hang on the content, which is why
    /// every path out of the app asks for this and not for the text alone.
    /// </remarks>
    /// <param name="content">The content to read.</param>
    /// <param name="markdown">The Markdown text including its sources, or an empty string when there is none.</param>
    /// <returns>True, when this content carries Markdown text.</returns>
    public static bool TryGetExportMarkdown(this IContent content, out string markdown)
    {
        if (content is not ContentText text)
        {
            markdown = string.Empty;
            return false;
        }

        var answer = text.Text.Trim();
        var sources = text.Sources.ToExportMarkdown();
        if (sources.Length == 0)
        {
            markdown = answer;
            return true;
        }

        if (answer.Length == 0)
        {
            markdown = sources;
            return true;
        }

        //
        // The blank line is not cosmetic: it ends a paragraph, a list, a table, or a block quote, so
        // that the heading of the source list stands on its own instead of being pulled into the
        // last block of the answer.
        //
        markdown = $"{Markdown.CloseOpenCodeFence(answer)}{Environment.NewLine}{Environment.NewLine}{sources}";
        return true;
    }
}