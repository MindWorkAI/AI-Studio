using AIStudio.Tools.Rust;

namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// A format of documents which are text themselves, e.g., Markdown, LaTeX, or web pages, whose words a model works on.
/// </summary>
/// <remarks>
/// <para>
/// The new document is the source with the words of each segment replaced; everything else stays byte for byte, in
/// the encoding of the source, see TextDocumentFile and TextSegmentWriter. A format brings how it reads its segments,
/// how new words fit into it, and, when its files name their encoding, how to find that name.
/// </para>
/// <para>
/// The segments reach the model in the order of the document, whatever order the reader of the format finds them in,
/// e.g., Markdig puts the footnotes of a Markdown file last.
/// </para>
/// </remarks>
public abstract class TextSegmentFormat : IDocumentSegmentFormat
{
    #region Implementation of IDocumentSegmentFormat

    /// <inheritdoc />
    public abstract FileTypeFilter FileType { get; }

    /// <inheritdoc />
    public abstract string LimitsNotice { get; }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentSegment>> ReadAsync(string path, CancellationToken token)
    {
        var file = await this.ReadFileAsync(path, token);
        return this.ReadSegments(file.Text).OrderBy(segment => segment.Start).Select(segment => segment.Segment).ToList();
    }

    /// <inheritdoc />
    public virtual async Task<SegmentWriteResult> WriteAsync(string sourcePath, string targetPath, SegmentResults results, string languageTag, ILogger logger, CancellationToken token)
    {
        var file = await this.ReadFileAsync(sourcePath, token);
        var result = TextSegmentWriter.Apply(file.Text, this.ReadSegments(file.Text), results, this.EncodedWords, out var text);
        if (result.Outcome is SegmentWriteOutcome.WRITTEN)
            await file.WriteAsync(targetPath, text, token);

        return result;
    }

    #endregion

    /// <summary>
    /// Reads the segments of a text of this format, in any order.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    protected abstract IReadOnlyList<TextSegment> ReadSegments(string text);

    /// <summary>
    /// Makes new words fit into a text of this format, e.g., escapes the characters which would read as markup.
    /// </summary>
    /// <param name="words">One run of new words between two tags, see TextSegmentWriter.Apply.</param>
    protected abstract string EncodedWords(string words);

    /// <summary>
    /// Finds the encoding which a text of this format names for itself, e.g., the charset of a web page.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <returns>The name of the encoding; null when the text names none, or the format has no way to name one.</returns>
    protected virtual string? DeclaredEncodingOf(string text) => null;

    /// <summary>
    /// Reads a document of this format, which has to be in Unicode, see TextDocumentFile.
    /// </summary>
    /// <param name="path">The document.</param>
    /// <param name="token">Cancels the reading.</param>
    protected Task<TextDocumentFile> ReadFileAsync(string path, CancellationToken token) => TextDocumentFile.ReadAsync(path, this.DeclaredEncodingOf, token);
}