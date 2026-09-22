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
    /// Reads every table a message holds, in the order they appear in it.
    /// </summary>
    /// <remarks>
    /// Two kinds of tables end up in an answer. Almost always it is a Markdown table written with
    /// pipes, which is what a model produces on its own; we turn its cells into a file. Rarely a
    /// model answers with a fenced code block marked as csv or tsv, which already is the finished
    /// file: we hand that through untouched rather than taking it apart and reassembling it.
    /// </remarks>
    /// <param name="markdown">The Markdown text of the message.</param>
    /// <param name="separator">The separator to write a Markdown table with, see CsvWriter.SeparatorFor.</param>
    /// <returns>The tables, or an empty list when the message holds none.</returns>
    public static IReadOnlyList<MessageTable> ExtractTables(string markdown, char separator)
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
        // What a table is about stands above it, not in it: models introduce their tables with a
        // heading. We remember every heading with its line so that each table can take the last
        // one before it, and fall back to its own first column heading when there is none.
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

        return tables.Concat(codeBlocks)
            .Where(entry => entry.Content is not null)
            .OrderBy(entry => entry.Line)
            .Select((entry, index) => new MessageTable(
                index + 1,
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
    /// Turns a fenced code block into a file, when the model marked it as tabular data.
    /// </summary>
    private static (string Fallback, FileExportFormat Format, string Text)? ToContent(FencedCodeBlock block)
    {
        var format = block.Info?.Trim() switch
        {
            "csv" => FileExportFormat.CSV,
            "tsv" => FileExportFormat.TSV,

            _ => FileExportFormat.NONE,
        };

        if (format is FileExportFormat.NONE)
            return null;

        var content = block.Lines.ToString();
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
    /// Writes the given text to a plain text file and lets the user save it.
    /// </summary>
    /// <param name="rustService">The Rust service, used for the save dialog.</param>
    /// <param name="dialogTitle">The title of the save dialog. The caller knows what the user is
    /// looking at, a chat message or the result of an assistant, so the caller names it.</param>
    /// <param name="format">The format to write. Must be a format which does not use Pandoc.</param>
    /// <param name="fileContent">What to write. The caller decides whether that is the entire
    /// message or one table out of it.</param>
    /// <param name="fileName">What the file is about, used to suggest a name in the save dialog.
    /// Null falls back to a generic name.</param>
    /// <returns>True, when the file was written.</returns>
    public static async Task<bool> ToFile(RustService rustService, string dialogTitle, FileExportFormat format, string fileContent, string? fileName = null)
    {
        if (format.UsesPandoc() || format.ToFileTypeFilter() is not { } fileTypeFilter)
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