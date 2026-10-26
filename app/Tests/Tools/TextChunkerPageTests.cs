using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools;
using AIStudio.Tools.Services;
using AIStudio.Tools.Services.Indexing;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks where a chunk says it starts and ends: its pages, its sheet and rows, or its lines.
/// </summary>
/// <remarks>
/// The user is shown where each passage the AI received is, and a range which ends too early or
/// too late sends them to the wrong place in the document. The chunker knows the locations from the
/// segments the runtime delivered, one per page, row or line, so these tests hand it such segments.
/// A fake tokenizer counts words, which is all it takes to make a chunk full.
/// </remarks>
[TestFixture]
public sealed class TextChunkerPageTests
{
    private static readonly EmbeddingProvider EMBEDDING_PROVIDER = new(1, "embedding-provider", "Embeddings", LLMProviders.SELF_HOSTED, new Model("embeddings", "embeddings"));

    private WordCountingTokenizer tokenizer = null!;
    private RustService rustService = null!;
    private TextChunker chunker = null!;

    [SetUp]
    public void PrepareChunker()
    {
        this.tokenizer = new WordCountingTokenizer();
        this.rustService = new RustService(this.tokenizer);
        this.chunker = new TextChunker(this.rustService, NullLogger.Instance);
    }

    [TearDown]
    public void DisposeChunker()
    {
        this.rustService.Dispose();
        this.tokenizer.Dispose();
    }

    [Test]
    public async Task AChunkHoldingTheWholeTextEndsOnItsLastPage()
    {
        var chunks = await this.SplitAsync(maxChunkTokenLength: 100, Page(1, "one two three"), Page(2, "four five six"), Page(3, "seven eight nine"));

        Assert.That(chunks.Select(chunk => (chunk.PageNumber, chunk.EndPageNumber)), Is.EqualTo(new (int?, int?)[] { (1, 3) }), "A short document of three pages is one chunk on all three of them.");
    }

    [Test]
    public async Task ChunksOfWholePagesEndOnTheirLastPage()
    {
        var chunks = await this.SplitAsync(maxChunkTokenLength: 9, Page(1, "one two three four"), Page(2, "five six seven eight"), Page(3, "nine ten eleven twelve"), Page(4, "thirteen fourteen fifteen sixteen"));

        Assert.That(chunks.Select(chunk => (chunk.PageNumber, chunk.EndPageNumber)), Is.EqualTo(new (int?, int?)[] { (1, 2), (3, 4) }), "Two pages fit into a chunk, so each chunk spans two of them.");
    }

    [Test]
    public async Task ChunksOfALongPageStayOnIt()
    {
        var chunks = await this.SplitAsync(maxChunkTokenLength: 4, Page(1, "one two"), Page(7, "alpha beta gamma delta. epsilon zeta eta theta. iota kappa lambda mu."));

        Assert.Multiple(() =>
        {
            Assert.That(chunks.Count(chunk => chunk.PageNumber is 7), Is.GreaterThan(1), "The page is too long for one chunk.");
            Assert.That(chunks.Where(chunk => chunk.PageNumber is 7).Select(chunk => chunk.EndPageNumber), Has.All.EqualTo(7), "A chunk cut out of a single page ends on it.");
        });
    }

    [Test]
    public async Task AChunkWithoutPagesHasNone()
    {
        var chunks = await this.SplitAsync(maxChunkTokenLength: 100, Page(null, "A text file has no pages."));

        Assert.That(chunks.Select(chunk => (chunk.PageNumber, chunk.EndPageNumber)), Is.EqualTo(new (int?, int?)[] { (null, null) }));
    }

    [Test]
    public async Task ChunksOfLinesNameTheirLines()
    {
        var chunks = await this.SplitAsync(maxChunkTokenLength: 6, Line(1, "one two three"), Line(2, "four five six"), Line(3, "seven eight nine"), Line(4, "ten eleven twelve"));

        Assert.That(chunks.Select(chunk => (chunk.Start.LineNumber, chunk.End.LineNumber)), Is.EqualTo(new (int?, int?)[] { (1, 2), (3, 4) }), "Two lines fit into a chunk, so each chunk spans two of them.");
    }

    /// <remarks>
    /// The runtime fences source code and CSV in a Markdown code block, and numbers the fence in
    /// front of the first line 0. That fence is no line of the file.
    /// </remarks>
    [Test]
    public async Task TheFenceAroundALineIsNoLineOfItsOwn()
    {
        var chunks = await this.SplitAsync(maxChunkTokenLength: 100, Line(0, "```csharp"), Line(1, "var total = 0;"), Line(2, "total++;"), Line(2, "```"));

        Assert.That(chunks.Select(chunk => (chunk.Start.LineNumber, chunk.End.LineNumber)), Is.EqualTo(new (int?, int?)[] { (1, 2) }));
    }

