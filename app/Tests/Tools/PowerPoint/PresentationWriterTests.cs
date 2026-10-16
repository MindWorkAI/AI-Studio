using AIStudio.Tools.PowerPoint;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using P14 = DocumentFormat.OpenXml.Office2010.PowerPoint;

namespace AIStudio.Tests.Tools.PowerPoint;

/// <summary>
/// Checks the presentations written from a template: valid files, filled placeholders, variants,
/// pictures, tables, sections, the imprint, and the template slides a deck keeps.
/// </summary>
/// <remarks>
/// The test template reads as L1 "Title" (A, B), L2 "Content" (A, B), L3 "Picture with text",
/// L4 "Backdrop" and L5 "Table".
/// </remarks>
[TestFixture]
public sealed class PresentationWriterTests
{
    private static readonly SlotBounds SUBTITLE = new(1_524_000, 3_602_038, 9_144_000, 1_655_762);
    private static readonly SlotBounds HEADING = new(838_200, 1_400_000, 3_000_000, 300_000);
    private static readonly SlotBounds LEFT_TEXT = new(838_200, 1_825_625, 5_150_000, 4_351_338);
    private static readonly SlotBounds RIGHT_PICTURE = new(6_200_000, 1_825_625, 5_150_000, 3_500_000);
    private static readonly SlotBounds WHOLE_SLIDE = new(0, 0, TestTemplate.SLIDE_WIDTH, TestTemplate.SLIDE_HEIGHT);
    private static readonly SlotBounds FOOTER = new(838_200, 6_356_350, 4_114_800, 365_125);
    private static readonly SlotBounds SLIDE_NUMBER = new(8_610_600, 6_356_350, 2_743_200, 365_125);

    /// <summary>
    /// Four times as wide as high, which neither slot of the test template is.
    /// </summary>
    private static readonly SlidePicture WIDE_PICTURE = new([1, 2, 3], "image/png", 400, 100);

    private static readonly PresentationWriteOptions OPTIONS = new("A", "Mission team");

    private static readonly SlideDeckSpec DECK = new("Mars",
    [
        new SlideSpec("L1", false, "Mars", "A plan", [], [], [], null),
        new SlideSpec("L2", true, "Why Mars", string.Empty, [Text(new SlideParagraph("Close to Earth", 0), new SlideParagraph("Two years per trip", 1))], [], [], null),
        new SlideSpec("L3", false, "The rocket", string.Empty, [Text(new SlideParagraph("Reusable", 0))], [], [1], null),
        new SlideSpec("L4", true, "Landing", string.Empty, [], [], [2], null),
        new SlideSpec("L5", false, "Costs", string.Empty, [], [], [], new SlideTableSpec(["Item", "Cost"], [["Fuel", "10"]])),
    ]);

