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
/// nothing may become a link which does not lead anywhere sensible.
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

        Assert.Multiple(() =>
        {
            Assert.That(sources, Has.Count.EqualTo(1));
            Assert.That(sources[0].Title, Is.EqualTo("handbook.pdf (Page 12)"));
            Assert.That(sources[0].URL, Does.EndWith("#page=12"), "The page is what opens the document where the passage is.");
            Assert.That(sources[0].Origin, Is.EqualTo(SourceOrigin.RAG));
        });
    }

    [Test]
    public void WithoutAReferenceTheDataSourceAndThePathAreNamed()
    {
        var path = AbsolutePath("handbook.pdf");

        var sources = TextContext(path: path).ToSources();

        Assert.Multiple(() =>
        {
            Assert.That(sources, Has.Count.EqualTo(1));
            Assert.That(sources[0].Title, Is.EqualTo("Handbooks"));
            Assert.That(sources[0].URL, Is.EqualTo(new Uri(path).AbsoluteUri), "A file becomes a link which opens it.");
        });
    }

    [Test]
    public void ARelativePathBecomesNoSource()
    {
        var sources = TextContext(path: Path.Combine("docs", "handbook.pdf")).ToSources();

        Assert.That(sources, Is.Empty, "A relative path would point somewhere else depending on where it is opened from.");
    }

    [Test]
    public void OnlyLinksWhichCanBeOpenedBecomeSources()
    {
        var sources = TextContext(path: string.Empty, links: ["javascript:alert(1)", "mailto:team@example.org", "https://example.org/wiki/mixing-console"]).ToSources();

        Assert.Multiple(() =>
        {
            Assert.That(sources.Select(source => source.URL), Is.EqualTo(new[] { "https://example.org/wiki/mixing-console" }), "An ERI server decides which links it sends, and a script is no source.");
            Assert.That(sources[0].Title, Is.EqualTo("Handbooks"));
        });
    }

    private static string AbsolutePath(string fileName) => Path.GetFullPath(Path.Combine(Path.GetTempPath(), fileName));

    private static RetrievalTextContext TextContext(string path, string referenceTitle = "", string referenceLink = "", IReadOnlyList<string>? links = null) => new()
    {
        DataSourceName = "Handbooks",
        Category = RetrievalContentCategory.TEXT,
        Type = RetrievalContentType.TEXT_DOCUMENT,
        Path = path,
        Links = links ?? [],
        MatchedText = "The mixing console is described here.",
        ReferenceTitle = referenceTitle,
        ReferenceLink = referenceLink,
    };
}