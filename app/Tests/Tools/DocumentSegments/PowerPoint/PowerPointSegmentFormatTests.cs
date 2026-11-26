using AIStudio.Tests.Tools.PowerPoint;
using AIStudio.Tools.DocumentSegments;
using AIStudio.Tools.DocumentSegments.PowerPoint;
using AIStudio.Tools.PowerPoint;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

using Microsoft.Extensions.Logging.Abstractions;

using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace AIStudio.Tests.Tools.DocumentSegments.PowerPoint;

/// <summary>
/// Checks which parts of a presentation a model works on, and that everything else stays as it is.
/// </summary>
[TestFixture]
public sealed class PowerPointSegmentFormatTests
{
    private const string LINK_ID = "rIdMars";
    private const string RICH_PARAGRAPH = "We fly <1>very</1> far,<2/><3>see the plan</3> on slide <4/>";

    private static readonly SlotBounds TEXT_BOX_BOUNDS = new(838_200, 1_825_625, 10_515_600, 1_000_000);
    private static readonly SlotBounds TABLE_BOUNDS = new(838_200, 3_000_000, 10_515_600, 741_680);

    private readonly PowerPointSegmentFormat format = new();
    private string testFolder = string.Empty;
    private string source = string.Empty;
    private string target = string.Empty;

