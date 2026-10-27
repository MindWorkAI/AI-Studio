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

    [Test]
    public void ListsAreNumberedLevelByLevel()
    {
        using var file = Write("1. Earth\n2. Mars\n   - Phobos\n   - Deimos\n3. Jupiter\n\n- Sun");
        using var document = WordprocessingDocument.Open(file, false);

        var items = ParagraphsOf(document).Where(paragraph => paragraph.ParagraphProperties?.NumberingProperties is not null).ToList();
        var levels = items.Select(item => item.ParagraphProperties!.NumberingProperties!.NumberingLevelReference!.Val!.Value).ToList();
        var instances = items.Select(item => item.ParagraphProperties!.NumberingProperties!.NumberingId!.Val!.Value).ToList();
        var styleIds = items.Select(item => item.ParagraphProperties!.ParagraphStyleId?.Val?.Value).Distinct().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(items.Select(item => item.InnerText), Is.EqualTo(new[] { "Earth", "Mars", "Phobos", "Deimos", "Jupiter", "Sun" }));
            Assert.That(levels, Is.EqualTo(new[] { 0, 0, 1, 1, 0, 0 }));
            Assert.That(new[] { instances[1], instances[4] }, Is.All.EqualTo(instances[0]), "The numbered list goes on counting after the list its second item holds.");
            Assert.That(NumberFormatOf(document, instances[0], 0), Is.EqualTo("decimal"));
            Assert.That(NumberFormatOf(document, instances[2], 1), Is.EqualTo("bullet"));
            Assert.That(instances[5], Is.EqualTo(instances[2]), "Bullets have nothing to count, so all bulleted lists share their numbering.");
            Assert.That(styleIds, Has.Count.EqualTo(1));
            Assert.That(StylesOf(document).Single(style => style.StyleId?.Value == styleIds[0]).StyleName?.Val?.Value, Is.EqualTo("List Paragraph"));
        });
    }

    [Test]
    public void EveryNumberedListCountsAnew()
    {
        using var file = Write("1. Earth\n2. Mars\n\nBetween the lists.\n\n3. Jupiter\n4. Saturn");
        using var document = WordprocessingDocument.Open(file, false);

        var instances = ParagraphsOf(document)
            .Select(paragraph => paragraph.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value)
            .OfType<int>()
            .Distinct()
            .ToList();

        var numbering = document.MainDocumentPart!.NumberingDefinitionsPart!.Numbering!;
        var starts = instances.Select(instance => numbering.Elements<W.NumberingInstance>().Single(candidate => candidate.NumberID?.Value == instance).Descendants<W.StartOverrideNumberingValue>().Single().Val?.Value);
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(instances, Has.Count.EqualTo(2), "Each list has a numbering of its own; otherwise Word would go on counting.");
            Assert.That(starts, Is.EqualTo(new int?[] { 1, 3 }), "Each list starts with the number the Markdown gives its first item.");
        });
    }

    [Test]
    public void ATaskListShowsItsBoxes()
    {
        using var file = Write("- [ ] Launch\n- [x] Land");
        using var document = WordprocessingDocument.Open(file, false);

        Assert.That(ParagraphsOf(document).Select(paragraph => paragraph.InnerText).Where(text => text.Length > 0), Is.EqualTo(new[] { "☐ Launch", "☑ Land" }));
    }

    [Test]
    public void ATableKeepsItsHeaderAndTheAlignmentOfItsColumns()
    {
        using var file = Write("| Planet | Moons |\n|---|--:|\n| Mars | 2 |\n| Earth |");
        using var document = WordprocessingDocument.Open(file, false);

        var body = document.MainDocumentPart!.Document!.Body!;
        var table = body.Elements<W.Table>().Single();
        var rows = table.Elements<W.TableRow>().ToList();
        var tableStyleId = table.GetFirstChild<W.TableProperties>()?.TableStyle?.Val?.Value;
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(rows.Select(row => row.Elements<W.TableCell>().Select(cell => cell.InnerText)), Is.EqualTo(new[] { new[] { "Planet", "Moons" }, new[] { "Mars", "2" }, new[] { "Earth", string.Empty } }), "A row with a cell too few gets an empty one.");
            Assert.That(rows.Select(row => row.TableRowProperties?.GetFirstChild<W.TableHeader>() is not null), Is.EqualTo(new[] { true, false, false }), "The header row repeats on every page.");
            Assert.That(rows.Select(row => row.Elements<W.TableCell>().ElementAt(1).Descendants<W.Justification>().Single().Val?.InnerText), Is.All.EqualTo("right"), "The second column is aligned to the right, as the line below the header says.");
            Assert.That(rows.Select(row => row.Elements<W.TableCell>().First().Descendants<W.Justification>().Any()), Is.All.False, "The first column keeps the alignment of the template.");
            Assert.That(StylesOf(document).Single(style => style.StyleId?.Value == tableStyleId).StyleName?.Val?.Value, Is.EqualTo("Table Grid"), "The template names no table style for new tables and lacks the grid, which is added.");
            Assert.That(table.NextSibling(), Is.InstanceOf<W.Paragraph>(), "Word wants the document to end with a paragraph.");
        });
    }

    [Test]
    public void TwoTablesInARowStayApart()
    {
        using var file = Write("| Planet |\n|---|\n| Mars |\n\n| Moon |\n|---|\n| Phobos |");
        using var document = WordprocessingDocument.Open(file, false);

        var body = document.MainDocumentPart!.Document!.Body!;
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(body.ChildElements.Select(element => element.GetType()), Is.EqualTo(new[] { typeof(W.Table), typeof(W.Paragraph), typeof(W.Table), typeof(W.Paragraph), typeof(W.SectionProperties) }), "Word would join two tables without a paragraph between them.");
        });
    }

    [Test]
    public void CodeKeepsItsLinesAndIndentation()
    {
        using var file = Write("```\nif (mars)\n\tland();\n    wait();\n```");
        using var document = WordprocessingDocument.Open(file, false);

        var code = ParagraphsOf(document).Single();
        var styleId = code.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(StylesOf(document).Single(style => style.StyleId?.Value == styleId).StyleName?.Val?.Value, Is.EqualTo("HTML Preformatted"));
            Assert.That(code.Descendants<W.Text>().Select(text => text.Text), Is.EqualTo(new[] { "if (mars)", "land();", "    wait();" }), "The spaces in front of a line stay.");
            Assert.That(code.Descendants<W.Break>().Count(), Is.EqualTo(2), "The block stays one paragraph, with its lines.");
            Assert.That(code.Descendants<W.TabChar>().Count(), Is.EqualTo(1));
        });
    }

    [Test]
    public void AQuotationTakesTheQuoteStyle()
    {
        using var file = Write("> Mars is red.\n\nEarth is blue.");
        using var document = WordprocessingDocument.Open(file, false);

        var styleIds = ParagraphsOf(document).Select(paragraph => paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(StylesOf(document).Single(style => style.StyleId?.Value == styleIds[0]).StyleName?.Val?.Value, Is.EqualTo("Quote"));
            Assert.That(styleIds[1], Is.Null, "The quotation ends with its paragraph.");
        });
    }

    [Test]
    public void FootnotesFollowTheTextWithTheirNumbers()
    {
        using var file = Write("Mars is red.[^1]\n\n[^1]: Its dust holds iron oxide.");
        using var document = WordprocessingDocument.Open(file, false);

        var raised = document.MainDocumentPart!.Document!.Body!.Descendants<W.Run>()
            .Where(run => run.RunProperties?.VerticalTextAlignment?.Val?.InnerText == "superscript")
            .Select(run => run.InnerText);

        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(ParagraphsOf(document).Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { "Mars is red.1", string.Empty, "1 Its dust holds iron oxide." }), "A line separates the notes from the text.");
            Assert.That(raised, Is.EqualTo(new[] { "1", "1" }));
        });
    }

    [Test]
    public void TheSourcesAndTheNoticeOfAnAnswerComeAlong()
    {
        // The way TryGetExportMarkdown and the notice that AI generated the text end an answer:
        using var file = Write("Mars is close.\n\n# Sources\n\n## Web\n- [1] [NASA: Mars](https://example.org/nasa)\n- [2] [ESA: Mars](https://example.org/esa)\n\n**This content was generated using artificial intelligence (AI).**");
        using var document = WordprocessingDocument.Open(file, false);

        var paragraphs = ParagraphsOf(document);
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(paragraphs.Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { "Mars is close.", "Sources", "Web", "[1] NASA: Mars", "[2] ESA: Mars", "This content was generated using artificial intelligence (AI)." }));
            Assert.That(paragraphs[1].ParagraphProperties?.ParagraphStyleId?.Val?.Value, Is.EqualTo(TestWordTemplate.HEADING_1_STYLE_ID));
            Assert.That(paragraphs.Skip(3).Take(2).Select(paragraph => paragraph.Descendants<W.Hyperlink>().Count()), Is.All.EqualTo(1), "Each source links to where it came from.");
            Assert.That(paragraphs[^1].Descendants<W.Bold>(), Is.Not.Empty);
        });
    }

    private static string? NumberFormatOf(WordprocessingDocument document, int instanceId, int level)
    {
        var numbering = document.MainDocumentPart!.NumberingDefinitionsPart!.Numbering!;
        var definitionId = numbering.Elements<W.NumberingInstance>().Single(instance => instance.NumberID?.Value == instanceId).AbstractNumId!.Val!.Value;
        return numbering.Elements<W.AbstractNum>()
            .Single(definition => definition.AbstractNumberId?.Value == definitionId)
            .Elements<W.Level>()
            .Single(candidate => candidate.LevelIndex?.Value == level)
            .NumberingFormat?.Val?.InnerText;
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