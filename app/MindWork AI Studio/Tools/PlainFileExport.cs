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
    /// pipes, which is what a model produces on its own; we turn its cells into a file and offer
    /// both separators, because a comma collides with the decimal comma of German numbers. Rarely
    /// a model answers with a fenced code block marked as csv or tsv, which already is the
    /// finished file: we hand that through untouched rather than taking it apart and reassembling it.
    /// </remarks>
    /// <param name="markdown">The Markdown text of the message.</param>
    /// <returns>The tables, or an empty list when the message holds none.</returns>
    public static IReadOnlyList<MessageTable> ExtractTables(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return [];

        //
        // We let Markdig do the reading. It is already part of the app, the pipeline we reuse has
        // table support switched on, and it knows every corner of the syntax that a regular
        // expression of ours would have to learn one bug at a time.
        //
        var document = Markdig.Markdown.Parse(markdown, Markdown.SAFE_MARKDOWN_PIPELINE);

        var tables = document.Descendants<Table>()
            .Select(table => (table.Line, Contents: ToContents(table)));

        var codeBlocks = document.Descendants<FencedCodeBlock>()
            .Select(block => (block.Line, Contents: ToContents(block)));

        //
        // The ordinal counts the tables of the message, not the entries of the menu, so the two
        // entries of one Markdown table share it. That is what lets the menu name a table even
        // when another one in the same answer starts with the same heading.
        //
        return tables.Concat(codeBlocks)
            .Where(entry => entry.Contents.Count > 0)
            .OrderBy(entry => entry.Line)
            .SelectMany((entry, index) => entry.Contents.Select(content => new MessageTable(index + 1, content.Caption, content.Format, content.Content)))
            .ToList();
    }

    /// <summary>
    /// Turns a Markdown table into one file per separator we offer.
    /// </summary>
    private static IReadOnlyList<(string Caption, FileExportFormat Format, string Content)> ToContents(Table table)
    {
        var rows = table.OfType<TableRow>()
            .Select(row => row.OfType<TableCell>().Select(ToPlainText).ToArray())
            .Where(fields => fields.Length > 0)
            .ToList();

        if (rows.Count is 0)
            return [];

        var caption = rows[0].FirstOrDefault() ?? string.Empty;

        return
        [
            (caption, FileExportFormat.CSV, ToDelimitedText(rows, ',')),
            (caption, FileExportFormat.TSV, ToDelimitedText(rows, '\t')),
        ];
    }

    /// <summary>
    /// Turns a fenced code block into a file, when the model marked it as tabular data.
    /// </summary>
    private static IReadOnlyList<(string Caption, FileExportFormat Format, string Content)> ToContents(FencedCodeBlock block)
    {
        var format = block.Info?.Trim() switch
        {
            "csv" => FileExportFormat.CSV,
            "tsv" => FileExportFormat.TSV,

            _ => FileExportFormat.NONE,
        };

        if (format is FileExportFormat.NONE)
            return [];

        var content = block.Lines.ToString();
        var separator = format is FileExportFormat.TSV ? '\t' : ',';
        var firstLine = content.AsSpan();
        var lineEnd = firstLine.IndexOf('\n');
        if (lineEnd >= 0)
            firstLine = firstLine[..lineEnd];

        var separatorPosition = firstLine.IndexOf(separator);
        var caption = (separatorPosition >= 0 ? firstLine[..separatorPosition] : firstLine).Trim().Trim('"').ToString();

        return [(caption, format, content)];
    }

    private static string ToDelimitedText(IEnumerable<string[]> rows, char separator)
    {
        var text = new StringBuilder();
        foreach (var fields in rows)
            text.AppendLine(CsvWriter.ToRow(separator, fields));

        return text.ToString();
    }

    /// <summary>
    /// Reads the text of a table cell, without the Markdown which decorates it.
    /// </summary>
    /// <remarks>
    /// A spreadsheet has no use for the asterisks around a bold number: they would keep it from
    /// being completely recognized as a number. So we keep what a reader would read and drop the rest.
    /// </remarks>
    private static string ToPlainText(TableCell cell)
    {
        var text = new StringBuilder();
        foreach (var inline in cell.Descendants<LeafInline>())
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
    /// <returns>True, when the file was written.</returns>
    public static async Task<bool> ToFile(RustService rustService, string dialogTitle, FileExportFormat format, string fileContent)
    {
        if (format.UsesPandoc() || format.ToFileTypeFilter() is not { } fileTypeFilter)
            throw new ArgumentOutOfRangeException(nameof(format), format, "AI Studio cannot write this format itself.");

        var response = await rustService.SaveFile(dialogTitle, [fileTypeFilter], format.ToSuggestedFileName());
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