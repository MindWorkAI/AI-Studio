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
/// up between the content.
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
    public static void Write(Stream documentFile, string markdown)
    {
        using var document = WordprocessingDocument.Open(documentFile, true);

        // A template (.dotx) becomes a document; everything else about it stays:
        if (document.DocumentType != WordprocessingDocumentType.Document)
            document.ChangeDocumentType(WordprocessingDocumentType.Document);

        var mainPart = document.MainDocumentPart ?? throw new InvalidDataException("The file contains no Word document.");
        var body = mainPart.Document?.Body ?? throw new InvalidDataException("The file contains no Word document.");

        var sectionProperties = ClearBody(body);
        RemoveWhatOnlyTheBodyUsed(mainPart);

        var content = new MarkdownWordConverter(mainPart, WordStyles.Of(mainPart)).Convert(markdown);

        // Word refuses a body without any paragraph:
        if (content.Count is 0)
            content.Add(new W.Paragraph());

        foreach (var element in content)
            if (sectionProperties is null)
                body.Append(element);
            else
                body.InsertBefore(element, sectionProperties);
    }

    /// <summary>
    /// Removes everything from the body but the properties of its last section.
    /// </summary>
    /// <remarks>
    /// These properties hold the page size and margins, and they refer to the headers and footers.
    /// The sections before, e.g., a cover page with a header of its own, go with their paragraphs.
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