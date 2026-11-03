using DocumentFormat.OpenXml;

using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.Word;

/// <summary>
/// Reads what the pages of a Word template hold besides their sample text, while its body is still complete.
/// </summary>
/// <remarks>
/// A template shows on its first page how the first page of a document should look. Either the page
/// holds text of its own, e.g., a handout whose first paragraphs start below a picture, or it is a
/// cover page without any, e.g., a picture with the title and the number of a report, followed by a
/// page break. A cover page goes with the body, apart from its header and footer. On a page with
/// text, the answer takes the place of the sample text, starts where that one does, and keeps the
/// shapes of the design around it, see WordShapes. So does the page after it, when the first page
/// stays a section of its own, with the shapes of the first page of the last section.
///
/// A page ends with the first page break the file states: a page break within a line, a paragraph
/// which starts on a new page, or the end of a section whose next section starts on a new page. Where
/// Word broke a page because its text filled it, the file marks this only as far as Word laid the
/// text out when it saved the file; such a mark ends a page as well.
/// </remarks>
internal sealed class WordTemplatePages
{
    private WordTemplatePages(bool firstPageIsCover, IReadOnlyList<W.Paragraph> leadingEmptyParagraphs, IReadOnlyList<W.Run> firstPageShapes, IReadOnlyList<W.Run> nextPageShapes)
    {
        this.FirstPageIsCover = firstPageIsCover;
        this.LeadingEmptyParagraphs = leadingEmptyParagraphs;
        this.FirstPageShapes = firstPageShapes;
        this.NextPageShapes = nextPageShapes;
    }

    /// <summary>
    /// Whether the first page holds no text of its own, but only pictures, shapes, and empty paragraphs.
    /// </summary>
    public bool FirstPageIsCover { get; }

    /// <summary>
    /// The empty paragraphs at the top of the first page, before its first text; none when the first
    /// page is a cover page.
    /// </summary>
    /// <remarks>
    /// Templates move the text of their first page below a picture or a title with empty paragraphs.
    /// These carry the properties of those paragraphs only, without anything the paragraphs held, so
    /// that they take up the same height.
    /// </remarks>
    public IReadOnlyList<W.Paragraph> LeadingEmptyParagraphs { get; }

    /// <summary>
    /// The shapes of the design of the first page, each in a run of its own; none when the first page is a cover page.
    /// </summary>
    public IReadOnlyList<W.Run> FirstPageShapes { get; }

    /// <summary>
    /// The shapes of the design of the first page of the last section, each in a run of its own, for
    /// the page after the first one; none when the template has a single section, or the first page
    /// is a cover page.
    /// </summary>
    public IReadOnlyList<W.Run> NextPageShapes { get; }

    /// <summary>
    /// Reads the pages of a template.
    /// </summary>
    /// <param name="body">The body of the template, not cleared yet.</param>
    /// <param name="estimate">Estimates where the paragraphs of the template start on their page.</param>
    /// <returns>What the pages hold.</returns>
    public static WordTemplatePages Of(W.Body body, WordLayoutEstimate estimate)
    {
        var sections = body.Descendants<W.SectionProperties>().ToList();
        var blocks = body.ChildElements.Where(element => element is not W.SectionProperties).ToList();
        var firstPage = PageFrom(blocks, 0, sections);
        if (!firstPage.Any(HasTextOfItsOwn))
            return new(true, [], [], []);

        var leadingEmptyParagraphs = firstPage.TakeWhile(IsEmpty).Cast<W.Paragraph>().ToList();
        var firstArea = WordTextArea.Of(sections.FirstOrDefault() ?? new W.SectionProperties());
        var textTop = firstArea.Top + leadingEmptyParagraphs.Sum(paragraph => estimate.HeightOf(paragraph, firstArea.ColumnWidth));
        var firstPageShapes = WordShapes.DesignOf(firstPage, firstArea, textTop, estimate);

        var nextPageShapes = new List<W.Run>();
        var lastSectionStart = blocks.FindLastIndex(block => block is W.Paragraph { ParagraphProperties.SectionProperties: not null }) + 1;
        if (lastSectionStart > 0 && body.Elements<W.SectionProperties>().LastOrDefault() is { } lastSection)
        {
            var lastArea = WordTextArea.Of(lastSection);
            nextPageShapes = WordShapes.DesignOf(PageFrom(blocks, lastSectionStart, sections), lastArea, lastArea.Top, estimate);
        }

        return new(false, leadingEmptyParagraphs.Select(EmptyCopyOf).ToList(), firstPageShapes, nextPageShapes);
    }

