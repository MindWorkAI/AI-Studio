using System.Text.RegularExpressions;
using AIStudio.Chat;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

namespace AIStudio.Tools;

public static partial class PlainFileExport
{
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(PlainFileExport));

    private static string TB(string fallbackEn) => I18N.I.T(fallbackEn, typeof(PlainFileExport).Namespace, nameof(PlainFileExport));

    /// <summary>
    /// Reads the first complete Markdown code block which holds tabular data.
    /// </summary>
    /// <remarks>
    /// Models mark such a block with the name of the separator they used. The separator only
    /// decides the file format where it has an established extension of its own: comma, semicolon,
    /// and pipe separated data all belong into a .csv file, whereas tab separated data has .tsv.
    /// </remarks>
    /// <param name="markdown">The Markdown text to read.</param>
    /// <param name="extract">The tabular data, or the default when there is none.</param>
    /// <returns>True, when the text holds tabular data.</returns>
    public static bool TryExtractTabularContent(string markdown, out TabularExtract extract)
    {
        var match = TabularCodeFenceRegex().Match(markdown);
        if (!match.Success)
        {
            extract = default;
            return false;
        }

        var format = match.Groups["separator"].Value.Equals("tsv", StringComparison.OrdinalIgnoreCase)
            ? FileExportFormat.TSV
            : FileExportFormat.CSV;

        extract = new(match.Groups["content"].Value, format);
        return true;
    }

    [GeneratedRegex(
        """
        # Matches an opening Markdown code fence, which CommonMark lets you indent by up to three
        # spaces, and captures both the delimiter and the character it is made of.
        ^[ ]{0,3}(?<fence>(?<fenceChar>`|~)\k<fenceChar>{2,})

        # Matches the name of the separator the model used, followed by the end of the opening
        # fence line. Besides comma separated values, models also produce tab, pipe, and semicolon
        # separated ones.
        [ \t]*(?<separator>csv|tsv|psv|ssv)[ \t]*\r?\n

        # Captures the content of the first matching fenced code block.
        (?<content>[\s\S]*?)

        # Matches the closing fence, which CommonMark lets you write longer than the opening one,
        # followed by a line ending or the end of the input.
        ^[ ]{0,3}\k<fence>\k<fenceChar>*[ \t]*(?=\r?\n|$)
        """,
        RegexOptions.IgnoreCase |
        RegexOptions.Multiline |
        RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex TabularCodeFenceRegex();

    /// <summary>
    /// Writes the given content to a plain text file and lets the user save it.
    /// </summary>
    /// <param name="rustService">The Rust service, used for the save dialog.</param>
    /// <param name="format">The format to write. Must be a format which does not use Pandoc.</param>
    /// <param name="markdownContent">The content to export.</param>
    /// <returns>True, when the file was written.</returns>
    public static async Task<bool> ToFile(RustService rustService, FileExportFormat format, IContent markdownContent)
    {
        if (format.UsesPandoc() || format.ToFileTypeFilter() is not { } fileTypeFilter)
            throw new ArgumentOutOfRangeException(nameof(format), format, "AI Studio cannot write this format itself.");

        //
        // We work out what we are going to write before we ask for a path: when there is nothing
        // to write, the user should learn that right away instead of picking a file first and
        // getting an error afterward.
        //
        if (!markdownContent.TryGetMarkdownText(out var markdownText))
        {
            LOGGER.LogWarning("Cannot export the content as {ExportFormat}, because it carries no text.", format);
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("Only text messages can be exported.")));
            return false;
        }

        string fileContent;
        if (format is FileExportFormat.MARKDOWN)
            fileContent = markdownText;
        else if (TryExtractTabularContent(markdownText, out var tabularExtract) && tabularExtract.Format == format)
            fileContent = tabularExtract.Content;
        else
        {
            //
            // The message changed between showing the menu entry and clicking it: what looked like
            // a table a moment ago is gone, or it is no longer the format the user asked for.
            //
            LOGGER.LogWarning("Cannot export the content as {ExportFormat}, because it holds no matching table.", format);
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("This message no longer holds a table which could be exported.")));
            return false;
        }

        var response = await rustService.SaveFile(TB("Export chat"), [fileTypeFilter]);
        if (response.UserCancelled)
        {
            LOGGER.LogInformation("User cancelled the save dialog.");
            return false;
        }

        LOGGER.LogInformation("The user chose the path '{SaveFilePath}' for the {ExportFormat} export.", response.SaveFilePath, format);

        try
        {
            await File.WriteAllTextAsync(response.SaveFilePath, fileContent);
            await MessageBus.INSTANCE.SendSuccess(new(Icons.Material.Filled.CheckCircle, TB("Document export successful")));
            
            return true;
        }
        catch (Exception ex)
        {
            LOGGER.LogError(ex, "Error during {ExportFormat} export.", format);
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("Error during document export")));
            return false;
        }
    }
}