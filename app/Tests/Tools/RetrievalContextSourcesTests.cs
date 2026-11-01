using AIStudio.Tools;
using AIStudio.Tools.RAG;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which sources a retrieved passage lends to the answer.
/// </summary>
/// <remarks>
/// The sources below an answer are links the user opens, and they travel into every export. The
/// classic RAG process and Semantic Search both take them from here, so a passage has to name the
/// same sources whichever of the two found it. A source has to open where the passage is, and
/// nothing may become a link which does not lead anywhere sensible. What names nothing to open
/// still becomes a source, named as text, since the user reads under it what the AI received.
/// </remarks>
[TestFixture]
public sealed class RetrievalContextSourcesTests
{
    [Test]
    public void APassageNamesItsOwnReferenceFirst()
    {
        var sources = TextContext(
            path: AbsolutePath("handbook.pdf"),
            referenceTitle: "handbook.pdf (Page 12)",
            referenceLink: $"{new Uri(AbsolutePath("handbook.pdf")).AbsoluteUri}#page=12").ToSources();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sources, Has.Count.EqualTo(1));
            Assert.That(sources[0].Title, Is.EqualTo("handbook.pdf (Page 12)"));
            Assert.That(sources[0].URL, Does.EndWith("#page=12"), "The page is what opens the document where the passage is.");
            Assert.That(sources[0].Origin, Is.EqualTo(SourceOrigin.RAG));
        }
    }

    [Test]
    public void WithoutAReferenceTheDataSourceAndThePathAreNamed()
    {
        var path = AbsolutePath("handbook.pdf");

        var sources = TextContext(path: path).ToSources();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sources, Has.Count.EqualTo(1));
            Assert.That(sources[0].Title, Is.EqualTo("Handbooks"));
            Assert.That(sources[0].URL, Is.EqualTo(new Uri(path).AbsoluteUri), "A file becomes a link which opens it.");
        }
    }

    [Test]
    public void ARelativePathBecomesASourceWithoutLink()
    {
        var sources = TextContext(path: Path.Combine("docs", "handbook.pdf")).ToSources();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sources, Has.Count.EqualTo(1), "The AI received the passage, so it needs a source to be read at.");
            Assert.That(sources[0].Title, Is.EqualTo("Handbooks"));
            Assert.That(sources[0].HasNoLink(), Is.True, "A relative path would point somewhere else depending on where it is opened from.");
            Assert.That(sources[0].TryGetDocumentLocation(out _), Is.False);
        }
    }

    [Test]
    public void APathOnlyTheDataSourceKnowsBecomesASourceWithoutLink()
    {
        var sources = TextContext(path: "db://collection/42").ToSources();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sources, Has.Count.EqualTo(1));
            Assert.That(sources[0].HasNoLink(), Is.True, "An ERI server may name its content in a way no program here can open.");
        }
    }

    [Test]
    public void ThePassagesOfOneDocumentGatherUnderOneSource()
    {
        List<Source> sources = [..TextContext(path: "db://collection/42").ToSources()];

        sources.MergeSources(TextContext(path: "db://collection/42").ToSources());
        sources.MergeSources(TextContext(path: "db://collection/43").ToSources());

        Assert.That(sources, Has.Count.EqualTo(2), "The same document is one source; another document is another.");
    }

    [Test]
    public void AContextWithoutAnyNameGetsNoSource()
    {
        var context = TextContext(path: "db://collection/42", dataSourceName: string.Empty);

        Assert.That(context.ToSources(), Is.Empty, "A source without a name would be an entry nobody can tell apart.");
    }

    [Test]
    public void OnlyLinksWhichCanBeOpenedBecomeSources()
    {
        var sources = TextContext(path: string.Empty, links: ["javascript:alert(1)", "mailto:team@example.org", "https://example.org/wiki/mixing-console"]).ToSources();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sources.Select(source => source.URL), Is.EqualTo(new[] { "https://example.org/wiki/mixing-console" }), "An ERI server decides which links it sends, and a script is no source.");
            Assert.That(sources[0].Title, Is.EqualTo("Handbooks"));
        }
    }

    private static string AbsolutePath(string fileName) => Path.GetFullPath(Path.Combine(Path.GetTempPath(), fileName));

    private static RetrievalTextContext TextContext(string path, string referenceTitle = "", string referenceLink = "", IReadOnlyList<string>? links = null, string dataSourceName = "Handbooks") => new()
    {
        DataSourceName = dataSourceName,
        Category = RetrievalContentCategory.TEXT,
        Type = RetrievalContentType.TEXT_DOCUMENT,
        Path = path,
        Links = links ?? [],
        MatchedText = "The mixing console is described here.",
        ReferenceTitle = referenceTitle,
        ReferenceLink = referenceLink,
    };
}