    /// <summary>
    /// Returns the blocks of the page which starts with the given block, up to the next page break.
    /// </summary>
    /// <param name="blocks">The paragraphs and tables of the body.</param>
    /// <param name="start">The index of the first block of the page.</param>
    /// <param name="sections">The properties of all sections, in the order of the document.</param>
    private static List<OpenXmlElement> PageFrom(List<OpenXmlElement> blocks, int start, List<W.SectionProperties> sections)
    {
        var page = new List<OpenXmlElement>();
        foreach (var block in blocks.Skip(start))
        {
            if (page.Count > 0 && StartsNewPage(block))
                break;

            page.Add(block);
            if (block.OwnDescendants<W.Break>().Any(lineBreak => lineBreak.Type?.InnerText is "page") || EndsSectionBeforeNewPage(block, sections))
                break;
        }

        return page;
    }

    /// <summary>
    /// Determines whether a block starts on a new page, or Word broke the page within it when it last laid the text out.
    /// </summary>
    private static bool StartsNewPage(OpenXmlElement block) => block is W.Paragraph { ParagraphProperties.PageBreakBefore: { } pageBreakBefore } && pageBreakBefore.Val?.Value is not false
        || block.OwnDescendants<W.LastRenderedPageBreak>().Any();

    /// <summary>
    /// Determines whether a block ends a section, and the next section starts on a new page.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <param name="sections">The properties of all sections, in the order of the document.</param>
    private static bool EndsSectionBeforeNewPage(OpenXmlElement block, List<W.SectionProperties> sections)
    {
        if (block is not W.Paragraph { ParagraphProperties.SectionProperties: { } section })
            return false;

        // A section without a type starts on a new page:
        var next = sections.IndexOf(section) + 1;
        return next < sections.Count && sections[next].GetFirstChild<W.SectionType>()?.Val?.InnerText is not ("continuous" or "nextColumn");
    }

    /// <summary>
    /// Whether a block holds text of its own, apart from the text of its text boxes.
    /// </summary>
    private static bool HasTextOfItsOwn(OpenXmlElement block) => block.OwnDescendants<W.Text>().Any(text => !string.IsNullOrWhiteSpace(text.Text));

    /// <summary>
    /// Whether a block is a paragraph which takes up no more than the height of an empty line: no
    /// text, no picture in the line, and no page or section break. Shapes which float above the text
    /// do not count.
    /// </summary>
    private static bool IsEmpty(OpenXmlElement block) => block is W.Paragraph paragraph
        && paragraph.ParagraphProperties?.SectionProperties is null
        && !HasTextOfItsOwn(paragraph)
        && !paragraph.OwnDescendants<W.Break>().Any()
        && !paragraph.OwnDescendants<DW.Inline>().Any()
        && !paragraph.OwnDescendants<W.EmbeddedObject>().Any();

    /// <summary>
    /// Returns a paragraph with the properties of the given one, but without its content.
    /// </summary>
    private static W.Paragraph EmptyCopyOf(W.Paragraph paragraph)
    {
        if (paragraph.ParagraphProperties?.CloneNode(true) is not W.ParagraphProperties properties)
            return new W.Paragraph();

        properties.ParagraphPropertiesChange = null;
        return new W.Paragraph(properties);
    }
}