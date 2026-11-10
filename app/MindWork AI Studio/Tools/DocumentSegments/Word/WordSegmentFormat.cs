using AIStudio.Tools.OpenXml;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

using DocumentFormat.OpenXml.Packaging;

namespace AIStudio.Tools.DocumentSegments.Word;

/// <summary>
/// Word documents (.docx), whose paragraphs a model works on, while layout, styles, pictures, tables, headers,
/// footers, and notes stay as they are.
/// </summary>
/// <remarks>
/// The new document comes about in a copy of the source in the temporary folder, which goes into the file the
/// user chose once it is complete; TemplateCopy says why. See WordSegmentReader for what becomes a segment.
/// </remarks>
public sealed class WordSegmentFormat : IDocumentSegmentFormat
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(WordSegmentFormat).Namespace, nameof(WordSegmentFormat));

    /// <summary>
    /// The folder below the temporary folder of the system, where the documents come about.
    /// </summary>
    private const string TEMPORARY_DIRECTORY = "mindwork-ai-studio-documents";

    /// <inheritdoc />
    public FileTypeFilter FileType => FileTypes.MS_WORD;

    /// <inheritdoc />
    public string LimitsNotice => TB("Charts, SmartArt, comments, the alternative texts of pictures, and the properties of the document, such as its title, stay as they are. Paragraphs keep their direction, so a translation into a language written from right to left, such as Arabic or Hebrew, may need adjustments. New text often needs more room than the original, so please check text boxes and table cells of a fixed size.");

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentSegment>> ReadAsync(string path, CancellationToken token) => Task.Run<IReadOnlyList<DocumentSegment>>(() =>
    {
        using var file = TemplateFile.OpenRead(path);
        using var document = WordprocessingDocument.Open(file, false);
        return WordSegmentReader.Read(document).Select(paragraph => paragraph.Segment).ToList();
    }, token);

    /// <inheritdoc />
    public async Task<SegmentWriteResult> WriteAsync(string sourcePath, string targetPath, SegmentResults results, string languageTag, ILogger logger, CancellationToken token)
    {
        using var copy = await TemplateCopy.CreateAsync(sourcePath, TEMPORARY_DIRECTORY, ".docx", logger, token);
        var result = await Task.Run(() => WriteInto(copy, results, languageTag), token);
        if (result.Outcome is SegmentWriteOutcome.WRITTEN)
            await copy.WriteIntoAsync(targetPath, token);

        return result;
    }

    /// <summary>
    /// Writes the new words into the copy of the source, which is the source read again.
    /// </summary>
    private static SegmentWriteResult WriteInto(TemplateCopy copy, SegmentResults results, string languageTag)
    {
        using var file = copy.OpenForEditing();
        using var document = WordprocessingDocument.Open(file, true);
        var paragraphs = WordSegmentReader.Read(document);
        if (paragraphs.Any(paragraph => !results.Covers(paragraph.Segment)))
            return SegmentWriteResult.SOURCE_CHANGED;

        var exactCount = 0;
        var simplifiedCount = 0;
        foreach (var paragraph in paragraphs)
        {
            // A paragraph which stays as it was keeps everything, its runs included:
            if (!results.TryGetText(paragraph.Segment, out var newText) || newText == paragraph.Segment.Key)
                continue;

            if (WordParagraphWriter.Write(paragraph, newText, languageTag))
                exactCount++;
            else
                simplifiedCount++;
        }

        return new(SegmentWriteOutcome.WRITTEN, exactCount, simplifiedCount);
    }
}