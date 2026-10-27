using AIStudio.Tools.Word;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tests.Tools.Word;

/// <summary>
/// Checks the documents written into a template: valid files, the body replaced, the styles of the
/// template used, and nothing of the removed body left behind.
/// </summary>
/// <remarks>
/// TestWordTemplate describes what the template holds.
/// </remarks>
[TestFixture]
public sealed class WordDocumentWriterTests
{
    [Test]
    public void TheBodyHoldsTheContentOnlyAndTheLastSectionStays()
    {
        using var file = Write("Mars is close to Earth.");
        using var document = WordprocessingDocument.Open(file, false);

        var body = document.MainDocumentPart!.Document!.Body!;
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(body.Elements<W.Paragraph>().Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { "Mars is close to Earth." }), "The cover page and the sample text of the template are gone.");
            Assert.That(body.LastChild, Is.InstanceOf<W.SectionProperties>(), "The last section keeps its page size and header.");
            Assert.That(document.MainDocumentPart.HeaderParts.Select(part => part.Header!.InnerText), Is.EqualTo(new[] { TestWordTemplate.HEADER_TEXT }), "The header of the last section stays, the one of the removed cover page goes.");
        });
    }

    [Test]
    public void WhatOnlyTheBodyUsedIsGone()
    {
        using var file = Write("Mars is close to Earth.");
        using var document = WordprocessingDocument.Open(file, false);

        var mainPart = document.MainDocumentPart!;
        Assert.Multiple(() =>
        {
            Assert.That(mainPart.ImageParts, Is.Empty, "The picture of the sample text is gone.");
            Assert.That(mainPart.HyperlinkRelationships, Is.Empty, "The target of the sample link is gone.");
            Assert.That(mainPart.WordprocessingCommentsPart, Is.Null, "The comment on the sample text is gone.");
            Assert.That(mainPart.FootnotesPart!.Footnotes!.Elements<W.Footnote>().Select(footnote => footnote.Type?.InnerText), Is.EqualTo(new[] { "separator", "continuationSeparator" }), "Only the separators of the notes remain.");
        });
    }

    [Test]
    public void ATemplateBecomesADocument()
    {
        using var file = Write("Mars is close to Earth.", asTemplate: true);
        using var document = WordprocessingDocument.Open(file, false);

        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(document.DocumentType, Is.EqualTo(WordprocessingDocumentType.Document));
        });
    }

    [Test]
    public void AHeadingTakesTheStyleOfTheTemplateByItsName()
    {
        using var file = Write("# Mars\n\n## Orbit\n\nClose to Earth.");
        using var document = WordprocessingDocument.Open(file, false);

        var styleIds = ParagraphsOf(document).Select(paragraph => paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value).ToList();
        var addedStyle = StylesOf(document).SingleOrDefault(style => style.StyleId?.Value == styleIds[1]);
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(styleIds[0], Is.EqualTo(TestWordTemplate.HEADING_1_STYLE_ID), "A German Word stores the first heading under an ID of its own, and the name finds it.");
            Assert.That(addedStyle?.StyleName?.Val?.Value, Is.EqualTo("heading 2"), "The template lacks the second heading level, which is added under the name Word knows.");
            Assert.That(addedStyle?.BasedOn?.Val?.Value, Is.EqualTo(TestWordTemplate.NORMAL_STYLE_ID), "The added heading looks like the text of the template, except for its size.");
            Assert.That(styleIds[2], Is.Null, "A paragraph takes the default style of the template.");
        });
    }

    [Test]
    public void ALinkLeadsToItsTarget()
    {
        using var file = Write("Read [the plan](https://example.org/mars) or <https://example.org/moon>.");
        using var document = WordprocessingDocument.Open(file, false);

        var mainPart = document.MainDocumentPart!;
        var links = mainPart.Document!.Body!.Descendants<W.Hyperlink>().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(links.Select(link => link.InnerText), Is.EqualTo(new[] { "the plan", "https://example.org/moon" }));
            Assert.That(links.Select(link => mainPart.HyperlinkRelationships.Single(relationship => relationship.Id == link.Id?.Value).Uri.ToString()), Is.EqualTo(new[] { "https://example.org/mars", "https://example.org/moon" }));
            Assert.That(links.SelectMany(link => link.Descendants<W.RunStyle>()).Select(style => style.Val?.Value), Is.All.EqualTo(TestWordTemplate.HYPERLINK_STYLE_ID), "A link takes the link style of the template.");
        });
    }

    [Test]
    public void ALinkWithinTheAnswerKeepsItsTextOnly()
    {
        using var file = Write("As said [above](#mars), Mars is close.");
        using var document = WordprocessingDocument.Open(file, false);

        Assert.Multiple(() =>
        {
            Assert.That(document.MainDocumentPart!.Document!.Body!.Descendants<W.Hyperlink>(), Is.Empty, "A heading of the answer is no place in the document.");
            Assert.That(ParagraphsOf(document).Single().InnerText, Is.EqualTo("As said above, Mars is close."));
        });
    }

    [Test]
    public void WhatMarkdownMarksIsFormattedDirectly()
    {
        using var file = Write("**far** *close* ~~flat~~ `orbit`");
        using var document = WordprocessingDocument.Open(file, false);

        var runs = ParagraphsOf(document).Single().Elements<W.Run>().Where(run => run.InnerText.Trim().Length > 0).ToDictionary(run => run.InnerText);
        var codeStyle = StylesOf(document).Single(style => style.StyleId?.Value == runs["orbit"].RunProperties?.RunStyle?.Val?.Value);
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(runs["far"].RunProperties?.Bold, Is.Not.Null);
            Assert.That(runs["close"].RunProperties?.Italic, Is.Not.Null);
            Assert.That(runs["flat"].RunProperties?.Strike, Is.Not.Null);
            Assert.That(codeStyle.StyleName?.Val?.Value, Is.EqualTo("HTML Code"), "Code takes a character style, which the template lacks and which is added.");
        });
    }

    [Test]
    public void ACharacterXmlCannotCarryIsLeftOut()
    {
        using var file = Write("Mars\u0001 is close.");
        using var document = WordprocessingDocument.Open(file, false);

        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(ParagraphsOf(document).Single().InnerText, Is.EqualTo("Mars is close."));
        });
    }

    [Test]
    public void AnEmptyAnswerLeavesAnEmptyParagraph()
    {
        using var file = Write(string.Empty);
        using var document = WordprocessingDocument.Open(file, false);

        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(ParagraphsOf(document), Has.Count.EqualTo(1), "Word refuses a body without any paragraph.");
        });
    }

    private static MemoryStream Write(string markdown, bool asTemplate = false)
    {
        // The writer edits a copy of the template, as WordDocumentFile does with one on disk:
        var file = TestWordTemplate.Create(asTemplate);
        WordDocumentWriter.Write(file, markdown);
        file.Position = 0;
        return file;
    }

    private static List<W.Paragraph> ParagraphsOf(WordprocessingDocument document) => document.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().ToList();

    private static List<W.Style> StylesOf(WordprocessingDocument document) => document.MainDocumentPart!.StyleDefinitionsPart!.Styles!.Elements<W.Style>().ToList();

    private static List<string> ValidationErrorsOf(WordprocessingDocument document) => new OpenXmlValidator(FileFormatVersions.Office2019)
        .Validate(document)
        .Select(error => $"{error.Part?.Uri} {error.Path?.XPath}: {error.Description}")
        .ToList();
}