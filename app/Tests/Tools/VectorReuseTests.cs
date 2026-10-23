using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tests.Tools.Databases;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Security;
using AIStudio.Tools.Services;
using AIStudio.Tools.Services.Indexing;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that a document read again keeps the vectors of its chunks whose text did not change.
/// </summary>
/// <remarks>
/// Every chunk sent to the embedding provider costs time, and at a cloud provider money. A file
/// which was only touched, or changed near its end, yields the same texts as before for most of its
/// chunks, and the vectors of those texts are already stored.
/// </remarks>
[TestFixture]
public sealed class VectorReuseTests
{
    private const string DATA_SOURCE_ID = "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11";
    private const string EMBEDDING_ID = "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01";
    private const string PATH = "/tmp/test-data/handbook.md";

    [Test]
    public async Task AnUnchangedDocumentIsNotEmbeddedAgain()
    {
        await using var run = await IndexRun.CreateAsync();
        await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.", "Chapter three.");
        var firstVectors = run.Vectors.Points.Values.OrderBy(point => point.ChunkIndex).Select(point => point.Vector).ToList();
        run.Provider.EmbeddedTexts.Clear();

        // Touched only: a new fingerprint, and so new chunk ids, but the same texts:
        await run.IndexAsync("fingerprint-2", "Chapter one.", "Chapter two.", "Chapter three.");

        Assert.Multiple(() =>
        {
            Assert.That(run.Provider.EmbeddedTexts, Is.Empty, "Every text has its vector stored already.");
            Assert.That(run.Vectors.Points.Values.OrderBy(point => point.ChunkIndex).Select(point => point.Vector), Is.EqualTo(firstVectors), "Each chunk keeps the vector of its text.");
            Assert.That(run.Vectors.Points.Values.Select(point => point.Fingerprint), Is.All.EqualTo("fingerprint-2"), "The old points are gone, only the new ones remain.");
        });
    }

    [Test]
    public async Task OnlyTheChangedChunkIsEmbedded()
    {
        await using var run = await IndexRun.CreateAsync();
        await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.", "Chapter three.");
        run.Provider.EmbeddedTexts.Clear();

        await run.IndexAsync("fingerprint-2", "Chapter one.", "Chapter two, revised.", "Chapter three.");

        Assert.That(run.Provider.EmbeddedTexts, Is.EqualTo(new[] { "Chapter two, revised." }));
    }

