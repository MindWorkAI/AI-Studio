using System.Text;

using AIStudio.Tools.Rust;

namespace AIStudio.Tools;

/// <summary>
/// A file found in a message, ready to be written: a table the model wrote, or one of its code blocks.
/// </summary>
/// <remarks>
/// The export menu and the writer ask the file how it is named and written, never its format
/// directly: code in Python and code in Rust share the format SOURCE_CODE, and only the language
/// of the file tells them apart.
/// </remarks>
/// <param name="Ordinal">Which table or which code block of the message this is, counting from one
/// within its kind; the format tells the two kinds apart, see FileExportFormatExtensions.IsTabular.
/// This is what tells two files of one kind apart even when they carry the same heading.</param>
/// <param name="Position">Where the table or code block starts in the Markdown text the file was
/// read from, as the index of its first character. A block the chat renders finds its file by it:
/// the chat renders an answer in parts, around its formulas, and each part knows where it starts.</param>
/// <param name="Caption">What the file is about: the heading above it, or else the first column
/// heading of a table. Empty for a code block without a heading above it.</param>
/// <param name="Format">The format this content is written as.</param>
/// <param name="Language">The language of the code, when the format is SOURCE_CODE; NONE for
/// every other file.</param>
/// <param name="Content">The finished file content.</param>
public sealed record MessageFile(int Ordinal, int Position, string Caption, FileExportFormat Format, SourceCodeLanguage Language, string Content)
{
    private bool IsSourceCode => this.Format is FileExportFormat.SOURCE_CODE;

    /// <summary>
    /// Returns the name of the file's type as shown to the user, for example "Webpage (.html)" or
    /// "Python (.py)".
    /// </summary>
    public string ToName() => this.IsSourceCode ? this.Language.ToName() : this.Format.ToName();

    /// <summary>
    /// Returns the icon of the file's type.
    /// </summary>
    public string ToIcon() => this.Format.ToIcon();

    /// <summary>
    /// Returns what tells a file of this type apart: its extension, for example ".html", or the
    /// fixed name a tool looks for, such as Dockerfile.
    /// </summary>
    public string ToFileExtensionOrName() => this.IsSourceCode ? this.Language.ToFileExtensionOrName() : this.Format.ToFileExtension();

    /// <summary>
    /// Returns the filter which the save dialog offers for the file.
    /// </summary>
    /// <returns>The filter, or null when AI Studio cannot write the file itself.</returns>
    public FileTypeFilter? ToFileTypeFilter() => this.IsSourceCode ? this.Language.ToFileTypeFilter() : this.Format.ToFileTypeFilter();

    /// <summary>
    /// Returns the file name the save dialog starts with.
    /// </summary>
    /// <param name="name">What the file is about. Null or blank falls back to a generic name.</param>
    /// <returns>The suggested file name.</returns>
    public string ToSuggestedFileName(string? name = null) => this.IsSourceCode ? this.Language.ToSuggestedFileName(name) : this.Format.ToSuggestedFileName(name);

    /// <summary>
    /// Returns the encoding the file gets written with.
    /// </summary>
    public Encoding ToFileEncoding() => this.IsSourceCode ? this.Language.ToFileEncoding() : this.Format.ToFileEncoding();
}