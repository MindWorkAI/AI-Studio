using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.Word;

/// <summary>
/// Writes Markdown into a copy of a Word template.
/// </summary>
/// <remarks>
/// The document is the template itself, with the text of its body replaced: styles, page layout,
/// headers and footers, and the theme stay exactly as their author made them. A cover page or
/// sample text of the template goes along with the rest of the body, so that nothing of it ends
/// up between the content. The first page keeps the header and footer of the first page of the
/// template, see MergeSections, and the text starts where the one of the template does, see
/// WordTemplatePages. When the text runs in other columns there, the first page keeps them as
/// well, see FirstPageSectionOf.
/// </remarks>
internal static class WordDocumentWriter
{
    private const string RELATIONSHIPS_NAMESPACE = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>
    /// Writes the Markdown into a copy of the template, which it edits in place.
    /// </summary>
    /// <remarks>
    /// The caller copies the template, e.g., into a temporary file as WordDocumentFile does, so that
    /// the template stays untouched and no further copy of the file is held in memory.
    /// </remarks>
    /// <param name="documentFile">A copy of the template, a .docx or a .dotx, positioned at its start; readable, writable, and seekable.</param>
    /// <param name="markdown">The Markdown text to write.</param>
    /// <param name="topHeadingLevel">The heading level of the Markdown which becomes the first heading
    /// level of the template, see MarkdownWordConverter.TopHeadingLevelOf.</param>
    public static void Write(Stream documentFile, string markdown, int topHeadingLevel = 1)
    {
        using var document = WordprocessingDocument.Open(documentFile, true);

        // A template (.dotx) becomes a document; everything else about it stays:
        if (document.DocumentType != WordprocessingDocumentType.Document)
            document.ChangeDocumentType(WordprocessingDocumentType.Document);

        var mainPart = document.MainDocumentPart ?? throw new InvalidDataException("The file contains no Word document.");
        var body = mainPart.Document?.Body ?? throw new InvalidDataException("The file contains no Word document.");

        var pages = WordTemplatePages.Of(body);
        var firstPageSection = FirstPageSectionOf(body, pages);
        var firstPageSpace = MergeSections(body, firstPageSection is not null);
        var sectionProperties = ClearBody(body);

        var numbering = new WordNumbering(mainPart);
        var content = new MarkdownWordConverter(mainPart, WordStyles.Of(mainPart), numbering).Convert(markdown, topHeadingLevel);
        content.InsertRange(0, pages.LeadingEmptyParagraphs);
        if (firstPageSpace > 0)
            content.Insert(0, SpaceOf(firstPageSpace));

        if (firstPageSection is not null && sectionProperties is not null)
            sectionProperties = EndFirstPage(content, firstPageSection, sectionProperties, new WordLayoutEstimate(mainPart.StyleDefinitionsPart?.Styles, numbering));

        foreach (var element in content)
            if (sectionProperties is null)
                body.Append(element);
            else
                body.InsertBefore(element, sectionProperties);

        // Only now does the document refer to everything it keeps, e.g., the header of the first page:
        RemoveWhatOnlyTheBodyUsed(mainPart);
    }

    /// <summary>
    /// Returns the properties of the first section, when the first page keeps a layout of its own.
    /// </summary>
    /// <remarks>
    /// Some templates run the text of their first page in other columns than the one of the pages
    /// after it, e.g., next to a wide margin for pictures, which is on the left on the first page and
    /// on the right on the others. Then the first page stays a section of its own. A cover page keeps
    /// no text, so the document takes the layout of the pages after it throughout, as it does when
    /// the text of the first page only starts further down.
    /// </remarks>
    /// <param name="body">The body of the template, not cleared yet.</param>
    /// <param name="pages">What the pages of the template hold.</param>
    /// <returns>A copy of the properties; null when the first page takes the layout of the last section.</returns>
    private static W.SectionProperties? FirstPageSectionOf(W.Body body, WordTemplatePages pages)
    {
        var sections = body.Descendants<W.SectionProperties>().ToList();
        if (pages.FirstPageIsCover || sections.Count < 2 || body.Elements<W.SectionProperties>().LastOrDefault() is not { } lastSection)
            return null;

        return WordTextArea.Of(sections[0]).HasSameColumnsAs(WordTextArea.Of(lastSection)) ? null : (W.SectionProperties)sections[0].CloneNode(true);
    }

