using System.Text;

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

    private static readonly Encoding WITH_BYTE_ORDER_MARK = new UTF8Encoding(true);
    private static readonly Encoding WITHOUT_BYTE_ORDER_MARK = new UTF8Encoding(false);

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
    /// Every format an entire answer can be written as.
    /// </summary>
    /// <remarks>
    /// The tabular formats are missing on purpose: they hold one table out of an answer, never the
    /// answer itself. Whoever offers a table adds them.
    /// </remarks>
    public static readonly IReadOnlyList<FileExportFormat> ANSWER_FORMATS = [..DOCUMENT_FORMATS, ..TEXT_FORMATS];

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
        FileExportFormat.CSV => TB("Table (.csv)"),
        FileExportFormat.TSV => TB("Table (.tsv)"),

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
    /// Reads which format a model means when it names a language behind the opening fence of a
    /// code block.
    /// </summary>
    /// <remarks>
    /// A model which answers with a finished file puts it into a code block and names its language,
    /// as in ```html. Models do not agree on the spelling, so we accept the usual names of a format
    /// in any case. Only formats which are plain text appear here: a code block holds text, never
    /// a Word document.
    /// </remarks>
    /// <param name="language">The language behind the opening fence, as Markdig reads it into
    /// FencedCodeBlock.Info.</param>
    /// <param name="format">The format the language names, or NONE when it names none of ours.</param>
    /// <returns>True, when the language names a format AI Studio writes.</returns>
    public static bool TryFromCodeFenceLanguage(string? language, out FileExportFormat format)
    {
        format = language?.Trim().ToLowerInvariant() switch
        {
            "html" => FileExportFormat.HTML,
            "latex" or "tex" => FileExportFormat.LATEX,
            "markdown" or "md" => FileExportFormat.MARKDOWN,
            "csv" => FileExportFormat.CSV,
            "tsv" => FileExportFormat.TSV,

            _ => FileExportFormat.NONE,
        };

        return format is not FileExportFormat.NONE;
    }

    /// <summary>
    /// Determines whether the format holds a table rather than a text.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <returns>True for the formats a spreadsheet opens.</returns>
    public static bool IsTabular(this FileExportFormat format) => format is FileExportFormat.CSV or FileExportFormat.TSV;

    /// <summary>
    /// Returns the file name the save dialog starts with.
    /// </summary>
    /// <remarks>
    /// Without a name, the dialog opens with an empty field and the user easily ends up with a
    /// file which carries no extension at all. The fallback name is deliberately not translated:
    /// a file name should survive being copied between systems and locales.
    /// </remarks>
    /// <param name="format">The format.</param>
    /// <param name="name">What the file is about, for example the heading above a table. Anything
    /// a file name cannot hold is removed. Null or blank falls back to a generic name.</param>
    /// <returns>The suggested file name, including its extension.</returns>
    public static string ToSuggestedFileName(this FileExportFormat format, string? name = null)
    {
        var fileName = ToFileNameFragment(name);
        return $"{(fileName.Length is 0 ? "export" : fileName)}{format.ToFileExtension()}";
    }

    /// <summary>
    /// Turns arbitrary text into something a file system accepts as a name.
    /// </summary>
    /// <remarks>
    /// We do not ask the runtime which characters are invalid: macOS forbids almost nothing, so a
    /// name taken from there would break as soon as the file reaches a Windows share. The fixed
    /// set below is what no common file system accepts, plus the length limit which keeps the name
    /// readable in a dialog.
    /// </remarks>
    private static string ToFileNameFragment(string? name)
    {
        const int MAX_LENGTH = 60;
        const string FORBIDDEN_CHARACTERS = @"\/:*?""<>|";

        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var fragment = new StringBuilder(name.Length);
        var lastWasSpace = false;
        foreach (var character in name)
        {
            var isSpace = char.IsWhiteSpace(character) || char.IsControl(character) || FORBIDDEN_CHARACTERS.Contains(character);
            if (isSpace)
            {
                // Collapse whatever we dropped into a single space, so "Table 1: People"
                // becomes "Table 1 People" instead of "Table 1  People":
                if (fragment.Length > 0)
                    lastWasSpace = true;

                continue;
            }

            if (lastWasSpace)
            {
                fragment.Append(' ');
                lastWasSpace = false;
            }

            fragment.Append(character);
            if (fragment.Length >= MAX_LENGTH)
                break;
        }

        // A trailing dot makes a file invisible on Unix and is dropped by Windows:
        return fragment.ToString().TrimEnd('.');
    }

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
        FileExportFormat.HTML => FileTypes.HTML_DOCUMENT,
        FileExportFormat.CSV => FileTypes.CSV,
        FileExportFormat.TSV => FileTypes.TSV,

        _ => null,
    };

    /// <summary>
    /// Returns the encoding the file gets written with.
    /// </summary>
    /// <remarks>
    /// Everything is UTF-8, the question is only whether the file starts with a byte order mark.
    /// Tabular files get one, because Excel otherwise reads them in the local ANSI code page and
    /// turns every umlaut into garbage. Text files get none: editors, compilers, and LaTeX have
    /// no use for it and some of them stumble over it.
    /// </remarks>
    /// <param name="format">The format.</param>
    /// <returns>The encoding to write the file with.</returns>
    public static Encoding ToFileEncoding(this FileExportFormat format) => format switch
    {
        FileExportFormat.CSV or FileExportFormat.TSV => WITH_BYTE_ORDER_MARK,

        _ => WITHOUT_BYTE_ORDER_MARK,
    };

    /// <summary>
    /// Wraps a text into a comment of the format: whoever opens the file in an editor reads it,
    /// while a browser or a LaTeX run skips it.
    /// </summary>
    /// <remarks>
    /// A comment in HTML, and so in Markdown, ends at the first --> it holds, and a browser takes
    /// --!> for the same; the rest of the text would spill onto the page from there. The title of
    /// a web page may hold either, so a space goes in before the bracket, which keeps the text
    /// readable and ends nothing. Every other pair of dashes stays, because a web address may carry
    /// one, as in the xn-- of a domain with an umlaut. A LaTeX comment has no end to watch for: it
    /// runs to the end of its line, so every line starts one.
    /// </remarks>
    /// <param name="format">The format.</param>
    /// <param name="text">The text to put into the comment.</param>
    /// <param name="comment">The comment, or an empty string when the format has none.</param>
    /// <returns>True, when the format knows comments.</returns>
    public static bool TryToComment(this FileExportFormat format, string text, out string comment)
    {
        var lines = text.TrimEnd().ReplaceLineEndings("\n").Split('\n');
        switch (format)
        {
            case FileExportFormat.HTML or FileExportFormat.MARKDOWN:
                var commentText = string.Join(Environment.NewLine, lines).Replace("-->", "-- >").Replace("--!>", "--! >");
                comment = $"<!--{Environment.NewLine}{commentText}{Environment.NewLine}-->";
                return true;

            case FileExportFormat.LATEX:
                comment = string.Join(Environment.NewLine, lines.Select(line => line.Length is 0 ? "%" : $"% {line}"));
                return true;

            default:
                comment = string.Empty;
                return false;
        }
    }

    /// <summary>
    /// Determines whether a link into a local file may name the page it points at.
    /// </summary>
    /// <remarks>
    /// A page is named by the fragment of the link, the way the PDF open parameters call for. A
    /// browser and a PDF reader follow that and open the document on the page; Word and LibreOffice
    /// take the fragment for part of the file name, look for a file which does not exist, and refuse
    /// the link altogether. There the page is dropped, so the link at least opens the document --
    /// which page it was stays in the title of the source. Verified on 2026-09-15 with LibreOffice
    /// on an exported .odt. A format added later keeps the page unless it is known to stumble too.
    /// </remarks>
    /// <param name="format">The format.</param>
    /// <returns>True, when a reader of this format follows such a link.</returns>
    public static bool FollowsPageAnchors(this FileExportFormat format) => format switch
    {
        FileExportFormat.MICROSOFT_WORD or FileExportFormat.OPEN_DOCUMENT_TEXT => false,

        _ => true,
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