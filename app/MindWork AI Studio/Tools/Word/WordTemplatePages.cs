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
/// page break. A cover page stays as it is, with its placeholders, and so does what the template
/// places between it and its text, e.g., a table of document properties and a table of contents.
/// On a page with text, the answer takes the place of the sample text, starts where that one does,
/// and keeps the shapes of the design around it, see WordShapes. So does the page after it, when
/// the first page stays a section of its own, with the shapes of the first page of the main section.
///
/// The main section holds the text of the template, which starts with its first heading after the
/// first page. Its layout, headers, and footers become those of the pages of the answer. Sections
/// after it go with the sample text, e.g., pages with instructions to remove before sending the
/// document. Without such a heading, the section after a cover page is the main one, or else the
/// last section.
///
/// A page ends with the first page break the file states: a page break within a line, a paragraph
/// which starts on a new page, or the end of a section whose next section starts on a new page. Where
/// Word broke a page because its text filled it, the file marks this only as far as Word laid the
/// text out when it saved the file; such a mark ends a page as well.
/// </remarks>
internal sealed class WordTemplatePages
{
    /// <summary>
    /// Whether the first page holds no text of its own, but only pictures, shapes, and empty paragraphs.
    /// </summary>
    public bool FirstPageIsCover { get; private init; }

    /// <summary>
    /// The blocks before the text of a template whose first page is a cover page, which stay as they
    /// are: the cover page, and what follows it up to the first heading; none when the first page
    /// holds text.
    /// </summary>
    /// <remarks>
    /// They are the blocks of the template themselves, taken out of the body, without the marks of
    /// comments and notes, which go with the rest of the body.
    /// </remarks>
    public IReadOnlyList<OpenXmlElement> FrontMatter { get; private init; } = [];

    /// <summary>
    /// How many sections end within the front matter; their properties stay where they are.
    /// </summary>
    public int SectionsOfFrontMatter { get; private init; }

    /// <summary>
    /// The properties of the main section, which holds the text of the template; null when the body states no sections.
    /// </summary>
    public W.SectionProperties? MainSection { get; private init; }

    /// <summary>
    /// The empty paragraphs at the top of the first page, before its first text; none when the first
    /// page is a cover page.
    /// </summary>
    /// <remarks>
    /// Templates move the text of their first page below a picture or a title with empty paragraphs.
    /// These carry the properties of those paragraphs only, without anything the paragraphs held, so
    /// that they take up the same height.
    /// </remarks>
    public IReadOnlyList<W.Paragraph> LeadingEmptyParagraphs { get; private init; } = [];

    /// <summary>
    /// The shapes of the design of the first page, each in a run of its own; none when the first page is a cover page.
    /// </summary>
    public IReadOnlyList<W.Run> FirstPageShapes { get; private init; } = [];

    /// <summary>
    /// The shapes of the design of the first page of the main section, each in a run of its own, for
    /// the page after the first one; none when the main section is the first one, or the first page
    /// is a cover page.
    /// </summary>
    public IReadOnlyList<W.Run> NextPageShapes { get; private init; } = [];

    /// <summary>
    /// Reads the pages of a template.
    /// </summary>
    /// <param name="body">The body of the template, not cleared yet.</param>
    /// <param name="styles">The styles of the template, which tell the headings.</param>
    /// <param name="estimate">Estimates where the paragraphs of the template start on their page.</param>
    /// <returns>What the pages hold.</returns>
    public static WordTemplatePages Of(W.Body body, WordStyles styles, WordLayoutEstimate estimate)
    {
        var sections = body.Descendants<W.SectionProperties>().ToList();
        var blocks = body.ChildElements.Where(element => element is not W.SectionProperties).ToList();
        var firstPage = PageFrom(blocks, 0, sections);
        var firstHeading = blocks.FindIndex(firstPage.Count, block => block is W.Paragraph paragraph && styles.IsHeading(paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value));
        if (!firstPage.Any(HasTextOfItsOwn))
        {
            var frontMatter = (firstHeading < 0 ? firstPage : blocks.Take(firstHeading)).ToList();
            var sectionsOfFrontMatter = frontMatter.Count(EndsSection);
            EmptyTablesOfContents(frontMatter);
            return new WordTemplatePages
            {
                FirstPageIsCover = true,
                FrontMatter = frontMatter.Select(WithoutCommentsAndNotes).ToList(),
                SectionsOfFrontMatter = sectionsOfFrontMatter,
                MainSection = firstHeading >= 0 ? SectionOf(blocks, firstHeading, sections) : sections.ElementAtOrDefault(sectionsOfFrontMatter) ?? sections.LastOrDefault(),
            };
        }

        var mainSection = firstHeading < 0 ? sections.LastOrDefault() : SectionOf(blocks, firstHeading, sections);
        var leadingEmptyParagraphs = firstPage.TakeWhile(IsEmpty).Cast<W.Paragraph>().ToList();
        var firstArea = WordTextArea.Of(sections.FirstOrDefault() ?? new W.SectionProperties());
        var textTop = firstArea.Top + leadingEmptyParagraphs.Sum(paragraph => estimate.HeightOf(paragraph, firstArea.ColumnWidth));

        var nextPageShapes = new List<W.Run>();
        var mainIndex = mainSection is null ? -1 : sections.IndexOf(mainSection);
        if (mainSection is not null && mainIndex > 0)
        {
            var mainArea = WordTextArea.Of(mainSection);
            var mainStart = blocks.FindIndex(block => block is W.Paragraph { ParagraphProperties.SectionProperties: { } section } && section == sections[mainIndex - 1]) + 1;
            nextPageShapes = WordShapes.DesignOf(PageFrom(blocks, mainStart, sections), mainArea, mainArea.Top, estimate);
        }

        return new WordTemplatePages
        {
            MainSection = mainSection,
            LeadingEmptyParagraphs = leadingEmptyParagraphs.Select(EmptyCopyOf).ToList(),
            FirstPageShapes = WordShapes.DesignOf(firstPage, firstArea, textTop, estimate),
            NextPageShapes = nextPageShapes,
        };
    }

