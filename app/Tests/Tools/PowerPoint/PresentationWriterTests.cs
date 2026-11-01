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
/// pictures, tables, sections, and the template slides a deck keeps or edits.
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

    private static readonly PresentationWriteOptions OPTIONS = new("A", "Mission team", false);

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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(document.PresentationPart!.SlideParts.Count(), Is.EqualTo(5), "The sample slides of the template are gone, parts included.");
            Assert.That(SlidesOf(document).Select(TitleOf), Is.EqualTo(new[] { "Mars", "Why Mars", "The rocket", "Landing", "Costs" }), "The presentation holds the slides of the deck, nothing more.");
        }
    }

    [Test]
    public async Task EveryChapterBecomesASection()
    {
        var (file, _) = await WriteAsync(DECK, OPTIONS, new TestTemplateOptions(ExampleSlides: 2), new FakePictureSource(WIDE_PICTURE, WIDE_PICTURE));
        using var document = PresentationDocument.Open(file, false);

        var sections = document.PresentationPart!.Presentation!.Descendants<P14.Section>().ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sections.Select(section => section.Name?.Value), Is.EqualTo(new[] { "Mars", "Why Mars", "Landing" }), "The section of the template's samples is gone, and the title slide forms a section named after the presentation.");
            Assert.That(sections.Select(section => section.Descendants<P14.SectionSlideIdListEntry>().Count()), Is.EqualTo(new[] { 1, 2, 2 }));
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(placeholders, Is.EqualTo(new (string, uint?)[] { ("title", null), ("obj", 1), ("ftr", 10), ("sldNum", 11) }));
            Assert.That(textBody.Elements<A.Paragraph>().Select(paragraph => paragraph.ParagraphProperties?.Level?.Value ?? 0), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(textBody.InnerText, Is.EqualTo("Close to EarthTwo years per trip"));
            Assert.That(slide.Descendants<P.Shape>().Any(shape => shape.InnerText == "Mission team"), Is.True, "The footer carries the text the user chose.");
            Assert.That(slide.Descendants<A.Field>().Single().Type?.Value, Is.EqualTo("slidenum"), "PowerPoint fills in the number itself.");
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(LayoutNamesOf(chosen), Is.EqualTo(new[] { "B Title", "B Content", "B Content", "B Content" }), "Chapters keep the variant.");
            Assert.That(LayoutNamesOf(unknown), Is.EqualTo(new[] { "A Title", "A Content", "A Content", "A Content" }), "A variant the template lacks falls back to its first one.");
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(transform.Extents!.Cx!.Value, Is.EqualTo(RIGHT_PICTURE.Width));
            Assert.That(transform.Extents.Cy!.Value, Is.EqualTo(RIGHT_PICTURE.Width / 4));
            Assert.That(transform.Offset!.Y!.Value, Is.EqualTo(RIGHT_PICTURE.Y + (RIGHT_PICTURE.Height - RIGHT_PICTURE.Width / 4) / 2));
            Assert.That(crop.Left?.Value, Is.EqualTo(27_778), "16:9 out of 4:1 keeps 44.4 % of the width.");
            Assert.That(crop.Right?.Value, Is.EqualTo(27_778));
            Assert.That(crop.Top, Is.Null);
            Assert.That(slides[2].ImageParts.Count(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task AnIconIsNeverCropped()
    {
        var (file, _) = await WriteAsync(DECK, OPTIONS, pictures: new FakePictureSource(WIDE_PICTURE, WIDE_PICTURE with { IsIcon = true }));
        using var document = PresentationDocument.Open(file, false);

        // The icon lands in the backdrop, which the validator advises against, and keeps its edges anyway:
        var icon = SlidesOf(document)[3].Slide!.Descendants<P.Picture>().Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(icon.BlipFill!.GetFirstChild<A.SourceRectangle>(), Is.Null);
            Assert.That(icon.ShapeProperties!.Transform2D!.Extents!.Cx!.Value, Is.EqualTo(WHOLE_SLIDE.Width));
            Assert.That(icon.ShapeProperties.Transform2D.Extents.Cy!.Value, Is.EqualTo(WHOLE_SLIDE.Width / 4));
            Assert.That(ValidationErrorsOf(document), Is.Empty);
        }
    }

    [Test]
    public async Task APictureInAFormatPowerPointDoesNotShowIsSkipped()
    {
        var (file, result) = await WriteAsync(DECK, OPTIONS, pictures: new FakePictureSource(WIDE_PICTURE with { ContentType = "image/webp" }, WIDE_PICTURE));
        using var document = PresentationDocument.Open(file, false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SkippedPictures, Is.EqualTo(new[] { 1 }));
            Assert.That(SlidesOf(document)[2].Slide!.Descendants<P.Picture>(), Is.Empty, "The slot stays empty rather than holding a picture nobody can see.");
            Assert.That(ValidationErrorsOf(document), Is.Empty);
        }
    }

    [Test]
    public async Task AnEmptyPictureSlotStaysOnlyWhenTheUserAsksForIt()
    {
        var deck = new SlideDeckSpec("Mars",
        [
            new SlideSpec("L3", false, "The rocket", string.Empty, [Text(new SlideParagraph("Reusable", 0))], [], [], null),
        ]);

        var (left, _) = await WriteAsync(deck, OPTIONS);
        var (kept, _) = await WriteAsync(deck, OPTIONS with { KeepEmptyPicturePlaceholders = true });
        using var leftDocument = PresentationDocument.Open(left, false);
        using var keptDocument = PresentationDocument.Open(kept, false);
        var keptSlide = SlidesOf(keptDocument)[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(PicturePlaceholdersOf(SlidesOf(leftDocument)[0]), Is.Empty, "By default, no prompt text stays behind.");
            Assert.That(PicturePlaceholdersOf(keptSlide), Has.Count.EqualTo(1));
            Assert.That(PlaceholderShapeOf(keptSlide, "pic", 20).TextBody, Is.Null, "Without a text of its own, the placeholder shows the prompt text of the layout.");
            Assert.That(keptSlide.ImageParts, Is.Empty);
            Assert.That(ValidationErrorsOf(keptDocument), Is.Empty);
        }
    }

    [Test]
    public async Task ASkippedPictureLeavesItsPlaceholderWhenTheUserAsksForIt()
    {
        var (file, result) = await WriteAsync(DECK, OPTIONS with { KeepEmptyPicturePlaceholders = true }, pictures: new FakePictureSource(WIDE_PICTURE with { ContentType = "image/webp" }, WIDE_PICTURE));
        using var document = PresentationDocument.Open(file, false);
        var slide = SlidesOf(document)[2];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SkippedPictures, Is.EqualTo(new[] { 1 }));
            Assert.That(slide.Slide!.Descendants<P.Picture>(), Is.Empty);
            Assert.That(PicturePlaceholdersOf(slide), Has.Count.EqualTo(1), "The user can insert the picture there themselves.");
            Assert.That(ValidationErrorsOf(document), Is.Empty);
        }
    }

    [Test]
    public async Task TheTableUsesTheStyleOfTheTemplate()
    {
        var (file, _) = await WriteAsync(DECK, OPTIONS);
        using var document = PresentationDocument.Open(file, false);

        var table = SlidesOf(document)[4].Slide!.Descendants<A.Table>().Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(table.TableProperties!.GetFirstChild<A.TableStyleId>()?.Text, Is.EqualTo(TestTemplate.DEFAULT_TABLE_STYLE_ID));
            Assert.That(table.TableProperties.FirstRow?.Value, Is.True, "The headers are the first row.");
            Assert.That(table.Elements<A.TableRow>().Select(row => row.InnerText), Is.EqualTo(new[] { "ItemCost", "Fuel10" }));
            Assert.That(table.TableGrid!.Elements<A.GridColumn>().Sum(column => column.Width!.Value), Is.EqualTo(TestTemplate.MASTER_BODY_BOUNDS.Width), "The columns span the slot exactly.");
        }
    }

    [Test]
    public async Task ATemplateFileBecomesAPresentation()
    {
        var (file, _) = await WriteAsync(DECK, OPTIONS, new TestTemplateOptions(AsTemplate: true));
        using var document = PresentationDocument.Open(file, false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(document.DocumentType, Is.EqualTo(PresentationDocumentType.Presentation));
            Assert.That(ValidationErrorsOf(document), Is.Empty);
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(slides.Select(TitleOf), Is.EqualTo(new[] { "Consultation hour", "Agenda", "News", "Questions", "Impressum" }));
            Assert.That(document.PresentationPart!.SlideParts.Count(), Is.EqualTo(5), "The slide the deck leaves out is gone, part included.");
            Assert.That(BodyParagraphsOf(slides[2]).Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { "AI Studio:", "EINSTEIN:" }));
            Assert.That(slides[3].Slide!.Show, Is.Null, "The user chose the slide, so it shows although the template hides it.");
            Assert.That(document.PresentationPart.Presentation!.SlideIdList!.Elements<P.SlideId>().Select(slideId => slideId.Id!.Value), Is.Unique);
        }
    }

    [Test]
    public async Task AKeptSlideStaysWhereTheDeckPutsItWhateverItsTitle()
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(SlidesOf(document).Select(TitleOf), Is.EqualTo(new[] { "Consultation hour", "Impressum", "Agenda", "News" }), "An imprint is a slide like any other: nothing moves it to the end.");
            Assert.That(sections.Select(section => section.Name?.Value), Is.EqualTo(new[] { "Weekly", "Agenda" }));
            Assert.That(sections.Select(section => section.Descendants<P14.SectionSlideIdListEntry>().Count()), Is.EqualTo(new[] { 2, 2 }));
        }
    }

    [Test]
    public async Task KeptSlidesTakeTheColorVariantAndTheFooterOfThePresentation()
    {
        var deck = new SlideDeckSpec("Weekly", [Keep(1), Keep(2)]);

        var (withFooter, _) = await WriteAsync(deck, OPTIONS with { VariantLabel = "B" }, TEMPLATE_WITH_SLIDES);
        var (withoutFooter, _) = await WriteAsync(deck, OPTIONS with { FooterText = string.Empty }, TEMPLATE_WITH_SLIDES);
        using var document = PresentationDocument.Open(withFooter, false);
        var slides = SlidesOf(document);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(slides.Select(slide => slide.SlideLayoutPart?.SlideLayout?.CommonSlideData?.Name?.Value), Is.EqualTo(new[] { "B Title", "B Content" }));
            Assert.That(document.PresentationPart!.SlideMasterParts.Single().SlideLayoutParts.Count(), Is.EqualTo(Layouts().Length), "The layout a slide leaves stays in the template.");
            Assert.That(FootersOf(slides[0]), Is.EqualTo(new[] { "Mission team" }));
            Assert.That(FootersOf(slides[1]), Is.EqualTo(new[] { "Mission team" }));
            Assert.That(FootersOfSlide(withoutFooter, 1), Is.EqualTo(new[] { "Old footer" }), "Without a footer of the presentation, a kept slide keeps its own.");
        }
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
        await templateFile.CopyToAsync(output);
        output.Position = 0;
        await PresentationWriter.WriteAsync(output, template, new SlideDeckSpec("Weekly", [Keep(1)]), OPTIONS with { VariantLabel = "B" }, new FakePictureSource(), CancellationToken.None);
        output.Position = 0;

        using var document = PresentationDocument.Open(output, false);
        var slide = SlidesOf(document)[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(slide.SlideLayoutPart?.SlideLayout?.CommonSlideData?.Name?.Value, Is.EqualTo("B Content"));
            Assert.That(slide.Slide!.Descendants<P.PlaceholderShape>().Select(placeholder => (placeholder.Type?.InnerText ?? "obj", placeholder.Index?.Value)), Is.EqualTo(new (string, uint?)[] { ("title", null), ("obj", 2), ("ftr", 12) }));
            Assert.That(FootersOf(slide), Is.EqualTo(new[] { "Mission team" }));
        }
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

        using (Assert.EnterMultipleScope())
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
        }
    }

    [Test]
    public async Task WhatAnEditedSlideLeavesEmptyKeepsTheContentOfTheTemplate()
    {
        var deck = new SlideDeckSpec("Weekly", [Edit(2, labels: ["This week"])]);

        var (file, _) = await WriteAsync(deck, OPTIONS, TEMPLATE_WITH_SLIDES);
        using var document = PresentationDocument.Open(file, false);
        var slide = SlidesOf(document)[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TitleOf(slide), Is.EqualTo("News"));
            Assert.That(BodyParagraphsOf(slide).Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { "AI Studio:", "EINSTEIN:" }));
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(TitleOf(slide), Is.EqualTo("The rocket"));
            Assert.That(BodyParagraphsOf(slide).Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { "Reusable" }));
            Assert.That(slide.Slide!.Descendants<P.Picture>().Count(), Is.EqualTo(1));
            Assert.That(pictureData.ToArray(), Is.EqualTo(new byte[] { 4, 5, 6 }), "The old picture leaves the file along with its shape.");
            Assert.That(slide.Slide!.Descendants<P.Picture>().Single().ShapeProperties!.Transform2D!.Extents!.Cy!.Value, Is.EqualTo(RIGHT_PICTURE.Height), "The old picture was fit into its slot, which therefore stays as large as the layout makes it.");
        }
    }

    [Test]
    public async Task APictureGoesWhereTheSlideMovedItsSlot()
    {
        var moved = new SlotBounds(6_200_000, 4_000_000, 2_000_000, 1_500_000);
        var templateOptions = new TestTemplateOptions(Slides: [new TestSlide(4, (TestPlaceholder.Title(), "The rocket"), (TestPlaceholder.Picture(20, moved), string.Empty))]);
        var deck = new SlideDeckSpec("Mars", [Edit(1, pictures: [1])]);

        var (file, _) = await WriteAsync(deck, OPTIONS, templateOptions, new FakePictureSource(WIDE_PICTURE));
        using var document = PresentationDocument.Open(file, false);
        var transform = SlidesOf(document)[0].Slide!.Descendants<P.Picture>().Single().ShapeProperties!.Transform2D!;
        var bounds = new SlotBounds(transform.Offset!.X!.Value, transform.Offset.Y!.Value, transform.Extents!.Cx!.Value, transform.Extents.Cy!.Value);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(moved.Contains(bounds, 0, 0), Is.True, $"The picture fits into the slot where the slide put it, but lies at {bounds}.");
            Assert.That(bounds.Width, Is.EqualTo(moved.Width), "The wide picture takes the whole width of the moved slot.");
        }
    }

    [Test]
    public async Task ATableOfAnEditedSlideReplacesTheFreeTableOfTheTemplate()
    {
        var deck = new SlideDeckSpec("Mars",
        [
            Edit(1, table: new SlideTableSpec(["Item", "Cost"], [["Fuel", "10"], ["Food", "5"]])),
            Edit(2, table: new SlideTableSpec(["Step", "Date"], [["Launch", "2030"]])),
        ]);

        var (file, _) = await WriteAsync(deck, OPTIONS, TEMPLATE_WITH_FREE_TABLES);
        using var document = PresentationDocument.Open(file, false);
        var slides = SlidesOf(document);
        var shapeTree = slides[0].Slide!.CommonSlideData!.ShapeTree!;
        var table = shapeTree.Elements<P.GraphicFrame>().Single();
        var note = shapeTree.Elements<P.Shape>().Single(shape => shape.TextBody?.InnerText == "Source: last year");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(RowsOf(table), Is.EqualTo(new[] { "Item | Cost", "Fuel | 10", "Food | 5" }), "No row of the old table is left below the shorter new one.");
            Assert.That(table.Descendants<P.PlaceholderShape>().Select(placeholder => (placeholder.Type?.InnerText, placeholder.Index?.Value)), Is.EqualTo(new (string?, uint?)[] { ("tbl", 11) }));
            Assert.That(shapeTree.Elements().ToList().IndexOf(table), Is.LessThan(shapeTree.Elements().ToList().IndexOf(note)), "The new table lies where the old one did, behind what lay in front of it.");
            Assert.That(RowsOf(slides[1].Slide!.Descendants<P.GraphicFrame>().Single()), Is.EqualTo(new[] { "Step | Date", "Launch | 2030" }));
            Assert.That(slides[1].Slide!.Descendants<P.PlaceholderShape>().Count(placeholder => placeholder.Type?.InnerText == "tbl"), Is.EqualTo(1), "The empty placeholder the free table stood in for is gone.");
        }
    }

    [Test]
    public async Task WhatAnEditedSlideLeavesEmptyKeepsTheFreeTable()
    {
        var deck = new SlideDeckSpec("Mars", [Edit(1, "New costs")]);

        var (file, _) = await WriteAsync(deck, OPTIONS, TEMPLATE_WITH_FREE_TABLES);
        using var document = PresentationDocument.Open(file, false);
        var slide = SlidesOf(document)[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TitleOf(slide), Is.EqualTo("New costs"));
            Assert.That(RowsOf(slide.Slide!.Descendants<P.GraphicFrame>().Single()), Has.Count.EqualTo(6));
        }
    }

    [Test]
    public async Task AnEditedSlideLosesWhatTheModelRemoves()
    {
        var deck = new SlideDeckSpec("Weekly", [Edit(1, texts: [Text(new SlideParagraph("A new release", 0))]) with { Removals = ["element 1", "element 2", "label 1"] }]);

        var (file, _) = await WriteAsync(deck, OPTIONS, TEMPLATE_WITH_ELEMENTS);
        using var document = PresentationDocument.Open(file, false);
        var slide = SlidesOf(document)[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(BodyParagraphsOf(slide).Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { "A new release" }));
            Assert.That(slide.Slide!.Descendants<P.Picture>(), Is.Empty);
            Assert.That(slide.ImageParts, Is.Empty, "The picture leaves the file along with its shape.");
            Assert.That(slide.Slide.Descendants<P.Shape>().Select(shape => shape.TextBody?.InnerText), Has.None.EqualTo("Bildquelle hier angeben"));
            Assert.That(slide.Slide.Descendants<P.PlaceholderShape>().Where(placeholder => placeholder.Index?.Value == 17), Is.Empty, "The label of the template is gone, with its sample text.");
            Assert.That(slide.Slide.Descendants<A.SolidFill>().Count(), Is.EqualTo(1), "What the model does not remove stays.");
            Assert.That(slide.ChartParts.Count(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task AnEditedSlideFillsTheCopiesOfItsSlotsInTheirFormatting()
    {
        using var templateFile = TestTemplate.Create(new TestTemplateOptions(Slides: [StackedHeadings.Slide()]), StackedHeadings.Layout());
        var deck = new SlideDeckSpec("Missions", [Edit(1, texts: [Text(new SlideParagraph("To Mars", 0)), Text(new SlideParagraph("To the Moon", 0))], labels: ["Mars", "Moon"]) with { Removals = ["label 3", "text 3"] }]);

        var (file, _) = await WriteFromAsync(templateFile, deck, OPTIONS);
        using var document = PresentationDocument.Open(file, false);
        var slide = SlidesOf(document)[0];
        var secondHeading = ShapeWithIdOf(slide, 5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(PlaceholderShapeOf(slide, "body", 17).TextBody!.InnerText, Is.EqualTo("Mars"));
            Assert.That(BodyParagraphsOf(slide).Select(paragraph => paragraph.InnerText), Is.EqualTo(new[] { "To Mars" }));
            Assert.That(secondHeading.TextBody!.InnerText, Is.EqualTo("Moon"), "The second label is the first copy of the heading.");
            Assert.That(secondHeading.ShapeProperties!.GetFirstChild<A.SolidFill>()?.RgbColorModelHex?.Val?.Value, Is.EqualTo(StackedHeadings.HEADING_FILL), "A copy keeps the look of the heading it repeats.");
            Assert.That(ShapeWithIdOf(slide, 6).TextBody!.InnerText, Is.EqualTo("To the Moon"));
            Assert.That(slide.Slide!.Descendants<P.NonVisualDrawingProperties>().Select(properties => properties.Id!.Value), Has.None.EqualTo(7U).And.None.EqualTo(8U), "The third heading and its text are removed.");
            Assert.That(slide.Slide.Descendants<P.Shape>().Select(shape => shape.TextBody?.InnerText), Has.Some.EqualTo("Bildquelle hier angeben"), "What the model does not remove stays.");
            Assert.That(slide.Slide.Descendants<P.Picture>().Count(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task TheCopiesOfSlotsStayFillableOnAnotherVariant()
    {
        using var templateFile = TestTemplate.Create(new TestTemplateOptions(Slides: [StackedHeadings.Slide()]), StackedHeadings.Layout("A Content"), StackedHeadings.Layout("B Content"));
        var deck = new SlideDeckSpec("Missions", [Edit(1, labels: ["Mars", "Moon", "Venus"])]);

        var (file, _) = await WriteFromAsync(templateFile, deck, OPTIONS with { VariantLabel = "B" });
        using var document = PresentationDocument.Open(file, false);
        var slide = SlidesOf(document)[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(slide.SlideLayoutPart?.SlideLayout?.CommonSlideData?.Name?.Value, Is.EqualTo("B Content"));
            Assert.That(PlaceholderShapeOf(slide, "body", 17).TextBody!.InnerText, Is.EqualTo("Mars"));
            Assert.That(ShapeWithIdOf(slide, 5).TextBody!.InnerText, Is.EqualTo("Moon"));
            Assert.That(ShapeWithIdOf(slide, 7).TextBody!.InnerText, Is.EqualTo("Venus"));
            Assert.That(ShapeWithIdOf(slide, 6).TextBody!.InnerText, Is.EqualTo("Duis autem"), "A copy the slide leaves empty keeps the content of the template.");
        }
    }

    [Test]
    public async Task RemovingAChartRemovesItsPart()
    {
        var deck = new SlideDeckSpec("Weekly", [Edit(1) with { Removals = ["element 4"] }]);

        var (file, _) = await WriteAsync(deck, OPTIONS, TEMPLATE_WITH_ELEMENTS);
        using var document = PresentationDocument.Open(file, false);
        var slide = SlidesOf(document)[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(slide.Slide!.Descendants<P.GraphicFrame>(), Is.Empty);
            Assert.That(slide.ChartParts, Is.Empty);
            Assert.That(slide.ImageParts.Count(), Is.EqualTo(1), "The picture nobody removed keeps its part.");
        }
    }

    [Test]
    public async Task RemovingAnAnimatedShapeDropsTheAnimationsOfItsSlide()
    {
        using var templateFile = TestTemplate.Create(TEMPLATE_WITH_ELEMENTS with { Slides = [TEMPLATE_WITH_ELEMENTS.Slides![0], TEMPLATE_WITH_ELEMENTS.Slides[0]] }, Layouts());
        using (var templateDocument = PresentationDocument.Open(templateFile, true))
        {
            // The text box naming the source of the picture fades in on both slides:
            foreach (var templateSlide in SlidesOf(templateDocument))
                templateSlide.Slide!.Timing = new P.Timing(new P.BuildList(new P.BuildParagraph { ShapeId = "6", GroupId = 0U }));
        }

        var deck = new SlideDeckSpec("Weekly", [Edit(1) with { Removals = ["element 2"] }, Edit(2) with { Removals = ["element 3"] }]);
        var (file, _) = await WriteFromAsync(templateFile, deck, OPTIONS);
        using var document = PresentationDocument.Open(file, false);
        var slides = SlidesOf(document);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ValidationErrorsOf(document), Is.Empty);
            Assert.That(slides[0].Slide!.Timing, Is.Null, "An animation of a shape which is gone would break the slide.");
            Assert.That(slides[1].Slide!.Timing, Is.Not.Null, "The animated shape is still there.");
        }
    }

    /// <summary>
    /// Slides of the Layouts: a title slide, three content slides of which one is hidden
    /// and one an imprint, and a hint nobody keeps.
    /// </summary>
    private static readonly TestTemplateOptions TEMPLATE_WITH_SLIDES = new(Slides:
    [
        new TestSlide(0, (TestPlaceholder.Title(), "Consultation hour"), (new TestPlaceholder(P.PlaceholderValues.SubTitle, 1), "Every Monday"), (new TestPlaceholder(P.PlaceholderValues.Footer, 10), "Old footer")),
        new TestSlide(2, (TestPlaceholder.Title(), "News"), (new TestPlaceholder(null, 1), "AI Studio:\nEINSTEIN:"), (new TestPlaceholder(P.PlaceholderValues.Footer, 10), "Old footer")),
        new TestSlide(2, (TestPlaceholder.Title(), "Questions")) { IsHidden = true },
        new TestSlide(2, (TestPlaceholder.Title(), "Impressum"), (new TestPlaceholder(null, 1), "Thema:\tConsultation hour\nDatum:\t2026-10-12\nAutoren:\tThe team")),
        new TestSlide(0, (TestPlaceholder.Title(), "A hint to delete")),
    ]);

    /// <summary>
    /// Slides of the Layouts on the table layout whose tables are no placeholders: one
    /// pasted onto the slide, with a note in front of it, and one standing in for an empty placeholder.
    /// </summary>
    private static readonly TestTemplateOptions TEMPLATE_WITH_FREE_TABLES = new(Slides:
    [
        new TestSlide(6, (TestPlaceholder.Title(), "Costs"))
        {
            Elements =
            [
                TestElement.Table(TestTemplate.MASTER_BODY_BOUNDS, ["Item", "Cost"], ["A", "1"], ["B", "2"], ["C", "3"], ["D", "4"], ["E", "5"]),
                TestElement.TextBox(new SlotBounds(838_200, 6_000_000, 4_000_000, 300_000), "Source: last year"),
            ],
        },
        new TestSlide(6, (TestPlaceholder.Title(), "Plan"), (new TestPlaceholder(P.PlaceholderValues.Table, 11), string.Empty))
        {
            Elements = [TestElement.Table(TestTemplate.MASTER_BODY_BOUNDS, ["Step", "Date"], ["Test", "2029"])],
        },
    ]);

    /// <summary>
    /// A slide of the Layouts on the content layout with a sample label, and with
    /// elements of its own: a picture (element 1), a text box naming its source (element 2), a
    /// colored rectangle (element 3), and a chart (element 4).
    /// </summary>
    private static readonly TestTemplateOptions TEMPLATE_WITH_ELEMENTS = new(Slides:
    [
        new TestSlide(2, (TestPlaceholder.Title(), "News"), (new TestPlaceholder(P.PlaceholderValues.Body, 17), "Lorem ipsum"), (new TestPlaceholder(null, 1), "AI Studio:\nEINSTEIN:"))
        {
            Elements =
            [
                TestElement.Picture(RIGHT_PICTURE, 7, 8, 9),
                TestElement.TextBox(new SlotBounds(6_200_000, 5_400_000, 5_150_000, 300_000), "Bildquelle hier angeben"),
                TestElement.Rectangle(new SlotBounds(0, 6_000_000, TestTemplate.SLIDE_WIDTH, 300_000)),
                TestElement.Chart(LEFT_TEXT),
            ],
        },
    ]);

    private static SlideSpec Keep(int templateSlide, bool startsChapter = false) => new(string.Empty, startsChapter, string.Empty, string.Empty, [], [], [], null, templateSlide);

    private static SlideSpec Edit(int templateSlide, string title = "", IReadOnlyList<SlideTextBlock>? texts = null, IReadOnlyList<string>? labels = null, IReadOnlyList<int>? pictures = null, SlideTableSpec? table = null) => new(string.Empty, false, title, string.Empty, texts ?? [], labels ?? [], pictures ?? [], table, templateSlide);

    /// <summary>
    /// The rows of a table, with " | " between their cells.
    /// </summary>
    private static List<string> RowsOf(P.GraphicFrame table) => table.Descendants<A.TableRow>()
        .Select(row => string.Join(" | ", row.Elements<A.TableCell>().Select(cell => cell.InnerText)))
        .ToList();

    /// <summary>
    /// The shape of a slide which fills the placeholder of the given type and index.
    /// </summary>
    private static P.Shape PlaceholderShapeOf(SlidePart slide, string type, uint? index) => slide.Slide!.Descendants<P.Shape>()
        .Single(shape => shape.Descendants<P.PlaceholderShape>().Any(placeholder => (placeholder.Type?.InnerText ?? "obj") == type && placeholder.Index?.Value == index));

    /// <summary>
    /// The empty placeholders of a slide which wait for a picture.
    /// </summary>
    private static List<P.Shape> PicturePlaceholdersOf(SlidePart slide) => slide.Slide!.Descendants<P.Shape>()
        .Where(shape => shape.Descendants<P.PlaceholderShape>().Any(placeholder => placeholder.Type?.InnerText == "pic"))
        .ToList();

    /// <summary>
    /// The shape of a slide with the given ID, e.g., a text box the template slide carries.
    /// </summary>
    private static P.Shape ShapeWithIdOf(SlidePart slide, uint id) => slide.Slide!.Descendants<P.Shape>()
        .Single(shape => shape.NonVisualShapeProperties?.NonVisualDrawingProperties?.Id?.Value == id);

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

    private static async Task<WrittenPresentation> WriteAsync(SlideDeckSpec deck, PresentationWriteOptions options, TestTemplateOptions? templateOptions = null, IPictureSource? pictures = null)
    {
        using var templateFile = TestTemplate.Create(templateOptions ?? new TestTemplateOptions(), Layouts());
        return await WriteFromAsync(templateFile, deck, options, pictures);
    }

    /// <summary>
    /// Writes a presentation from a template file of the test's own, e.g., from a presentation written before.
    /// </summary>
    private static async Task<WrittenPresentation> WriteFromAsync(Stream templateFile, SlideDeckSpec deck, PresentationWriteOptions options, IPictureSource? pictures = null)
    {
        templateFile.Position = 0;
        var template = PresentationTemplate.Read(templateFile);
        templateFile.Position = 0;

        // The writer edits a copy of the template, as the Slide Assistant does with one on disk:
        var output = new MemoryStream();
        await templateFile.CopyToAsync(output);
        output.Position = 0;
        var result = await PresentationWriter.WriteAsync(output, template, deck, options, pictures ?? new FakePictureSource(), CancellationToken.None);
        output.Position = 0;
        return new WrittenPresentation(output, result);
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

    /// <summary>
    /// A presentation a test wrote, and what the writer reported beyond writing it.
    /// </summary>
    /// <param name="File">The presentation, positioned at its start.</param>
    /// <param name="Result">What happened beyond writing.</param>
    private sealed record WrittenPresentation(MemoryStream File, PresentationWriteResult Result);
}