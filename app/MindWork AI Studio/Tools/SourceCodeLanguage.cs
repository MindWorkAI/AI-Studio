using System.Text;

using AIStudio.Tools.Rust;

namespace AIStudio.Tools;

/// <summary>
/// A language a model names behind the opening fence of a code block, together with what a file
/// in that language needs.
/// </summary>
/// <remarks>
/// Most languages go by their extension. A few tools look for a fixed file name instead, such as
/// Docker for its Dockerfile, so such a language names the file and leaves the extension empty.
/// </remarks>
/// <param name="Name">The name of the language as shown to the user, for example Python. The names
/// of languages are proper names and stay untranslated.</param>
/// <param name="FileExtension">The file extension, including the leading dot. Empty when the
/// language has a fixed file name.</param>
/// <param name="FileName">The fixed file name a tool looks for, for example Dockerfile. Empty when
/// the language goes by its extension.</param>
/// <param name="NeedsByteOrderMark">Whether the file has to start with a byte order mark. Windows
/// PowerShell 5.1, which still ships with Windows, reads a script without one in the local ANSI code
/// page and garbles every umlaut.</param>
/// <param name="NeedsWindowsLineEndings">Whether the lines have to end with CR LF. The Windows
/// command prompt misses jump labels in a batch file whose lines end with LF alone.</param>
public sealed record SourceCodeLanguage(string Name, string FileExtension, string FileName = "", bool NeedsByteOrderMark = false, bool NeedsWindowsLineEndings = false)
{
    /// <summary>
    /// No language at all, for a file which is not a code block of this kind.
    /// </summary>
    public static readonly SourceCodeLanguage NONE = new(string.Empty, string.Empty);

    private static readonly Encoding WITH_BYTE_ORDER_MARK = new UTF8Encoding(true);
    private static readonly Encoding WITHOUT_BYTE_ORDER_MARK = new UTF8Encoding(false);

    /// <summary>
    /// Whether the language has a fixed file name rather than an extension.
    /// </summary>
    private bool HasFixedFileName => this.FileName.Length > 0;

    /// <summary>
    /// Returns the name of the language as shown to the user, together with its file.
    /// </summary>
    /// <returns>For example "Python (.py)", or the file name alone, such as "Dockerfile".</returns>
    public string ToName() => this.HasFixedFileName ? this.FileName : $"{this.Name} ({this.FileExtension})";

    /// <summary>
    /// Returns what tells a file of the language apart: its extension, or its fixed file name.
    /// </summary>
    /// <returns>For example ".py" or "Dockerfile".</returns>
    public string ToFileExtensionOrName() => this.HasFixedFileName ? this.FileName : this.FileExtension;

    /// <summary>
    /// Returns the filter which the save dialog offers for a file of the language.
    /// </summary>
    /// <remarks>
    /// A filter by file name holds no extension, so the dialog shows every file, cf.
    /// FileTypes.ToFileDialogFilter. That is what we want for a Dockerfile.
    /// </remarks>
    /// <returns>The filter.</returns>
    public FileTypeFilter ToFileTypeFilter() => this.HasFixedFileName
        ? FileTypeFilter.ByFileName(this.Name, this.FileName)
        : FileTypeFilter.Leaf(this.Name, this.FileExtension.TrimStart('.'));

    /// <summary>
    /// Returns the file name the save dialog starts with.
    /// </summary>
    /// <remarks>
    /// A fixed file name wins over whatever the file is about: Docker finds a Dockerfile, but no
    /// "Backend.Dockerfile" unless somebody tells it so.
    /// </remarks>
    /// <param name="name">What the file is about, for example the heading above the code block.
    /// Null or blank falls back to a generic name.</param>
    /// <returns>The suggested file name.</returns>
    public string ToSuggestedFileName(string? name = null) => this.HasFixedFileName ? this.FileName : FileExportFormatExtensions.SuggestFileName(name, this.FileExtension);

    /// <summary>
    /// Returns the encoding the file gets written with: UTF-8, with a byte order mark only where
    /// the language needs one.
    /// </summary>
    /// <returns>The encoding to write the file with.</returns>
    public Encoding ToFileEncoding() => this.NeedsByteOrderMark ? WITH_BYTE_ORDER_MARK : WITHOUT_BYTE_ORDER_MARK;
}