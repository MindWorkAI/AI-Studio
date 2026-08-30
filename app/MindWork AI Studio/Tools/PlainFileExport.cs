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
    /// Extracts the content of the first complete Markdown code block marked as CSV.
    /// </summary>
    public static bool TryExtractCsvContent(IContent content, out string csvContent)
    {
        if (content is ContentText text)
        {
            var match = CsvCodeFenceRegex().Match(text.Text);
            if (match.Success)
            {
                csvContent = match.Groups["content"].Value;
                return true;
            }
        }

        csvContent = string.Empty;
        return false;
    }

    [GeneratedRegex(
        """
        # Matches an opening Markdown code fence and captures its delimiter.
        ^(?<fence>`{3,}|~{3,})

        # Matches the csv language identifier and the end of the opening fence line.
        # Also allow tab-separated, pipe-separated and semicolon separated values at the code fence
        [ \t]*(csv|tsv|psv|ssv)[ \t]*\r?\n

        # Captures the content of the first matching fenced code block.
        (?<content>[\s\S]*?)

        # Matches the corresponding closing fence followed by a line ending or end of input.
        ^\k<fence>[ \t]*(?=\r?\n|$)
        """,
        RegexOptions.IgnoreCase |
        RegexOptions.Multiline |
        RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex CsvCodeFenceRegex();
    
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

        var response = await rustService.SaveFile(TB("Export chat"), [fileTypeFilter]);
        if (response.UserCancelled)
        {
            LOGGER.LogInformation("User cancelled the save dialog.");
            return false;
        }

        LOGGER.LogInformation("The user chose the path '{SaveFilePath}' for the {ExportFormat} export.", response.SaveFilePath, format);

        try
        {
            var fileContent = format switch
            {
                FileExportFormat.MARKDOWN => markdownContent switch
                {
                    ContentText text => text.Text,
                    ContentImage _ => "Image export is not yet possible.",
                    _ => "Unknown content type. Cannot export document."
                },
                FileExportFormat.CSV when TryExtractCsvContent(markdownContent, out var csvContent) => csvContent,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
            };

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
