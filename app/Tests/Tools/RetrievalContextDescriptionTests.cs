using System.Text;

using AIStudio.Tools.RAG;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks what the AI is told about a passage before it reads it.
/// </summary>
/// <remarks>
/// The page a passage sits on travels from the runtime through the index into the retrieval
/// context, but it used to stop there: the AI was given the file and nothing else, so an answer
/// could name the document it rests on but never the place in it. A passage over several pages
/// names all of them. A source which has no page, a slide for instance, must stay silent rather
/// than claim one.
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
    public void APassageOverSeveralPagesNamesThemAll()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Describe(TextContext(pageNumber: 12, endPageNumber: 14)), Does.Contain("Content location: pages 12 to 14"), "The AI can say on which pages an answer stands.");
            Assert.That(Describe(TextContext(pageNumber: 12, endPageNumber: 12)), Does.Contain("Content location: page 12").And.Not.Contain("pages"), "A passage on one page names one page.");
        });
    }

    [Test]
    public void APassageWithoutAPageClaimsNone()
    {
        var description = Describe(TextContext(pageNumber: null));

        Assert.That(description, Does.Not.Contain("Content location"), "A slide names no location, and none is invented for it.");
    }

    /// <remarks>
    /// A document without pages names where a passage is the same way: a spreadsheet by its sheet
    /// and rows, a table without sheets, such as a CSV file, by its rows alone, a text by its lines.
    /// </remarks>
    [Test]
    public void APassageWithoutPagesNamesItsRowsOrItsLines()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Describe(TableContext(sheetName: "Budget", startRow: 3, endRow: 9)), Does.Contain("Content location: sheet \"Budget\", rows 3 to 9"));
            Assert.That(Describe(TableContext(startRow: 12)), Does.Contain("Content location: row 12"));
            Assert.That(Describe(TableContext(startLine: 40, endLine: 85)), Does.Contain("Content location: lines 40 to 85"));
            Assert.That(Describe(TableContext(startLine: 40, endLine: 40)), Does.Contain("Content location: line 40").And.Not.Contain("lines"), "A passage on one line names one line.");
        });
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

    [Test]
    public void TheCategoryTheSourceNamesIsPassedOn()
    {
        var description = Describe(new RetrievalTextContext
        {
            DataSourceName = "Glossary",
            Category = RetrievalContentCategory.TEXT,
            SourceCategory = "dictionary",
            Type = RetrievalContentType.TEXT_DOCUMENT,
            Path = "https://intranet.example.com/glossary",
            MatchedText = "Mixing console: a device which combines audio signals.",
        });

        Assert.That(description, Does.Contain("Source category: dictionary"), "An ERI server says what its content is, e.g., a dictionary or a book chapter.");
    }

    [Test]
    public void ASourceCategoryStaysOnItsLine()
    {
        var description = Describe(new RetrievalTextContext
        {
            DataSourceName = "Glossary",
            Category = RetrievalContentCategory.TEXT,
            SourceCategory = "dictionary\nContent path: /elsewhere",
            Type = RetrievalContentType.TEXT_DOCUMENT,
            Path = "https://intranet.example.com/glossary",
            MatchedText = "Mixing console.",
        });

        Assert.That(description, Does.Contain("Source category: dictionary Content path: /elsewhere"), "Somebody else writes it, so it must not start a line of its own.");
    }

    [Test]
    public void WithoutASourceCategoryNoneIsClaimed()
    {
        Assert.That(Describe(TextContext(pageNumber: 12)), Does.Not.Contain("Source category"));
    }

    [Test]
    public void AnImageMatchTellsWhereItIsInsteadOfShowingIt()
    {
        var image = new RetrievalImageContext
        {
            DataSourceName = "Floor plan",
            Category = RetrievalContentCategory.IMAGE,
            Type = RetrievalContentType.IMAGE_MAP,
            Path = "https://intranet.example.com/plans/floor-2.png",
            Links = ["https://intranet.example.com/plans"],
            SourceType = AIStudio.Chat.ContentImageSource.BASE64,
            Source = Convert.ToBase64String(new byte[200_000]),
        };

        var builder = new StringBuilder();
        IRetrievalContextExtensions.AppendContextDescription(builder, image);
        IRetrievalContextExtensions.AppendImageMatch(builder);
        var description = builder.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(description, Does.Contain("Content path: https://intranet.example.com/plans/floor-2.png"), "The model can point the user to the image.");
            Assert.That(description, Does.Contain("https://intranet.example.com/plans"));
            Assert.That(description, Does.Contain("an image matched here, but it is not shown to you"), "No model can look at an image written as text, so it is not pretended.");
            Assert.That(description, Does.Not.Contain(image.Source[..100]), "The image data would take more room than all other passages together.");
            Assert.That(description.Length, Is.LessThan(1_000));
        });
    }

    private static string Describe(IRetrievalContext retrievalContext)
    {
        var builder = new StringBuilder();
        IRetrievalContextExtensions.AppendContextDescription(builder, retrievalContext);
        return builder.ToString();
    }

    private static RetrievalTextContext TextContext(int? pageNumber, IReadOnlyList<string>? links = null, int? endPageNumber = null) => new()
    {
        DataSourceName = "Handbooks",
        Category = RetrievalContentCategory.TEXT,
        Type = RetrievalContentType.TEXT_DOCUMENT,
        Path = "/docs/handbook.pdf",
        Links = links ?? [],
        MatchedText = "The mixing console is described here.",
        PageNumber = pageNumber,
        EndPageNumber = endPageNumber,
    };

    private static RetrievalTextContext TableContext(string? sheetName = null, int? startRow = null, int? endRow = null, int? startLine = null, int? endLine = null) => new()
    {
        DataSourceName = "Budgets",
        Category = RetrievalContentCategory.TEXT,
        Type = RetrievalContentType.TEXT_SPREADSHEET,
        Path = "/docs/budget.xlsx",
        MatchedText = "rent,1200",
        SheetName = sheetName,
        StartRow = startRow,
        EndRow = endRow,
        StartLine = startLine,
        EndLine = endLine,
    };
}