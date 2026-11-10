using AIStudio.Tools.DocumentSegments.Html;
using AIStudio.Tools.DocumentSegments.PowerPoint;
using AIStudio.Tools.DocumentSegments.Word;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// The kinds of documents whose text a model works on segment by segment.
/// </summary>
public static class DocumentSegmentFormats
{
    private static readonly IReadOnlyList<IDocumentSegmentFormat> FORMATS =
    [
        new WordSegmentFormat(),
        new PowerPointSegmentFormat(),
        new MarkdownSegmentFormat(),
        new HtmlSegmentFormat(),
    ];

    /// <summary>
    /// Finds the format of a document by its extension.
    /// </summary>
    /// <param name="path">The document.</param>
    /// <returns>The format, or null when no format reads documents of this kind yet.</returns>
    public static IDocumentSegmentFormat? For(string path)
    {
        var extension = Path.GetExtension(path);
        return FORMATS.FirstOrDefault(format => FileTypes.IsAllowedExtension(extension, format.FileType));
    }
}