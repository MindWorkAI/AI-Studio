using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools;

/// <summary>
/// Everything AI Studio needs to know about an export format: how it is named, how it is shown,
/// which file it produces, and who writes that file.
/// </summary>
/// <remarks>
/// This is the single place where an export format is described. Adding another one means adding
/// an enum member and one line per method here; neither the exporters nor the export menu need
/// to know about it.
/// </remarks>
public static class FileExportFormatExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(FileExportFormatExtensions).Namespace, nameof(FileExportFormatExtensions));

    /// <summary>
    /// The formats which lay the text out as a document you would hand to somebody, in the order
    /// the export menu shows them.
    /// </summary>
    public static readonly IReadOnlyList<FileExportFormat> DOCUMENT_FORMATS =
    [
        FileExportFormat.MICROSOFT_WORD,
        FileExportFormat.OPEN_DOCUMENT_TEXT,
        FileExportFormat.LATEX,
    ];

    /// <summary>
    /// The formats which keep the text as text, in the order the export menu shows them.
    /// </summary>
    public static readonly IReadOnlyList<FileExportFormat> TEXT_FORMATS =
    [
        FileExportFormat.MARKDOWN,
        FileExportFormat.HTML,
    ];

    /// <summary>
    /// Returns the name of the format as shown to the user.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <returns>The name of the format.</returns>
    public static string ToName(this FileExportFormat format) => format switch
    {
        FileExportFormat.MICROSOFT_WORD => TB("Microsoft Word (.docx)"),
        FileExportFormat.OPEN_DOCUMENT_TEXT => TB("OpenDocument Text (.odt), e.g. LibreOffice"),
        FileExportFormat.LATEX => TB("LaTeX (.tex)"),
        FileExportFormat.MARKDOWN => TB("Markdown (.md)"),
        FileExportFormat.HTML => TB("Webpage (.html)"),
        FileExportFormat.CSV => TB("Table, comma-separated (.csv)"),
        FileExportFormat.TSV => TB("Table, tab-separated (.tsv)"),

        _ => TB("Unknown format"),
    };

    /// <summary>
    /// Returns the icon of the format.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <returns>The icon of the format.</returns>
    public static string ToIcon(this FileExportFormat format) => format switch
    {
        FileExportFormat.MICROSOFT_WORD => Icons.Custom.FileFormats.FileWord,
        FileExportFormat.OPEN_DOCUMENT_TEXT => Icons.Custom.FileFormats.FileDocument,
        FileExportFormat.LATEX => Icons.Material.Filled.Functions,
        FileExportFormat.MARKDOWN => Icons.Material.Filled.TextFields,
        FileExportFormat.HTML => Icons.Material.Filled.Html,
        FileExportFormat.CSV or FileExportFormat.TSV => Icons.Material.Filled.TableChart,

        _ => Icons.Material.Filled.Help,
    };

    /// <summary>
    /// Returns the file extension of the format, including the leading dot.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <returns>The file extension, or an empty string when the format writes no file.</returns>
    public static string ToFileExtension(this FileExportFormat format) => format switch
    {
        FileExportFormat.MICROSOFT_WORD => ".docx",
        FileExportFormat.OPEN_DOCUMENT_TEXT => ".odt",
        FileExportFormat.LATEX => ".tex",
        FileExportFormat.MARKDOWN => ".md",
        FileExportFormat.HTML => ".html",
        FileExportFormat.CSV => ".csv",
        FileExportFormat.TSV => ".tsv",

        _ => string.Empty,
    };

    /// <summary>
    /// Returns the file name the save dialog starts with.
    /// </summary>
    /// <remarks>
    /// Without a name, the dialog opens with an empty field and the user easily ends up with a
    /// file which carries no extension at all. The name is deliberately not translated: a file
    /// name should survive being copied between systems and locales.
    /// </remarks>
    /// <param name="format">The format.</param>
    /// <returns>The suggested file name, including its extension.</returns>
    public static string ToSuggestedFileName(this FileExportFormat format) => $"export{format.ToFileExtension()}";

    /// <summary>
    /// Returns the filter which the save dialog offers for the format.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <returns>The filter, or null when the format cannot be written.</returns>
    public static FileTypeFilter? ToFileTypeFilter(this FileExportFormat format) => format switch
    {
        FileExportFormat.MICROSOFT_WORD => FileTypes.MS_WORD,
        FileExportFormat.OPEN_DOCUMENT_TEXT => FileTypes.ODT,
        FileExportFormat.LATEX => FileTypes.TEX,
        FileExportFormat.MARKDOWN => FileTypes.MARKDOWN,
        FileExportFormat.HTML => FileTypes.HTML,
        FileExportFormat.CSV => FileTypes.CSV,
        FileExportFormat.TSV => FileTypes.TSV,

        _ => null,
    };

    /// <summary>
    /// Returns the name Pandoc knows the format by.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <returns>The Pandoc output format, or an empty string when AI Studio writes the file itself.</returns>
    public static string ToPandocOutputFormat(this FileExportFormat format) => format switch
    {
        FileExportFormat.MICROSOFT_WORD => "docx",
        FileExportFormat.OPEN_DOCUMENT_TEXT => "odt",
        FileExportFormat.LATEX => "latex",
        FileExportFormat.HTML => "html",

        _ => string.Empty,
    };

    /// <summary>
    /// Determines whether writing the format needs Pandoc.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <returns>True, when Pandoc converts the message; false, when AI Studio writes the file itself.</returns>
    public static bool UsesPandoc(this FileExportFormat format) => !string.IsNullOrWhiteSpace(format.ToPandocOutputFormat());
}