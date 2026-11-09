using AIStudio.Tools.Rust;

namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// A kind of document whose text a model works on segment by segment, while everything else of the
/// document stays as it is, e.g., Word documents or Markdown files.
/// </summary>
public interface IDocumentSegmentFormat
{
    /// <summary>
    /// The kind of file this format reads and writes, which the save dialog offers.
    /// </summary>
    public FileTypeFilter FileType { get; }

    /// <summary>
    /// What of a document of this kind stays as it is, for the user, e.g., "Code and formulas stay as they are."
    /// </summary>
    public string LimitsNotice { get; }

    /// <summary>
    /// Reads the segments of a document, in the order of the document.
    /// </summary>
    /// <param name="path">The document.</param>
    /// <param name="token">Cancels the reading.</param>
    /// <returns>The segments, including those without letters.</returns>
    public Task<IReadOnlyList<DocumentSegment>> ReadAsync(string path, CancellationToken token);

    /// <summary>
    /// Writes a copy of a document with the new texts of its segments.
    /// </summary>
    /// <remarks>
    /// The format reads the source again and writes nothing when the results do not cover one of its
    /// segments. It writes only into the target, which the user chose, and leaves the source as it is.
    /// </remarks>
    /// <param name="sourcePath">The document the segments were read from.</param>
    /// <param name="targetPath">The new document, which the user chose.</param>
    /// <param name="results">What became of the segments.</param>
    /// <param name="languageTag">The IETF tag of the language of the new texts, e.g., "de-DE", which the format notes where the document records languages; empty when the language is unknown or unchanged.</param>
    /// <param name="token">Cancels the writing.</param>
    public Task<SegmentWriteResult> WriteAsync(string sourcePath, string targetPath, SegmentResults results, string languageTag, CancellationToken token);
}