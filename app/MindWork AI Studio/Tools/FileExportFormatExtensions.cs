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
/// to know about it. Code is the one exception: SOURCE_CODE stands for code in every language, so
/// its file takes the extension and the save dialog filter from its language, see
/// SourceCodeLanguage, and the format itself names none.
/// </remarks>
public static class FileExportFormatExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(FileExportFormatExtensions).Namespace, nameof(FileExportFormatExtensions));

    private static readonly Encoding WITH_BYTE_ORDER_MARK = new UTF8Encoding(true);
    private static readonly Encoding WITHOUT_BYTE_ORDER_MARK = new UTF8Encoding(false);

    private static readonly IReadOnlyList<FileExportFormat> DOCUMENT_FORMATS =
    [
        FileExportFormat.MICROSOFT_WORD,
        FileExportFormat.OPEN_DOCUMENT_TEXT,
        FileExportFormat.LATEX,
    ];

    private static readonly IReadOnlyList<FileExportFormat> TEXT_FORMATS =
    [
        FileExportFormat.MARKDOWN,
        FileExportFormat.HTML,
    ];

    private static readonly IReadOnlyList<FileExportFormat> ANSWER_FORMATS = [..DOCUMENT_FORMATS, ..TEXT_FORMATS];

    /// <param name="format">The format.</param>
    extension(FileExportFormat format)
    {
        /// <summary>
        /// The formats which lay the text out as a document you would hand to somebody, in the order
        /// the export menu shows them.
        /// </summary>
        public static IReadOnlyList<FileExportFormat> DocumentFormats => DOCUMENT_FORMATS;

        /// <summary>
        /// The formats which keep the text as text, in the order the export menu shows them.
        /// </summary>
        public static IReadOnlyList<FileExportFormat> TextFormats => TEXT_FORMATS;

        /// <summary>
        /// Every format an entire answer can be written as.
        /// </summary>
        /// <remarks>
        /// The tabular formats are missing on purpose: they hold one table out of an answer, never the
        /// answer itself. Whoever offers a table adds them.
        /// </remarks>
        public static IReadOnlyList<FileExportFormat> AnswerFormats => ANSWER_FORMATS;

        /// <summary>
        /// Returns the name of the format as shown to the user.
        /// </summary>
        /// <value>The name of the format.</value>
        public string Name => format switch
        {
            FileExportFormat.MICROSOFT_WORD => TB("Microsoft Word (.docx)"),
            FileExportFormat.OPEN_DOCUMENT_TEXT => TB("OpenDocument Text (.odt), e.g. LibreOffice"),
            FileExportFormat.LATEX => TB("LaTeX (.tex)"),
            FileExportFormat.MARKDOWN => TB("Markdown (.md)"),
            FileExportFormat.HTML => TB("Webpage (.html)"),
            FileExportFormat.CSV => TB("Table (.csv)"),
            FileExportFormat.TSV => TB("Table (.tsv)"),
            FileExportFormat.SOURCE_CODE => TB("Source code"),

            _ => TB("Unknown format"),
        };

        /// <summary>
        /// Returns the icon of the format.
        /// </summary>
        /// <value>The icon of the format.</value>
        public string Icon => format switch
        {
            FileExportFormat.MICROSOFT_WORD => AppIcons.FILE_FORMAT_WORD,
            FileExportFormat.OPEN_DOCUMENT_TEXT => AppIcons.FILE_FORMAT_ODT,
            FileExportFormat.LATEX => AppIcons.FILE_FORMAT_LATEX,
            FileExportFormat.MARKDOWN => AppIcons.FILE_FORMAT_MARKDOWN,
            FileExportFormat.HTML => AppIcons.FILE_FORMAT_HTML,
            FileExportFormat.CSV or FileExportFormat.TSV => AppIcons.FILE_FORMAT_TABLE,
            FileExportFormat.SOURCE_CODE => AppIcons.CODE,

            _ => AppIcons.UNKNOWN,
        };

        /// <summary>
        /// Returns the file extension of the format, including the leading dot.
        /// </summary>
        /// <value>The file extension, or an empty string when the format writes no file or, as code
        /// does, takes the extension from its language.</value>
        public string FileExtension => format switch
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
        /// Determines whether the format holds a table rather than a text.
        /// </summary>
        /// <value>True for the formats a spreadsheet opens.</value>
        public bool IsTabular => format is FileExportFormat.CSV or FileExportFormat.TSV;

        /// <summary>
        /// Determines whether a file of the format is plain text, which AI Studio writes as it is.
        /// </summary>
        /// <remarks>
        /// That holds for a web page and a LaTeX document as well, even though an entire answer needs
        /// Pandoc to become one: the answer is Markdown, whereas a page the model wrote is a finished
        /// file already. A Word or an OpenDocument file is an archive, and only Pandoc produces one. The
        /// list is spelled out on purpose, so a format added later counts as plain text only once
        /// somebody says so.
        /// </remarks>
        /// <value>True, when a text written as it is makes a valid file of the format.</value>
        public bool IsPlainText => format is FileExportFormat.LATEX or FileExportFormat.MARKDOWN or FileExportFormat.HTML or FileExportFormat.CSV or FileExportFormat.TSV or FileExportFormat.SOURCE_CODE;

        /// <summary>
        /// Returns the file name the save dialog starts with.
        /// </summary>
        /// <remarks>
        /// Without a name, the dialog opens with an empty field and the user easily ends up with a
        /// file which carries no extension at all. The fallback name is deliberately not translated:
        /// a file name should survive being copied between systems and locales.
        /// </remarks>
        /// <param name="name">What the file is about, for example the heading above a table. Anything
        /// a file name cannot hold is removed. Null or blank falls back to a generic name.</param>
        /// <returns>The suggested file name, including its extension.</returns>
        public string ToSuggestedFileName(string? name = null) => SuggestFileName(name, format.FileExtension);

        /// <summary>
        /// Returns the filter which the save dialog offers for the format.
        /// </summary>
        /// <value>The filter, or null when the format cannot be written or, as code does, takes the
        /// filter from its language.</value>
        public FileTypeFilter? FileTypeFilter => format switch
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
        /// <value>The encoding to write the file with.</value>
        public Encoding FileEncoding => format switch
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
        /// <param name="text">The text to put into the comment.</param>
        /// <param name="comment">The comment, or an empty string when the format has none.</param>
        /// <returns>True, when the format knows comments.</returns>
        public bool TryToComment(string text, out string comment)
        {
            var lines = text.TrimEnd().ReplaceLineEndings("\n").Split('\n');
            switch (format)
            {
                case FileExportFormat.HTML or FileExportFormat.MARKDOWN:
                    var commentText = string.Join('\n', lines).Replace("-->", "-- >").Replace("--!>", "--! >");
                    comment = $"<!--\n{commentText}\n-->";
                    return true;

                case FileExportFormat.LATEX:
                    comment = string.Join('\n', lines.Select(line => line.Length is 0 ? "%" : $"% {line}"));
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
        /// <value>True, when a reader of this format follows such a link.</value>
        public bool FollowsPageAnchors => format switch
        {
            FileExportFormat.MICROSOFT_WORD or FileExportFormat.OPEN_DOCUMENT_TEXT => false,

            _ => true,
        };

        /// <summary>
        /// Determines whether Pandoc has to be told the title of a document in the format.
        /// </summary>
        /// <remarks>
        /// A web page shows its title in the browser tab. Without one, Pandoc names the page after its
        /// input file, which is a temporary file of ours with a random name. Word and OpenDocument show
        /// no such title, and handed one anyway, they keep it as a document property nobody asked for;
        /// verified with Pandoc 3.8.3 on 2026-09-23. LaTeX ignores it.
        /// </remarks>
        /// <value>True, when a document of this format needs a title besides its content.</value>
        public bool NeedsPageTitle => format is FileExportFormat.HTML;

        /// <summary>
        /// Returns the name Pandoc knows the format by.
        /// </summary>
        /// <value>The Pandoc output format, or an empty string when AI Studio writes the file itself.</value>
        public string PandocOutputFormat => format switch
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
        /// <value>True, when Pandoc converts the message; false, when AI Studio writes the file itself.</value>
        public bool UsesPandoc => !string.IsNullOrWhiteSpace(format.PandocOutputFormat);

        /// <summary>
        /// Reads which format a model means when it names a language behind the opening fence of a
        /// code block.
        /// </summary>
        /// <remarks>
        /// A model which answers with a finished file puts it into a code block and names its language,
        /// as in ```html. Models do not agree on the spelling, so we accept the usual names of a format
        /// in any case. Only formats which are plain text appear here: a code block holds text, never
        /// a Word document. A block in any other language, or in none, is code, see
        /// SourceCodeLanguages.FromCodeFenceLanguage.
        /// </remarks>
        /// <param name="language">The language behind the opening fence, as Markdig reads it into
        /// FencedCodeBlock.Info.</param>
        /// <param name="namedFormat">The format the language names, or NONE when it names none of ours.</param>
        /// <returns>True, when the language names a format AI Studio writes.</returns>
        public static bool TryFromCodeFenceLanguage(string? language, out FileExportFormat namedFormat)
        {
            namedFormat = language?.Trim().ToLowerInvariant() switch
            {
                "html" => FileExportFormat.HTML,
                "latex" or "tex" => FileExportFormat.LATEX,
                "markdown" or "md" => FileExportFormat.MARKDOWN,
                "csv" => FileExportFormat.CSV,
                "tsv" => FileExportFormat.TSV,

                _ => FileExportFormat.NONE,
            };

            return namedFormat is not FileExportFormat.NONE;
        }
    }

    /// <summary>
    /// Returns the file name the save dialog starts with, for a file with the given extension.
    /// </summary>
    /// <remarks>
    /// The same rules as for a format apply, see ToSuggestedFileName. Code blocks need this, since
    /// their extension comes from the language of the block, not from a format.
    /// </remarks>
    /// <param name="name">What the file is about. Null or blank falls back to a generic name.</param>
    /// <param name="fileExtension">The file extension, including the leading dot.</param>
    /// <returns>The suggested file name, including its extension.</returns>
    internal static string SuggestFileName(string? name, string fileExtension)
    {
        var fileName = ToFileNameFragment(name);
        return $"{(fileName.Length is 0 ? "export" : fileName)}{fileExtension}";
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
    internal static string ToFileNameFragment(string? name)
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
}