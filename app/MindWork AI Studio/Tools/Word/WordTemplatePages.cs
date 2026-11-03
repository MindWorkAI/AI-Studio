using DocumentFormat.OpenXml;

using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.Word;

/// <summary>
/// Reads what the first page of a Word template holds besides its sample text, while its body is still complete.
/// </summary>
/// <remarks>
/// A template shows on its first page how the first page of a document should look. Either the page
/// holds text of its own, e.g., a handout whose first paragraphs start below a picture, or it is a
/// cover page without any, e.g., a picture with the title and the number of a report, followed by a
/// page break. A cover page goes with the body, apart from its header and footer. On a page with
/// text, the answer takes the place of the sample text, and starts where that one does.
///
/// The first page ends with the first page break the file states: a page break within a line, a
/// paragraph which starts on a new page, or the end of a section whose next section starts on a new
/// page. Where Word breaks a page because its text fills it, the file states nothing reliable.
/// </remarks>
internal sealed class WordTemplatePages
{
    private WordTemplatePages(bool firstPageIsCover, IReadOnlyList<W.Paragraph> leadingEmptyParagraphs)
    {
        this.FirstPageIsCover = firstPageIsCover;
        this.LeadingEmptyParagraphs = leadingEmptyParagraphs;
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
    /// Reads the pages of a template.
    /// </summary>
    /// <param name="body">The body of the template, not cleared yet.</param>
    /// <returns>What the pages hold.</returns>
    public static WordTemplatePages Of(W.Body body)
    {
        var firstPage = FirstPageOf(body);
        if (!firstPage.Any(HasTextOfItsOwn))
            return new(true, []);

        var leadingEmptyParagraphs = firstPage.TakeWhile(IsEmpty).Cast<W.Paragraph>().Select(EmptyCopyOf).ToList();
        return new(false, leadingEmptyParagraphs);
    }

    /// <summary>
    /// Returns the blocks of the body up to the first page break.
    /// </summary>
    private static List<OpenXmlElement> FirstPageOf(W.Body body)
    {
        var sections = body.Descendants<W.SectionProperties>().ToList();
        var page = new List<OpenXmlElement>();
        foreach (var block in body.ChildElements.Where(element => element is not W.SectionProperties))
        {
            if (page.Count > 0 && block is W.Paragraph { ParagraphProperties.PageBreakBefore: { } pageBreakBefore } && pageBreakBefore.Val?.Value is not false)
                break;

            page.Add(block);
            if (OwnElementsOf<W.Break>(block).Any(lineBreak => lineBreak.Type?.InnerText is "page") || EndsSectionBeforeNewPage(block, sections))
                break;
        }

        return page;
    }

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
    private static bool HasTextOfItsOwn(OpenXmlElement block) => OwnElementsOf<W.Text>(block).Any(text => !string.IsNullOrWhiteSpace(text.Text));

    /// <summary>
    /// Whether a block is a paragraph which takes up no more than the height of an empty line: no
    /// text, no picture in the line, and no page or section break. Shapes which float above the text
    /// do not count.
    /// </summary>
    private static bool IsEmpty(OpenXmlElement block) => block is W.Paragraph paragraph
        && paragraph.ParagraphProperties?.SectionProperties is null
        && !HasTextOfItsOwn(paragraph)
        && !OwnElementsOf<W.Break>(paragraph).Any()
        && !OwnElementsOf<DW.Inline>(paragraph).Any()
        && !OwnElementsOf<W.EmbeddedObject>(paragraph).Any();

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

    /// <summary>
    /// Returns the elements of a kind within a block, apart from those in text boxes, which float
    /// elsewhere, and from the older markup Word reads only when it understands no newer one.
    /// </summary>
    private static IEnumerable<T> OwnElementsOf<T>(OpenXmlElement element) where T : OpenXmlElement
    {
        foreach (var child in element.ChildElements)
        {
            if (child is W.TextBoxContent or AlternateContentFallback)
                continue;

            if (child is T match)
                yield return match;

            foreach (var descendant in OwnElementsOf<T>(child))
                yield return descendant;
        }
    }
}