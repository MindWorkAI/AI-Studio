using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.Word;

/// <summary>
/// Writes Markdown into a copy of a Word template.
/// </summary>
/// <remarks>
/// The document is the template itself, with the text of its body replaced: styles, page layout,
/// headers and footers, and the theme stay exactly as their author made them. The sample text of
/// the template goes along with the rest of the body, so that nothing of it ends up between the
/// content, and so do pages after it, e.g., instructions. A cover page without text stays as it
/// is, together with what follows it up to the text, e.g., a table of contents. Otherwise, the
/// first page keeps the header and footer of the first page of the template, see MergeSections,
/// and the text starts where the one of the template does, see WordTemplatePages. When the text
/// runs in other columns there, the first page keeps them as well, see FirstPageSectionOf. The
/// pictures, boxes, and lines of the design around the text stay on their page, see WordShapes.
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
    /// <param name="headingLevels">The heading levels of the Markdown which become the first heading
    /// level of the template, see MarkdownWordConverter.HeadingLevelsOf.</param>
    public static void Write(Stream documentFile, string markdown, WordHeadingLevels headingLevels)
    {
        using var document = WordprocessingDocument.Open(documentFile, true);

        // A template (.dotx) becomes a document; everything else about it stays:
        if (document.DocumentType != WordprocessingDocumentType.Document)
            document.ChangeDocumentType(WordprocessingDocumentType.Document);

        var mainPart = document.MainDocumentPart ?? throw new InvalidDataException("The file contains no Word document.");
        var body = mainPart.Document?.Body ?? throw new InvalidDataException("The file contains no Word document.");

        var numbering = new WordNumbering(mainPart);
        var styles = WordStyles.Of(mainPart);
        var pages = WordTemplatePages.Of(body, styles, new WordLayoutEstimate(mainPart.StyleDefinitionsPart?.Styles, numbering));
        var firstPageSection = FirstPageSectionOf(body, pages);
        var firstPageSpace = MergeSections(body, pages, firstPageSection is not null);
        var sectionProperties = ClearBody(body);

        // A title box of the template takes the heading the answer starts with:
        var converter = new MarkdownWordConverter(mainPart, styles, numbering);
        var titleBox = WordShapes.TitleBoxOf(pages.FirstPageShapes, styles);
        var text = markdown;
        var title = titleBox is null ? null : converter.TitleOf(markdown, out text);
        if (titleBox is not null && title is not null)
            WordShapes.FillTitleBox(titleBox, title, styles);

        var content = converter.Convert(text, title is null ? headingLevels.TopLevel : headingLevels.TopLevelBelowTitle);
        content.InsertRange(0, pages.LeadingEmptyParagraphs);
        if (firstPageSpace > 0)
            content.Insert(0, SpaceOf(firstPageSpace));

        // A cover page stays, with what follows it up to the text, e.g., a table of contents, which Word fills when it opens the document:
        content.InsertRange(0, pages.FrontMatter);
        if (pages.FrontMatter.Any(block => block.Descendants<W.FieldChar>().Any() || block.Descendants<W.SimpleField>().Any()))
            UpdateFieldsOnOpen(mainPart);

        // The estimate knows the styles the content added by now:
        var estimate = new WordLayoutEstimate(mainPart.StyleDefinitionsPart?.Styles, numbering);
        KeepTextClearOfFooters(mainPart, firstPageSection, sectionProperties, pages, estimate);
        if (firstPageSection is not null && sectionProperties is not null)
        {
            (sectionProperties, var nextPageStart) = EndFirstPage(content, firstPageSection, sectionProperties, estimate);
            PlaceShapes(content, nextPageStart, pages.NextPageShapes);
        }

        PlaceShapes(content, 0, pages.FirstPageShapes);
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
    /// on the right on the others. Then the first page stays a section of its own. A cover page stays
    /// with its own sections anyway, and so does the document take the layout of the main section
    /// throughout when the text of the first page only starts further down.
    /// </remarks>
    /// <param name="body">The body of the template, not cleared yet.</param>
    /// <param name="pages">What the pages of the template hold.</param>
    /// <returns>A copy of the properties; null when the first page takes the layout of the main section.</returns>
    private static W.SectionProperties? FirstPageSectionOf(W.Body body, WordTemplatePages pages)
    {
        var sections = body.Descendants<W.SectionProperties>().ToList();
        if (pages.FirstPageIsCover || sections.Count < 2 || pages.MainSection is not { } mainSection || mainSection == sections[0])
            return null;

        return WordTextArea.Of(sections[0]).HasSameColumnsAs(WordTextArea.Of(mainSection)) ? null : (W.SectionProperties)sections[0].CloneNode(true);
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
    /// <returns>The properties of the section which the body ends with, and the index of the first
    /// block of the next page in the content; its count when everything fits on the first page.</returns>
    private static (W.SectionProperties LastSection, int NextPageStart) EndFirstPage(List<OpenXmlElement> content, W.SectionProperties firstPageSection, W.SectionProperties lastSection, WordLayoutEstimate estimate)
    {
        var count = estimate.CountFittingOnFirstPage(content, WordTextArea.Of(firstPageSection));
        if (count == content.Count)
        {
            lastSection.Parent!.ReplaceChild(firstPageSection, lastSection);
            return (firstPageSection, count);
        }

        // The last paragraph of a section carries its properties:
        if (content[count - 1] is W.Paragraph lastParagraph)
            (lastParagraph.ParagraphProperties ??= new W.ParagraphProperties()).SectionProperties = firstPageSection;
        else
            content.Insert(count++, new W.Paragraph(new W.ParagraphProperties { SectionProperties = firstPageSection }));

        // An empty paragraph which separates two blocks would start the next page with a gap:
        if (count < content.Count && content[count] is W.Paragraph { HasChildren: false })
            content.RemoveAt(count);

        return (lastSection, count);
    }

    /// <summary>
    /// Moves the bottom margin of the sections up as far as the shapes of their footers reach across
    /// the text, see WordShapes.FootOf.
    /// </summary>
    /// <remarks>
    /// Templates keep their sample text short enough to end above such a shape, e.g., an address in
    /// a text box above the bottom margin; an answer runs on. The footers which count are those the
    /// pages of a section show: the one of its first page, when it has one of its own, and those of
    /// the others. A first page which stays a section of its own counts the footer of its first page
    /// only, and the last section after it inherits the footers it does not state, as in Word. So it
    /// does from the sections of a cover page and what follows it, whose margins stay as they are.
    /// </remarks>
    /// <param name="mainPart">The main part of the document, which keeps the footers.</param>
    /// <param name="firstPageSection">The properties of the section of the first page; null when the first page has none of its own.</param>
    /// <param name="lastSection">The properties of the last section; null when the body states none.</param>
    /// <param name="pages">What the pages of the template hold, e.g., the sections of a cover page.</param>
    /// <param name="estimate">Estimates the height of the paragraphs of the footers.</param>
    private static void KeepTextClearOfFooters(MainDocumentPart mainPart, W.SectionProperties? firstPageSection, W.SectionProperties? lastSection, WordTemplatePages pages, WordLayoutEstimate estimate)
    {
        var hasEvenPages = mainPart.DocumentSettingsPart?.Settings?.GetFirstChild<W.EvenAndOddHeaders>() is { } evenAndOdd && evenAndOdd.Val?.Value is not false;
        if (firstPageSection is not null)
            KeepTextClearOf(FootersOf(mainPart, firstPageSection, [], true, hasEvenPages), firstPageSection, estimate);

        var sectionsBefore = firstPageSection is not null
            ? new List<W.SectionProperties> { firstPageSection }
            : pages.FrontMatter.Select(block => (block as W.Paragraph)?.ParagraphProperties?.SectionProperties).OfType<W.SectionProperties>().ToList();

        if (lastSection is not null)
            KeepTextClearOf(FootersOf(mainPart, lastSection, sectionsBefore, false, hasEvenPages), lastSection, estimate);
    }

    /// <summary>
    /// Returns the footers which the pages of a section show.
    /// </summary>
    /// <param name="mainPart">The main part of the document, which keeps the footers.</param>
    /// <param name="section">The properties of the section.</param>
    /// <param name="sectionsBefore">The properties of the sections before it, in the order of the document, whose footers it inherits.</param>
    /// <param name="firstPageOnly">Whether only the first page of the section counts.</param>
    /// <param name="hasEvenPages">Whether the document shows other headers and footers on even pages.</param>
    private static IEnumerable<W.Footer> FootersOf(MainDocumentPart mainPart, W.SectionProperties section, IReadOnlyList<W.SectionProperties> sectionsBefore, bool firstPageOnly, bool hasEvenPages)
    {
        var hasFirstPage = section.GetFirstChild<W.TitlePage>() is { } titlePage && titlePage.Val?.Value is not false;
        var pages = new List<string>();
        if (hasFirstPage)
            pages.Add("first");

        if (!firstPageOnly || !hasFirstPage)
            pages.Add("default");

        if (!firstPageOnly && hasEvenPages)
            pages.Add("even");

        // The nearest section which states a footer of a kind passes it on:
        var references = sectionsBefore.Reverse().Prepend(section).SelectMany(candidate => candidate.Elements<W.FooterReference>()).ToList();
        foreach (var page in pages)
            if (references.FirstOrDefault(reference => TypeOf(reference) == page)?.Id?.Value is { } id && mainPart.TryGetPartById(id, out var part) && part is FooterPart { Footer: { } footer })
                yield return footer;
    }

    /// <summary>
    /// Moves the bottom margin of a section up as far as the shapes of its footers reach across the text.
    /// </summary>
    private static void KeepTextClearOf(IEnumerable<W.Footer> footers, W.SectionProperties section, WordLayoutEstimate estimate)
    {
        if (section.GetFirstChild<W.PageMargin>() is not { } margin)
            return;

        var area = WordTextArea.Of(section);
        var foot = footers.Select(footer => WordShapes.FootOf(footer, area, (int)(margin.Footer?.Value ?? 720U), estimate)).DefaultIfEmpty(0).Max();

        // A negative margin keeps its size even when the footer grows:
        if (foot > area.Bottom)
            margin.Bottom = margin.Bottom?.Value < 0 ? -foot : foot;
    }

    /// <summary>
    /// Puts the shapes of the design of a page into the first paragraph of the page.
    /// </summary>
    /// <remarks>
    /// The shapes state their positions in relation to the page, so it matters only on which page
    /// their paragraph is. A page which starts with a table gets a paragraph of a single point for them.
    /// </remarks>
    /// <param name="content">The paragraphs and tables of the document.</param>
    /// <param name="pageStart">The index of the first block of the page in the content.</param>
    /// <param name="shapes">The shapes, each in a run of its own, see WordTemplatePages.</param>
    private static void PlaceShapes(List<OpenXmlElement> content, int pageStart, IReadOnlyList<W.Run> shapes)
    {
        if (shapes.Count is 0 || pageStart >= content.Count)
            return;

        if (content[pageStart] is not W.Paragraph paragraph)
            content.Insert(pageStart, paragraph = SpaceOf(20));

        OpenXmlElement? previous = paragraph.ParagraphProperties;
        foreach (var shape in shapes)
            previous = previous is null ? paragraph.PrependChild(shape) : paragraph.InsertAfter(shape, previous);
    }

    /// <summary>
    /// Gives the last section the layout of the main section, and what the document keeps of the
    /// sections before it, which go with the body.
    /// </summary>
    /// <remarks>
    /// The pages of the answer take the layout of the main section, which holds the text of the
    /// template, see WordTemplatePages; sections after it go, e.g., pages of instructions with
    /// headers of their own. So the last section becomes a copy of the main one.
    ///
    /// Templates of organizations often start with a section of their own, e.g., a cover page. Its
    /// headers and footers carry the logos and the address, and the sections after it state none:
    /// Word lets a section without a header or footer of a kind inherit the one of the section
    /// before. So the last section takes the headers and footers for regular and even pages which it
    /// inherits. The first page of the document is the first page of the template, so its header and
    /// footer, and whether it has ones of its own at all, come from the first section, e.g., a big
    /// picture above the title. The template leaves room below such a header with a larger top margin
    /// on the first page, which the space this returns stands in for.
    ///
    /// Sections may stay before the last one: those of a cover page and of what follows it up to the
    /// text, or the section of a first page with columns of its own, see FirstPageSectionOf. They
    /// bring their headers and footers along, and the last section inherits them, as in Word. It takes
    /// only those which the sections between them state, and none the sections before refer to as
    /// well: Word refuses a document in which two sections refer to the same header or footer, which
    /// the validator of the Open XML SDK does not notice. After a first page of its own, the last
    /// section starts on a new page, whatever the section before it in the template did.
    /// </remarks>
    /// <param name="body">The body of the template, not cleared yet.</param>
    /// <param name="pages">What the pages of the template hold.</param>
    /// <param name="keepsFirstPage">Whether the first page stays a section of its own.</param>
    /// <returns>How much further down the text of the first page starts than on the other pages, in twentieths of a point; 0 when it does not.</returns>
    private static int MergeSections(W.Body body, WordTemplatePages pages, bool keepsFirstPage)
    {
        var lastSection = body.Elements<W.SectionProperties>().LastOrDefault();
        var sections = body.Descendants<W.SectionProperties>().ToList();
        if (lastSection is null || sections.Count < 2)
            return 0;

        var mainIndex = pages.MainSection is { } mainSection ? sections.IndexOf(mainSection) : -1;
        if (mainIndex < 0)
            mainIndex = sections.Count - 1;

        if (mainIndex < sections.Count - 1)
        {
            var copy = (W.SectionProperties)sections[mainIndex].CloneNode(true);
            body.ReplaceChild(copy, lastSection);
            lastSection = copy;
        }

        var firstSection = sections[0];
        var sectionsBefore = keepsFirstPage ? 1 : pages.SectionsOfFrontMatter;
        var partsOfSectionsBefore = sections.Take(sectionsBefore).SelectMany(section => section.Elements<W.HeaderFooterReferenceType>()).Select(reference => reference.Id?.Value).ToHashSet();
        var references = new Dictionary<(Type Kind, string Pages), W.HeaderFooterReferenceType>();
        foreach (var reference in sections.Take(mainIndex + 1).Skip(sectionsBefore).SelectMany(section => section.Elements<W.HeaderFooterReferenceType>()))
            if (sectionsBefore > 0 ? !partsOfSectionsBefore.Contains(reference.Id?.Value) : TypeOf(reference) is not "first")
                references[(reference.GetType(), TypeOf(reference))] = reference;

        if (sectionsBefore is 0)
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
            lastSection.RemoveAllChildren<W.SectionType>();

        if (sectionsBefore > 0)
            return 0;

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
        InsertBeforeFirst(section, new W.TitlePage(), element => followers.Contains(element.LocalName));
    }

    /// <summary>
    /// Makes Word update the fields of the document when it opens it, after asking, e.g., a table of
    /// contents, which lists the headings of the answer only then.
    /// </summary>
    /// <remarks>
    /// The schema wants the switch after the settings of the document as a whole and before those of
    /// its shapes, notes, and compatibility; the extensions of later versions of Word come last.
    /// </remarks>
    private static void UpdateFieldsOnOpen(MainDocumentPart mainPart)
    {
        var settingsPart = mainPart.DocumentSettingsPart ?? mainPart.AddNewPart<DocumentSettingsPart>();
        var settings = settingsPart.Settings ??= new W.Settings();
        settings.RemoveAllChildren<W.UpdateFieldsOnOpen>();

        string[] followers = ["hdrShapeDefaults", "footnotePr", "endnotePr", "compat", "docVars", "rsids", "attachedSchema", "themeFontLang", "clrSchemeMapping", "doNotIncludeSubdocsInStats", "doNotAutoCompressPictures", "forceUpgrade", "captions", "readModeInkLockDown", "smartTagType", "shapeDefaults", "doNotEmbedSmartTags", "decimalSymbol", "listSeparator"];
        InsertBeforeFirst(settings, new W.UpdateFieldsOnOpen { Val = true }, element => followers.Contains(element.LocalName) || element.NamespaceUri != settings.NamespaceUri);
    }

    /// <summary>
    /// Inserts an element before the first child which the schema wants after it, or at the end.
    /// </summary>
    private static void InsertBeforeFirst(OpenXmlElement parent, OpenXmlElement element, Func<OpenXmlElement, bool> isFollower)
    {
        if (parent.ChildElements.FirstOrDefault(isFollower) is { } follower)
            parent.InsertBefore(element, follower);
        else
            parent.Append(element);
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