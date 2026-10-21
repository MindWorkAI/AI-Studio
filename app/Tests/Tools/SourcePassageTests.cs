using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Tools;
using AIStudio.Tools.RAG;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how the passages of an answer are kept, and how they find their way to their sources.
/// </summary>
/// <remarks>
/// Below an answer, the user opens a source to read what the AI received from it. The list of
/// sources keeps one entry per document, whichever page its link names, so the passages have to
/// be matched the very same way: a passage found under no source can never be read, and one found
/// under the wrong source tells the user something false. Nothing here asserts on the wording of a
/// location: I18N is process-wide state without a reset, so an Init somewhere else would decide
/// whether these pass.
/// </remarks>
[TestFixture]
public sealed class SourcePassageTests
{
    private static readonly string HANDBOOK_URL = new Uri(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "handbook.pdf"))).AbsoluteUri;
    private static readonly string MANUAL_URL = new Uri(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "manual.pdf"))).AbsoluteUri;

    [Test]
    public void TheSameChunkFoundTwiceIsKeptOnce()
    {
        List<SourcePassage> passages = [Passage($"{HANDBOOK_URL}#page=3", "The mixing console.")];

        passages.MergePassages([Passage($"{HANDBOOK_URL}#page=3", "The mixing console."), Passage($"{HANDBOOK_URL}#page=3", "The amplifier.")]);

        Assert.That(passages.Select(passage => passage.Text), Is.EqualTo(new[] { "The mixing console.", "The amplifier." }), "Two searches for one answer may find the same chunk, but the AI read it once.");
    }

    [Test]
    public void TheSameTextOnAnotherPageIsAnotherPassage()
    {
        List<SourcePassage> passages = [Passage($"{HANDBOOK_URL}#page=3", "Safety notes.")];

        passages.MergePassages([Passage($"{HANDBOOK_URL}#page=12", "Safety notes.")]);

        Assert.That(passages.Select(passage => passage.SourceUrl), Is.EqualTo(new[] { $"{HANDBOOK_URL}#page=3", $"{HANDBOOK_URL}#page=12" }), "A heading repeated on every page is a passage of each of them.");
    }

    [Test]
    public void ASourceShowsThePassagesOfAllPagesOfItsDocument()
    {
        SourcePassage[] passages =
        [
            Passage($"{HANDBOOK_URL}#page=3", "The mixing console."),
            Passage($"{MANUAL_URL}#page=3", "The amplifier."),
            Passage($"{HANDBOOK_URL}#page=12", "The equalizer."),
        ];

        var found = passages.GetPassagesOf(new Source("handbook.pdf (Page 12)", $"{HANDBOOK_URL}#page=12", SourceOrigin.RAG));

        Assert.That(found.Select(passage => passage.Text), Is.EqualTo(new[] { "The mixing console.", "The equalizer." }), "The list keeps one entry per document, so that entry stands for all of its pages, and for nothing else.");
    }

    [Test]
    public void AWebSourceFindsItsPassagesHoweverItsAddressIsWritten()
    {
        SourcePassage[] passages = [Passage("https://example.org/wiki/mixing-console#history", "The first console.")];

        var found = passages.GetPassagesOf(new Source("Wiki", "https://Example.ORG/wiki/mixing-console", SourceOrigin.RAG));

        Assert.That(found, Has.Count.EqualTo(1), "The list of sources tells this address apart from the other one in no way.");
    }

    [Test]
    public void EveryPassageShowsUpUnderTheSourceTheListKept()
    {
        SourcePassage[] passages =
        [
            Passage($"{HANDBOOK_URL}#page=3", "The mixing console."),
            Passage($"{HANDBOOK_URL}#page=12", "The equalizer."),
            Passage($"{MANUAL_URL}#page=3", "The amplifier."),
            Passage("https://example.org/wiki/mixing-console#history", "The first console."),
            Passage("https://Example.ORG/wiki/mixing-console", "The modern console."),
        ];

        //
        // The sources are listed as an answer lists them, i.e., merged. The first link of a
        // document wins, so the list names some page of it -- the passages must not care which:
        //
        var sources = new List<Source>();
        sources.MergeSources(passages.Select(passage => new Source(passage.Text, passage.SourceUrl, SourceOrigin.RAG)));

        var shown = sources.SelectMany(source => passages.GetPassagesOf(source)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(sources, Has.Count.EqualTo(3), "Three documents, so three sources.");
            Assert.That(shown, Is.EquivalentTo(passages), "Each passage shows up under exactly one source.");
        });
    }

    [Test]
    public void APassageNamesItsPageOrItsPages()
    {
        var onePage = Passage(HANDBOOK_URL, "Text.", pageNumber: 4).GetLocationLabel();
        var twoPages = Passage(HANDBOOK_URL, "Text.", pageNumber: 4, endPageNumber: 5).GetLocationLabel();
        var endingWhereItStarts = Passage(HANDBOOK_URL, "Text.", pageNumber: 4, endPageNumber: 4).GetLocationLabel();

        Assert.Multiple(() =>
        {
            Assert.That(onePage, Does.Contain("4").And.Not.Contain("5"));
            Assert.That(twoPages, Does.Contain("4").And.Contain("5"));
            Assert.That(twoPages, Is.Not.EqualTo(onePage));
            Assert.That(endingWhereItStarts, Is.EqualTo(onePage), "A passage which ends on the page it starts on is on one page.");
            Assert.That(Passage(HANDBOOK_URL, "Text.").GetLocationLabel(), Is.Empty, "A slide or a sheet has no page, and none is invented for it.");
        });
    }

    [Test]
    public void SeveralPassagesNameEachOfTheirPagesOnceInTheOrderOfTheDocument()
    {
        var label = new[]
        {
            Passage(HANDBOOK_URL, "Results.", pageNumber: 11),
            Passage(HANDBOOK_URL, "Introduction.", pageNumber: 1),
            Passage(HANDBOOK_URL, "Discussion.", pageNumber: 11),
            Passage(HANDBOOK_URL, "Method.", pageNumber: 4, endPageNumber: 5),
            Passage(HANDBOOK_URL, "A slide."),
        }.GetLocationLabel();

        Assert.Multiple(() =>
        {
            Assert.That(label, Does.Contain("1, 4–5, 11"), "Each page once, ranges as ranges, in the order of the document; a passage without a page adds nothing.");
            Assert.That(new[] { Passage(HANDBOOK_URL, "A.", pageNumber: 3), Passage(HANDBOOK_URL, "B.", pageNumber: 3) }.GetLocationLabel(), Is.EqualTo(Passage(HANDBOOK_URL, "A.", pageNumber: 3).GetLocationLabel()), "Two passages on one page name one page.");
            Assert.That(new[] { Passage(HANDBOOK_URL, "A slide."), Passage(HANDBOOK_URL, "A sheet.") }.GetLocationLabel(), Is.Empty);
        });
    }

    [Test]
    public void TheTitleOfASourceNamesThePagesOfAllItsPassages()
    {
        IList<Source> sources =
        [
            new("handbook.pdf (Page 1)", $"{HANDBOOK_URL}#page=1", SourceOrigin.RAG),
            new("Search result", "https://example.org/search", SourceOrigin.TOOL),
        ];

        SourcePassage[] passages = [Passage($"{HANDBOOK_URL}#page=1", "Introduction.", pageNumber: 1), Passage($"{HANDBOOK_URL}#page=11", "Results.", pageNumber: 11)];
        sources.NamePassagesInTitles(passages);

        Assert.Multiple(() =>
        {
            Assert.That(sources[0].Title, Is.EqualTo($"Handbooks ({passages.GetLocationLabel()})"), "The page of the second passage would otherwise go unmentioned.");
            Assert.That(sources[0].Title, Does.Contain("1, 11"));
            Assert.That(sources[0].URL, Is.EqualTo($"{HANDBOOK_URL}#page=1"), "The link still opens the document where the first passage is.");
            Assert.That(sources[1].Title, Is.EqualTo("Search result"), "A source without passages keeps its title.");
        });
    }

    [Test]
    public void PassagesWithoutAPageAreCountedOnceThereAreSeveral()
    {
        IList<Source> oneChunk = [new("manual.txt (Chunk 3)", $"{MANUAL_URL}#chunk=2", SourceOrigin.RAG)];
        IList<Source> twoChunks = [new("manual.txt (Chunk 3)", $"{MANUAL_URL}#chunk=2", SourceOrigin.RAG)];

        oneChunk.NamePassagesInTitles([Passage($"{MANUAL_URL}#chunk=2", "Step one.")]);
        twoChunks.NamePassagesInTitles([Passage($"{MANUAL_URL}#chunk=2", "Step one."), Passage($"{MANUAL_URL}#chunk=7", "Step two.")]);

        Assert.Multiple(() =>
        {
            Assert.That(oneChunk[0].Title, Is.EqualTo("manual.txt (Chunk 3)"), "One passage without a page keeps the title its data source gave it.");
            Assert.That(twoChunks[0].Title, Does.StartWith("Handbooks (").And.Contain("2"), "Two passages under a title naming one chunk would tell the user something false.");
        });
    }

    [Test]
    public void ATranscriptIsKnownAsSuch()
    {
        Assert.Multiple(() =>
        {
            Assert.That((Passage(HANDBOOK_URL, "Welcome.") with { Type = RetrievalContentType.TEXT_TRANSCRIPT }).IsTranscript(), Is.True);
            Assert.That(Passage(HANDBOOK_URL, "Welcome.").IsTranscript(), Is.False);
        });
    }

    [Test]
    public void ThePassagesAreStoredWithTheAnswer()
    {
        var passage = Passage($"{HANDBOOK_URL}#page=3", "The mixing console.", pageNumber: 3) with { SurroundingContent = ["Before.", "After."] };
        var thread = new ChatThread
        {
            ChatId = Guid.NewGuid(),
            Blocks =
            [
                new ContentBlock
                {
                    ContentType = ContentType.TEXT,
                    Role = ChatRole.AI,
                    Content = new ContentText { Text = "The console has 24 channels.", SourcePassages = [passage] },
                },
            ],
        };

        var json = JsonSerializer.Serialize(thread, WorkspaceBehaviour.JSON_OPTIONS);
        var loaded = (ContentText)JsonSerializer.Deserialize<ChatThread>(json, WorkspaceBehaviour.JSON_OPTIONS)!.Blocks.Single().Content!;
        using var document = JsonDocument.Parse(json);
        var storedPassage = document.RootElement.GetProperty("blocks")[0].GetProperty("content").GetProperty("source_passages")[0];

        Assert.Multiple(() =>
        {
            Assert.That(storedPassage.EnumerateObject().Select(property => property.Name), Is.EquivalentTo(new[] { "source_url", "data_source_name", "path", "category", "type", "text", "surrounding_content", "page_number", "end_page_number" }), "A computed property would end up in every chat file as well.");
            Assert.That(storedPassage.GetProperty("type").GetString(), Is.EqualTo("TEXT_DOCUMENT"), "The type is stored by name, so a type added later does not shift the stored ones.");

            var loadedPassage = loaded.SourcePassages.Single();
            Assert.That(loadedPassage.SourceUrl, Is.EqualTo(passage.SourceUrl));
            Assert.That(loadedPassage.Text, Is.EqualTo(passage.Text));
            Assert.That(loadedPassage.SurroundingContent, Is.EqualTo(passage.SurroundingContent));
            Assert.That(loadedPassage.PageNumber, Is.EqualTo(3));
            Assert.That(loadedPassage.Type, Is.EqualTo(RetrievalContentType.TEXT_DOCUMENT));
        });
    }

    [Test]
    public void AnAnswerStoredWithoutPassagesStillLoads()
    {
        var content = JsonSerializer.Deserialize<ContentText>("""{ "text": "An answer from before passages were kept." }""", WorkspaceBehaviour.JSON_OPTIONS)!;

        Assert.That(content.SourcePassages, Is.Empty);
    }

    [Test]
    public void ACopiedAnswerKeepsItsPassages()
    {
        var original = new ContentText { Text = "The console has 24 channels.", SourcePassages = [Passage(HANDBOOK_URL, "The mixing console.")] };

        var copy = (ContentText)original.DeepClone();
        original.SourcePassages.Clear();

        Assert.That(copy.SourcePassages.Select(passage => passage.Text), Is.EqualTo(new[] { "The mixing console." }), "A copy which shares the list with its original loses its passages along with it.");
    }

    private static SourcePassage Passage(string sourceUrl, string text, int? pageNumber = null, int? endPageNumber = null) => new()
    {
        SourceUrl = sourceUrl,
        DataSourceName = "Handbooks",
        Path = sourceUrl,
        Category = RetrievalContentCategory.TEXT,
        Type = RetrievalContentType.TEXT_DOCUMENT,
        Text = text,
        PageNumber = pageNumber,
        EndPageNumber = endPageNumber,
    };
}