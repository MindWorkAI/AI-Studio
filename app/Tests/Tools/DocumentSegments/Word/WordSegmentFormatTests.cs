using AIStudio.Tools.DocumentSegments;
using AIStudio.Tools.DocumentSegments.Word;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

using Microsoft.Extensions.Logging.Abstractions;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tests.Tools.DocumentSegments.Word;

/// <summary>
/// Checks which parts of a Word document a model works on, and that everything else stays as it is.
/// </summary>
[TestFixture]
public sealed class WordSegmentFormatTests
{
    private const string LINK_PARAGRAPH = "Read the <1>manual</1> first.<2/>Done";

    private readonly WordSegmentFormat format = new();
    private string testFolder = string.Empty;
    private string source = string.Empty;
    private string target = string.Empty;

    [SetUp]
    public void CreateDocument()
    {
        this.testFolder = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "mindwork-ai-studio-tests", Guid.NewGuid().ToString("N"))).FullName;
        this.source = Path.Join(this.testFolder, "source.docx");
        this.target = Path.Join(this.testFolder, "target.docx");
        TestWordDocument.CreateSample(this.source);
    }

    [TearDown]
    public void RemoveFolder() => Directory.Delete(this.testFolder, true);

    [Test]
    public async Task TheParagraphsBecomeSegmentsWithTags()
    {
        var keys = (await this.format.ReadAsync(this.source, CancellationToken.None)).Select(segment => segment.Key).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(keys, Does.Contain("Getting started"));
            Assert.That(keys, Does.Contain(LINK_PARAGRAPH), "A link is a pair, a tab a tag of its own.");
            Assert.That(keys, Does.Contain("See the note<1/>"), "The reference to a footnote is a tag of its own.");
            Assert.That(keys, Does.Contain("<1>Introduction</1>"), "The result of a field is words within a pair.");
            Assert.That(keys, Does.Contain("Chapter"), "Bookmarks are no tags.");
            Assert.That(keys, Does.Contain("Mixed <1>bold</1> words"), "A word in bold is a pair.");
            Assert.That(keys, Does.Contain("Confidential"), "Headers are read.");
            Assert.That(keys, Does.Contain("<1/> A note."), "Footnotes are read, without their separators.");
        });
    }

    [Test]
    public async Task WritingTheSourceBackChangesNothing()
    {
        var result = await this.format.WriteAsync(this.source, this.target, await this.ResultsAsync(key => key), "de-DE", NullLogger.Instance, CancellationToken.None);

        using var sourceDocument = WordprocessingDocument.Open(this.source, false);
        using var targetDocument = WordprocessingDocument.Open(this.target, false);
        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(SegmentWriteOutcome.WRITTEN));
            Assert.That(BodyOf(targetDocument).OuterXml, Is.EqualTo(BodyOf(sourceDocument).OuterXml));
        });
    }

    [Test]
    public async Task NewWordsKeepTheStructure()
    {
        var result = await this.format.WriteAsync(this.source, this.target, await this.ResultsAsync(key => key.ToUpperInvariant()), "de-DE", NullLogger.Instance, CancellationToken.None);

        using var sourceDocument = WordprocessingDocument.Open(this.source, false);
        using var document = WordprocessingDocument.Open(this.target, false);
        var paragraphs = BodyOf(document).Elements<W.Paragraph>().ToList();
        var link = paragraphs[1].Descendants<W.Hyperlink>().SingleOrDefault();
        Assert.Multiple(() =>
        {
            Assert.That(ValidationErrorsOf(document), Is.EqualTo(ValidationErrorsOf(sourceDocument)), "The new document is as valid as the source.");
            Assert.That(result.SimplifiedCount, Is.Zero, "Every paragraph keeps its inline structure and formatting.");

            Assert.That(paragraphs[0].InnerText, Is.EqualTo("GETTING STARTED"));
            Assert.That(link?.InnerText, Is.EqualTo("MANUAL"), "The link encloses its new words.");
            Assert.That(link?.Id?.Value, Is.EqualTo("rIdManual"), "The link keeps its address.");
            Assert.That(paragraphs[1].Descendants<W.TabChar>().Count(), Is.EqualTo(1), "The tab stays.");
            Assert.That(paragraphs[1].InnerText, Is.EqualTo("READ THE MANUAL FIRST.DONE"));

            Assert.That(paragraphs[2].Descendants<W.FootnoteReference>().Select(reference => reference.Id?.Value), Is.EqualTo(new long?[] { 1 }), "The reference to the footnote stays.");

            Assert.That(paragraphs[3].Descendants<W.FieldChar>().Select(fieldChar => fieldChar.FieldCharType?.Value), Is.EqualTo(new[] { W.FieldCharValues.Begin, W.FieldCharValues.Separate, W.FieldCharValues.End }), "The field keeps its parts in their order.");
            Assert.That(paragraphs[3].Descendants<W.Text>().Select(text => text.Text), Is.EqualTo(new[] { "INTRODUCTION" }), "The new words are the result of the field.");
            Assert.That(paragraphs[3].Descendants<W.FieldCode>().Single().Text, Is.EqualTo(" REF _Ref1 \\h "), "The instruction of the field stays.");

            Assert.That(paragraphs[4].ChildElements.First(), Is.TypeOf<W.BookmarkStart>(), "The bookmark starts before the new words.");
            Assert.That(paragraphs[4].ChildElements.Last(), Is.TypeOf<W.BookmarkEnd>(), "The bookmark ends after the new words.");
            Assert.That(paragraphs[4].InnerText, Is.EqualTo("CHAPTER"));

            Assert.That(paragraphs[5].InnerText, Is.EqualTo("MIXED BOLD WORDS"));
            Assert.That(paragraphs[5].Elements<W.Run>().Where(run => run.RunProperties?.Bold is not null).Select(run => run.InnerText), Is.EqualTo(new[] { "BOLD" }), "The word in bold stays bold.");
            Assert.That(paragraphs[6].InnerText, Is.EqualTo("2026"));

            Assert.That(document.MainDocumentPart!.HeaderParts.Single().Header!.InnerText, Is.EqualTo("CONFIDENTIAL"));
            Assert.That(document.MainDocumentPart.FootnotesPart!.Footnotes!.Elements<W.Footnote>().Last().InnerText, Is.EqualTo(" A NOTE."));
            Assert.That(link?.Descendants<W.Languages>().SingleOrDefault()?.Val?.Value, Is.EqualTo("de-DE"), "New words carry their language.");
        });
    }

    [Test]
    public async Task WhatTheModelDroppedFollowsItsWords()
    {
        var results = await this.ResultsAsync(key => key == LINK_PARAGRAPH ? "Lies zuerst das Handbuch. Fertig" : key);

        var result = await this.format.WriteAsync(this.source, this.target, results, "de-DE", NullLogger.Instance, CancellationToken.None);

        using var document = WordprocessingDocument.Open(this.target, false);
        var paragraph = BodyOf(document).Elements<W.Paragraph>().ElementAt(1);
        Assert.Multiple(() =>
        {
            Assert.That(result.SimplifiedCount, Is.EqualTo(1));
            Assert.That(paragraph.InnerText, Is.EqualTo("Lies zuerst das Handbuch. Fertig"));
            Assert.That(paragraph.Descendants<W.Hyperlink>(), Is.Empty, "A link whose tags the model dropped goes.");
            Assert.That(paragraph.Elements<W.Run>().Last().GetFirstChild<W.TabChar>(), Is.Not.Null, "The tab follows the words rather than getting lost.");
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

    private static W.Body BodyOf(WordprocessingDocument document) => document.MainDocumentPart!.Document!.Body!;

    /// <summary>
    /// What the validator finds, without where it finds it: new runs move the places of what the source already had.
    /// </summary>
    private static List<string> ValidationErrorsOf(WordprocessingDocument document) => new OpenXmlValidator(FileFormatVersions.Office2019)
        .Validate(document)
        .Select(error => $"{error.Part?.Uri}: {error.Description}")
        .Order(StringComparer.Ordinal)
        .ToList();
}