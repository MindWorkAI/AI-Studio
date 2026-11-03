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
    public void TheHeadersAndFootersOfTheCoverPageStay()
    {
        using var file = Write("Mars is close to Earth.", new TestWordTemplateOptions(CoverPage: true));
        using var document = WordprocessingDocument.Open(file, false);

        var mainPart = document.MainDocumentPart!;
        var section = mainPart.Document!.Body!.Elements<W.SectionProperties>().Single();
        var parts = section.Elements<W.HeaderFooterReferenceType>().ToDictionary(reference => $"{reference.LocalName} {reference.Type?.InnerText}", reference => mainPart.GetPartById(reference.Id!.Value!));
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(parts.Keys, Is.EquivalentTo(new[] { "headerReference default", "headerReference first", "footerReference default" }), "The last section inherits the header and footer of the cover page, which the template states only there.");
            Assert.That(((HeaderPart)parts["headerReference default"]).Header!.InnerText, Is.EqualTo(TestWordTemplate.HEADER_TEXT));
            Assert.That(((HeaderPart)parts["headerReference first"]).Header!.InnerText, Is.EqualTo(TestWordTemplate.COVER_HEADER_TEXT), "The first page of the document is the first page of the template.");
            Assert.That(((FooterPart)parts["footerReference default"]).Footer!.InnerText, Is.EqualTo(TestWordTemplate.FOOTER_TEXT));
            Assert.That(((FooterPart)parts["footerReference default"]).ImageParts, Is.Not.Empty, "The logo in the footer stays.");
            Assert.That(section.GetFirstChild<W.TitlePage>(), Is.Not.Null, "Only with this switch does the first page show its own header.");
        });
    }

    [Test]
    public void TheTextOfTheFirstPageStartsBelowTheHeaderOfTheCoverPage()
    {
        using var file = Write("Mars is close to Earth.", new TestWordTemplateOptions(CoverPage: true));
        using var document = WordprocessingDocument.Open(file, false);

        var paragraphs = ParagraphsOf(document);
        var spacing = paragraphs[0].ParagraphProperties?.SpacingBetweenLines;
        Assert.Multiple(() =>
        {
            Assert.That(paragraphs.Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { string.Empty, "Mars is close to Earth." }));
            Assert.That(spacing?.Line?.Value, Is.EqualTo((TestWordTemplate.COVER_TOP_MARGIN - TestWordTemplate.TOP_MARGIN).ToString()), "The empty paragraph makes up for the larger top margin of the cover page.");
            Assert.That(spacing?.LineRule?.InnerText, Is.EqualTo("exact"));
        });
    }

    [Test]
    public void TheTextStartsBelowTheEmptyParagraphsOfTheFirstPage()
    {
        using var file = Write("Mars is close to Earth.", new TestWordTemplateOptions(CoverPage: true, LeadingEmptyParagraphs: 2));
        using var document = WordprocessingDocument.Open(file, false);

        var paragraphs = ParagraphsOf(document);
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(paragraphs.Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { string.Empty, string.Empty, string.Empty, "Mars is close to Earth." }));
            Assert.That(paragraphs[0].ParagraphProperties?.SpacingBetweenLines?.LineRule?.InnerText, Is.EqualTo("exact"), "The space for the larger top margin of the cover page comes first, as the margin does.");
            Assert.That(paragraphs.Skip(1).Take(2).Select(paragraph => paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value), Is.All.EqualTo(TestWordTemplate.HEADING_1_STYLE_ID), "The empty paragraphs move the text down as far as in the template, with the height of their style.");
        });
    }

    [Test]
    public void ACoverPageWithoutTextLeavesNoEmptyParagraphs()
    {
        using var file = Write("Mars is close to Earth.", new TestWordTemplateOptions(LeadingEmptyParagraphs: 2, EmptyCoverPage: true));
        using var document = WordprocessingDocument.Open(file, false);

        Assert.That(ParagraphsOf(document).Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { "Mars is close to Earth." }), "Otherwise the text would start where the cover page ends.");
    }

    [Test]
    public void ATemplateBecomesADocument()
    {
        using var file = Write("Mars is close to Earth.", new TestWordTemplateOptions(AsTemplate: true));
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
            Assert.That(addedStyle?.BasedOn?.Val?.Value, Is.EqualTo(TestWordTemplate.HEADING_1_STYLE_ID), "The added heading looks like the heading above it, which fits the design of the template.");
            Assert.That(addedStyle?.StyleRunProperties?.FontSize, Is.Null, "A size of its own could make it bigger than the heading above it.");
            Assert.That(styleIds[2], Is.Null, "A paragraph takes the default style of the template.");
        });
    }

    [Test]
    public void TheTopHeadingOfTheAnswerTakesTheFirstHeadingOfTheTemplate()
    {
        // Models start with the second level; the sources AI Studio adds below the answer start with the first:
        using var file = Write("## Mars\n\n### Orbit\n\nClose to Earth.\n\n# Sources", topHeadingLevel: 2);
        using var document = WordprocessingDocument.Open(file, false);

        var styleIds = ParagraphsOf(document).Select(paragraph => paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value).ToList();
        var styleNames = styleIds.Select(id => StylesOf(document).SingleOrDefault(style => style.StyleId?.Value == id)?.StyleName?.Val?.Value).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(styleIds[0], Is.EqualTo(TestWordTemplate.HEADING_1_STYLE_ID), "Otherwise a template which numbers its chapters would start with chapter 0.1.");
            Assert.That(styleNames[1], Is.EqualTo("heading 2"));
            Assert.That(styleIds[3], Is.EqualTo(TestWordTemplate.HEADING_1_STYLE_ID), "A heading above the top level of the answer stays at the first level.");
        });
    }

    [TestCase("", 1, Description = "Without headings, nothing moves.")]
    [TestCase("Mars is close.\n\n- Earth\n- Moon", 1)]
    [TestCase("## Mars\n\n### Orbit", 2, Description = "What models mostly write.")]
    [TestCase("### Mars\n\n## Orbit", 2, Description = "The top level counts, not the first heading.")]
    [TestCase("# Mars\n\n## Orbit", 1)]
    public void TheTopHeadingLevelIsTheLowestOne(string markdown, int expectedLevel)
    {
        Assert.That(MarkdownWordConverter.TopHeadingLevelOf(markdown), Is.EqualTo(expectedLevel));
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
    public void ListsTakeTheNumberingOfTheTemplate()
    {
        using var file = Write("- Earth\n- Mars\n  - Phobos\n\n1. Launch\n2. Land\n\nBetween the lists.\n\n1. Return", new TestWordTemplateOptions(ListNumbering: true));
        using var document = WordprocessingDocument.Open(file, false);

        var items = ParagraphsOf(document).Where(paragraph => paragraph.ParagraphProperties?.NumberingProperties is not null).ToList();
        var instances = items.Select(item => item.ParagraphProperties!.NumberingProperties!.NumberingId!.Val!.Value).ToList();
        var styleIds = items.Select(item => item.ParagraphProperties!.ParagraphStyleId?.Val?.Value).ToList();
        var numbering = document.MainDocumentPart!.NumberingDefinitionsPart!.Numbering!;
        var numberedDefinitions = new[] { instances[3], instances[5] }.Select(id => numbering.Elements<W.NumberingInstance>().Single(candidate => candidate.NumberID?.Value == id).AbstractNumId?.Val?.Value);
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(items.Select(item => item.InnerText), Is.EqualTo(new[] { "Earth", "Mars", "Phobos", "Launch", "Land", "Return" }));
            Assert.That(instances.Take(3), Is.All.EqualTo(TestWordTemplate.BULLET_INSTANCE_ID), "Bulleted lists, the inner one included, take the dashes of the template.");
            Assert.That(styleIds.Take(3), Is.All.EqualTo(TestWordTemplate.LIST_PARAGRAPH_STYLE_ID));
            Assert.That(styleIds.Skip(3), Is.All.EqualTo(TestWordTemplate.LIST_NUMBER_STYLE_ID), "Word keeps a style of its own for numbered lists, which the template has.");
            Assert.That(instances[4], Is.EqualTo(instances[3]));
            Assert.That(instances[5], Is.Not.EqualTo(instances[3]), "Each numbered list counts anew.");
            Assert.That(numberedDefinitions, Is.All.EqualTo(TestWordTemplate.NUMBER_DEFINITION_ID), "Both numbered lists take the numbers of the template.");
            Assert.That(new[] { instances[3], instances[5] }, Is.All.Not.EqualTo(TestWordTemplate.NUMBER_INSTANCE_ID), "The instance of the template itself would go on counting.");
        });
    }

    [Test]
    public void ListsKeepTheIndentationOfTheirStyle()
    {
        using var file = Write("- Earth\n  - Phobos\n\n1. Launch", new TestWordTemplateOptions(ListNumbering: true));
        using var document = WordprocessingDocument.Open(file, false);

        var indentations = ParagraphsOf(document).ToDictionary(paragraph => paragraph.InnerText, paragraph => paragraph.ParagraphProperties?.Indentation);
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(indentations["Earth"]?.Left?.Value, Is.EqualTo(TestWordTemplate.LIST_STYLE_INDENTATION.ToString()), "The style pulls the dashes out further than its numbering, which a numbering on the paragraph itself would undo.");
            Assert.That(indentations["Earth"]?.Hanging?.Value, Is.EqualTo(TestWordTemplate.LIST_STYLE_INDENTATION.ToString()));
            Assert.That(indentations["Phobos"], Is.Null, "A deeper level moves in as the numbering of the template says.");
            Assert.That(indentations["Launch"], Is.Null, "\"List Number\" of the template states no indentation, so its numbering does.");
        });
    }

    [Test]
    public void FurtherParagraphsOfAnItemCarryNoBullet()
    {
        using var file = Write("- Earth\n\n  Close to the sun.\n\n  - Phobos\n\n    A small moon.\n- Mars", new TestWordTemplateOptions(ListNumbering: true));
        using var document = WordprocessingDocument.Open(file, false);

        var further = ParagraphsOf(document).Single(paragraph => paragraph.InnerText == "Close to the sun.");
        var furtherInside = ParagraphsOf(document).Single(paragraph => paragraph.InnerText == "A small moon.");
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(further.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value, Is.Zero, "Its style brings the dashes along, which only the first paragraph of an item shows.");
            Assert.That(further.ParagraphProperties?.Indentation?.Left?.Value, Is.EqualTo(TestWordTemplate.LIST_STYLE_INDENTATION.ToString()), "It starts where the text of the item does, as the style of the template states it.");
            Assert.That(further.ParagraphProperties?.Indentation?.Hanging?.Value, Is.EqualTo("0"), "Its first line starts with the others, despite the hanging indentation of the style.");
            Assert.That(furtherInside.ParagraphProperties?.Indentation?.Left?.Value, Is.EqualTo((TestWordTemplate.LIST_INDENTATION * 2).ToString()), "Further in, it starts where the numbering of the template puts the text.");
        });
    }

    [Test]
    public void EmptyParagraphsSeparateTheBlocksWhenTheTemplateLeavesNoSpace()
    {
        using var file = Write("## Mars\n\nClose to Earth.\n\n- Phobos\n- Deimos\n\n| Moon |\n|---|\n| Phobos |\n\nThe end.", new TestWordTemplateOptions(NormalSpacingAfter: 0), topHeadingLevel: 2);
        using var document = WordprocessingDocument.Open(file, false);

        var blocks = document.MainDocumentPart!.Document!.Body!.ChildElements.Select(element => element switch
        {
            W.Table => "table",
            W.SectionProperties => "section",

            _ => element.InnerText,
        });

        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(blocks, Is.EqualTo(new[] { "Mars", "Close to Earth.", string.Empty, "Phobos", "Deimos", string.Empty, "table", string.Empty, "The end.", "section" }), "The template separates its own text this way. A heading brings its own space along, and nothing follows the last block.");
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

    private static MemoryStream Write(string markdown, TestWordTemplateOptions? options = null, int topHeadingLevel = 1)
    {
        // The writer edits a copy of the template, as WordDocumentFile does with one on disk:
        var file = TestWordTemplate.Create(options);
        WordDocumentWriter.Write(file, markdown, topHeadingLevel);
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