    /// <summary>
    /// Ends the first page after the blocks which fit on it, with the properties of its own section.
    /// </summary>
    /// <remarks>
    /// Where Word breaks the page is up to the fonts at hand, so the section ends where an estimate
    /// finds the page well filled, see WordLayoutEstimate. When everything fits, the document has a
    /// single section, the one of the first page.
    /// </remarks>
    /// <param name="content">The paragraphs and tables of the document.</param>
    /// <param name="firstPageSection">The properties of the section of the first page.</param>
    /// <param name="lastSection">The properties of the last section, which the body ends with.</param>
    /// <param name="estimate">Estimates how much room the content takes up.</param>
    /// <returns>The properties of the section which the body ends with.</returns>
    private static W.SectionProperties EndFirstPage(List<OpenXmlElement> content, W.SectionProperties firstPageSection, W.SectionProperties lastSection, WordLayoutEstimate estimate)
    {
        var count = estimate.CountFittingOnFirstPage(content, WordTextArea.Of(firstPageSection));
        if (count == content.Count)
        {
            lastSection.Parent!.ReplaceChild(firstPageSection, lastSection);
            return firstPageSection;
        }

        // The last paragraph of a section carries its properties:
        if (content[count - 1] is W.Paragraph lastParagraph)
            (lastParagraph.ParagraphProperties ??= new W.ParagraphProperties()).SectionProperties = firstPageSection;
        else
            content.Insert(count++, new W.Paragraph(new W.ParagraphProperties { SectionProperties = firstPageSection }));

        // An empty paragraph which separates two blocks would start the next page with a gap:
        if (count < content.Count && content[count] is W.Paragraph { HasChildren: false })
            content.RemoveAt(count);

        return lastSection;
    }

    /// <summary>
    /// Gives the last section what the document keeps of the sections before it, which go with the body.
    /// </summary>
    /// <remarks>
    /// Templates of organizations often start with a section of their own, e.g., a cover page. Its
    /// headers and footers carry the logos and the address, and the sections after it state none:
    /// Word lets a section without a header or footer of a kind inherit the one of the section
    /// before. So the last section takes the headers and footers for regular and even pages which it
    /// inherits. The first page of the document is the first page of the template, so its header and
    /// footer, and whether it has ones of its own at all, come from the first section, e.g., a big
    /// picture above the title. The template leaves room below such a header with a larger top margin
    /// on the first page, which the space this returns stands in for.
    ///
    /// When the first page stays a section of its own, see FirstPageSectionOf, it brings its header
    /// and footer along. The last section then inherits all kinds of them, as in Word, and starts on
    /// a new page, whatever the section before it in the template did.
    /// </remarks>
    /// <param name="body">The body of the template, not cleared yet.</param>
    /// <param name="keepsFirstPage">Whether the first page stays a section of its own.</param>
    /// <returns>How much further down the text of the first page starts than on the other pages, in twentieths of a point; 0 when it does not.</returns>
    private static int MergeSections(W.Body body, bool keepsFirstPage)
    {
        var lastSection = body.Elements<W.SectionProperties>().LastOrDefault();
        var sections = body.Descendants<W.SectionProperties>().ToList();
        if (lastSection is null || sections.Count < 2)
            return 0;

        var firstSection = sections[0];
        var references = new Dictionary<(Type Kind, string Pages), W.HeaderFooterReferenceType>();
        foreach (var reference in sections.SelectMany(section => section.Elements<W.HeaderFooterReferenceType>()).Where(reference => keepsFirstPage || TypeOf(reference) is not "first"))
            references[(reference.GetType(), TypeOf(reference))] = reference;

        if (!keepsFirstPage)
            foreach (var reference in firstSection.Elements<W.HeaderFooterReferenceType>().Where(reference => TypeOf(reference) is "first"))
                references[(reference.GetType(), "first")] = reference;

        //
        // The references go first in the properties of a section. Cloned before the old ones are
        // removed, since some of them are among those, and kept in the order of the template:
        //
        var mergedReferences = references.Values.Select(reference => (W.HeaderFooterReferenceType)reference.CloneNode(true)).ToList();
        foreach (var reference in lastSection.Elements<W.HeaderFooterReferenceType>().ToList())
            reference.Remove();

        for (var index = mergedReferences.Count - 1; index >= 0; index--)
            lastSection.PrependChild(mergedReferences[index]);

        if (keepsFirstPage)
        {
            lastSection.RemoveAllChildren<W.SectionType>();
            return 0;
        }

        lastSection.RemoveAllChildren<W.TitlePage>();
        if (firstSection.GetFirstChild<W.TitlePage>() is { } titlePage && titlePage.Val?.Value is not false)
            InsertTitlePage(lastSection);

        return Math.Max(0, WordTextArea.Of(firstSection).Top - WordTextArea.Of(lastSection).Top);
    }

    /// <summary>
    /// The type of a header or footer reference: "default", "first", or "even". A reference which states none is the default one.
    /// </summary>
    private static string TypeOf(W.HeaderFooterReferenceType reference) => reference.Type?.InnerText ?? "default";