    [SetUp]
    public void CreatePresentation()
    {
        this.testFolder = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "mindwork-ai-studio-tests", Guid.NewGuid().ToString("N"))).FullName;
        this.source = Path.Join(this.testFolder, "source.pptx");
        this.target = Path.Join(this.testFolder, "target.pptx");

        var slide = new TestSlide(0, (TestPlaceholder.Title(), "Mission to Mars"))
        {
            Elements = [TestElement.TextBox(TEXT_BOX_BOUNDS, "rich"), TestElement.Table(TABLE_BOUNDS, ["Name", "Value"], ["Speed", "42"])],
        };

        using (var stream = TestTemplate.Create(new TestTemplateOptions(Slides: [slide]), new TestLayout("Title only", TestPlaceholder.Title())))
            File.WriteAllBytes(this.source, stream.ToArray());

        using var document = PresentationDocument.Open(this.source, true);
        var slidePart = document.PresentationPart!.SlideParts.Single();
        slidePart.AddHyperlinkRelationship(new Uri("https://example.org/mars"), true, LINK_ID);

        // A paragraph as PowerPoint writes one: runs in their language, a word in bold, a line break, a link, and the number of the slide:
        var paragraph = slidePart.Slide!.Descendants<A.Paragraph>().Single(candidate => candidate.InnerText == "rich");
        paragraph.RemoveAllChildren();
        paragraph.Append(
            new A.Run(new A.RunProperties { Language = "en-US", Dirty = false }, new A.Text("We fly ")),
            new A.Run(new A.RunProperties { Language = "en-US", Bold = true }, new A.Text("very")),
            new A.Run(new A.RunProperties { Language = "en-US", SpellingError = true }, new A.Text(" far,")),
            new A.Break(new A.RunProperties { Language = "en-US" }),
            new A.Run(new A.RunProperties(new A.HyperlinkOnClick { Id = LINK_ID }) { Language = "en-US" }, new A.Text("see the plan")),
            new A.Run(new A.RunProperties { Language = "en-US" }, new A.Text(" on slide ")),
            new A.Field(new A.RunProperties { Language = "en-US" }, new A.Text("1")) { Id = "{B6F15528-21DE-4FAA-801E-634DDDAF4B2B}", Type = "slidenum" },
            new A.EndParagraphRunProperties { Language = "en-US" });

        var notesPart = slidePart.AddNewPart<NotesSlidePart>();
        notesPart.NotesSlide = new P.NotesSlide(new P.CommonSlideData(new P.ShapeTree(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new A.TransformGroup()),
            new P.Shape(
                new P.NonVisualShapeProperties(
                    new P.NonVisualDrawingProperties { Id = 2U, Name = "Notes" },
                    new P.NonVisualShapeDrawingProperties(),
                    new P.ApplicationNonVisualDrawingProperties(new P.PlaceholderShape { Type = P.PlaceholderValues.Body, Index = 1U })),
                new P.ShapeProperties(),
                new P.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.Run(new A.Text("Speak slowly."))))))));
    }

    [TearDown]
    public void RemoveFolder() => Directory.Delete(this.testFolder, true);

    [Test]
    public async Task TheTextsOfSlidesAndNotesBecomeSegmentsWithTags()
    {
        var keys = (await this.format.ReadAsync(this.source, CancellationToken.None)).Select(segment => segment.Key).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(keys, Does.Contain("Mission to Mars"));
            Assert.That(keys, Does.Contain(RICH_PARAGRAPH), "Bold and a link are pairs, a line break and a field tags of their own; language and spell check make no difference.");
            Assert.That(keys, Is.SupersetOf(new[] { "Name", "Value", "Speed", "42" }), "The cells of tables are read.");
            Assert.That(keys, Does.Contain("Speak slowly."), "The notes are read.");
            Assert.That(keys, Has.None.Contains("Click"), "The layouts stay out.");
        });
    }

    [Test]
    public async Task WritingTheSourceBackChangesNothing()
    {
        var result = await this.format.WriteAsync(this.source, this.target, await this.ResultsAsync(key => key), "de-DE", NullLogger.Instance, CancellationToken.None);

        using var sourceDocument = PresentationDocument.Open(this.source, false);
        using var targetDocument = PresentationDocument.Open(this.target, false);
        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(SegmentWriteOutcome.WRITTEN));
            Assert.That(SlideOf(targetDocument).OuterXml, Is.EqualTo(SlideOf(sourceDocument).OuterXml));
        });
    }

    [Test]
    public async Task NewWordsKeepTheStructure()
    {
        var result = await this.format.WriteAsync(this.source, this.target, await this.ResultsAsync(key => key.ToUpperInvariant()), "de-DE", NullLogger.Instance, CancellationToken.None);

        using var sourceDocument = PresentationDocument.Open(this.source, false);
        using var document = PresentationDocument.Open(this.target, false);
        var slide = SlideOf(document);
        var paragraph = slide.Descendants<A.Paragraph>().Single(candidate => candidate.Descendants<A.Field>().Any());
        var runs = paragraph.Elements<A.Run>().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.EqualTo(ValidationErrorsOf(sourceDocument)), "The new presentation is as valid as the source.");
            Assert.That(result.SimplifiedCount, Is.Zero);

            Assert.That(slide.Descendants<A.Paragraph>().Select(candidate => candidate.InnerText), Is.SupersetOf(new[] { "MISSION TO MARS", "NAME", "VALUE", "SPEED", "42" }));
            Assert.That(runs.Select(run => run.Text!.Text), Is.EqualTo(new[] { "WE FLY ", "VERY", " FAR,", "SEE THE PLAN", " ON SLIDE " }));
            Assert.That(runs.Where(run => run.RunProperties?.Bold?.Value == true).Select(run => run.Text!.Text), Is.EqualTo(new[] { "VERY" }), "The word in bold stays bold.");
            Assert.That(runs.Where(run => run.RunProperties?.GetFirstChild<A.HyperlinkOnClick>()?.Id?.Value == LINK_ID).Select(run => run.Text!.Text), Is.EqualTo(new[] { "SEE THE PLAN" }), "The link keeps its words and its address.");
            Assert.That(runs.Select(run => run.RunProperties?.Language?.Value), Has.All.EqualTo("de-DE"), "New words carry their language.");
            Assert.That(runs.Select(run => run.RunProperties?.SpellingError), Has.All.Null, "New words carry no mark of an earlier spell check.");
            Assert.That(paragraph.Elements<A.Break>().Count(), Is.EqualTo(1), "The line break stays.");
            Assert.That(paragraph.Elements<A.Field>().Single().Type?.Value, Is.EqualTo("slidenum"), "The number of the slide stays a field.");
            Assert.That(paragraph.ChildElements.Last(), Is.TypeOf<A.EndParagraphRunProperties>(), "The properties of the end of the paragraph come last.");

            Assert.That(document.PresentationPart!.SlideParts.Single().NotesSlidePart!.NotesSlide!.Descendants<A.Paragraph>().Single().InnerText, Is.EqualTo("SPEAK SLOWLY."));
        });
    }

    [Test]
    public async Task TheFormattingFollowsItsWords()
    {
        var results = await this.ResultsAsync(key => key == RICH_PARAGRAPH ? "<3>Siehe den Plan</3> auf Folie <4/>:<2/>Wir fliegen <1>sehr</1> weit." : key);

        await this.format.WriteAsync(this.source, this.target, results, "de-DE", NullLogger.Instance, CancellationToken.None);

        using var document = PresentationDocument.Open(this.target, false);
        var paragraph = SlideOf(document).Descendants<A.Paragraph>().Single(candidate => candidate.Descendants<A.Field>().Any());
        Assert.Multiple(() =>
        {
            Assert.That(paragraph.ChildElements.Select(child => child.LocalName), Is.EqualTo(new[] { "r", "r", "fld", "r", "br", "r", "r", "r", "endParaRPr" }));
            Assert.That(paragraph.Elements<A.Run>().Where(run => run.RunProperties?.Bold?.Value == true).Select(run => run.Text!.Text), Is.EqualTo(new[] { "sehr" }));
            Assert.That(paragraph.Elements<A.Run>().First().RunProperties?.GetFirstChild<A.HyperlinkOnClick>(), Is.Not.Null, "The link moved to the front with its words.");
        });
    }

    /// <remarks>
    /// The format skips a paragraph whose text stays as it was, so this test hands every key to the writer itself:
    /// a writer which puts a paragraph together again from its own tags has to yield the same presentation.
    /// </remarks>
    [Test]
    public void WritingEveryKeyThroughTheWriterKeepsThePresentation()
    {
        File.Copy(this.source, this.target);
        using (var document = PresentationDocument.Open(this.target, true))
        {
            foreach (var paragraph in SlideSegmentReader.Read(document))
                Assert.That(SlideParagraphWriter.Write(paragraph, paragraph.Segment.Key, string.Empty), Is.True, $"'{paragraph.Segment.Key}' keeps its inline structure and formatting.");
        }

        using var sourceDocument = PresentationDocument.Open(this.source, false);
        using var targetDocument = PresentationDocument.Open(this.target, false);
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(targetDocument), Is.EqualTo(ValidationErrorsOf(sourceDocument)), "The new presentation is as valid as the source.");
            foreach (var (sourceRoot, targetRoot) in RootsOf(sourceDocument).Zip(RootsOf(targetDocument)))
            {
                Assert.That(StructureOf(targetRoot), Is.EqualTo(StructureOf(sourceRoot)), $"Every element of {sourceRoot.LocalName} besides the runs stays, in its order.");
                Assert.That(FormattedTextOf(targetRoot), Is.EqualTo(FormattedTextOf(sourceRoot)), $"Every word of {sourceRoot.LocalName} keeps its formatting.");
            }
        });
    }

    [Test]
    public async Task WhatTheModelDroppedFollowsItsWords()
    {
        // The model left out the line break and the number of the slide:
        var results = await this.ResultsAsync(key => key == RICH_PARAGRAPH ? "Wir fliegen <1>sehr</1> weit, <3>siehe den Plan</3> auf Folie" : key);

        var result = await this.format.WriteAsync(this.source, this.target, results, "de-DE", NullLogger.Instance, CancellationToken.None);

        using var sourceDocument = PresentationDocument.Open(this.source, false);
        using var document = PresentationDocument.Open(this.target, false);
        var paragraph = SlideOf(document).Descendants<A.Paragraph>().Single(candidate => candidate.Descendants<A.Field>().Any());
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.EqualTo(ValidationErrorsOf(sourceDocument)), "The new presentation is as valid as the source.");
            Assert.That(result.SimplifiedCount, Is.EqualTo(1));
            Assert.That(paragraph.ChildElements.Select(child => child.LocalName).TakeLast(3), Is.EqualTo(new[] { "br", "fld", "endParaRPr" }), "The line break and the field follow the words rather than getting lost.");
        });
    }

    [Test]
    public async Task TooManyChangesOfFormattingKeepTheMostCommonFormatting()
    {
        // A glossary in the title: each term in bold comes first, but most letters are those of the plain explanations:
        using (var document = PresentationDocument.Open(this.source, true))
        {
            var title = SlideOf(document).Descendants<A.Paragraph>().Single(candidate => candidate.InnerText == "Mission to Mars");
            title.RemoveAllChildren<A.Run>();
            var end = title.GetFirstChild<A.EndParagraphRunProperties>();
            foreach (var run in Enumerable.Range(0, 13).SelectMany(_ => new[]
                     {
                         new A.Run(new A.RunProperties { Language = "en-US", Bold = true }, new A.Text("Term ")),
                         new A.Run(new A.RunProperties { Language = "en-US" }, new A.Text("explained in plain words. ")),
                     }))
            {
                if (end is null)
                    title.AppendChild(run);
                else
                    title.InsertBefore(run, end);
            }
        }

        var results = await this.ResultsAsync(key => key.StartsWith("Term ", StringComparison.Ordinal) ? key.ToUpperInvariant() : key);
        var result = await this.format.WriteAsync(this.source, this.target, results, string.Empty, NullLogger.Instance, CancellationToken.None);

        using var written = PresentationDocument.Open(this.target, false);
        var paragraph = SlideOf(written).Descendants<A.Paragraph>().Single(candidate => candidate.InnerText.StartsWith("TERM EXPLAINED", StringComparison.Ordinal));
        Assert.Multiple(() =>
        {
            Assert.That(result.SimplifiedCount, Is.EqualTo(1), "Thirteen terms in bold are more pairs than a model keeps in place reliably.");
            Assert.That(paragraph.Elements<A.Run>().Where(run => run.RunProperties?.Bold?.Value == true), Is.Empty, "The new words get the formatting of most letters, not the one of the first word.");
        });
    }

    [Test]
    public async Task AChangedSourceWritesNothing()
    {
        var result = await this.format.WriteAsync(this.source, this.target, new SegmentResults(new Dictionary<string, SegmentOutcome>()), "de-DE", NullLogger.Instance, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(SegmentWriteOutcome.SOURCE_CHANGED));
            Assert.That(File.Exists(this.target), Is.False);
        });
    }

    private async Task<SegmentResults> ResultsAsync(Func<string, string> transform)
    {
        var segments = await this.format.ReadAsync(this.source, CancellationToken.None);
        return new SegmentResults(segments
            .Where(segment => segment.HasWords)
            .DistinctBy(segment => segment.Key)
            .ToDictionary(segment => segment.Key, segment => SegmentOutcome.Transformed(transform(segment.Key))));
    }

    private static P.Slide SlideOf(PresentationDocument document) => document.PresentationPart!.SlideParts.Single().Slide!;

    private static IEnumerable<OpenXmlElement> RootsOf(PresentationDocument document)
    {
        var slidePart = document.PresentationPart!.SlideParts.Single();
        yield return slidePart.Slide!;
        yield return slidePart.NotesSlidePart!.NotesSlide!;
    }

    /// <summary>
    /// The elements of a part in their order, apart from runs and their properties, which a writer may split or join.
    /// </summary>
    private static List<string> StructureOf(OpenXmlElement root) => root
        .Descendants()
        .Where(element => element is not (A.Run or A.Text or A.RunProperties) && !element.Ancestors<A.RunProperties>().Any())
        .Select(element => element.LocalName)
        .ToList();

    /// <summary>
    /// The words of a part with their formatting, joined where the formatting stays the same; language and spell check are no formatting, see SlideSegmentReader.
    /// </summary>
    private static List<string> FormattedTextOf(OpenXmlElement root)
    {
        var spans = new List<(string Format, string Text)>();
        foreach (var text in root.Descendants<A.Text>())
        {
            var format = string.Empty;
            if (text.Parent?.GetFirstChild<A.RunProperties>() is { } properties)
            {
                var copy = (A.RunProperties)properties.CloneNode(true);
                copy.Language = null;
                copy.AlternativeLanguage = null;
                copy.SpellingError = null;
                copy.Dirty = null;
                copy.NoProof = null;
                copy.SmartTagClean = null;

                // Properties which change nothing count as none, as for the reader:
                format = copy.HasAttributes || copy.HasChildren ? copy.OuterXml : string.Empty;
            }

            if (spans.Count > 0 && spans[^1].Format == format)
                spans[^1] = (format, spans[^1].Text + text.Text);
            else
                spans.Add((format, text.Text));
        }

        return spans.Select(span => $"{span.Format}: {span.Text}").ToList();
    }

    /// <summary>
    /// What the validator finds, without where it finds it: new runs move the places of what the source already had.
    /// </summary>
    private static List<string> ValidationErrorsOf(PresentationDocument document) => new OpenXmlValidator(FileFormatVersions.Office2019)
        .Validate(document)
        .Select(error => $"{error.Part?.Uri}: {error.Description}")
        .Order(StringComparer.Ordinal)
        .ToList();
}