    [Test]
    public async Task TheFileIsValidAndHoldsTheNewSlidesOnly()
    {
        var (file, _) = await WriteAsync(DECK, OPTIONS, new TestTemplateOptions(ExampleSlides: 2), new FakePictureSource(WIDE_PICTURE, WIDE_PICTURE));
        using var document = PresentationDocument.Open(file, false);

        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(document.PresentationPart!.SlideParts.Count(), Is.EqualTo(6), "The sample slides of the template are gone, parts included; the imprint closes the presentation.");
            Assert.That(SlidesOf(document).Select(TitleOf), Is.EqualTo(new[] { "Mars", "Why Mars", "The rocket", "Landing", "Costs", "Impressum" }));
        });
    }

    [Test]
    public async Task EveryChapterBecomesASection()
    {
        var (file, _) = await WriteAsync(DECK, OPTIONS, new TestTemplateOptions(ExampleSlides: 2), new FakePictureSource(WIDE_PICTURE, WIDE_PICTURE));
        using var document = PresentationDocument.Open(file, false);

        var sections = document.PresentationPart!.Presentation!.Descendants<P14.Section>().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(sections.Select(section => section.Name?.Value), Is.EqualTo(new[] { "Mars", "Why Mars", "Landing", "Impressum" }), "The section of the template's samples is gone, the title slide forms a section named after the presentation, and the imprint one of its own.");
            Assert.That(sections.Select(section => section.Descendants<P14.SectionSlideIdListEntry>().Count()), Is.EqualTo(new[] { 1, 2, 2, 1 }));
        });
    }

    [Test]
    public async Task OnlyPlaceholdersWithContentAreWritten()
    {
        var (file, _) = await WriteAsync(DECK, OPTIONS);
        using var document = PresentationDocument.Open(file, false);

        // The second slide has no heading for its text, so the label placeholder (idx 17) stays away:
        var slide = SlidesOf(document)[1].Slide!;
        var placeholders = slide.Descendants<P.PlaceholderShape>().Select(placeholder => (placeholder.Type?.InnerText ?? "obj", placeholder.Index?.Value)).ToList();
        var textBody = slide.Descendants<P.Shape>().Single(shape => shape.Descendants<P.PlaceholderShape>().Single().Index?.Value == 1).TextBody!;
        Assert.Multiple(() =>
        {
            Assert.That(placeholders, Is.EqualTo(new (string, uint?)[] { ("title", null), ("obj", 1), ("ftr", 10), ("sldNum", 11) }));
            Assert.That(textBody.Elements<A.Paragraph>().Select(paragraph => paragraph.ParagraphProperties?.Level?.Value ?? 0), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(textBody.InnerText, Is.EqualTo("Close to EarthTwo years per trip"));
            Assert.That(slide.Descendants<P.Shape>().Any(shape => shape.InnerText == "Mission team"), Is.True, "The footer carries the text the user chose.");
            Assert.That(slide.Descendants<A.Field>().Single().Type?.Value, Is.EqualTo("slidenum"), "PowerPoint fills in the number itself.");
        });
    }

    [Test]
    public async Task AllSlidesUseTheChosenVariant()
    {
        var deck = new SlideDeckSpec("Mars",
        [
            new SlideSpec("L1", false, "Mars", string.Empty, [], [], [], null),
            new SlideSpec("L2", true, "One", string.Empty, [], [], [], null),
            new SlideSpec("L2", false, "One more", string.Empty, [], [], [], null),
            new SlideSpec("L2", true, "Two", string.Empty, [], [], [], null),
        ]);

        var (chosen, _) = await WriteAsync(deck, OPTIONS with { VariantLabel = "B" });
        var (unknown, _) = await WriteAsync(deck, OPTIONS with { VariantLabel = "Z" });

        Assert.Multiple(() =>
        {
            Assert.That(LayoutNamesOf(chosen), Is.EqualTo(new[] { "B Title", "B Content", "B Content", "B Content", "B Content" }), "Chapters keep the variant, and so does the imprint.");
            Assert.That(LayoutNamesOf(unknown), Is.EqualTo(new[] { "A Title", "A Content", "A Content", "A Content", "A Content" }), "A variant the template lacks falls back to its first one.");
        });
    }

    [Test]
    public async Task APictureFillsABackdropAndFitsIntoASmallSlot()
    {
        var (file, _) = await WriteAsync(DECK, OPTIONS, pictures: new FakePictureSource(WIDE_PICTURE, WIDE_PICTURE));
        using var document = PresentationDocument.Open(file, false);
        var slides = SlidesOf(document);

        // In the small slot, the whole picture is scaled down to the slot's width and centered:
        var small = slides[2].Slide!.Descendants<P.Picture>().Single();
        var transform = small.ShapeProperties!.Transform2D!;

        // The backdrop is filled, so the sides of the wide picture are cut off evenly:
        var backdrop = slides[3].Slide!.Descendants<P.Picture>().Single();
        var crop = backdrop.BlipFill!.GetFirstChild<A.SourceRectangle>()!;

        Assert.Multiple(() =>
        {
            Assert.That(transform.Extents!.Cx!.Value, Is.EqualTo(RIGHT_PICTURE.Width));
            Assert.That(transform.Extents.Cy!.Value, Is.EqualTo(RIGHT_PICTURE.Width / 4));
            Assert.That(transform.Offset!.Y!.Value, Is.EqualTo(RIGHT_PICTURE.Y + (RIGHT_PICTURE.Height - RIGHT_PICTURE.Width / 4) / 2));
            Assert.That(crop.Left?.Value, Is.EqualTo(27_778), "16:9 out of 4:1 keeps 44.4 % of the width.");
            Assert.That(crop.Right?.Value, Is.EqualTo(27_778));
            Assert.That(crop.Top, Is.Null);
            Assert.That(slides[2].ImageParts.Count(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task APictureInAFormatPowerPointDoesNotShowIsSkipped()
    {
        var (file, result) = await WriteAsync(DECK, OPTIONS, pictures: new FakePictureSource(WIDE_PICTURE with { ContentType = "image/webp" }, WIDE_PICTURE));
        using var document = PresentationDocument.Open(file, false);

        Assert.Multiple(() =>
        {
            Assert.That(result.SkippedPictures, Is.EqualTo(new[] { 1 }));
            Assert.That(SlidesOf(document)[2].Slide!.Descendants<P.Picture>(), Is.Empty, "The slot stays empty rather than holding a picture nobody can see.");
            Assert.That(ValidationErrorsOf(document), Is.Empty);
        });
    }

    [Test]
    public async Task TheTableUsesTheStyleOfTheTemplate()
    {
        var (file, _) = await WriteAsync(DECK, OPTIONS);
        using var document = PresentationDocument.Open(file, false);

        var table = SlidesOf(document)[4].Slide!.Descendants<A.Table>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(table.TableProperties!.GetFirstChild<A.TableStyleId>()?.Text, Is.EqualTo(TestTemplate.DEFAULT_TABLE_STYLE_ID));
            Assert.That(table.TableProperties.FirstRow?.Value, Is.True, "The headers are the first row.");
            Assert.That(table.Elements<A.TableRow>().Select(row => row.InnerText), Is.EqualTo(new[] { "ItemCost", "Fuel10" }));
            Assert.That(table.TableGrid!.Elements<A.GridColumn>().Sum(column => column.Width!.Value), Is.EqualTo(TestTemplate.MASTER_BODY_BOUNDS.Width), "The columns span the slot exactly.");
        });
    }

    [Test]
    public async Task ATemplateFileBecomesAPresentation()
    {
        var (file, _) = await WriteAsync(DECK, OPTIONS, new TestTemplateOptions(AsTemplate: true));
        using var document = PresentationDocument.Open(file, false);

        Assert.Multiple(() =>
        {
            Assert.That(document.DocumentType, Is.EqualTo(PresentationDocumentType.Presentation));
            Assert.That(ValidationErrorsOf(document), Is.Empty);
        });
    }

    [Test]
    public async Task EveryPresentationEndsWithTheImprintInTheDesignOfTheTemplate()
    {
        var (file, _) = await WriteAsync(DECK, OPTIONS);
        using var document = PresentationDocument.Open(file, false);
        var imprint = SlidesOf(document)[^1];

        Assert.Multiple(() =>
        {
            Assert.That(TitleOf(imprint), Is.EqualTo("Impressum"));
            Assert.That(imprint.SlideLayoutPart?.SlideLayout?.CommonSlideData?.Name?.Value, Is.EqualTo("A Content"), "The plainest layout with a title and a text, in the variant of the title slide.");
            Assert.That(document.PresentationPart!.SlideMasterParts.Count(), Is.EqualTo(1), "The imprint brings no design of its own.");
            Assert.That(document.PresentationPart.Presentation!.SlideIdList!.Elements<P.SlideId>().Select(slideId => slideId.Id!.Value), Is.Unique);
            Assert.That(imprint.Slide!.Descendants<A.Field>().Single().Type?.Value, Is.EqualTo("slidenum"), "The imprint has a slide number like every other slide.");
            Assert.That(ValidationErrorsOf(document), Is.Empty);
        });
    }

    [Test]
    public async Task TheImprintNamesTheTopicAndKeepsThePlaceholders()
    {
        var (file, _) = await WriteAsync(DECK, OPTIONS);
        using var document = PresentationDocument.Open(file, false);
        var paragraphs = BodyParagraphsOf(SlidesOf(document)[^1]);

        Assert.Multiple(() =>
        {
            Assert.That(paragraphs.Select(paragraph => paragraph.InnerText), Is.EqualTo(new[]
            {
                "Thema: \tMars",
                "Datum: \t2023-01-01 (JJJJ-MM-TT)",
                "Autor: \tVorname Nachname",
                "Institut: \tLorem ipsum dolor sit amet",
                "Bildcredits: \tAlle Bilder „Urheber (Lizenz)“, sofern nicht anders angegeben",
            }));

            Assert.That(paragraphs.All(paragraph => paragraph.ParagraphProperties?.GetFirstChild<A.NoBullet>() is not null), Is.True, "The lines are facts rather than points.");
            Assert.That(paragraphs[0].Elements<A.Run>().Last().RunProperties?.Bold?.Value, Is.True, "The topic stands out.");
            Assert.That(paragraphs[^1].Elements<A.Break>(), Is.Empty, "The picture credits are one sentence, which wraps where the slide needs it.");
        });
    }

    [Test]
    public async Task TheImprintIsWrittenInTheLanguageOfThePresentation()
    {
        var english = new ImprintTexts("Imprint", "Topic:", "Date:", "2023-01-01 (YYYY-MM-DD)", "Author:", "First name Last name", "Institute:", "Picture credits:", "All pictures “Creator (license)”,\nunless stated otherwise");
        var (file, _) = await WriteAsync(DECK with { Imprint = english }, OPTIONS);
        using var document = PresentationDocument.Open(file, false);
        var imprint = SlidesOf(document)[^1];

        Assert.Multiple(() =>
        {
            Assert.That(TitleOf(imprint), Is.EqualTo("Imprint"));
            Assert.That(BodyParagraphsOf(imprint).Select(paragraph => paragraph.InnerText), Is.EqualTo(new[]
            {
                "Topic: \tMars",
                "Date: \t2023-01-01 (YYYY-MM-DD)",
                "Author: \tFirst name Last name",
                "Institute: \tLorem ipsum dolor sit amet",
                "Picture credits: \tAll pictures “Creator (license)”,unless stated otherwise",
            }));

            Assert.That(document.PresentationPart!.Presentation!.Descendants<P14.Section>().Last().Name?.Value, Is.EqualTo("Imprint"), "The section of the imprint is named like its slide.");
        });
    }

    [Test]
    public async Task TheImprintHasTheFooterOfThePresentation()
    {
        var (withFooter, _) = await WriteAsync(DECK, OPTIONS);
        var (withoutFooter, _) = await WriteAsync(DECK, OPTIONS with { FooterText = string.Empty });

        Assert.Multiple(() =>
        {
            Assert.That(FootersOfImprint(withFooter), Is.EqualTo(new[] { "Mission team" }));
            Assert.That(FootersOfImprint(withoutFooter), Is.Empty);
        });
    }

    [Test]
    public async Task ATemplateWithoutALayoutForTitleAndTextGetsNoImprint()
    {
        using var templateFile = TestTemplate.Create(
            new TestLayout("Title", TestPlaceholder.Title(), TestPlaceholder.Subtitle(SUBTITLE)),
            new TestLayout("Backdrop", TestPlaceholder.Picture(11, WHOLE_SLIDE), TestPlaceholder.Title()));

        var template = PresentationTemplate.Read(templateFile);
        templateFile.Position = 0;

        var deck = new SlideDeckSpec("Mars", [new SlideSpec("L1", false, "Mars", "A plan", [], [], [], null)]);
        using var output = new MemoryStream();
        await PresentationWriter.WriteAsync(templateFile, template, deck, OPTIONS, new FakePictureSource(), output, CancellationToken.None);
        output.Position = 0;

        using var document = PresentationDocument.Open(output, false);
        Assert.That(SlidesOf(document).Select(TitleOf), Is.EqualTo(new[] { "Mars" }));
    }

    [Test]
    public async Task KeptTemplateSlidesStayInTheOrderOfTheDeck()
    {
        var deck = new SlideDeckSpec("Weekly",
        [
            Keep(1),
            new SlideSpec("L2", false, "Agenda", string.Empty, [Text(new SlideParagraph("News", 0))], [], [], null),
            Keep(2),
            Keep(3),
            Keep(4),
        ]);

        var (file, _) = await WriteAsync(deck, OPTIONS, TEMPLATE_WITH_SLIDES);
        using var document = PresentationDocument.Open(file, false);
        var slides = SlidesOf(document);

        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(slides.Select(TitleOf), Is.EqualTo(new[] { "Consultation hour", "Agenda", "News", "Questions", "Impressum" }), "The imprint of the template takes the place of the one added otherwise.");
            Assert.That(document.PresentationPart!.SlideParts.Count(), Is.EqualTo(5), "The slide the deck leaves out is gone, part included.");
            Assert.That(BodyParagraphsOf(slides[2]).Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { "AI Studio:", "EINSTEIN:" }));
            Assert.That(slides[3].Slide!.Show, Is.Null, "The user chose the slide, so it shows although the template hides it.");
            Assert.That(document.PresentationPart.Presentation!.SlideIdList!.Elements<P.SlideId>().Select(slideId => slideId.Id!.Value), Is.Unique);
        });
    }

    [Test]
    public async Task TheImprintOfTheTemplateClosesThePresentationInASectionOfItsOwn()
    {
        var deck = new SlideDeckSpec("Weekly",
        [
            Keep(1),
            Keep(4),
            new SlideSpec("L2", true, "Agenda", string.Empty, [Text(new SlideParagraph("News", 0))], [], [], null),
            Keep(2),
        ]);

        var (file, _) = await WriteAsync(deck, OPTIONS, TEMPLATE_WITH_SLIDES);
        using var document = PresentationDocument.Open(file, false);
        var sections = document.PresentationPart!.Presentation!.Descendants<P14.Section>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(SlidesOf(document).Select(TitleOf), Is.EqualTo(new[] { "Consultation hour", "Agenda", "News", "Impressum" }));
            Assert.That(sections.Select(section => section.Name?.Value), Is.EqualTo(new[] { "Weekly", "Agenda", "Impressum" }));
            Assert.That(sections.Select(section => section.Descendants<P14.SectionSlideIdListEntry>().Count()), Is.EqualTo(new[] { 1, 2, 1 }));
        });
    }

    [Test]
    public async Task WithoutTheImprintOfTheTemplateTheImprintIsAdded()
    {
        var deck = new SlideDeckSpec("Weekly", [Keep(1), Keep(2)]);

        var (file, _) = await WriteAsync(deck, OPTIONS, TEMPLATE_WITH_SLIDES);
        using var document = PresentationDocument.Open(file, false);
        var slides = SlidesOf(document);

        Assert.Multiple(() =>
        {
            Assert.That(slides.Select(TitleOf), Is.EqualTo(new[] { "Consultation hour", "News", "Impressum" }));
            Assert.That(BodyParagraphsOf(slides[^1])[0].InnerText, Is.EqualTo("Thema: \tWeekly"), "The imprint is the one written for the presentation.");
        });
    }

    [Test]
    public async Task KeptSlidesTakeTheColorVariantAndTheFooterOfThePresentation()
    {
        var deck = new SlideDeckSpec("Weekly", [Keep(1), Keep(2)]);

        var (withFooter, _) = await WriteAsync(deck, OPTIONS with { VariantLabel = "B" }, TEMPLATE_WITH_SLIDES);
        var (withoutFooter, _) = await WriteAsync(deck, OPTIONS with { FooterText = string.Empty }, TEMPLATE_WITH_SLIDES);
        using var document = PresentationDocument.Open(withFooter, false);
        var slides = SlidesOf(document);

        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(slides.Select(slide => slide.SlideLayoutPart?.SlideLayout?.CommonSlideData?.Name?.Value), Is.EqualTo(new[] { "B Title", "B Content", "B Content" }));
            Assert.That(document.PresentationPart!.SlideMasterParts.Single().SlideLayoutParts.Count(), Is.EqualTo(Layouts().Length), "The layout a slide leaves stays in the template.");
            Assert.That(FootersOf(slides[0]), Is.EqualTo(new[] { "Mission team" }));
            Assert.That(FootersOf(slides[1]), Is.EqualTo(new[] { "Mission team" }));
            Assert.That(FootersOfSlide(withoutFooter, 1), Is.EqualTo(new[] { "Old footer" }), "Without a footer of the presentation, a kept slide keeps its own.");
        });
    }

    [Test]
    public async Task AKeptSlideIsLinkedToTheIndicesOfItsNewVariant()
    {
        using var templateFile = TestTemplate.Create(
            new TestTemplateOptions(Slides: [new TestSlide(0, (TestPlaceholder.Title(), "News"), (new TestPlaceholder(null, 1), "A point"), (new TestPlaceholder(P.PlaceholderValues.Footer, 10), "Old footer"))]),
            new TestLayout("A Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, TestTemplate.MASTER_BODY_BOUNDS), TestPlaceholder.Footer(10, FOOTER)),
            new TestLayout("B Content", TestPlaceholder.Title(), TestPlaceholder.Text(2, TestTemplate.MASTER_BODY_BOUNDS), TestPlaceholder.Footer(12, FOOTER)));

        var template = PresentationTemplate.Read(templateFile);
        templateFile.Position = 0;

        using var output = new MemoryStream();
        await PresentationWriter.WriteAsync(templateFile, template, new SlideDeckSpec("Weekly", [Keep(1)]), OPTIONS with { VariantLabel = "B" }, new FakePictureSource(), output, CancellationToken.None);
        output.Position = 0;

        using var document = PresentationDocument.Open(output, false);
        var slide = SlidesOf(document)[0];
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(slide.SlideLayoutPart?.SlideLayout?.CommonSlideData?.Name?.Value, Is.EqualTo("B Content"));
            Assert.That(slide.Slide!.Descendants<P.PlaceholderShape>().Select(placeholder => (placeholder.Type?.InnerText ?? "obj", placeholder.Index?.Value)), Is.EqualTo(new (string, uint?)[] { ("title", null), ("obj", 2), ("ftr", 12) }));
            Assert.That(FootersOf(slide), Is.EqualTo(new[] { "Mission team" }));
        });
    }

    [Test]
    public async Task AnEditedSlideTakesTheNewTextsInTheFormattingOfTheOldOnes()
    {
        using var templateFile = TestTemplate.Create(TEMPLATE_WITH_SLIDES, Layouts());
        using (var templateDocument = PresentationDocument.Open(templateFile, true))
        {
            //
            // The author made the title of the news slide bold by hand, and left a sub-point
            // waiting for text: without a bullet, and in a size of its own:
            //
            var news = SlidesOf(templateDocument)[1];
            foreach (var run in PlaceholderShapeOf(news, "title", null).Descendants<A.Run>().ToList())
                run.RunProperties = new A.RunProperties { Bold = true };

            PlaceholderShapeOf(news, "obj", 1).TextBody!.Append(new A.Paragraph(new A.ParagraphProperties(new A.NoBullet()) { Level = 1 }, new A.EndParagraphRunProperties { FontSize = 1600 }));
        }

        var deck = new SlideDeckSpec("Weekly", [Edit(2, "News of the week", texts: [Text(new SlideParagraph("A new release", 0), new SlideParagraph("Faster", 1))], labels: ["This week"])]);
        var (file, _) = await WriteFromAsync(templateFile, deck, OPTIONS);
        using var document = PresentationDocument.Open(file, false);
        var slide = SlidesOf(document)[0];
        var paragraphs = BodyParagraphsOf(slide);

        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(TitleOf(slide), Is.EqualTo("News of the week"));
            Assert.That(PlaceholderShapeOf(slide, "title", null).Descendants<A.Run>().Select(run => run.RunProperties?.Bold?.Value), Is.All.True, "The new title keeps the formatting of the old one.");
            Assert.That(paragraphs.Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { "A new release", "Faster" }));
            Assert.That(paragraphs[1].ParagraphProperties?.Level?.Value, Is.EqualTo(1));
            Assert.That(paragraphs[1].ParagraphProperties?.GetFirstChild<A.NoBullet>(), Is.Not.Null, "A sub-point keeps what the author chose for sub-points.");
            Assert.That(paragraphs[1].Elements<A.Run>().Single().RunProperties?.FontSize?.Value, Is.EqualTo(1600));
            Assert.That(paragraphs[0].Elements<A.Run>().Single().RunProperties?.FontSize, Is.Null, "A main point keeps what the author chose for main points.");
            Assert.That(PlaceholderShapeOf(slide, "body", 17).TextBody!.InnerText, Is.EqualTo("This week"), "A slot the template slide left out gets its placeholder from the layout.");
            Assert.That(slide.Slide!.Descendants<P.NonVisualDrawingProperties>().Select(properties => properties.Id!.Value), Is.Unique);
        });
    }

    [Test]
    public async Task WhatAnEditedSlideLeavesEmptyKeepsTheContentOfTheTemplate()
    {
        var deck = new SlideDeckSpec("Weekly", [Edit(2, labels: ["This week"])]);

        var (file, _) = await WriteAsync(deck, OPTIONS, TEMPLATE_WITH_SLIDES);
        using var document = PresentationDocument.Open(file, false);
        var slide = SlidesOf(document)[0];

        Assert.Multiple(() =>
        {
            Assert.That(TitleOf(slide), Is.EqualTo("News"));
            Assert.That(BodyParagraphsOf(slide).Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { "AI Studio:", "EINSTEIN:" }));
        });
    }

    [Test]
    public async Task APictureOfAnEditedSlideReplacesTheOneOfTheTemplate()
    {
        // Last week's presentation is this week's template:
        var (lastWeek, _) = await WriteAsync(DECK, OPTIONS, pictures: new FakePictureSource(WIDE_PICTURE, WIDE_PICTURE));
        var tallPicture = new SlidePicture([4, 5, 6], "image/png", 100, 400);
        var deck = new SlideDeckSpec("Mars", [Edit(3, pictures: [1])]);

        var (file, _) = await WriteFromAsync(lastWeek, deck, OPTIONS, new FakePictureSource(tallPicture));
        using var document = PresentationDocument.Open(file, false);
        var slide = SlidesOf(document)[0];
        using var pictureData = new MemoryStream();
        using (var stream = slide.ImageParts.Single().GetStream(FileMode.Open, FileAccess.Read))
            stream.CopyTo(pictureData);

        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(TitleOf(slide), Is.EqualTo("The rocket"));
            Assert.That(BodyParagraphsOf(slide).Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { "Reusable" }));
            Assert.That(slide.Slide!.Descendants<P.Picture>().Count(), Is.EqualTo(1));
            Assert.That(pictureData.ToArray(), Is.EqualTo(new byte[] { 4, 5, 6 }), "The old picture leaves the file along with its shape.");
        });
    }

    /// <summary>
    /// Slides of <see cref="Layouts"/>: a title slide, three content slides of which one is hidden
    /// and one the imprint, and a hint nobody keeps.
    /// </summary>
    private static readonly TestTemplateOptions TEMPLATE_WITH_SLIDES = new(Slides:
    [
        new TestSlide(0, (TestPlaceholder.Title(), "Consultation hour"), (new TestPlaceholder(P.PlaceholderValues.SubTitle, 1), "Every Monday"), (new TestPlaceholder(P.PlaceholderValues.Footer, 10), "Old footer")),
        new TestSlide(2, (TestPlaceholder.Title(), "News"), (new TestPlaceholder(null, 1), "AI Studio:\nEINSTEIN:"), (new TestPlaceholder(P.PlaceholderValues.Footer, 10), "Old footer")),
        new TestSlide(2, (TestPlaceholder.Title(), "Questions")) { IsHidden = true },
        new TestSlide(2, (TestPlaceholder.Title(), "Impressum"), (new TestPlaceholder(null, 1), "Thema:\tConsultation hour\nDatum:\t2026-10-12\nAutoren:\tThe team")),
        new TestSlide(0, (TestPlaceholder.Title(), "A hint to delete")),
    ]);

    private static SlideSpec Keep(int templateSlide, bool startsChapter = false) => new(string.Empty, startsChapter, string.Empty, string.Empty, [], [], [], null, templateSlide);

    private static SlideSpec Edit(int templateSlide, string title = "", IReadOnlyList<SlideTextBlock>? texts = null, IReadOnlyList<string>? labels = null, IReadOnlyList<int>? pictures = null) => new(string.Empty, false, title, string.Empty, texts ?? [], labels ?? [], pictures ?? [], null, templateSlide);

    /// <summary>
    /// The shape of a slide which fills the placeholder of the given type and index.
    /// </summary>
    private static P.Shape PlaceholderShapeOf(SlidePart slide, string type, uint? index) => slide.Slide!.Descendants<P.Shape>()
        .Single(shape => shape.Descendants<P.PlaceholderShape>().Any(placeholder => (placeholder.Type?.InnerText ?? "obj") == type && placeholder.Index?.Value == index));

    private static TestLayout[] Layouts() =>
    [
        new("A Title", TestPlaceholder.Title(), TestPlaceholder.Subtitle(SUBTITLE), TestPlaceholder.Footer(10, FOOTER), TestPlaceholder.SlideNumber(11, SLIDE_NUMBER)),
        new("B Title", TestPlaceholder.Title(), TestPlaceholder.Subtitle(SUBTITLE), TestPlaceholder.Footer(10, FOOTER), TestPlaceholder.SlideNumber(11, SLIDE_NUMBER)),
        new("A Content", TestPlaceholder.Title(), TestPlaceholder.Label(17, HEADING), TestPlaceholder.Text(1, TestTemplate.MASTER_BODY_BOUNDS), TestPlaceholder.Footer(10, FOOTER), TestPlaceholder.SlideNumber(11, SLIDE_NUMBER)),
        new("B Content", TestPlaceholder.Title(), TestPlaceholder.Label(17, HEADING), TestPlaceholder.Text(1, TestTemplate.MASTER_BODY_BOUNDS), TestPlaceholder.Footer(10, FOOTER), TestPlaceholder.SlideNumber(11, SLIDE_NUMBER)),
        new("Picture with text", TestPlaceholder.Title(), TestPlaceholder.Text(1, LEFT_TEXT), TestPlaceholder.Picture(20, RIGHT_PICTURE)),
        new("Backdrop", TestPlaceholder.Picture(11, WHOLE_SLIDE), TestPlaceholder.Title()),
        new("Table", TestPlaceholder.Title(), TestPlaceholder.Table(11, TestTemplate.MASTER_BODY_BOUNDS)),
    ];

    private static async Task<(MemoryStream File, PresentationWriteResult Result)> WriteAsync(SlideDeckSpec deck, PresentationWriteOptions options, TestTemplateOptions? templateOptions = null, IPictureSource? pictures = null)
    {
        using var templateFile = TestTemplate.Create(templateOptions ?? new TestTemplateOptions(), Layouts());
        return await WriteFromAsync(templateFile, deck, options, pictures);
    }

    /// <summary>
    /// Writes a presentation from a template file of the test's own, e.g., from a presentation written before.
    /// </summary>
    private static async Task<(MemoryStream File, PresentationWriteResult Result)> WriteFromAsync(Stream templateFile, SlideDeckSpec deck, PresentationWriteOptions options, IPictureSource? pictures = null)
    {
        templateFile.Position = 0;
        var template = PresentationTemplate.Read(templateFile);
        templateFile.Position = 0;

        var output = new MemoryStream();
        var result = await PresentationWriter.WriteAsync(templateFile, template, deck, options, pictures ?? new FakePictureSource(), output, CancellationToken.None);
        output.Position = 0;
        return (output, result);
    }

    private static List<string> ValidationErrorsOf(PresentationDocument document) => new OpenXmlValidator(FileFormatVersions.Office2019)
        .Validate(document)
        .Select(error => $"{error.Part?.Uri} {error.Path?.XPath}: {error.Description}")
        .ToList();

    /// <summary>
    /// The paragraphs of the text placeholder with index 1.
    /// </summary>
    private static List<A.Paragraph> BodyParagraphsOf(SlidePart slide) => slide.Slide!.Descendants<P.Shape>()
        .Single(shape => shape.Descendants<P.PlaceholderShape>().Any(placeholder => placeholder.Index?.Value == 1))
        .TextBody!.Elements<A.Paragraph>()
        .ToList();

    private static List<string> FootersOfImprint(MemoryStream file) => FootersOfSlide(file, ^1);

    private static List<string> FootersOfSlide(MemoryStream file, Index slide)
    {
        using var document = PresentationDocument.Open(file, false);
        return FootersOf(SlidesOf(document)[slide]);
    }

    private static List<string> FootersOf(SlidePart slide) => slide.Slide!.Descendants<P.Shape>()
        .Where(shape => shape.Descendants<P.PlaceholderShape>().Any(placeholder => placeholder.Type?.InnerText == "ftr"))
        .Select(shape => shape.TextBody!.InnerText)
        .ToList();

    private static List<SlidePart> SlidesOf(PresentationDocument document)
    {
        var presentationPart = document.PresentationPart!;
        return presentationPart.Presentation!.SlideIdList!.Elements<P.SlideId>()
            .Select(slideId => (SlidePart)presentationPart.GetPartById(slideId.RelationshipId!.Value!))
            .ToList();
    }

    private static string TitleOf(SlidePart slide) => slide.Slide!.Descendants<P.Shape>()
        .Single(shape => shape.Descendants<P.PlaceholderShape>().Any(placeholder => placeholder.Type?.InnerText == "title"))
        .TextBody!.InnerText;

    private static List<string?> LayoutNamesOf(MemoryStream file)
    {
        using var document = PresentationDocument.Open(file, false);
        return SlidesOf(document).Select(slide => slide.SlideLayoutPart?.SlideLayout?.CommonSlideData?.Name?.Value).ToList();
    }

    private static SlideTextBlock Text(params SlideParagraph[] paragraphs) => new(paragraphs);

    private sealed class FakePictureSource(params SlidePicture?[] pictures) : IPictureSource
    {
        public Task<SlidePicture?> LoadAsync(int number, CancellationToken token) => Task.FromResult(number >= 1 && number <= pictures.Length ? pictures[number - 1] : null);
    }
}