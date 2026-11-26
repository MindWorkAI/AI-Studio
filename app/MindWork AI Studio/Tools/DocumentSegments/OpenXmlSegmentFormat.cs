using AIStudio.Tools.OpenXml;
using AIStudio.Tools.Rust;

using DocumentFormat.OpenXml.Packaging;

namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// A format of Office documents, e.g., Word documents, whose paragraphs a model works on.
/// </summary>
/// <remarks>
/// The new document comes about in a copy of the source in the temporary folder, which goes into the file the user
/// chose once it is complete; TemplateCopy says why. The copy is read again before anything is written, so that the
/// paragraphs the writer changes are those of the copy, and nothing is written when the results miss one of them.
/// A paragraph whose text stays as it was keeps everything, its runs included.
/// </remarks>
/// <typeparam name="TDocument">The kind of Office document, e.g., WordprocessingDocument.</typeparam>
/// <typeparam name="TParagraph">A paragraph as the reader of the format returns it, with all its writer needs.</typeparam>
internal abstract class OpenXmlSegmentFormat<TDocument, TParagraph> : IDocumentSegmentFormat where TDocument : OpenXmlPackage
{
    /// <summary>
    /// The folder below the temporary folder of the system, where the documents come about.
    /// </summary>
    private const string TEMPORARY_DIRECTORY = "mindwork-ai-studio-documents";

    #region Implementation of IDocumentSegmentFormat

    /// <inheritdoc />
    public abstract FileTypeFilter FileType { get; }

    /// <inheritdoc />
    public abstract string LimitsNotice { get; }

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentSegment>> ReadAsync(string path, CancellationToken token) => Task.Run<IReadOnlyList<DocumentSegment>>(() =>
    {
        using var file = TemplateFile.OpenRead(path);
        using var document = this.Open(file, isEditable: false);
        return this.ReadParagraphs(document).Select(this.SegmentOf).ToList();
    }, token);

    /// <inheritdoc />
    public async Task<SegmentWriteResult> WriteAsync(string sourcePath, string targetPath, SegmentResults results, string languageTag, ILogger logger, CancellationToken token)
    {
        using var copy = await TemplateCopy.CreateAsync(sourcePath, TEMPORARY_DIRECTORY, this.FileExtension, logger, token);
        var result = await Task.Run(() => this.WriteInto(copy, results, languageTag), token);
        if (result.Outcome is SegmentWriteOutcome.WRITTEN)
            await copy.WriteIntoAsync(targetPath, token);

        return result;
    }

    #endregion

    /// <summary>
    /// The extension of the documents, with its dot, e.g., ".docx", for the copy in the temporary folder.
    /// </summary>
    protected abstract string FileExtension { get; }

    /// <summary>
    /// Opens a document.
    /// </summary>
    /// <param name="stream">The file of the document.</param>
    /// <param name="isEditable">Whether the document gets written.</param>
    protected abstract TDocument Open(Stream stream, bool isEditable);

    /// <summary>
    /// Reads the paragraphs of a document which can be segments, in the order of the document.
    /// </summary>
    /// <param name="document">The document.</param>
    protected abstract IReadOnlyList<TParagraph> ReadParagraphs(TDocument document);

    /// <summary>
    /// Gets the segment of a paragraph.
    /// </summary>
    /// <param name="paragraph">The paragraph, as ReadParagraphs returned it.</param>
    protected abstract DocumentSegment SegmentOf(TParagraph paragraph);

    /// <summary>
    /// Replaces the words of a paragraph with its new words.
    /// </summary>
    /// <param name="paragraph">The paragraph, as ReadParagraphs returned it from the copy.</param>
    /// <param name="newText">The new words with tags, as the model wrote them.</param>
    /// <param name="languageTag">The IETF tag of the language of the new words; empty to leave the languages as they are.</param>
    /// <returns>Whether the paragraph kept all of its inline structure and formatting.</returns>
    protected abstract bool Write(TParagraph paragraph, string newText, string languageTag);

    /// <summary>
    /// Writes the new words into the copy of the source, which is the source read again.
    /// </summary>
    private SegmentWriteResult WriteInto(TemplateCopy copy, SegmentResults results, string languageTag)
    {
        using var file = copy.OpenForEditing();
        using var document = this.Open(file, isEditable: true);
        var paragraphs = this.ReadParagraphs(document);
        if (paragraphs.Any(paragraph => !results.Covers(this.SegmentOf(paragraph))))
            return SegmentWriteResult.SOURCE_CHANGED;

        var exactCount = 0;
        var simplifiedCount = 0;
        foreach (var paragraph in paragraphs)
        {
            var segment = this.SegmentOf(paragraph);
            if (!results.TryGetText(segment, out var newText) || newText == segment.Key)
                continue;

            if (this.Write(paragraph, newText, languageTag))
                exactCount++;
            else
                simplifiedCount++;
        }

        return new(SegmentWriteOutcome.WRITTEN, exactCount, simplifiedCount);
    }
}