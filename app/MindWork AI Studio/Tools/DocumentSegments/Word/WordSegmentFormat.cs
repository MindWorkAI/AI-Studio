using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

using DocumentFormat.OpenXml.Packaging;

namespace AIStudio.Tools.DocumentSegments.Word;

/// <summary>
/// Word documents (.docx), whose paragraphs a model works on, while layout, styles, pictures, tables, headers,
/// footers, and notes stay as they are.
/// </summary>
/// <remarks>
/// How the new document comes about is the same for every Office document, see OpenXmlSegmentFormat. See
/// WordSegmentReader for what becomes a segment.
/// </remarks>
internal sealed class WordSegmentFormat : OpenXmlSegmentFormat<WordprocessingDocument, WordParagraph>
{
    private static string TA(string fallbackEN) => I18N.I.TA(fallbackEN, typeof(WordSegmentFormat).Namespace, nameof(WordSegmentFormat));

    /// <inheritdoc />
    public override FileTypeFilter FileType => FileTypes.MS_WORD;

    /// <inheritdoc />
    public override string LimitsNotice => TA("Charts, SmartArt, comments, the alternative texts of pictures, and the properties of the document, such as its title, stay as they are. Paragraphs keep their direction, so new text in a language written from right to left, such as Arabic or Hebrew, may need adjustments. New text often needs more room than the original, so please check text boxes and table cells of a fixed size.");

    /// <inheritdoc />
    protected override string FileExtension => ".docx";

    /// <inheritdoc />
    protected override WordprocessingDocument Open(Stream stream, bool isEditable) => WordprocessingDocument.Open(stream, isEditable);

    /// <inheritdoc />
    protected override IReadOnlyList<WordParagraph> ReadParagraphs(WordprocessingDocument document) => WordSegmentReader.Read(document);

    /// <inheritdoc />
    protected override DocumentSegment SegmentOf(WordParagraph paragraph) => paragraph.Segment;

    /// <inheritdoc />
    protected override bool Write(WordParagraph paragraph, string newText, string languageTag) => WordParagraphWriter.Write(paragraph, newText, languageTag);
}