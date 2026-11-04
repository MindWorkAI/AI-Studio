using AIStudio.Tools.Word;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tests.Tools.Word;

/// <summary>
/// Checks the estimate of how much room paragraphs take up, and of how many of them fill a page.
/// </summary>
/// <remarks>
/// The styles of the tests: "Normal" with 10 pt and single spacing, "Exact" with lines of an exact
/// height and some space around each paragraph, and "Heading", which keeps with the next paragraph.
/// </remarks>
[TestFixture]
public sealed class WordLayoutEstimateTests
{
    private const string NORMAL_STYLE_ID = "Normal";
    private const string EXACT_STYLE_ID = "Exact";
    private const string HEADING_STYLE_ID = "Heading";

    /// <summary>
    /// The font size of "Normal", in half points.
    /// </summary>
    private const int FONT_SIZE = 20;

    private const int EXACT_LINE = 300;
    private const int SPACE_BEFORE = 100;
    private const int SPACE_AFTER = 50;

    /// <summary>
    /// The height of a paragraph of "Exact" with one line.
    /// </summary>
    private const int EXACT_PARAGRAPH = SPACE_BEFORE + EXACT_LINE + SPACE_AFTER;

    private const int WIDTH = 6000;

    private MemoryStream stream = null!;
    private WordprocessingDocument document = null!;
    private WordLayoutEstimate estimate = null!;

    [SetUp]
    public void CreateEstimate()
    {
        this.stream = new MemoryStream();
        this.document = WordprocessingDocument.Create(this.stream, WordprocessingDocumentType.Document);
        var mainPart = this.document.AddMainDocumentPart();
        mainPart.Document = new W.Document(new W.Body());
        var styles = new W.Styles(
            new W.Style
            {
                Type = W.StyleValues.Paragraph,
                StyleId = NORMAL_STYLE_ID,
                Default = true,
                StyleName = new W.StyleName { Val = "Normal" },
                StyleParagraphProperties = new W.StyleParagraphProperties { SpacingBetweenLines = new W.SpacingBetweenLines { After = "0", Line = "240", LineRule = W.LineSpacingRuleValues.Auto } },
                StyleRunProperties = new W.StyleRunProperties { FontSize = new W.FontSize { Val = FONT_SIZE.ToString() } },
            },
            new W.Style
            {
                Type = W.StyleValues.Paragraph,
                StyleId = EXACT_STYLE_ID,
                StyleName = new W.StyleName { Val = "Exact" },
                BasedOn = new W.BasedOn { Val = NORMAL_STYLE_ID },
                StyleParagraphProperties = new W.StyleParagraphProperties { SpacingBetweenLines = new W.SpacingBetweenLines { Before = SPACE_BEFORE.ToString(), After = SPACE_AFTER.ToString(), Line = EXACT_LINE.ToString(), LineRule = W.LineSpacingRuleValues.Exact } },
            },
            new W.Style
            {
                Type = W.StyleValues.Paragraph,
                StyleId = HEADING_STYLE_ID,
                StyleName = new W.StyleName { Val = "heading 1" },
                BasedOn = new W.BasedOn { Val = EXACT_STYLE_ID },
                StyleParagraphProperties = new W.StyleParagraphProperties { KeepNext = new W.KeepNext() },
            });

        this.estimate = new WordLayoutEstimate(styles, new WordNumbering(mainPart));
    }

    [TearDown]
    public void DisposeDocument()
    {
        this.document.Dispose();
        this.stream.Dispose();
    }

    [Test]
    public void AParagraphTakesItsLinesAndTheSpaceAroundIt()
    {
        var paragraph = ParagraphOf(EXACT_STYLE_ID, new W.Text("Mars"), new W.Break(), new W.Text("Phobos"));
        Assert.That(this.estimate.HeightOf(paragraph, WIDTH), Is.EqualTo(SPACE_BEFORE + 2 * EXACT_LINE + SPACE_AFTER), "The break in the line starts a second one.");
    }

    [Test]
    public void ALongTextTakesSeveralLines()
    {
        const int CHARACTERS = 150;
        var lines = (int)Math.Ceiling(CHARACTERS * FONT_SIZE * 10 * WordLayoutEstimate.CHARACTER_WIDTH / WIDTH);
        var paragraph = ParagraphOf(NORMAL_STYLE_ID, new W.Text(new string('x', CHARACTERS)));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(lines, Is.GreaterThan(1));
            Assert.That(this.estimate.HeightOf(paragraph, WIDTH), Is.EqualTo((int)(lines * FONT_SIZE * 10 * WordLayoutEstimate.LINE_HEIGHT)), "Single spacing takes a little more than the font size per line.");
        }
    }

    [Test]
    public void AHeadingDoesNotEndThePage()
    {
        var blocks = Enumerable.Range(0, 9).Select(_ => ParagraphOf(EXACT_STYLE_ID, new W.Text("Mars")))
            .Append(ParagraphOf(HEADING_STYLE_ID, new W.Text("Moons")))
            .Append(ParagraphOf(EXACT_STYLE_ID, new W.Text("Phobos")))
            .Append(ParagraphOf(EXACT_STYLE_ID, new W.Text("Deimos")))
            .ToList<OpenXmlElement>();

        // Room for ten paragraphs, the heading included:
        Assert.That(this.estimate.CountFittingOnFirstPage(blocks, AreaFor(10 * EXACT_PARAGRAPH)), Is.EqualTo(9), "The heading goes to the next page, together with the paragraph after it.");
    }

    [Test]
    public void TheFirstBlockOfTheTextIsOnThePageEvenWhenItDoesNotFit()
    {
        var hugeParagraph = ParagraphOf(EXACT_STYLE_ID, Enumerable.Range(0, 100).SelectMany(_ => new OpenXmlElement[] { new W.Text("Mars"), new W.Break() }).ToArray());
        var blocks = new List<OpenXmlElement>
        {
            new W.Paragraph(),
            ParagraphOf(HEADING_STYLE_ID, new W.Text("Moons")),
            hugeParagraph,
            ParagraphOf(EXACT_STYLE_ID, new W.Text("Phobos")),
        };

        Assert.That(this.estimate.CountFittingOnFirstPage(blocks, AreaFor(3 * EXACT_PARAGRAPH)), Is.EqualTo(3), "Otherwise the page would stay empty, apart from an empty paragraph and a heading.");
    }

    [Test]
    public void AllBlocksWhichFitAreOnThePage()
    {
        var blocks = Enumerable.Range(0, 3).Select(_ => ParagraphOf(EXACT_STYLE_ID, new W.Text("Mars"))).ToList<OpenXmlElement>();
        Assert.That(this.estimate.CountFittingOnFirstPage(blocks, AreaFor(10 * EXACT_PARAGRAPH)), Is.EqualTo(3));
    }

    private static W.Paragraph ParagraphOf(string styleId, params OpenXmlElement[] content) => new(
        new W.ParagraphProperties { ParagraphStyleId = new W.ParagraphStyleId { Val = styleId } },
        new W.Run(content));

    /// <summary>
    /// A page with a column of the given room for text, once the estimate leaves its share of it free, and of WIDTH.
    /// </summary>
    private static WordTextArea AreaFor(int room)
    {
        const int MARGIN = 1000;
        var height = (int)Math.Ceiling(room / WordLayoutEstimate.FILL);
        return new WordTextArea(WIDTH + 2 * MARGIN, height + 2 * MARGIN, MARGIN, MARGIN, MARGIN, MARGIN, 0, 1, 720);
    }
}