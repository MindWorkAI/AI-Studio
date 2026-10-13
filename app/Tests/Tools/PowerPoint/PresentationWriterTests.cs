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
/// pictures, tables, and sections.
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

    private static readonly PresentationWriteOptions OPTIONS = new("A", true, "Mission team");

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
            Assert.That(document.PresentationPart!.SlideParts.Count(), Is.EqualTo(5), "The sample slides of the template are gone, parts included.");
            Assert.That(SlidesOf(document).Select(TitleOf), Is.EqualTo(new[] { "Mars", "Why Mars", "The rocket", "Landing", "Costs" }));
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
            Assert.That(sections.Select(section => section.Name?.Value), Is.EqualTo(new[] { "Mars", "Why Mars", "Landing" }), "The section of the template's samples is gone, and the title slide forms a section named after the presentation.");
            Assert.That(sections.Select(section => section.Descendants<P14.SectionSlideIdListEntry>().Count()), Is.EqualTo(new[] { 1, 2, 2 }));
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
    public async Task EveryChapterAfterTheFirstMovesOnToTheNextVariant()
    {
        var deck = new SlideDeckSpec("Mars",
        [
            new SlideSpec("L1", false, "Mars", string.Empty, [], [], [], null),
            new SlideSpec("L2", true, "One", string.Empty, [], [], [], null),
            new SlideSpec("L2", false, "One more", string.Empty, [], [], [], null),
            new SlideSpec("L2", true, "Two", string.Empty, [], [], [], null),
            new SlideSpec("L2", false, "Two more", string.Empty, [], [], [], null),
        ]);

        var (perChapter, _) = await WriteAsync(deck, OPTIONS);
        var (fixedVariant, _) = await WriteAsync(deck, OPTIONS with { VariantLabel = "B", ChangeVariantPerChapter = false });

        Assert.Multiple(() =>
        {
            Assert.That(LayoutNamesOf(perChapter), Is.EqualTo(new[] { "A Title", "A Content", "A Content", "B Content", "B Content" }));
            Assert.That(LayoutNamesOf(fixedVariant), Is.EqualTo(new[] { "B Title", "B Content", "B Content", "B Content", "B Content" }));
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