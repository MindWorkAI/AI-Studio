using System.Text;

using AIStudio.Tools.RAG;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks what the AI is told about a passage before it reads it.
/// </summary>
/// <remarks>
/// The page a passage sits on travels from the runtime through the index into the retrieval
/// context, but it used to stop there: the AI was given the file and nothing else, so an answer
/// could name the document it rests on but never the place in it. A source which has no page, a
/// slide for instance, must stay silent rather than claim one.
/// </remarks>
[TestFixture]
public sealed class RetrievalContextDescriptionTests
{
    [Test]
    public void AKnownPageIsPartOfWhatTheAIIsTold()
    {
        var description = Describe(TextContext(pageNumber: 12));

        Assert.That(description, Does.Contain("Content location: page 12"), "The AI is told the page, so it can say where an answer comes from.");
    }

    [Test]
    public void APassageWithoutAPageClaimsNone()
    {
        var description = Describe(TextContext(pageNumber: null));

        Assert.That(description, Does.Not.Contain("Content location"), "A slide or a sheet has no page, and none is invented for it.");
    }

    /// <remarks>
    /// The location belongs to the document, so it is stated with it and before the passage itself
    /// follows further down.
    /// </remarks>
    [Test]
    public void ThePageIsStatedWithTheDocumentItBelongsTo()
    {
        var description = Describe(TextContext(pageNumber: 12));
        var lines = description.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();

        Assert.That(lines, Is.EqualTo(new[]
        {
            "Data source name: Handbooks",
            "Content category: TEXT",
            "Content type: TEXT_DOCUMENT",
            "Content path: /docs/handbook.pdf",
            "Content location: page 12",
        }), "Name, kind, path and place of the document, in that order.");
    }

    [Test]
    public void AdditionalLinksStillFollowTheLocation()
    {
        var description = Describe(TextContext(pageNumber: 12, links: ["https://example.com/handbook"]));

        Assert.Multiple(() =>
        {
            Assert.That(description, Does.Contain("Additional links:"), "The links a data source delivers are still passed on.");
            Assert.That(description.IndexOf("Content location", StringComparison.Ordinal), Is.LessThan(description.IndexOf("Additional links", StringComparison.Ordinal)), "The place inside the document is stated before links pointing elsewhere.");
        });
    }

    private static string Describe(IRetrievalContext retrievalContext)
    {
        var builder = new StringBuilder();
        IRetrievalContextExtensions.AppendContextDescription(builder, retrievalContext);
        return builder.ToString();
    }

    private static RetrievalTextContext TextContext(int? pageNumber, IReadOnlyList<string>? links = null) => new()
    {
        DataSourceName = "Handbooks",
        Category = RetrievalContentCategory.TEXT,
        Type = RetrievalContentType.TEXT_DOCUMENT,
        Path = "/docs/handbook.pdf",
        Links = links ?? [],
        MatchedText = "The mixing console is described here.",
        PageNumber = pageNumber,
    };
}