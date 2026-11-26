using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

using DocumentFormat.OpenXml.Packaging;

namespace AIStudio.Tools.DocumentSegments.PowerPoint;

/// <summary>
/// PowerPoint presentations (.pptx), whose slides and notes a model works on, while the design, the pictures, the
/// animations, and the masters and layouts of the slides stay as they are.
/// </summary>
/// <remarks>
/// How the new presentation comes about is the same for every Office document, see OpenXmlSegmentFormat. See
/// SlideSegmentReader for what becomes a segment.
/// </remarks>
internal sealed class PowerPointSegmentFormat : OpenXmlSegmentFormat<PresentationDocument, SlideTextParagraph>
{
    private static string TA(string fallbackEN) => I18N.I.TA(fallbackEN, typeof(PowerPointSegmentFormat).Namespace, nameof(PowerPointSegmentFormat));

    /// <inheritdoc />
    public override FileTypeFilter FileType => FileTypes.POWER_POINT_PRESENTATION;

    /// <inheritdoc />
    public override string LimitsNotice => TA("Charts, SmartArt, the alternative texts of pictures, the properties of the presentation, such as its title, and the masters and layouts of the slides stay as they are. Paragraphs keep their direction, so new text in a language written from right to left, such as Arabic or Hebrew, may need adjustments. New text often needs more room than the original, so please check the text boxes on your slides.");

    /// <inheritdoc />
    protected override string FileExtension => ".pptx";

    /// <inheritdoc />
    protected override PresentationDocument Open(Stream stream, bool isEditable) => PresentationDocument.Open(stream, isEditable);

    /// <inheritdoc />
    protected override IReadOnlyList<SlideTextParagraph> ReadParagraphs(PresentationDocument document) => SlideSegmentReader.Read(document);

    /// <inheritdoc />
    protected override DocumentSegment SegmentOf(SlideTextParagraph paragraph) => paragraph.Segment;

    /// <inheritdoc />
    protected override bool Write(SlideTextParagraph paragraph, string newText, string languageTag) => SlideParagraphWriter.Write(paragraph, newText, languageTag);
}