    [Test]
    public async Task ChunksOfALongLineStayOnIt()
    {
        var chunks = await this.SplitAsync(maxChunkTokenLength: 4, Line(1, "one two"), Line(7, "alpha beta gamma delta. epsilon zeta eta theta. iota kappa lambda mu."));

        Assert.Multiple(() =>
        {
            Assert.That(chunks.Count(chunk => chunk.Start.LineNumber is 7), Is.GreaterThan(1), "The line is too long for one chunk.");
            Assert.That(chunks.Where(chunk => chunk.Start.LineNumber is 7).Select(chunk => chunk.End.LineNumber), Has.All.EqualTo(7), "A chunk cut out of a single line ends on it.");
        });
    }

    /// <remarks>
    /// Row 0 of a sheet is its heading together with the fence in front of its table, which is no
    /// row of the sheet. Each sheet here fits into one chunk of its own.
    /// </remarks>
    [Test]
    public async Task ChunksOfRowsNameTheirSheetAndRows()
    {
        var chunks = await this.SplitAsync(maxChunkTokenLength: 8,
            Row("Budget", 0, "# Budget ```csv"), Row("Budget", 3, "rent 1200"), Row("Budget", 4, "food 400"), Row("Budget", 4, "```"),
            Row("Summary", 0, "# Summary ```csv"), Row("Summary", 1, "total 1600"), Row("Summary", 1, "```"));

        Assert.That(chunks.Select(chunk => (chunk.Start.SheetName, chunk.Start.RowNumber, chunk.End.RowNumber)), Is.EqualTo(new (string?, int?, int?)[]
        {
            ("Budget", 3, 4),
            ("Summary", 1, 1),
        }), "A sheet whose table starts further down starts at that row.");
    }

    /// <remarks>
    /// Every sheet is a table of its own, with columns of its own. Both sheets would fit into one
    /// chunk together, and with an overlap the second would start with the end of the first.
    /// </remarks>
    [Test]
    public async Task AChunkNeverRunsFromOneSheetIntoTheNext()
    {
        var chunks = await this.SplitAsync(new ChunkingOptions(100, 4),
            Row("Budget", 0, "# Budget ```csv"), Row("Budget", 1, "rent 1200"), Row("Budget", 2, "food 400"), Row("Budget", 2, "```"),
            Row("Summary", 0, "# Summary ```csv"), Row("Summary", 1, "total 1600"), Row("Summary", 1, "```"));

        Assert.Multiple(() =>
        {
            Assert.That(chunks.Select(chunk => (chunk.Start.SheetName, chunk.Start.RowNumber, chunk.End.RowNumber)), Is.EqualTo(new (string?, int?, int?)[] { ("Budget", 1, 2), ("Summary", 1, 1) }));
            Assert.That(chunks[1].Text, Does.StartWith("# Summary"), "The second sheet does not start with the overlap of the first.");
        });
    }

    [Test]
    public async Task ASheetWithoutRowsMakesNoChunk()
    {
        var chunks = await this.SplitAsync(maxChunkTokenLength: 100,
            Row("Empty", 0, "# Empty ```csv"), Row("Empty", 0, "```"),
            Row("Budget", 0, "# Budget ```csv"), Row("Budget", 1, "rent 1200"), Row("Budget", 1, "```"));

        Assert.That(chunks.Select(chunk => chunk.Start.SheetName), Is.EqualTo(new[] { "Budget" }), "A sheet with nothing but its heading and its fences holds nothing to find.");
    }

    private Task<List<EmbeddingChunk>> SplitAsync(int maxChunkTokenLength, params TextSegment[] segments) => this.SplitAsync(new ChunkingOptions(maxChunkTokenLength, 0), segments);

    private async Task<List<EmbeddingChunk>> SplitAsync(ChunkingOptions options, params TextSegment[] segments)
    {
        var text = new SegmentedText(string.Join("\n", segments.Select(segment => segment.Text)), segments);
        var chunks = new List<EmbeddingChunk>();
        await foreach (var chunk in this.chunker.SplitAsync(text, TextChunker.DOCUMENT_STRATEGY, options, EMBEDDING_PROVIDER, CancellationToken.None))
            chunks.Add(chunk);

        return chunks;
    }

    private static TextSegment Page(int? pageNumber, string text) => new(text, WordCountingTokenizer.Count(text), SourceLocation.OnPage(pageNumber));

    private static TextSegment Line(int lineNumber, string text) => new(text, WordCountingTokenizer.Count(text), SourceLocation.OnLine(lineNumber));

    private static TextSegment Row(string sheetName, int rowNumber, string text) => new(text, WordCountingTokenizer.Count(text), SourceLocation.InSheet(sheetName, rowNumber));
}