    /// <remarks>
    /// A mail keeps its fingerprint when it is read again. Its new chunks still get ids of their
    /// own, as the next generation, so none of them overwrites an old point, and a text which moved
    /// to another chunk still finds its vector there.
    /// </remarks>
    [Test]
    public async Task ADocumentReadAgainUnderTheSameFingerprintGetsNewIdsAndKeepsItsVectors()
    {
        await using var run = await IndexRun.CreateAsync();
        await run.IndexAsync("mail-hash", "The body of the mail.", "Attachment: offer.pdf");
        var oldPointIds = run.Vectors.Points.Keys.ToList();
        run.Provider.EmbeddedTexts.Clear();

        await run.IndexAsync("mail-hash", "Attachment: offer.pdf", "The body of the mail.");

        var points = run.Vectors.Points.Values.OrderBy(point => point.ChunkIndex).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(run.Provider.EmbeddedTexts, Is.Empty);
            Assert.That(points.Select(point => point.Vector), Is.EqualTo(new[] { CountingProvider.VectorOf("Attachment: offer.pdf"), CountingProvider.VectorOf("The body of the mail.") }), "Each text keeps its own vector.");
            Assert.That(points.Select(point => point.PointId), Has.None.AnyOf(oldPointIds.ToArray()), "The new chunks have ids of their own, and the old points are gone.");
        });
    }

    [Test]
    public async Task EachIndexingOfADocumentIsANewGeneration()
    {
        await using var run = await IndexRun.CreateAsync();
        await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.");
        var firstGeneration = await run.Store.Client.GetStoredChunksAsync(DATA_SOURCE_ID, PATH, CancellationToken.None);

        await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.");
        await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.");
        var thirdGeneration = await run.Store.Client.GetStoredChunksAsync(DATA_SOURCE_ID, PATH, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(firstGeneration.Select(chunk => chunk.Generation), Is.All.Zero, "A document stored for the first time gets the ids documents got before generations existed.");
            Assert.That(thirdGeneration.Select(chunk => chunk.Generation), Is.All.EqualTo(2));
            Assert.That(thirdGeneration.Select(chunk => chunk.TextHash), Is.EquivalentTo(new[] { ChunkTextHash.Of("Chapter one."), ChunkTextHash.Of("Chapter two.") }), "Every chunk stores the hash of its text.");
            Assert.That(run.Vectors.Points.Keys, Is.EquivalentTo(thirdGeneration.Select(chunk => chunk.ChunkId)), "Only the points of the last generation remain.");
        });
    }

    /// <remarks>
    /// Only the ids and hashes of the stored chunks are held in memory. Each batch reads just the
    /// vectors it can use, so a document of any size needs no more than one batch of them at a time,
    /// and a document changed at its start, where little can be reused, reads hardly any.
    /// </remarks>
    [Test]
    public async Task OnlyTheVectorsOfUnchangedTextsAreRead()
    {
        await using var run = await IndexRun.CreateAsync();
        await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.", "Chapter three.");
        run.Provider.EmbeddedTexts.Clear();

        await run.IndexAsync("fingerprint-2", "Chapter one, revised.", "Chapter two.", "Chapter three, revised.");

        Assert.Multiple(() =>
        {
            Assert.That(run.Vectors.RetrieveRequests.SelectMany(pointIds => pointIds), Is.EqualTo(new[] { IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, "fingerprint-1", 1) }), "Only the vector of the second chapter, whose text stayed, is read.");
            Assert.That(run.Provider.EmbeddedTexts, Is.EqualTo(new[] { "Chapter one, revised.", "Chapter three, revised." }));
        });
    }

    /// <remarks>
    /// The old vectors go only once every new chunk is stored, so a chunk late in the document can
    /// still take over a vector.
    /// </remarks>
    [Test]
    public async Task TheOldVectorsStayUntilTheNewChunksAreStored()
    {
        await using var run = await IndexRun.CreateAsync();
        await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.");
        var oldPointIds = run.Vectors.Points.Keys.ToList();
        run.Provider.FailOn = "Chapter two, revised.";

        Assert.ThrowsAsync<InvalidOperationException>(() => run.IndexAsync("fingerprint-2", "Chapter one.", "Chapter two, revised."));
        Assert.That(run.Vectors.Points.Keys, Is.SupersetOf(oldPointIds), "A run which fails half way has deleted none of the old vectors yet.");
    }

    /// <remarks>
    /// A run which was interrupted leaves points behind whose rows are gone. Once the document is
    /// stored again, every point of it which the run did not write goes, those included.
    /// </remarks>
    [Test]
    public async Task APointAnInterruptedRunLeftBehindGoes()
    {
        await using var run = await IndexRun.CreateAsync();
        await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.");
        await run.Store.Client.DeleteFileAsync(DATA_SOURCE_ID, PATH, CancellationToken.None);

        await run.IndexAsync("fingerprint-2", "Chapter one.", "Chapter two.");

        Assert.That(run.Vectors.Points.Values.Select(point => point.Fingerprint), Is.All.EqualTo("fingerprint-2"));
    }

    /// <summary>
    /// One data source with one document, indexed through IndexedRunContext as the app does it, into
    /// a real index store and a vector store in memory.
    /// </summary>
    private sealed class IndexRun(TemporaryIndexStore store) : IAsyncDisposable
    {
        private readonly DataSourceEmbeddingManifest manifest = new();

        private readonly EmbeddingProvider embeddingProvider = new(1, EMBEDDING_ID, "Test embeddings", LLMProviders.OPEN_AI, new("text-embedding-3-small", "text-embedding-3-small"));

        private readonly DataSourceLocalDirectory dataSource = new()
        {
            Num = 1,
            Id = DATA_SOURCE_ID,
            Name = "Test data",
            Type = DataSourceType.LOCAL_DIRECTORY,
            EmbeddingId = EMBEDDING_ID,
            ConfidenceLevel = ConfidenceLevel.LOW,
            Path = "/tmp/test-data",
        };

        public TemporaryIndexStore Store => store;

        public MemoryVectorStore Vectors { get; } = new();

        public CountingProvider Provider { get; } = new();

        public static async Task<IndexRun> CreateAsync()
        {
            var store = await TemporaryIndexStore.CreateAsync();
            await store.Client.UpsertDataSourceAsync(DATA_SOURCE_ID, "LOCAL_DIRECTORY", EMBEDDING_ID, "signature", string.Empty, 0, CancellationToken.None);
            return new IndexRun(store);
        }

        /// <summary>
        /// Indexes the document with the given chunks, in place of whatever it had stored.
        /// </summary>
        /// <param name="fingerprint">The fingerprint of the document, which its chunk ids derive from.</param>
        /// <param name="chunkTexts">The texts of its chunks.</param>
        public async Task IndexAsync(string fingerprint, params string[] chunkTexts)
        {
            var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
            var context = new IndexedRunContext(
                this.dataSource,
                this.embeddingProvider,
                this.Provider,
                this.Vectors,
                store.Client,
                this.manifest,
                new RunShare(RunShare.DEFAULT_DURATION),
                new IndexingPass(new DataSourceRunSignature("TEST-EMBEDDING-SIGNATURE", string.Empty)),
                new PromptInjectionGuardService(null!, settingsManager, NullLogger<PromptInjectionGuardService>.Instance, NullLoggerFactory.Instance).BeginAction(),
                settingsManager,
                _ => { },
                NullLogger.Instance);

            var now = DateTimeOffset.UtcNow;
            var document = new EmbeddingDocument(
                PATH,
                new EmbeddingStateFile(IndexedDocumentIds.CreateParentId(DATA_SOURCE_ID, PATH), PATH, "handbook.md", "handbook.md", "md", fingerprint, 64, now, now, now, 0),
                "handbook.md",
                _ => Chunks(chunkTexts));

            await context.IndexDocumentAsync(document, (_, _) => { }, CancellationToken.None);
        }

        public ValueTask DisposeAsync() => store.DisposeAsync();

        private static async IAsyncEnumerable<EmbeddingChunk> Chunks(IEnumerable<string> texts)
        {
            await Task.CompletedTask;
            foreach (var text in texts)
                yield return new EmbeddingChunk(text, null, null);
        }
    }

    /// <summary>
    /// An embedding provider which notes every text it is asked to embed, and answers with a vector made from the text.
    /// </summary>
    private sealed class CountingProvider : NoProvider, IProvider
    {
        public List<string> EmbeddedTexts { get; } = [];

        /// <summary>
        /// A text the provider refuses to embed, as a provider which is down would.
        /// </summary>
        public string? FailOn { get; set; }

        // Different for every text of these tests, so a vector which went to the wrong chunk shows:
        public static IReadOnlyList<float> VectorOf(string text) => Convert.FromHexString(ChunkTextHash.Of(text)[..8]).Select(value => (float)value).ToList();

        Task<IReadOnlyList<IReadOnlyList<float>>> IProvider.EmbedTextAsync(Model embeddingModel, SettingsManager settingsManager, CancellationToken token, params List<string> texts)
        {
            if (this.FailOn is not null && texts.Contains(this.FailOn))
                throw new HttpRequestException("The embedding provider is not reachable.");

            this.EmbeddedTexts.AddRange(texts);
            return Task.FromResult<IReadOnlyList<IReadOnlyList<float>>>(texts.Select(VectorOf).ToList());
        }
    }
}