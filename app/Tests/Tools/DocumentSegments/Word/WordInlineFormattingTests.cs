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

    [Test]
    public async Task TooManyChangesOfFormattingKeepTheMostCommonFormatting()
    {
        // A glossary: each term in bold comes first, but most letters are those of the plain explanations:
        var runs = Enumerable.Range(0, 13).SelectMany(_ => new[] { Bold("Term "), Run("explained in plain words. ") }).ToArray<DocumentFormat.OpenXml.OpenXmlElement>();
        Create(this.source, Paragraph(runs));
        var key = string.Concat(Enumerable.Repeat("Term explained in plain words. ", 13));

        var result = await this.WriteAsync(key, key.ToUpperInvariant());

        using var document = WordprocessingDocument.Open(this.target, false);
        var paragraph = document.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.SimplifiedCount, Is.EqualTo(1));
            Assert.That(paragraph.InnerText, Is.EqualTo(key.ToUpperInvariant()));
            Assert.That(TextsWhere(paragraph, run => run.RunProperties?.Bold is not null), Is.Empty, "The new words get the formatting of most letters, not the one of the first word.");
        });
    }

    [Test]
    public async Task ATagMovedIntoAnInsertionGoesRightBehindIt()
    {
        // A linked tab has no words, so it is a tag of its own, which the model may move; Word allows no link within an insertion:
        var linkedTab = new W.Hyperlink(new W.Run(new W.TabChar())) { Id = LINK_ID };
        Create(this.source, Paragraph(linkedTab, Run(" text "), new W.InsertedRun(Run("inserted")) { Id = "1", Author = "Tester" }));
        var key = (await this.KeysAsync()).Single();

        var result = await this.WriteAsync(key, "Text <2>eingefügt <1/>und mehr</2>");

        using var document = WordprocessingDocument.Open(this.target, false);
        var paragraph = document.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(key, Is.EqualTo("<1/> text <2>inserted</2>"));
            Assert.That(ValidationErrorsOf(document), Is.EqualTo(this.SourceValidationErrors()), "The new document is as valid as the source.");
            Assert.That(result.SimplifiedCount, Is.Zero, "Nothing got lost, the link only moved next to the insertion.");
            Assert.That(paragraph.ChildElements.Select(child => child.LocalName), Is.EqualTo(new[] { "r", "ins", "hyperlink", "ins" }), "The insertion goes on behind the link.");
            Assert.That(paragraph.Elements<W.InsertedRun>().Select(insertion => insertion.InnerText), Is.EqualTo(new[] { "eingefügt ", "und mehr" }));
        });
    }

    [Test]
    public async Task ANestingWhichWordForbidsKeepsTheWords()
    {
        Create(this.source, Paragraph(Run("See "), new W.Hyperlink(new W.InsertedRun(Run("link")) { Id = "1", Author = "Tester" }) { Id = LINK_ID }));
        var key = (await this.KeysAsync()).Single();

        // The model swapped the pairs, so that the link would lie within the insertion:
        var result = await this.WriteAsync(key, "Siehe <2><1>Link</1></2>");

        using var document = WordprocessingDocument.Open(this.target, false);
        var paragraph = document.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(key, Is.EqualTo("See <1><2>link</2></1>"));
            Assert.That(ValidationErrorsOf(document), Is.EqualTo(this.SourceValidationErrors()), "The new document is as valid as the source.");
            Assert.That(result.SimplifiedCount, Is.EqualTo(1));
            Assert.That(paragraph.InnerText, Is.EqualTo("Siehe Link"), "Words beat structure.");
            Assert.That(paragraph.Descendants<W.InsertedRun>().Single().InnerText, Is.EqualTo("Link"));
        });
    }

    [Test]
    public async Task ASimpleFieldIsAPairAroundItsResult()
    {
        Create(this.source, Paragraph(Run("See "), new W.SimpleField(Run("Figure 1")) { Instruction = " REF _Ref1 \\h " }, Run(".")));
        var key = (await this.KeysAsync()).Single();

        var result = await this.WriteAsync(key, "Siehe <1>Abbildung 1</1>.");

        using var document = WordprocessingDocument.Open(this.target, false);
        var field = document.MainDocumentPart!.Document!.Body!.Descendants<W.SimpleField>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(key, Is.EqualTo("See <1>Figure 1</1>."));
            Assert.That(ValidationErrorsOf(document), Is.EqualTo(this.SourceValidationErrors()), "The new document is as valid as the source.");
            Assert.That(result.SimplifiedCount, Is.Zero);
            Assert.That(field.InnerText, Is.EqualTo("Abbildung 1"));
            Assert.That(field.Instruction?.Value, Is.EqualTo(" REF _Ref1 \\h "), "The field keeps its instruction.");
        });
    }

    [Test]
    public async Task CustomXmlKeepsItsPropertiesOnce()
    {
        var customXml = new W.CustomXmlRun(new W.CustomXmlProperties(new W.CustomXmlAttribute { Name = "role", Val = "author" }), Run("Alice")) { Element = "name" };
        Create(this.source, Paragraph(customXml, Run(" wrote this.")));
        var key = (await this.KeysAsync()).Single();

        await this.WriteAsync(key, "<1>Alicia</1> schrieb das.");

        using var document = WordprocessingDocument.Open(this.target, false);
        var written = document.MainDocumentPart!.Document!.Body!.Descendants<W.CustomXmlRun>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(key, Is.EqualTo("<1>Alice</1> wrote this."), "The properties of custom XML are no tag of their own.");
            Assert.That(ValidationErrorsOf(document), Is.EqualTo(this.SourceValidationErrors()), "The new document is as valid as the source.");
            Assert.That(written.Elements<W.CustomXmlProperties>().Count(), Is.EqualTo(1));
            Assert.That(written.FirstChild, Is.InstanceOf<W.CustomXmlProperties>(), "The properties come first, as the schema demands.");
        });
    }

    [Test]
    public async Task ACaptionBookmarkEnclosesTheNewLabelAndNumber()
    {
        // A caption as Word writes it: a cross reference points to the bookmark around its label and number:
        Create(this.source, Paragraph(
            new W.BookmarkStart { Id = "0", Name = "_Ref1" },
            Run("Figure "),
            new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Begin }),
            new W.Run(new W.FieldCode(" SEQ Figure \\* ARABIC ") { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }),
            new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }),
            Run("1"),
            new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.End }),
            new W.BookmarkEnd { Id = "0" },
            Run(": Overview of the system")));
        var key = (await this.KeysAsync()).Single();

        var result = await this.WriteAsync(key, "<1>Abbildung <2>1</2></1>: Übersicht über das System");

        using var document = WordprocessingDocument.Open(this.target, false);
        var paragraph = document.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().Single();
        var enclosed = paragraph.ChildElements.SkipWhile(child => child is not W.BookmarkStart).Skip(1).TakeWhile(child => child is not W.BookmarkEnd);
        Assert.Multiple(() =>
        {
            Assert.That(key, Is.EqualTo("<1>Figure <2>1</2></1>: Overview of the system"));
            Assert.That(ValidationErrorsOf(document), Is.EqualTo(this.SourceValidationErrors()), "The new document is as valid as the source.");
            Assert.That(result.SimplifiedCount, Is.Zero);
            Assert.That(string.Concat(enclosed.SelectMany(child => child.Descendants<W.Text>()).Select(text => text.Text)), Is.EqualTo("Abbildung 1"), "A cross reference shows the new label and number, not the whole caption.");
        });
    }

    [Test]
    public async Task ABookmarkWithoutWordsIsNoTag()
    {
        Create(this.source, Paragraph(new W.BookmarkStart { Id = "1", Name = "_GoBack" }, new W.BookmarkEnd { Id = "1" }, Run("Some text.")));

        Assert.That(await this.KeysAsync(), Is.EqualTo(new[] { "Some text." }));
    }

    [Test]
    public async Task OfTwoCrossingBookmarksTheFirstIsAPair()
    {
        Create(this.source, Paragraph(
            new W.BookmarkStart { Id = "0", Name = "First" },
            Run("one "),
            new W.BookmarkStart { Id = "1", Name = "Second" },
            Run("two"),
            new W.BookmarkEnd { Id = "0" },
            Run(" three"),
            new W.BookmarkEnd { Id = "1" }));
        var key = (await this.KeysAsync()).Single();

        await this.WriteAsync(key, key.ToUpperInvariant());

        using var document = WordprocessingDocument.Open(this.target, false);
        Assert.Multiple(() =>
        {
            Assert.That(key, Is.EqualTo("<1>one two</1> three"), "Pairs have to nest, so the second bookmark widens to the paragraph.");
            Assert.That(ValidationErrorsOf(document), Is.EqualTo(this.SourceValidationErrors()), "The new document is as valid as the source.");
            Assert.That(document.MainDocumentPart!.Document!.Body!.InnerText, Is.EqualTo("ONE TWO THREE"));
        });
    }

    private List<string> SourceValidationErrors()
    {
        using var document = WordprocessingDocument.Open(this.source, false);
        return ValidationErrorsOf(document);
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