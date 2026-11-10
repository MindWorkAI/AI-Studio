using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tests.Tools.DocumentSegments.Word;

/// <summary>
/// Builds the Word documents of the tests in code, so that no binary document is checked in.
/// </summary>
internal static class TestWordDocument
{
    /// <summary>
    /// The relationship of the link which every document of the tests has, for a W.Hyperlink to point to.
    /// </summary>
    public const string LINK_ID = "rIdManual";

    private const string HEADER_ID = "rIdHeader";

    /// <summary>
    /// A document with nothing but the given paragraphs.
    /// </summary>
    /// <param name="path">Where to create the document.</param>
    /// <param name="paragraphs">The paragraphs of its body.</param>
    public static void Create(string path, params W.Paragraph[] paragraphs)
    {
        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.AddHyperlinkRelationship(new Uri("https://example.org/manual"), true, LINK_ID);
        mainPart.Document = new W.Document(new W.Body(paragraphs));
    }

    /// <summary>
    /// A document with what Word documents commonly hold: a link, a tab, a reference to a footnote, a field
    /// with its result, a bookmark, words with mixed formatting, a header, and a footnote.
    /// </summary>
    /// <param name="path">Where to create the document.</param>
    public static void CreateSample(string path)
    {
        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.AddHyperlinkRelationship(new Uri("https://example.org/manual"), true, LINK_ID);

        var headerPart = mainPart.AddNewPart<HeaderPart>(HEADER_ID);
        headerPart.Header = new W.Header(Paragraph(Run("Confidential")));

        var footnotesPart = mainPart.AddNewPart<FootnotesPart>();
        footnotesPart.Footnotes = new W.Footnotes(
            new W.Footnote(Paragraph(new W.Run(new W.SeparatorMark()))) { Type = W.FootnoteEndnoteValues.Separator, Id = -1 },
            new W.Footnote(Paragraph(new W.Run(new W.ContinuationSeparatorMark()))) { Type = W.FootnoteEndnoteValues.ContinuationSeparator, Id = 0 },
            new W.Footnote(Paragraph(new W.Run(new W.FootnoteReferenceMark()), Run(" A note."))) { Id = 1 });

        mainPart.Document = new W.Document(new W.Body(
            Paragraph(Run("Getting started")),
            Paragraph(Run("Read the "), new W.Hyperlink(Run("manual")) { Id = LINK_ID }, Run(" first."), new W.Run(new W.TabChar()), Run("Done")),
            Paragraph(Run("See the note"), new W.Run(new W.FootnoteReference { Id = 1 })),
            Paragraph(
                new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Begin }),
                new W.Run(new W.FieldCode(" REF _Ref1 \\h ") { Space = SpaceProcessingModeValues.Preserve }),
                new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }),
                Run("Introduction"),
                new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.End })),
            Paragraph(new W.BookmarkStart { Id = "1", Name = "_Ref1" }, Run("Chapter"), new W.BookmarkEnd { Id = "1" }),
            Paragraph(Run("Mixed "), Bold("bold"), Run(" words")),
            Paragraph(Run("2026")),
            new W.SectionProperties(new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = HEADER_ID })));
    }

    public static W.Paragraph Paragraph(params OpenXmlElement[] content) => new(content);

    public static W.Run Run(string text, W.RunProperties? properties = null) => properties is null
        ? new W.Run(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve })
        : new W.Run(properties, new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });

    public static W.Run Bold(string text) => Run(text, new W.RunProperties(new W.Bold()));

    public static W.Run Italic(string text) => Run(text, new W.RunProperties(new W.Italic()));

    public static W.Hyperlink Link(string text) => new(Run(text)) { Id = LINK_ID };
}