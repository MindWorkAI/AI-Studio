using System.Text;

using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace AIStudio.Tools;

public static class PlainFileExport
{
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(PlainFileExport));

    private static string TB(string fallbackEn) => I18N.I.T(fallbackEn, typeof(PlainFileExport).Namespace, nameof(PlainFileExport));

    /// <summary>
    /// Reads every file a message holds, in the order they appear in it.
    /// </summary>
    /// <remarks>
    /// Two kinds of files end up in an answer. Almost always it is a Markdown table written with
    /// pipes, which is what a model produces on its own; we turn its cells into a file. Besides,
    /// a model answers with a fenced code block marked as a format we write, such as html, latex,
    /// markdown, or csv, whenever it was asked for a web page, a document, or data. Such a block
    /// already is the finished file: we hand it through untouched rather than taking it apart and
    /// reassembling it. We do not judge what the block holds, either. A browser shows a fragment of
    /// HTML just as well as an entire page, and a LaTeX fragment is still what the user asked for.
    /// </remarks>
    /// <param name="markdown">The Markdown text of the message.</param>
    /// <param name="separator">The separator to write a Markdown table with, see CsvWriter.SeparatorFor.</param>
    /// <returns>The files, or an empty list when the message holds none.</returns>
    public static IReadOnlyList<MessageFile> ExtractFiles(string markdown, char separator)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return [];

        //
        // We let Markdig do the reading. It is already part of the app, the pipeline we reuse has
        // table support switched on, and it knows every corner of the syntax that a regular
        // expression of ours would have to learn one bug at a time.
        //
        var document = Markdig.Markdown.Parse(markdown, Markdown.SAFE_MARKDOWN_PIPELINE);

        //
        // What a file is about stands above it, not in it: models introduce their tables and code
        // blocks with a heading. We remember every heading with its line so that each file can take
        // the last one before it. A table falls back to its own first column heading when there is
        // none; a code block has nothing comparable and stays without a caption.
        //
        var headings = document.Descendants<HeadingBlock>()
            .Select(heading => (heading.Line, Text: ToPlainText(heading)))
            .Where(heading => !string.IsNullOrWhiteSpace(heading.Text))
            .OrderBy(heading => heading.Line)
            .ToList();

        var tables = document.Descendants<Table>()
            .Select(table => (table.Line, Content: ToContent(table, separator)));

        var codeBlocks = document.Descendants<FencedCodeBlock>()
            .Select(block => (block.Line, Content: ToContent(block)));

        //
        // Tables and code blocks are counted apart. The menu falls back to that number when a
        // heading cannot tell two files apart, and "Table 2" has to be the second table of the
        // answer, not the second entry of the menu.
        //
        var numberOfTables = 0;
        var numberOfCodeBlocks = 0;
        return tables.Concat(codeBlocks)
            .Where(entry => entry.Content is not null)
            .OrderBy(entry => entry.Line)
            .Select(entry => new MessageFile(
                entry.Content!.Value.Format.IsTabular() ? ++numberOfTables : ++numberOfCodeBlocks,
                Caption: HeadingAbove(entry.Line) is { Length: > 0 } heading ? heading : entry.Content!.Value.Fallback,
                entry.Content!.Value.Format,
                entry.Content.Value.Text))
            .ToList();

        string HeadingAbove(int line) => headings.LastOrDefault(heading => heading.Line < line).Text ?? string.Empty;
    }

    /// <summary>
    /// Turns a Markdown table into a file.
    /// </summary>
    private static (string Fallback, FileExportFormat Format, string Text)? ToContent(Table table, char separator)
    {
        var rows = table.OfType<TableRow>()
            .Select(row => row.OfType<TableCell>().Select(ToPlainText).ToArray())
            .Where(fields => fields.Length > 0)
            .ToList();

        if (rows.Count is 0)
            return null;

        var text = new StringBuilder();
        foreach (var fields in rows)
            text.AppendLine(CsvWriter.ToRow(separator, fields));

        return (rows[0].FirstOrDefault() ?? string.Empty, FileExportFormat.CSV, text.ToString());
    }

    /// <summary>
    /// Turns a fenced code block into a file, when the model marked it as a format we write.
    /// </summary>
    /// <remarks>
    /// A block the model never closed is left out. That happens when an answer broke off, at the
    /// output limit of the model for example, and the file would end wherever the answer did: half
    /// a web page or half a table is nothing anybody wants to save.
    /// </remarks>
    private static (string Fallback, FileExportFormat Format, string Text)? ToContent(FencedCodeBlock block)
    {
        if (block.ClosingFencedCharCount is 0 || !FileExportFormatExtensions.TryFromCodeFenceLanguage(block.Info, out var format))
            return null;

        var content = block.Lines.ToString();
        if (!format.IsTabular())
            return (string.Empty, format, content);

        var blockSeparator = format is FileExportFormat.TSV ? '\t' : ',';
        var firstLine = content.AsSpan();
        var lineEnd = firstLine.IndexOf('\n');
        if (lineEnd >= 0)
            firstLine = firstLine[..lineEnd];

        var separatorPosition = firstLine.IndexOf(blockSeparator);
        var fallback = (separatorPosition >= 0 ? firstLine[..separatorPosition] : firstLine).Trim().Trim('"').ToString();

        return (fallback, format, content);
    }

    /// <summary>
    /// Reads the text of a table cell or a heading, without the Markdown which decorates it.
    /// </summary>
    /// <remarks>
    /// A spreadsheet has no use for the asterisks around a bold number: they would keep it from
    /// being recognized as a number. So we keep what a reader would read and drop the rest.
    /// </remarks>
    private static string ToPlainText(MarkdownObject container)
    {
        //
        // A leaf block, a heading for example, keeps its text in an inline container of its own.
        // Asking the block itself for its descendants walks its child blocks, and a leaf block has
        // none, so we would get nothing back. A table cell is a container block and needs the
        // opposite: its text sits in the paragraphs below it.
        //
        var inlines = container is LeafBlock leafBlock
            ? leafBlock.Inline?.Descendants<LeafInline>() ?? []
            : container.Descendants<LeafInline>();

        var text = new StringBuilder();
        foreach (var inline in inlines)
            switch (inline)
            {
                case CodeInline code:
                    text.Append(code.Content);
                    break;

                case LiteralInline literal:
                    text.Append(literal.Content.AsSpan());
                    break;

                case HtmlEntityInline entity:
                    text.Append(entity.Transcoded.AsSpan());
                    break;

                case AutolinkInline autolink:
                    text.Append(autolink.Url);
                    break;

                // A cell holds one line in a file, so a line break inside it becomes a space:
                case LineBreakInline:
                    text.Append(' ');
                    break;
            }

        return text.ToString().Trim();
    }

    /// <summary>
    /// Writes the given text to a plain text file as it is and lets the user save it.
    /// </summary>
    /// <remarks>
    /// Nothing is converted here, which is what sets this apart from PandocExport.ToDocument. A web
    /// page or a LaTeX document the model wrote is a finished file already and comes through here;
    /// an entire answer in one of these formats is Markdown and goes to Pandoc instead.
    /// </remarks>
    /// <param name="rustService">The Rust service, used for the save dialog.</param>
    /// <param name="dialogTitle">The title of the save dialog. The caller knows what the user is
    /// looking at, a chat message or the result of an assistant, so the caller names it.</param>
    /// <param name="format">The format to write. Must be a plain text format, see
    /// FileExportFormatExtensions.IsPlainText.</param>
    /// <param name="fileContent">The finished file. The caller decides whether that is the entire
    /// message or one file out of it.</param>
    /// <param name="fileName">What the file is about, used to suggest a name in the save dialog.
    /// Null falls back to a generic name.</param>
    /// <returns>True, when the file was written.</returns>
    public static async Task<bool> ToFile(RustService rustService, string dialogTitle, FileExportFormat format, string fileContent, string? fileName = null)
    {
        if (!format.IsPlainText() || format.ToFileTypeFilter() is not { } fileTypeFilter)
            throw new ArgumentOutOfRangeException(nameof(format), format, "AI Studio cannot write this format itself.");

        var response = await rustService.SaveFile(dialogTitle, [fileTypeFilter], format.ToSuggestedFileName(fileName));
        if (response.UserCancelled)
        {
            LOGGER.LogInformation("User cancelled the save dialog.");
            return false;
        }

        LOGGER.LogInformation("The user chose the path '{SaveFilePath}' for the {ExportFormat} export.", response.SaveFilePath, format);

        try
        {
            await File.WriteAllTextAsync(response.SaveFilePath, fileContent, format.ToFileEncoding());
            await MessageBus.INSTANCE.SendSuccess(new(Icons.Material.Filled.CheckCircle, TB("The export succeeded.")));

            return true;
        }
        catch (Exception ex)
        {
            LOGGER.LogError(ex, "Error during {ExportFormat} export.", format);
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("The export failed.")));
            return false;
        }
    }
}