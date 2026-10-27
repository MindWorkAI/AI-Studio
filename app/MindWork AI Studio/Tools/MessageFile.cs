using System.Text;

using AIStudio.Tools.Rust;

namespace AIStudio.Tools;

/// <summary>
/// A file found in a message, ready to be written: a table the model wrote, or a code block the
/// model marked as a format we write.
/// </summary>
/// <remarks>
/// The export menu and the writer ask the file how it is named and written, never its format
/// directly, so that a file can describe itself in more detail than its format does.
/// </remarks>
/// <param name="Ordinal">Which table or which code block of the message this is, counting from one
/// within its kind; the format tells the two kinds apart, see FileExportFormatExtensions.IsTabular.
/// This is what tells two files of one kind apart even when they carry the same heading.</param>
/// <param name="Caption">What the file is about: the heading above it, or else the first column
/// heading of a table. Empty for a code block without a heading above it.</param>
/// <param name="Format">The format this content is written as.</param>
/// <param name="Content">The finished file content.</param>
public sealed record MessageFile(int Ordinal, string Caption, FileExportFormat Format, string Content)
{
    /// <summary>
    /// Returns the name of the file's type as shown to the user, for example "Webpage (.html)".
    /// </summary>
    public string ToName() => this.Format.ToName();

    /// <summary>
    /// Returns the icon of the file's type.
    /// </summary>
    public string ToIcon() => this.Format.ToIcon();

    /// <summary>
    /// Returns what tells a file of this type apart, for example ".html".
    /// </summary>
    public string ToFileExtensionOrName() => this.Format.ToFileExtension();

    /// <summary>
    /// Returns the filter which the save dialog offers for the file.
    /// </summary>
    /// <returns>The filter, or null when AI Studio cannot write the file itself.</returns>
    public FileTypeFilter? ToFileTypeFilter() => this.Format.ToFileTypeFilter();

    /// <summary>
    /// Returns the file name the save dialog starts with.
    /// </summary>
    /// <param name="name">What the file is about. Null or blank falls back to a generic name.</param>
    /// <returns>The suggested file name.</returns>
    public string ToSuggestedFileName(string? name = null) => this.Format.ToSuggestedFileName(name);

    /// <summary>
    /// Returns the encoding the file gets written with.
    /// </summary>
    public Encoding ToFileEncoding() => this.Format.ToFileEncoding();
}