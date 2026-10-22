using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Services;
using AIStudio.Tools.Services.Indexing;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which pages a chunk says it spans.
/// </summary>
/// <remarks>
/// The user is shown the pages of each passage the AI received, and a page range which ends too
/// early or too late sends them to the wrong place in the document. The chunker knows the pages
/// from the segments the runtime delivered, one per page, so these tests hand it such segments.
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

    private async Task<List<EmbeddingChunk>> SplitAsync(int maxChunkTokenLength, params TextSegment[] segments)
    {
        var text = new SegmentedText(string.Join("\n", segments.Select(segment => segment.Text)), segments);
        var chunks = new List<EmbeddingChunk>();
        await foreach (var chunk in this.chunker.SplitAsync(text, TextChunker.DOCUMENT_STRATEGY, new ChunkingOptions(maxChunkTokenLength, 0), EMBEDDING_PROVIDER, CancellationToken.None))
            chunks.Add(chunk);

        return chunks;
    }

    private static TextSegment Page(int? pageNumber, string text) => new(text, WordCountingTokenizer.Count(text), pageNumber);

    /// <summary>
    /// Plays the tokenizer of the runtime: one token per word.
    /// </summary>
    private sealed class WordCountingTokenizer : HttpMessageHandler
    {
        public static int Count(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var text = body.RootElement.GetProperty("text").GetString() ?? string.Empty;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { success = true, token_count = Count(text), message = string.Empty }) };
        }
    }
}