    /// <summary>
    /// Switches on a header and footer of their own for the first page of a section.
    /// </summary>
    /// <remarks>
    /// The schema wants the switch after the layout of the page and before the direction of its text.
    /// </remarks>
    private static void InsertTitlePage(W.SectionProperties section)
    {
        string[] followers = ["textDirection", "bidi", "rtlGutter", "docGrid", "printerSettings", "footnoteColumns", "sectPrChange"];
        var follower = section.ChildElements.FirstOrDefault(element => followers.Contains(element.LocalName));
        if (follower is null)
            section.Append(new W.TitlePage());
        else
            section.InsertBefore(new W.TitlePage(), follower);
    }

    /// <summary>
    /// An empty paragraph of the given height, which moves the text down.
    /// </summary>
    private static W.Paragraph SpaceOf(int height) => new(new W.ParagraphProperties
    {
        SpacingBetweenLines = new W.SpacingBetweenLines { Before = "0", After = "0", Line = height.ToString(), LineRule = W.LineSpacingRuleValues.Exact },
    });

    /// <summary>
    /// Removes everything from the body but the properties of its last section.
    /// </summary>
    /// <remarks>
    /// These properties hold the page size and margins, and they refer to the headers and footers.
    /// The sections before, e.g., a cover page with a header of its own, go with their paragraphs,
    /// once MergeSections took over what the document keeps of them.
    /// </remarks>
    /// <param name="body">The body of the document.</param>
    /// <returns>The properties of the last section, before which the content goes; null when the body states none.</returns>
    private static W.SectionProperties? ClearBody(W.Body body)
    {
        var sectionProperties = body.Elements<W.SectionProperties>().LastOrDefault();
        foreach (var element in body.ChildElements.Where(element => element != sectionProperties).ToList())
            element.Remove();

        return sectionProperties;
    }

    /// <summary>
    /// Removes what only the removed body used, so that Word offers no repair and the document
    /// carries nothing of the template which nobody can see.
    /// </summary>
    /// <remarks>
    /// A picture, a chart, an embedded object, or the header of a removed section is a part of its
    /// own, which the body refers to by the ID of a relationship; the same holds for the target of a
    /// link. Whatever the remaining document no longer refers to goes. Comments are attached to the
    /// removed text, and footnotes and endnotes are called from it, so they go as well. Only the
    /// separators remain, which Word draws above the notes.
    /// </remarks>
    /// <param name="mainPart">The main part of the document, whose body was cleared.</param>
    private static void RemoveWhatOnlyTheBodyUsed(MainDocumentPart mainPart)
    {
        var referencedIds = mainPart.Document!.Descendants()
            .SelectMany(element => element.GetAttributes())
            .Where(attribute => attribute.NamespaceUri == RELATIONSHIPS_NAMESPACE)
            .Select(attribute => attribute.Value)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        foreach (var part in mainPart.Parts.Where(pair => IsComment(pair.OpenXmlPart) || (IsContent(pair.OpenXmlPart) && !referencedIds.Contains(pair.RelationshipId))).ToList())
            mainPart.DeletePart(part.RelationshipId);

        foreach (var relationship in mainPart.HyperlinkRelationships.Concat<ReferenceRelationship>(mainPart.ExternalRelationships).Where(candidate => !referencedIds.Contains(candidate.Id)).ToList())
            mainPart.DeleteReferenceRelationship(relationship);

        foreach (var footnote in mainPart.FootnotesPart?.Footnotes?.Elements<W.Footnote>().Where(note => IsNote(note.Type)).ToList() ?? [])
            footnote.Remove();

        foreach (var endnote in mainPart.EndnotesPart?.Endnotes?.Elements<W.Endnote>().Where(note => IsNote(note.Type)).ToList() ?? [])
            endnote.Remove();
    }

    /// <summary>
    /// Whether the body refers to a part of this kind by the ID of its relationship.
    /// </summary>
    private static bool IsContent(OpenXmlPart part) => part is ImagePart
        or ChartPart
        or ExtendedChartPart
        or DiagramDataPart
        or DiagramLayoutDefinitionPart
        or DiagramStylePart
        or DiagramColorsPart
        or DiagramPersistLayoutPart
        or EmbeddedObjectPart
        or EmbeddedPackagePart
        or EmbeddedControlPersistencePart
        or AlternativeFormatImportPart
        or HeaderPart
        or FooterPart;

    /// <summary>
    /// Whether the part holds comments or their authors.
    /// </summary>
    private static bool IsComment(OpenXmlPart part) => part is WordprocessingCommentsPart
        or WordprocessingCommentsExPart
        or WordprocessingCommentsIdsPart
        or WordCommentsExtensiblePart
        or WordprocessingPeoplePart;

    /// <summary>
    /// Whether a footnote or endnote is a note of the text rather than a separator Word draws above the notes.
    /// </summary>
    private static bool IsNote(EnumValue<W.FootnoteEndnoteValues>? type) => (type?.InnerText ?? "normal") == "normal";
}