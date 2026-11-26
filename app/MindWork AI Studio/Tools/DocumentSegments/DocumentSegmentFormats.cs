using AIStudio.Tools.DocumentSegments.Html;
using AIStudio.Tools.DocumentSegments.Latex;
using AIStudio.Tools.DocumentSegments.PowerPoint;
using AIStudio.Tools.DocumentSegments.Word;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// The kinds of documents whose text a model works on segment by segment.
/// </summary>
public static class DocumentSegmentFormats
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DocumentSegmentFormats).Namespace, nameof(DocumentSegmentFormats));

    private static readonly IReadOnlyList<IDocumentSegmentFormat> FORMATS =
    [
        new WordSegmentFormat(),
        new PowerPointSegmentFormat(),
        new MarkdownSegmentFormat(),
        new HtmlSegmentFormat(),
        new LatexSegmentFormat(),
    ];

    /// <summary>
    /// The kinds of files of all formats, as one entry of a file dialog.
    /// </summary>
    /// <remarks>
    /// Taken from the formats, so that a new format appears in the dialog without a list of its own.
    /// </remarks>
    public static FileTypeFilter AllKinds => FileTypeFilter.Parent(TB("Document"), [.. FORMATS.Select(format => format.FileType)]);

    /// <summary>
    /// Finds the format of a document by its extension.
    /// </summary>
    /// <param name="path">The document.</param>
    /// <returns>The format, or null when no format reads documents of this kind.</returns>
    public static IDocumentSegmentFormat? For(string path)
    {
        var extension = Path.GetExtension(path);
        return FORMATS.FirstOrDefault(format => FileTypes.IsAllowedExtension(extension, format.FileType));
    }
}