using AIStudio.Tools.Word;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
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
    public void ACoverPageWithoutTextStaysAsItIs()
    {
        using var file = Write("Mars is close to Earth.", new TestWordTemplateOptions(LeadingEmptyParagraphs: 2, EmptyCoverPage: true));
        using var document = WordprocessingDocument.Open(file, false);

        var paragraphs = ParagraphsOf(document);
        var mainPart = document.MainDocumentPart!;
        var sections = mainPart.Document!.Body!.Descendants<W.SectionProperties>().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(paragraphs.Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { string.Empty, string.Empty, string.Empty, string.Empty, "Mars is close to Earth." }), "Its placeholders are for somebody to fill in, and the text starts after it.");
            Assert.That(paragraphs.Take(2).Select(paragraph => paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value), Is.All.EqualTo(TestWordTemplate.HEADING_1_STYLE_ID));
            Assert.That(paragraphs[3].ParagraphProperties?.SectionProperties, Is.SameAs(sections[0]), "The cover page keeps its section, with its own header.");
            Assert.That(mainPart.HeaderParts.Select(part => part.Header!.InnerText), Is.EquivalentTo(new[] { TestWordTemplate.COVER_HEADER_TEXT, TestWordTemplate.HEADER_TEXT }));
            Assert.That(sections[1].Elements<W.HeaderReference>().Select(reference => mainPart.GetPartById(reference.Id!.Value!)).OfType<HeaderPart>().Select(part => part.Header!.InnerText), Is.EqualTo(new[] { TestWordTemplate.HEADER_TEXT }), "The pages of the text have the header of the template's text.");
        });
    }

    [Test]
    public void ATableOfContentsAfterTheCoverPageStays()
    {
        using var file = Write("Mars is close to Earth.", new TestWordTemplateOptions(EmptyCoverPage: true, TableOfContents: true));
        using var document = WordprocessingDocument.Open(file, false);

        var settings = document.MainDocumentPart!.DocumentSettingsPart?.Settings;
        var paragraphs = ParagraphsOf(document);
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(paragraphs.Select(TextOf), Is.EqualTo(new[] { string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, "Mars is close to Earth." }), "What the template places between its cover page and its first heading stays, the sample chapter goes.");
            Assert.That(paragraphs[2].Descendants<W.FieldCode>().Select(code => code.Text.Trim()), Is.EqualTo(new[] { "TOC \\o \"1-3\"" }), "The table of contents keeps its field, without the entries for the chapters of the sample text.");
            Assert.That(paragraphs.Skip(2).Take(2).SelectMany(paragraph => paragraph.Descendants<W.FieldChar>()).Select(mark => mark.FieldCharType?.InnerText), Is.EqualTo(new[] { "begin", "separate", "end" }));
            Assert.That(settings?.GetFirstChild<W.UpdateFieldsOnOpen>()?.Val?.Value, Is.True, "Word lists the headings of the answer in the table of contents once it updates the field.");
        });
    }

    [Test]
    public void PagesAfterTheTextOfTheTemplateGo()
    {
        using var file = Write("Mars is close to Earth.", new TestWordTemplateOptions(AppendixPage: true));
        using var document = WordprocessingDocument.Open(file, false);

        var mainPart = document.MainDocumentPart!;
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(mainPart.Document!.Body!.Descendants<W.SectionProperties>().Count(), Is.EqualTo(1));
            Assert.That(mainPart.HeaderParts.Select(part => part.Header!.InnerText), Is.EqualTo(new[] { TestWordTemplate.HEADER_TEXT }), "The pages take the header of the section with the text of the template, not the one of the instructions after it.");
            Assert.That(ParagraphsOf(document).Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { "Mars is close to Earth." }));
        });
    }

    [Test]
    public void TheFirstPageKeepsItsOwnColumns()
    {
        var markdown = string.Join("\n\n", Enumerable.Range(1, 200).Select(number => $"Paragraph {number}."));
        using var file = Write(markdown, new TestWordTemplateOptions(SidebarLayout: true));
        using var document = WordprocessingDocument.Open(file, false);

        var mainPart = document.MainDocumentPart!;
        var body = mainPart.Document!.Body!;
        var endsOfSections = ParagraphsOf(document).Where(paragraph => paragraph.ParagraphProperties?.SectionProperties is not null).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(ParagraphsOf(document), Has.Count.EqualTo(200), "No paragraph comes in addition to the text.");
            Assert.That(endsOfSections, Has.Count.EqualTo(1), "The first page is a section of its own.");
            Assert.That(endsOfSections.Single().InnerText, Does.StartWith("Paragraph "), "The section ends with a paragraph of the text, where the first page is well filled.");
            Assert.That(endsOfSections.Single().ParagraphProperties!.SectionProperties!.GetFirstChild<W.PageMargin>()!.Left!.Value, Is.EqualTo((uint)TestWordTemplate.SIDEBAR_MARGIN), "On the first page, the text runs next to the wide margin on the left.");
            Assert.That(body.Elements<W.SectionProperties>().Single().GetFirstChild<W.PageMargin>()!.Right!.Value, Is.EqualTo((uint)TestWordTemplate.SIDEBAR_MARGIN), "On the pages after it, the text runs next to the wide margin on the right.");
            Assert.That(mainPart.HeaderParts.Select(part => part.Header!.InnerText), Is.EquivalentTo(new[] { TestWordTemplate.HEADER_TEXT, TestWordTemplate.COVER_HEADER_TEXT }), "Each section keeps its header.");
        });
    }

    [Test]
    public void TheSectionsShareNoHeaderOrFooter()
    {
        var markdown = string.Join("\n\n", Enumerable.Range(1, 200).Select(number => $"Paragraph {number}."));
        using var file = Write(markdown, new TestWordTemplateOptions(CoverPage: true, SidebarLayout: true));
        using var document = WordprocessingDocument.Open(file, false);

        var sections = document.MainDocumentPart!.Document!.Body!.Descendants<W.SectionProperties>().ToList();
        var parts = sections.SelectMany(section => section.Elements<W.HeaderFooterReferenceType>()).Select(reference => reference.Id?.Value).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(sections, Has.Count.EqualTo(2));
            Assert.That(parts, Has.Count.EqualTo(3), "The first page keeps the headers and the footer of the cover page.");
            Assert.That(parts, Is.Unique, "Word refuses a document in which two sections refer to the same header or footer, which the validator does not notice.");
            Assert.That(sections[1].Elements<W.HeaderFooterReferenceType>(), Is.Empty, "The pages after the first one inherit the headers and footers, which the template states for the cover page only.");
        });
    }

    [Test]
    public void AShortTextKeepsTheColumnsOfTheFirstPage()
    {
        using var file = Write("Mars is close to Earth.", new TestWordTemplateOptions(SidebarLayout: true));
        using var document = WordprocessingDocument.Open(file, false);

        var mainPart = document.MainDocumentPart!;
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(ParagraphsOf(document).Where(paragraph => paragraph.ParagraphProperties?.SectionProperties is not null), Is.Empty, "A second section would start with an empty page.");
            Assert.That(mainPart.Document!.Body!.Elements<W.SectionProperties>().Single().GetFirstChild<W.PageMargin>()!.Left!.Value, Is.EqualTo((uint)TestWordTemplate.SIDEBAR_MARGIN), "The text fits on the first page, which keeps its layout.");
            Assert.That(mainPart.HeaderParts.Select(part => part.Header!.InnerText), Is.EqualTo(new[] { TestWordTemplate.COVER_HEADER_TEXT }), "Only the header of the first page is left.");
        });
    }

    [Test]
    public void AHeadingDoesNotEndTheFirstPage()
    {
        var markdown = string.Join("\n\n", Enumerable.Range(1, 100).Select(number => $"# Chapter {number}\n\nParagraph {number}."));
        using var file = Write(markdown, new TestWordTemplateOptions(SidebarLayout: true));
        using var document = WordprocessingDocument.Open(file, false);

        var paragraphs = ParagraphsOf(document);
        var end = paragraphs.FindIndex(paragraph => paragraph.ParagraphProperties?.SectionProperties is not null);
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(paragraphs[end].InnerText, Does.StartWith("Paragraph "), "A heading keeps with the paragraph after it.");
            Assert.That(paragraphs[end + 1].InnerText, Does.StartWith("Chapter "), "The next page starts with the heading.");
        });
    }

    [Test]
    public void TheShapesOfTheDesignStayOnTheirPage()
    {
        var markdown = string.Join("\n\n", Enumerable.Range(1, 200).Select(number => $"Paragraph {number}."));
        using var file = Write(markdown, new TestWordTemplateOptions(SidebarLayout: true, Shapes: true));
        using var document = WordprocessingDocument.Open(file, false);

        var mainPart = document.MainDocumentPart!;
        var body = mainPart.Document!.Body!;
        var paragraphs = ParagraphsOf(document);
        var nextPageStart = paragraphs.FindIndex(paragraph => paragraph.ParagraphProperties?.SectionProperties is not null) + 1;
        var marginPicture = body.Descendants<DW.Anchor>().Single(shape => ShapeNameOf(shape) == TestWordTemplate.MARGIN_PICTURE);
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(paragraphs[0].Descendants<DW.Anchor>().Select(ShapeNameOf), Is.EquivalentTo(new[] { TestWordTemplate.MARGIN_PICTURE, TestWordTemplate.MARGIN_CAPTION, TestWordTemplate.BACKGROUND_PICTURE }), "The sample picture within the text goes with it.");
            Assert.That(paragraphs[nextPageStart].Descendants<DW.Anchor>().Select(ShapeNameOf), Is.EqualTo(new[] { TestWordTemplate.NEXT_PAGE_PICTURE }), "The page after the first one keeps the shapes of the first page of the last section.");
            Assert.That(body.Descendants<DW.Anchor>().Count(), Is.EqualTo(4));
            Assert.That(body.Descendants<DW.Anchor>().Select(shape => $"{shape.GetFirstChild<DW.HorizontalPosition>()?.RelativeFrom?.InnerText} {shape.GetFirstChild<DW.VerticalPosition>()?.RelativeFrom?.InnerText}"), Is.All.EqualTo("page page"), "The shapes no longer depend on the paragraphs which anchored them in the template.");
            Assert.That(marginPicture.GetFirstChild<DW.HorizontalPosition>()?.GetFirstChild<DW.PositionOffset>()?.Text, Is.EqualTo(TestWordTemplate.MARGIN_PICTURE_LEFT.ToString()), "The picture stays in the margin, where the template put it next to the text.");
            Assert.That(marginPicture.GetFirstChild<DW.VerticalPosition>()?.GetFirstChild<DW.PositionOffset>()?.Text, Is.EqualTo((TestWordTemplate.TOP_MARGIN * WordTextArea.EMU_PER_TWIP).ToString()), "Its paragraph started where the text of the page does.");
            Assert.That(body.Descendants<AlternateContent>(), Is.Empty, "Word 2010 and later read the text box itself, without the alternative for older versions.");
            Assert.That(body.Descendants<W.TextBoxContent>().Single().InnerText, Is.EqualTo(TestWordTemplate.CAPTION_TEXT));
            Assert.That(mainPart.ImageParts.Count(), Is.EqualTo(3), "The pictures of the shapes stay, the one of the sample picture goes.");
        });
    }

    [Test]
    public void ShapesOfTheNextPageNeedAPageOfItsOwn()
    {
        using var file = Write("Mars is close to Earth.", new TestWordTemplateOptions(Shapes: true));
        using var document = WordprocessingDocument.Open(file, false);

        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(document.MainDocumentPart!.Document!.Body!.Descendants<DW.Anchor>().Select(ShapeNameOf), Is.EquivalentTo(new[] { TestWordTemplate.MARGIN_PICTURE, TestWordTemplate.MARGIN_CAPTION, TestWordTemplate.BACKGROUND_PICTURE }), "Without a section of its own, nothing tells where the page after the first one starts.");
        });
    }

    [Test]
    public void TheTextStaysClearOfTheShapesOfTheFooter()
    {
        using var file = Write("Mars is close to Earth.", new TestWordTemplateOptions(CoverPage: true, FooterShapes: true));
        using var document = WordprocessingDocument.Open(file, false);

        var margin = document.MainDocumentPart!.Document!.Body!.Elements<W.SectionProperties>().Single().GetFirstChild<W.PageMargin>()!;
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(margin.Bottom?.Value, Is.EqualTo(TestWordTemplate.FOOTER_CLEARANCE), "The text ends above the address, which floats across it; the logo in the margin beside the text reaches higher, but is in nobody's way.");
        });
    }

    [Test]
    public void AFirstPageWithoutFooterKeepsItsBottomMargin()
    {
        var markdown = string.Join("\n\n", Enumerable.Range(1, 200).Select(number => $"Paragraph {number}."));
        using var file = Write(markdown, new TestWordTemplateOptions(CoverPage: true, SidebarLayout: true, FooterShapes: true));
        using var document = WordprocessingDocument.Open(file, false);

        var sections = document.MainDocumentPart!.Document!.Body!.Descendants<W.SectionProperties>().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(sections, Has.Count.EqualTo(2));
            Assert.That(sections[0].GetFirstChild<W.PageMargin>()!.Bottom?.Value, Is.EqualTo(TestWordTemplate.BOTTOM_MARGIN), "The first page has a header of its own, but no footer, as in the template.");
            Assert.That(sections[1].GetFirstChild<W.PageMargin>()!.Bottom?.Value, Is.EqualTo(TestWordTemplate.FOOTER_CLEARANCE), "The pages after it inherit the footer with the address.");
        });
    }

    [Test]
    public void ACoverPageWithoutTextKeepsItsShapesAsTheyAre()
    {
        using var file = Write("Mars is close to Earth.", new TestWordTemplateOptions(EmptyCoverPage: true, Shapes: true));
        using var document = WordprocessingDocument.Open(file, false);

        var body = document.MainDocumentPart!.Document!.Body!;
        var marginPicture = body.Descendants<DW.Anchor>().Single(shape => ShapeNameOf(shape) == TestWordTemplate.MARGIN_PICTURE);
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(body.Descendants<DW.Anchor>().Select(ShapeNameOf), Is.EquivalentTo(new[] { TestWordTemplate.MARGIN_PICTURE, TestWordTemplate.MARGIN_CAPTION, TestWordTemplate.BACKGROUND_PICTURE, TestWordTemplate.SAMPLE_PICTURE }), "The cover page stays as a whole; the picture of the next page goes with the sample text.");
            Assert.That(marginPicture.GetFirstChild<DW.HorizontalPosition>()?.RelativeFrom?.InnerText, Is.EqualTo("column"), "The shapes stay where the template anchors them.");
        });
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
        using var file = Write("## Mars\n\n### Orbit\n\nClose to Earth.\n\n# Sources", headingLevels: new WordHeadingLevels(2, 3));
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

    [TestCase("", 1, 1, Description = "Without headings, nothing moves.")]
    [TestCase("Mars is close.\n\n- Earth\n- Moon", 1, 1)]
    [TestCase("## Mars\n\n### Orbit", 2, 3, Description = "What models mostly write.")]
    [TestCase("### Mars\n\n## Orbit", 2, 2, Description = "The top level counts, not the first heading.")]
    [TestCase("# Mars\n\n## Orbit", 1, 2)]
    [TestCase("# Mars\n\nClose to Earth.", 1, 1, Description = "No heading follows the title.")]
    [TestCase("Close to Earth.\n\n## Mars", 2, 2, Description = "The answer starts with no heading which could become the title.")]
    public void TheHeadingLevelsAreTheLowestOnes(string markdown, int expectedTopLevel, int expectedTopLevelBelowTitle)
    {
        Assert.That(MarkdownWordConverter.HeadingLevelsOf(markdown), Is.EqualTo(new WordHeadingLevels(expectedTopLevel, expectedTopLevelBelowTitle)));
    }

    [Test]
    public void TheTitleOfTheAnswerFillsTheTitleBox()
    {
        using var file = Write("# Mars\n\n## Orbit\n\nClose to Earth.", new TestWordTemplateOptions(TitleBox: true), new WordHeadingLevels(1, 2));
        using var document = WordprocessingDocument.Open(file, false);

        var box = document.MainDocumentPart!.Document!.Body!.Descendants<W.TextBoxContent>().Single();
        var titles = box.Elements<W.Paragraph>().Where(paragraph => paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value == TestWordTemplate.TITLE_STYLE_ID).ToList();
        var subtitle = box.Elements<W.Paragraph>().Single(paragraph => paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value == TestWordTemplate.SUBTITLE_STYLE_ID);
        var paragraphs = ParagraphsOf(document);
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(titles.Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { "Mars" }), "The title replaces both lines of the sample title.");
            Assert.That(titles[0].Descendants<W.Color>().Single().Val?.Value, Is.EqualTo(TestWordTemplate.TITLE_COLOR), "The title keeps the look of the sample title, e.g., white letters on a coloured box.");
            Assert.That(subtitle.InnerText, Is.Empty, "The answer has no subtitle.");
            Assert.That(paragraphs.Select(TextOf), Is.EqualTo(new[] { "Orbit", "Close to Earth." }), "The title is in the box only.");
            Assert.That(paragraphs[0].ParagraphProperties?.ParagraphStyleId?.Val?.Value, Is.EqualTo(TestWordTemplate.HEADING_1_STYLE_ID), "The headings below the title take the first heading level of the template.");
        });
    }

    [Test]
    public void TheTitleBoxKeepsItsSampleTextWhenTheAnswerStartsWithNoHeading()
    {
        using var file = Write("Close to Earth.\n\n# Mars", new TestWordTemplateOptions(TitleBox: true));
        using var document = WordprocessingDocument.Open(file, false);

        var box = document.MainDocumentPart!.Document!.Body!.Descendants<W.TextBoxContent>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(box.InnerText, Is.EqualTo(TestWordTemplate.SAMPLE_TITLE + TestWordTemplate.SAMPLE_TITLE_SECOND_LINE + TestWordTemplate.SAMPLE_SUBTITLE), "Somebody fills it in Word.");
            Assert.That(ParagraphsOf(document).Select(TextOf), Is.EqualTo(new[] { "Close to Earth.", "Mars" }));
        });
    }

    [Test]
    public void WithoutATitleBoxTheHeadingStaysInTheText()
    {
        using var file = Write("# Mars\n\n## Orbit", headingLevels: new WordHeadingLevels(1, 2));
        using var document = WordprocessingDocument.Open(file, false);

        var paragraphs = ParagraphsOf(document);
        Assert.Multiple(() =>
        {
            Assert.That(paragraphs.Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { "Mars", "Orbit" }));
            Assert.That(paragraphs[0].ParagraphProperties?.ParagraphStyleId?.Val?.Value, Is.EqualTo(TestWordTemplate.HEADING_1_STYLE_ID));
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
        using var file = Write("## Mars\n\nClose to Earth.\n\n- Phobos\n- Deimos\n\n| Moon |\n|---|\n| Phobos |\n\nThe end.", new TestWordTemplateOptions(NormalSpacingAfter: 0), headingLevels: new WordHeadingLevels(2, 2));
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

    private static MemoryStream Write(string markdown, TestWordTemplateOptions? options = null, WordHeadingLevels? headingLevels = null)
    {
        // The writer edits a copy of the template, as WordDocumentFile does with one on disk:
        var file = TestWordTemplate.Create(options);
        WordDocumentWriter.Write(file, markdown, headingLevels ?? WordHeadingLevels.AS_THEY_ARE);
        file.Position = 0;
        return file;
    }

    private static List<W.Paragraph> ParagraphsOf(WordprocessingDocument document) => document.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().ToList();

    private static string? ShapeNameOf(DW.Anchor shape) => shape.GetFirstChild<DW.DocProperties>()?.Name?.Value;

    /// <summary>
    /// The text of a paragraph without the text of the text boxes it anchors.
    /// </summary>
    private static string TextOf(W.Paragraph paragraph) => string.Concat(paragraph.OwnDescendants<W.Text>().Select(text => text.Text));

    private static List<W.Style> StylesOf(WordprocessingDocument document) => document.MainDocumentPart!.StyleDefinitionsPart!.Styles!.Elements<W.Style>().ToList();

    private static List<string> ValidationErrorsOf(WordprocessingDocument document) => new OpenXmlValidator(FileFormatVersions.Office2019)
        .Validate(document)
        .Select(error => $"{error.Part?.Uri} {error.Path?.XPath}: {error.Description}")
        .ToList();
}