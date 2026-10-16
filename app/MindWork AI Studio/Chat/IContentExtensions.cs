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
    /// <param name="keepPageAnchors">Whether a link into a local file may name its page. Only a
    /// format whose reader stumbles over such a link says no here; the clipboard and every text
    /// format keep the page.</param>
    /// <returns>True, when this content carries Markdown text.</returns>
    public static bool TryGetExportMarkdown(this IContent content, out string markdown, bool keepPageAnchors = true)
    {
        if (content is not ContentText text)
        {
            markdown = string.Empty;
            return false;
        }

        markdown = AppendSources(text.Text.Trim(), text.Sources.ToExportMarkdown(keepPageAnchors));
        return true;
    }

    /// <summary>
    /// Reads one file out of this content the way it leaves AI Studio, together with the sources
    /// the answer rests on.
    /// </summary>
    /// <remarks>
    /// A code block saved on its own came out of the same answer, so it rests on the same sources
    /// and takes them along. How depends on the format. Markdown is what the source list is written
    /// in, so a Markdown text gets it just as the entire answer does. A web page or a LaTeX document
    /// gets it as a comment at its end: anything visible would have to be woven into markup the
    /// model wrote. A fragment has no body to put it in, a page may hide whatever lies outside its
    /// layout, and one underscore in a title is enough to stop a LaTeX run. A comment breaks
    /// neither, and whoever opens the file finds it. A table gets no sources at all, since it has
    /// no column a link list would fit into.
    ///
    /// Apart from that, the file is what the model wrote, scripts of a web page included. Saving it
    /// is what the user chose to do; the chat still never renders it.
    /// </remarks>
    /// <param name="content">The content the file was found in.</param>
    /// <param name="file">The file, as PlainFileExport.ExtractFiles read it out of this content.</param>
    /// <returns>The content of the file to write.</returns>
    public static string ToExportContent(this IContent content, MessageFile file)
    {
        if (file.Format.IsTabular())
            return file.Content;

        var sources = content.Sources.ToExportMarkdown(file.Format.FollowsPageAnchors());
        if (file.Format is FileExportFormat.MARKDOWN)
            return AppendSources(file.Content, sources);

        if (sources.Length is 0 || !file.Format.TryToComment(sources, out var comment))
            return file.Content;

        return $"{file.Content}{Environment.NewLine}{Environment.NewLine}{comment}";
    }

    /// <summary>
    /// Puts the source list below a Markdown text.
    /// </summary>
    /// <param name="markdown">The Markdown text.</param>
    /// <param name="sources">The source list as SourceExtensions.ToExportMarkdown writes it, or an
    /// empty string when there are no sources.</param>
    /// <returns>The text followed by its sources.</returns>
    private static string AppendSources(string markdown, string sources)
    {
        if (sources.Length == 0)
            return markdown;

        if (markdown.Length == 0)
            return sources;

        //
        // The blank line is not cosmetic: it ends a paragraph, a list, a table, or a block quote, so
        // that the heading of the source list stands on its own instead of being pulled into the
        // last block of the answer.
        //
        return $"{Markdown.CloseOpenCodeFence(markdown)}{Environment.NewLine}{Environment.NewLine}{sources}";
    }
}