    /// <summary>
    /// Returns the properties of the section a block belongs to: those the block or the next one which ends a section carries.
    /// </summary>
    /// <param name="blocks">The paragraphs and tables of the body.</param>
    /// <param name="index">The index of the block.</param>
    /// <param name="sections">The properties of all sections, in the order of the document.</param>
    /// <returns>The properties; the last ones when no block after it ends a section, and null when the body states no sections.</returns>
    private static W.SectionProperties? SectionOf(List<OpenXmlElement> blocks, int index, List<W.SectionProperties> sections) => blocks.Skip(index)
        .Select(block => (block as W.Paragraph)?.ParagraphProperties?.SectionProperties)
        .FirstOrDefault(section => section is not null) ?? sections.LastOrDefault();

    /// <summary>
    /// Whether a block is a paragraph which ends a section.
    /// </summary>
    private static bool EndsSection(OpenXmlElement block) => block is W.Paragraph { ParagraphProperties.SectionProperties: not null };

    /// <summary>
    /// Empties the tables of contents among the blocks, whose entries name the chapters of the sample
    /// text and refer to them.
    /// </summary>
    /// <remarks>
    /// A table of contents is a field whose result Word computed when the template was saved. The
    /// field stays, so that Word lists the headings of the answer once it updates it; until then, the
    /// table stays empty rather than naming chapters which the document lacks, or showing errors for
    /// the page numbers of them. The result runs from the separate mark of the field to its end mark,
    /// often across many paragraphs; those between the two go.
    /// </remarks>
    private static void EmptyTablesOfContents(IEnumerable<OpenXmlElement> blocks)
    {
        foreach (var code in blocks.SelectMany(block => block.Descendants<W.FieldCode>()).Where(code => code.Text.TrimStart().StartsWith("TOC", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            if (code.Ancestors<W.Paragraph>().FirstOrDefault() is not { } start)
                continue;

            // The marks of the field after its code; fields within its result, e.g., page numbers, have marks of their own:
            W.FieldChar? separate = null, end = null;
            var depth = 0;
            var isAfterCode = false;
            foreach (var element in start.Descendants().Concat(start.ElementsAfter().SelectMany(sibling => sibling.Descendants())))
            {
                if (element == code)
                    isAfterCode = true;

                if (!isAfterCode || element is not W.FieldChar mark)
                    continue;

                var kind = mark.FieldCharType?.InnerText;
                if (kind is "begin")
                    depth++;
                else if (kind is "separate" && depth is 0)
                    separate = mark;
                else if (kind is "end" && depth is 0)
                {
                    end = mark;
                    break;
                }
                else if (kind is "end")
                    depth--;
            }

            if (separate?.Parent is not W.Run { Parent: { } separateParent } separateRun || end?.Parent is not W.Run { Parent: W.Paragraph endParagraph } endRun || separateParent != start || endParagraph.Parent != start.Parent)
                continue;

            var result = endParagraph == start
                ? separateRun.ElementsAfter().TakeWhile(element => element != endRun).ToList()
                : separateRun.ElementsAfter()
                    .Concat(start.ElementsAfter().TakeWhile(sibling => sibling != endParagraph))
                    .Concat(endRun.ElementsBefore().Where(element => element is not W.ParagraphProperties))
                    .ToList();

            foreach (var element in result)
                element.Remove();
        }
    }

    /// <summary>
    /// Removes what links a block to comments and notes, which go with the rest of the body, see
    /// WordDocumentWriter.RemoveWhatOnlyTheBodyUsed.
    /// </summary>
    private static OpenXmlElement WithoutCommentsAndNotes(OpenXmlElement block)
    {
        foreach (var mark in block.Descendants().Where(element => element is W.CommentRangeStart or W.CommentRangeEnd or W.CommentReference or W.FootnoteReference or W.EndnoteReference).ToList())
            mark.Remove();

        return block;
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