using AIStudio.Tools.DocumentSegments;
using AIStudio.Tools.DocumentSegments.Word;

using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging.Abstractions;

using W = DocumentFormat.OpenXml.Wordprocessing;

using static AIStudio.Tests.Tools.DocumentSegments.Word.TestWordDocument;

namespace AIStudio.Tests.Tools.DocumentSegments.Word;

/// <summary>
/// Checks that words in bold, in italics, or in a link in the middle of a sentence keep their formatting when the
/// model moves them.
/// </summary>
[TestFixture]
public sealed class WordInlineFormattingTests
{
    private const string SENTENCE = "This is <1>very</1> <2>important</2> and <3>linked</3> text.";

    private readonly WordSegmentFormat format = new();
    private string testFolder = string.Empty;
    private string source = string.Empty;
    private string target = string.Empty;

    [SetUp]
    public void CreateFolder()
    {
        this.testFolder = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "mindwork-ai-studio-tests", Guid.NewGuid().ToString("N"))).FullName;
        this.source = Path.Join(this.testFolder, "source.docx");
        this.target = Path.Join(this.testFolder, "target.docx");
    }

    [TearDown]
    public void RemoveFolder() => Directory.Delete(this.testFolder, true);

    [Test]
    public async Task ChangesOfFormattingBecomePairs()
    {
        this.CreateSentence();

        var keys = await this.KeysAsync();

        Assert.That(keys, Is.EqualTo(new[] { SENTENCE }), "Words in another formatting than most words of the paragraph go into pairs, like the link.");
    }

    [Test]
    public async Task TheFormattingFollowsItsWords()
    {
        this.CreateSentence();

        var result = await this.WriteAsync(SENTENCE, "<2>Wichtig</2> ist es <1>sehr</1>, und <3>verlinkt</3> auch.");

        using var document = WordprocessingDocument.Open(this.target, false);
        var paragraph = document.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.SimplifiedCount, Is.Zero);
            Assert.That(paragraph.InnerText, Is.EqualTo("Wichtig ist es sehr, und verlinkt auch."));
            Assert.That(TextsWhere(paragraph, run => run.RunProperties?.Bold is not null), Is.EqualTo(new[] { "sehr" }));
            Assert.That(TextsWhere(paragraph, run => run.RunProperties?.Italic is not null), Is.EqualTo(new[] { "Wichtig" }));
            Assert.That(paragraph.Elements<W.Hyperlink>().Select(link => link.InnerText), Is.EqualTo(new[] { "verlinkt" }));
            Assert.That(TextsWhere(paragraph, run => run.RunProperties?.Bold is null && run.RunProperties?.Italic is null), Is.EqualTo(new[] { " ist es ", ", und ", " auch." }), "The words around keep the most common formatting.");
        });
    }

    [Test]
    public async Task AMissingPairLosesItsFormattingButNotItsWords()
    {
        this.CreateSentence();

        var result = await this.WriteAsync(SENTENCE, "Das ist sehr <2>wichtig</2> und <3>verlinkter</3> Text.");

        using var document = WordprocessingDocument.Open(this.target, false);
        var paragraph = document.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.SimplifiedCount, Is.EqualTo(1));
            Assert.That(paragraph.InnerText, Is.EqualTo("Das ist sehr wichtig und verlinkter Text."));
            Assert.That(TextsWhere(paragraph, run => run.RunProperties?.Bold is not null), Is.Empty);
            Assert.That(TextsWhere(paragraph, run => run.RunProperties?.Italic is not null), Is.EqualTo(new[] { "wichtig" }));
        });
    }

    [Test]
    public async Task ASpaceGetsNoPairOfItsOwn()
    {
        Create(this.source, Paragraph(Run("Spaces"), Bold(" "), Run("between")));

        Assert.That(await this.KeysAsync(), Is.EqualTo(new[] { "Spaces between" }));
    }

    [Test]
    public async Task TheMostCommonFormattingNeedsNoPair()
    {
        Create(this.source, Paragraph(Bold("Mostly bold words "), Run("and"), Bold(" more bold words")));

        Assert.That(await this.KeysAsync(), Is.EqualTo(new[] { "Mostly bold words <1>and</1> more bold words" }));
    }

    [Test]
    public async Task TooManyChangesOfFormattingKeepOneFormatting()
    {
        var runs = Enumerable.Range(0, 13).SelectMany(_ => new[] { Run("plain "), Bold("bold ") }).ToArray<DocumentFormat.OpenXml.OpenXmlElement>();
        Create(this.source, Paragraph(runs));
        var key = string.Concat(Enumerable.Repeat("plain bold ", 13));

        var keys = await this.KeysAsync();
        var result = await this.WriteAsync(key, key.ToUpperInvariant());

        Assert.Multiple(() =>
        {
            Assert.That(keys, Is.EqualTo(new[] { key }), "Thirteen words in bold are more pairs than a model keeps in place reliably.");
            Assert.That(result.SimplifiedCount, Is.EqualTo(1), "The paragraph keeps one formatting and counts as simplified.");
        });
    }

    private void CreateSentence() => Create(this.source, Paragraph(Run("This is "), Bold("very"), Run(" "), Italic("important"), Run(" and "), Link("linked"), Run(" text.")));

    private async Task<List<string>> KeysAsync() => (await this.format.ReadAsync(this.source, CancellationToken.None)).Select(segment => segment.Key).ToList();

    private Task<SegmentWriteResult> WriteAsync(string key, string newText)
    {
        var results = new SegmentResults(new Dictionary<string, SegmentOutcome> { [key] = SegmentOutcome.Transformed(newText) });
        return this.format.WriteAsync(this.source, this.target, results, string.Empty, NullLogger.Instance, CancellationToken.None);
    }

    private static List<string> TextsWhere(W.Paragraph paragraph, Func<W.Run, bool> condition) => paragraph
        .Elements<W.Run>()
        .Where(condition)
        .Select(run => run.InnerText)
        .ToList();
}