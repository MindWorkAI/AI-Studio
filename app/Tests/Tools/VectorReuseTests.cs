using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tests.Tools.Databases;
using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Security;
using AIStudio.Tools.Services;
using AIStudio.Tools.Services.Indexing;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks what a document read again keeps: the vectors of its chunks whose text did not change,
/// and its whole stored version until the new one is complete.
/// </summary>
/// <remarks>
/// Every chunk sent to the embedding provider costs time, and at a cloud provider money. A file
/// which was only touched, or changed near its end, yields the same texts as before for most of its
/// chunks, and the vectors of those texts are already stored. An attempt which fails must not cost
/// the document the version it had, nor make the next attempt embed it all again.
/// </remarks>
[TestFixture]
public sealed class VectorReuseTests
{
    private const string DATA_SOURCE_ID = "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11";
    private const string EMBEDDING_ID = "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01";
    private const string PATH = "/tmp/test-data/handbook.md";

    private static readonly string PARENT_ID = IndexedDocumentIds.CreateParentId(DATA_SOURCE_ID, PATH);

    [Test]
    public async Task AnUnchangedDocumentIsNotEmbeddedAgain()
    {
        await using var run = await IndexRun.CreateAsync();
        await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.", "Chapter three.");
        var firstVectors = run.Vectors.Points.Values.OrderBy(point => point.ChunkIndex).Select(point => point.Vector).ToList();
        run.Provider.EmbeddedTexts.Clear();

        // Touched only: a new fingerprint, and so new chunk ids, but the same texts:
        await run.IndexAsync("fingerprint-2", "Chapter one.", "Chapter two.", "Chapter three.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(run.Provider.EmbeddedTexts, Is.Empty, "Every text has its vector stored already.");
            Assert.That(run.Vectors.Points.Values.OrderBy(point => point.ChunkIndex).Select(point => point.Vector), Is.EqualTo(firstVectors), "Each chunk keeps the vector of its text.");
            Assert.That(run.Vectors.Points.Values.Select(point => point.Fingerprint), Is.All.EqualTo("fingerprint-2"), "The old points are gone, only the new ones remain.");
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(run.Provider.EmbeddedTexts, Is.Empty);
            Assert.That(points.Select(point => point.Vector), Is.EqualTo(new[] { CountingProvider.VectorOf("Attachment: offer.pdf"), CountingProvider.VectorOf("The body of the mail.") }), "Each text keeps its own vector.");
            Assert.That(points.Select(point => point.PointId).Intersect(oldPointIds), Is.Empty, "The new chunks have ids of their own, and the old points are gone.");
        }
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
        var manifest = await run.Store.Client.GetManifestAsync(DATA_SOURCE_ID, CancellationToken.None);
        var hiddenChunks = await run.Store.Client.GetHiddenChunksAsync(DATA_SOURCE_ID, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstGeneration.Select(chunk => chunk.Generation), Is.All.Zero, "A document stored for the first time gets the ids documents got before generations existed.");
            Assert.That(thirdGeneration.Select(chunk => chunk.Generation), Is.All.EqualTo(2), "Only the chunks of the last generation remain.");
            Assert.That(thirdGeneration.Select(chunk => chunk.TextHash), Is.EquivalentTo(new[] { ChunkTextHash.Of("Chapter one."), ChunkTextHash.Of("Chapter two.") }), "Every chunk stores the hash of its text.");
            Assert.That(run.Vectors.Points.Keys, Is.EquivalentTo(thirdGeneration.Select(chunk => chunk.ChunkId)), "Only the points of the last generation remain.");
            Assert.That(run.Vectors.Points.Values.Select(point => point.Generation), Is.All.EqualTo(2), "Every point knows its generation, by which the vector search hides the others.");
            Assert.That(manifest.Files[PATH].Generation, Is.EqualTo(2), "The row of the document names the generation it is searched by.");
            Assert.That(hiddenChunks, Is.Empty, "A complete document has nothing to hide.");
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(run.Vectors.RetrieveRequests.SelectMany(pointIds => pointIds), Is.EqualTo(new[] { IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, "fingerprint-1", 1) }), "Only the vector of the second chapter, whose text stayed, is read.");
            Assert.That(run.Provider.EmbeddedTexts, Is.EqualTo(new[] { "Chapter one, revised.", "Chapter three, revised." }));
        }
    }

    /// <remarks>
    /// The provider fails for the fourth chapter, and is asked nothing more for the document: once
    /// the cause is fixed, a chunk boundary may move, and whatever the provider answered meanwhile
    /// would be paid for nothing. The rest of the document is still read, so the attempt holds
    /// every chunk it has a vector for, the chapters the provider embedded included. It stays next
    /// to the stored version, which is still what every search finds, and the next attempt asks the
    /// provider only for what is missing.
    /// </remarks>
    [Test]
    public async Task AnAttemptWhichFailsAtTheProviderKeepsWhatItPaidForUntilTheNextOne()
    {
        await using var run = await IndexRun.CreateAsync(embeddingBatchSize: 1);
        await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.");
        run.Provider.EmbeddedTexts.Clear();
        run.Provider.FailOn = "Chapter four.";

        var indexed = await run.IndexAsync("fingerprint-2", "Chapter one.", "Chapter two, revised.", "Chapter three.", "Chapter four.", "Chapter five.");

        var embeddedByTheAttempt = run.Provider.EmbeddedTexts.ToList();
        var chunksRead = run.ChunksRead;
        var chunksAfterTheAttempt = await run.StoredChunksAsync();
        var pointsAfterTheAttempt = run.Vectors.Points.Keys.ToList();
        var manifest = await run.Store.Client.GetManifestAsync(DATA_SOURCE_ID, CancellationToken.None);
        var hiddenChunks = await run.Store.Client.GetHiddenChunksAsync(DATA_SOURCE_ID, CancellationToken.None);
        var foundChapterOne = await run.SearchAsync("one");
        var foundChapterThree = await run.SearchAsync("three");

        run.Provider.FailOn = null;
        run.Provider.EmbeddedTexts.Clear();
        var indexedAgain = await run.IndexAsync("fingerprint-3", "Chapter one.", "Chapter two, revised.", "Chapter three.", "Chapter four.", "Chapter five.");
        var chunksAtTheEnd = await run.StoredChunksAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(indexed, Is.False);
            Assert.That(embeddedByTheAttempt, Is.EqualTo(new[] { "Chapter two, revised.", "Chapter three." }), "After the failure, the provider is not asked for the fifth chapter.");
            Assert.That(chunksRead, Is.EqualTo(5), "The document is still read to its end, since a later chunk could have taken over a stored vector.");
            Assert.That(chunksAfterTheAttempt.Where(chunk => chunk.Generation == 0).Select(chunk => chunk.TextHash), Is.EquivalentTo(new[] { "Chapter one.", "Chapter two." }.Select(ChunkTextHash.Of)), "The stored version stays.");
            Assert.That(chunksAfterTheAttempt.Where(chunk => chunk.Generation == 1).Select(chunk => chunk.TextHash), Is.EquivalentTo(new[] { "Chapter one.", "Chapter two, revised.", "Chapter three." }.Select(ChunkTextHash.Of)), "The attempt keeps every chunk it has a vector for.");
            Assert.That(pointsAfterTheAttempt, Is.EquivalentTo(chunksAfterTheAttempt.Select(chunk => chunk.ChunkId)), "The vector store keeps exactly the chunks the index keeps.");
            Assert.That(manifest.Files[PATH].Fingerprint, Is.EqualTo("fingerprint-1"), "The document is still known in its stored version, and read again in the next pass.");
            Assert.That(manifest.Files[PATH].Generation, Is.Zero);
            Assert.That(hiddenChunks, Is.EqualTo(new[] { new HiddenChunks(PARENT_ID, 0) }), "The vector search leaves out every generation but the stored one.");
            Assert.That(foundChapterOne, Is.EqualTo(new[] { "Chapter one." }), "The keyword search finds the first chapter once, in the stored version.");
            Assert.That(foundChapterThree, Is.Empty, "The third chapter is only in the attempt.");
            Assert.That(indexedAgain, Is.True);
            Assert.That(run.Provider.EmbeddedTexts, Is.EqualTo(new[] { "Chapter four.", "Chapter five." }), "The next attempt asks the provider only for what is missing.");
            Assert.That(chunksAtTheEnd.Select(chunk => chunk.Generation), Is.All.EqualTo(2));
            Assert.That(run.Vectors.Points.Keys, Is.EquivalentTo(chunksAtTheEnd.Select(chunk => chunk.ChunkId)));
        }
    }

    /// <remarks>
    /// Here the file cannot be read to its end, e.g., since another program locked it. What this
    /// attempt stored goes. Whatever the document had stored before stays as it was: its stored
    /// version, and an earlier attempt which got through, whose vectors the next one still needs.
    /// </remarks>
    [Test]
    public async Task AnAttemptWhichStopsEarlyLeavesWhatWasStoredBefore()
    {
        await using var run = await IndexRun.CreateAsync(embeddingBatchSize: 1);
        await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.");
        run.Provider.FailOn = "Chapter three.";
        await run.IndexAsync("fingerprint-2", "Chapter one.", "Chapter two, revised.", "Chapter three.");
        var chunksBefore = await run.StoredChunksAsync();
        run.Provider.FailOn = null;

        var indexed = await run.IndexAsync("fingerprint-3", "Chapter one.", "Chapter two, revised again.", IndexRun.UNREADABLE);

        var chunksAfterwards = await run.StoredChunksAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(indexed, Is.False);
            Assert.That(chunksBefore.Select(chunk => chunk.Generation).Distinct(), Is.EquivalentTo(new[] { 0, 1 }), "The stored version, and the attempt which failed at the provider.");
            Assert.That(chunksAfterwards, Is.EquivalentTo(chunksBefore), "Only what the attempt which stopped early stored goes.");
            Assert.That(run.Vectors.Points.Keys, Is.EquivalentTo(chunksBefore.Select(chunk => chunk.ChunkId)));
            Assert.That(run.Manifest.Files[PATH].Fingerprint, Is.EqualTo("fingerprint-1"));
        }
    }

    /// <remarks>
    /// A failure which lies in the document itself, here a document which yields no text, keeps the
    /// stored version as well: a file broken by an edit may be fixed again. The failure is recorded,
    /// so the file is not read again until it changes.
    /// </remarks>
    [Test]
    public async Task APermanentFailureKeepsTheStoredVersion()
    {
        await using var run = await IndexRun.CreateAsync();
        await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.");
        var storedPointIds = run.Vectors.Points.Keys.ToList();

        var indexed = await run.IndexAsync("fingerprint-2");

        var manifest = await run.Store.Client.GetManifestAsync(DATA_SOURCE_ID, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(indexed, Is.False);
            Assert.That(run.Vectors.Points.Keys, Is.EquivalentTo(storedPointIds));
            Assert.That(manifest.Files[PATH].Fingerprint, Is.EqualTo("fingerprint-1"), "The stored version can still be found.");
            Assert.That(manifest.PermanentFailures[PATH].Fingerprint, Is.EqualTo("fingerprint-2"), "The broken version is not read again until it changes.");
        }
    }

    /// <remarks>
    /// A document indexed for the first time has no stored version, and none of its chunks could
    /// take over a vector, so the attempt stops at the failure. What it got from the provider until
    /// then stays for the next attempt, under a row whose chunk count stays zero, and no search
    /// finds any of it until the document is complete.
    /// </remarks>
    [Test]
    public async Task ANewDocumentKeepsWhatItPaidForWhenTheProviderFails()
    {
        await using var run = await IndexRun.CreateAsync(embeddingBatchSize: 1);
        run.Provider.FailOn = "Chapter two.";

        var indexed = await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.", "Chapter three.");

        var chunksRead = run.ChunksRead;
        var manifest = await run.Store.Client.GetManifestAsync(DATA_SOURCE_ID, CancellationToken.None);
        var storedChunks = await run.StoredChunksAsync();
        var storedPoints = run.Vectors.Points.Keys.ToList();
        var hiddenChunks = await run.Store.Client.GetHiddenChunksAsync(DATA_SOURCE_ID, CancellationToken.None);
        var found = await run.SearchAsync("one");

        run.Provider.FailOn = null;
        run.Provider.EmbeddedTexts.Clear();
        var indexedAgain = await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.", "Chapter three.");
        var manifestAtTheEnd = await run.Store.Client.GetManifestAsync(DATA_SOURCE_ID, CancellationToken.None);
        var foundAtTheEnd = await run.SearchAsync("one");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(indexed, Is.False);
            Assert.That(chunksRead, Is.EqualTo(2), "The third chapter is never read: nothing stored could spare the provider a chunk.");
            Assert.That(manifest.Files, Is.Empty, "The document cannot be searched yet.");
            Assert.That(manifest.UnfinishedFiles, Is.EquivalentTo(new[] { PATH }));
            Assert.That(storedChunks.Select(chunk => chunk.TextHash), Is.EqualTo(new[] { ChunkTextHash.Of("Chapter one.") }), "The first chapter was embedded before the provider failed.");
            Assert.That(storedPoints, Is.EquivalentTo(storedChunks.Select(chunk => chunk.ChunkId)));
            Assert.That(hiddenChunks, Is.EqualTo(new[] { new HiddenChunks(PARENT_ID, null) }), "The vector search leaves out the whole document.");
            Assert.That(found, Is.Empty, "So does the keyword search.");
            Assert.That(indexedAgain, Is.True);
            Assert.That(run.Provider.EmbeddedTexts, Is.EqualTo(new[] { "Chapter two.", "Chapter three." }), "The next attempt reuses the first chapter.");
            Assert.That(manifestAtTheEnd.UnfinishedFiles, Is.Empty);
            Assert.That(foundAtTheEnd, Is.EqualTo(new[] { "Chapter one." }));
        }
    }

    /// <remarks>
    /// The next attempt of a new document stops early for a reason which is not the provider's,
    /// here a file which cannot be read to its end. What the attempt before kept stays, so the
    /// provider is not paid twice for the same chunks.
    /// </remarks>
    [Test]
    public async Task ANewDocumentKeepsWhatItPaidForWhenItsNextAttemptStopsEarly()
    {
        await using var run = await IndexRun.CreateAsync(embeddingBatchSize: 1);
        run.Provider.FailOn = "Chapter two.";
        await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.", "Chapter three.");
        var keptChunks = await run.StoredChunksAsync();
        run.Provider.FailOn = null;

        var indexed = await run.IndexAsync("fingerprint-1", "Chapter one.", IndexRun.UNREADABLE);

        var chunksAfterwards = await run.StoredChunksAsync();
        var pointsAfterwards = run.Vectors.Points.Keys.ToList();
        var manifest = await run.Store.Client.GetManifestAsync(DATA_SOURCE_ID, CancellationToken.None);
        run.Provider.EmbeddedTexts.Clear();
        var indexedAtLast = await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.", "Chapter three.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(indexed, Is.False);
            Assert.That(chunksAfterwards, Is.EquivalentTo(keptChunks), "Only what the attempt which stopped early stored goes.");
            Assert.That(pointsAfterwards, Is.EquivalentTo(keptChunks.Select(chunk => chunk.ChunkId)));
            Assert.That(manifest.UnfinishedFiles, Is.EquivalentTo(new[] { PATH }), "The document still cannot be searched.");
            Assert.That(indexedAtLast, Is.True);
            Assert.That(run.Provider.EmbeddedTexts, Is.EqualTo(new[] { "Chapter two.", "Chapter three." }), "The first chapter was paid for once.");
        }
    }

    /// <remarks>
    /// The provider fails for the first chunk already, so the attempt has nothing to keep.
    /// </remarks>
    [Test]
    public async Task ADocumentWhoseFirstIndexingFailsLeavesNothingBehind()
    {
        await using var run = await IndexRun.CreateAsync();
        run.Provider.FailOn = "Chapter one.";

        var indexed = await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.");

        var manifest = await run.Store.Client.GetManifestAsync(DATA_SOURCE_ID, CancellationToken.None);
        var storedChunks = await run.Store.Client.GetStoredChunksAsync(DATA_SOURCE_ID, PATH, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(indexed, Is.False);
            Assert.That(run.Vectors.Points, Is.Empty, "There is no stored version to keep.");
            Assert.That(storedChunks, Is.Empty);
            Assert.That(manifest.Files, Is.Empty);
            Assert.That(manifest.UnfinishedFiles, Is.Empty);
        }
    }

    /// <remarks>
    /// A run which was interrupted, e.g., since the app was closed, leaves a generation behind which
    /// was never completed. The next run reuses its vectors as well, and once the document is
    /// stored, everything of it but the last generation goes.
    /// </remarks>
    [Test]
    public async Task WhatAnInterruptedRunLeftBehindIsReusedAndThenRemoved()
    {
        await using var run = await IndexRun.CreateAsync();
        await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.");
        await run.IndexAsync("fingerprint-2", record: false, "Chapter one.", "Chapter two, revised.");
        run.Provider.EmbeddedTexts.Clear();

        await run.IndexAsync("fingerprint-3", "Chapter one.", "Chapter two, revised.");

        var storedChunks = await run.Store.Client.GetStoredChunksAsync(DATA_SOURCE_ID, PATH, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(run.Provider.EmbeddedTexts, Is.Empty, "The interrupted run had embedded the revised chapter already.");
            Assert.That(storedChunks.Select(chunk => chunk.Generation), Is.All.EqualTo(2));
            Assert.That(run.Vectors.Points.Keys, Is.EquivalentTo(storedChunks.Select(chunk => chunk.ChunkId)));
        }
    }

    /// <remarks>
    /// An edit of the file failed at the provider, and the attempt stays for its vectors. Then the
    /// edit is undone: the content is the stored one again, so no attempt would ever come which
    /// removes the one kept.
    /// </remarks>
    [Test]
    public async Task AnAttemptKeptForItsVectorsGoesOnceTheEditIsUndone()
    {
        await using var run = await IndexRun.CreateAsync(embeddingBatchSize: 1);
        await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.");
        run.Provider.FailOn = "Chapter three.";
        await run.IndexAsync("fingerprint-2", "Chapter one.", "Chapter two, revised.", "Chapter three.");
        var generationsAfterTheAttempt = (await run.StoredChunksAsync()).Select(chunk => chunk.Generation).Distinct().ToList();

        await run.RecordContentUnchangedAsync("fingerprint-3");

        var chunksAtTheEnd = await run.StoredChunksAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(generationsAfterTheAttempt, Is.EquivalentTo(new[] { 0, 1 }));
            Assert.That(chunksAtTheEnd.Select(chunk => chunk.TextHash), Is.EquivalentTo(new[] { "Chapter one.", "Chapter two." }.Select(ChunkTextHash.Of)), "Only the stored version stays.");
            Assert.That(run.Vectors.Points.Keys, Is.EquivalentTo(chunksAtTheEnd.Select(chunk => chunk.ChunkId)), "The vector store keeps exactly the chunks the index keeps.");
        }
    }

    [Test]
    public async Task WhatAnInterruptedRunLeftOfAnUnchangedDocumentGoesWithTheNextRun()
    {
        await using var run = await IndexRun.CreateAsync();
        await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.");
        await run.IndexAsync("fingerprint-2", record: false, "Chapter one.", "Chapter two, revised.");

        await run.RemoveLeftoverGenerationsAsync(isUnchanged: false);
        var generationsOfAChangedDocument = (await run.StoredChunksAsync()).Select(chunk => chunk.Generation).Distinct().ToList();
        await run.RemoveLeftoverGenerationsAsync(isUnchanged: true);

        var chunksAtTheEnd = await run.StoredChunksAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(generationsOfAChangedDocument, Is.EquivalentTo(new[] { 0, 1 }), "A document which changed is indexed again, and that attempt takes over the vectors.");
            Assert.That(chunksAtTheEnd.Select(chunk => chunk.Generation), Is.All.EqualTo(0), "Nothing else would ever remove what the interrupted run left of a document which did not change since.");
            Assert.That(run.Vectors.Points.Keys, Is.EquivalentTo(chunksAtTheEnd.Select(chunk => chunk.ChunkId)));
        }
    }

    [Test]
    public async Task TheIndexStoreKnowsEveryChunkBeforeItsPointIsWritten()
    {
        await using var run = await IndexRun.CreateAsync();
        await run.IndexAsync("fingerprint-1", "Chapter one.", "Chapter two.");
        var unknownPoints = new List<string>();
        run.Vectors.BeforeInsert = async points =>
        {
            // Called while the run indexes, long before it is disposed:
            // ReSharper disable once AccessToDisposedClosure
            var knownChunks = (await run.StoredChunksAsync()).Select(chunk => chunk.ChunkId).ToHashSet(StringComparer.Ordinal);
            unknownPoints.AddRange(points.Select(point => point.PointId).Where(pointId => !knownChunks.Contains(pointId)));
        };

        await run.IndexAsync("fingerprint-2", "Chapter one.", "Chapter two, revised.");

        Assert.That(unknownPoints, Is.Empty, "A point the index store does not know cannot be hidden: had the app stopped right after writing it, the vector search would find it next to the stored version.");
    }

    /// <summary>
    /// One data source with one document, indexed through IndexedRunContext and recorded through
    /// DocumentRunProgress as the indexers do it, into a real index store and a vector store in memory.
    /// </summary>
    private sealed class IndexRun(TemporaryIndexStore store, int embeddingBatchSize) : IAsyncDisposable
    {
        /// <summary>
        /// A chunk text at which the document cannot be read any further, as a file locked by another program.
        /// </summary>
        public const string UNREADABLE = "<unreadable>";

        private readonly EmbeddingProvider embeddingProvider = new EmbeddingProvider(1, EMBEDDING_ID, "Test embeddings", LLMProviders.OPEN_AI, new("text-embedding-3-small", "text-embedding-3-small")) with { EmbeddingBatchSize = embeddingBatchSize };

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

        public DataSourceEmbeddingManifest Manifest { get; } = new();

        public MemoryVectorStore Vectors { get; } = new();

        public CountingProvider Provider { get; } = new();

        /// <summary>
        /// How many chunks the last attempt read of the document.
        /// </summary>
        public int ChunksRead { get; private set; }

        /// <param name="embeddingBatchSize">How many chunks go to the embedding provider at once. Zero for the default.</param>
        public static async Task<IndexRun> CreateAsync(int embeddingBatchSize = 0)
        {
            var store = await TemporaryIndexStore.CreateAsync();
            await store.Client.UpsertDataSourceAsync(DATA_SOURCE_ID, "LOCAL_DIRECTORY", EMBEDDING_ID, "signature", string.Empty, 0, CancellationToken.None);
            return new IndexRun(store, embeddingBatchSize);
        }

        /// <summary>
        /// The chunks the document has stored now, of every generation.
        /// </summary>
        public Task<IReadOnlyList<StoredChunk>> StoredChunksAsync() => store.Client.GetStoredChunksAsync(DATA_SOURCE_ID, PATH, CancellationToken.None);

        /// <summary>
        /// Searches the keyword index of the data source.
        /// </summary>
        /// <param name="word">The word to search for.</param>
        /// <returns>The texts of the chunks found.</returns>
        public async Task<IReadOnlyList<string>> SearchAsync(string word) =>
            (await store.Client.SearchChunksAsync(DATA_SOURCE_ID, word, 10, CancellationToken.None)).Select(result => result.ChunkText).ToList();

        /// <summary>
        /// Indexes the document with the given chunks, and records it as indexed or as failed.
        /// </summary>
        /// <param name="fingerprint">The fingerprint of the document, which its chunk ids derive from.</param>
        /// <param name="chunkTexts">The texts of its chunks.</param>
        /// <returns>Whether the document was indexed.</returns>
        public Task<bool> IndexAsync(string fingerprint, params string[] chunkTexts) => this.IndexAsync(fingerprint, true, chunkTexts);

        /// <param name="fingerprint">The fingerprint of the document, which its chunk ids derive from.</param>
        /// <param name="record">Whether the document is recorded at all. Without, the run ends after the chunks are stored, as if the app were closed.</param>
        /// <param name="chunkTexts">The texts of its chunks.</param>
        /// <returns>Whether the document was indexed.</returns>
        public async Task<bool> IndexAsync(string fingerprint, bool record, params string[] chunkTexts)
        {
            var context = this.CreateContext();
            var progress = new DocumentRunProgress(context, 1, 0, string.Empty, [], NullLogger.Instance);
            var document = this.CreateDocument(fingerprint, chunkTexts);

            this.ChunksRead = 0;
            var isNew = !this.Manifest.Files.ContainsKey(PATH);
            try
            {
                var indexed = await context.IndexDocumentAsync(document, (_, _) => { }, CancellationToken.None);
                if (!record)
                    return false;

                await progress.RecordDocumentIndexedAsync(document, indexed, isNew, CancellationToken.None);
                return true;
            }
            catch (Exception exception)
            {
                await progress.RecordDocumentFailureAsync(document, exception, CancellationToken.None);
                return false;
            }
        }

        /// <summary>
        /// Records the document as read again under another fingerprint, while its content stayed the same, as a file saved again without edits.
        /// </summary>
        /// <param name="fingerprint">The new fingerprint of the document.</param>
        public Task RecordContentUnchangedAsync(string fingerprint) =>
            new DocumentRunProgress(this.CreateContext(), 1, 0, string.Empty, [], NullLogger.Instance).RecordContentUnchangedAsync(this.CreateDocument(fingerprint, []), this.Manifest.Files[PATH], CancellationToken.None);

        /// <summary>
        /// Removes what earlier runs left behind of the document, as a run of the file indexer does before it reads anything.
        /// </summary>
        /// <param name="isUnchanged">Whether the document did not change since its row was written.</param>
        public Task RemoveLeftoverGenerationsAsync(bool isUnchanged) => this.CreateContext().RemoveLeftoverGenerationsAsync(_ => isUnchanged, CancellationToken.None);

        public ValueTask DisposeAsync() => store.DisposeAsync();

        private IndexedRunContext CreateContext()
        {
            var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
            return new IndexedRunContext(
                this.dataSource,
                this.embeddingProvider,
                this.Provider,
                this.Vectors,
                store.Client,
                this.Manifest,
                new RunShare(RunShare.DEFAULT_DURATION),
                new IndexingPass(new DataSourceRunSignature("TEST-EMBEDDING-SIGNATURE", string.Empty)),
                new PromptInjectionGuardService(null!, settingsManager, NullLogger<PromptInjectionGuardService>.Instance, NullLoggerFactory.Instance).BeginAction(),
                settingsManager,
                _ => { },
                NullLogger.Instance);
        }

        private EmbeddingDocument CreateDocument(string fingerprint, string[] chunkTexts)
        {
            var now = DateTimeOffset.UtcNow;
            return new EmbeddingDocument(
                PATH,
                new EmbeddingStateFile(IndexedDocumentIds.CreateParentId(DATA_SOURCE_ID, PATH), PATH, "handbook.md", "handbook.md", "md", fingerprint, 64, now, now, now, 0),
                "handbook.md",
                _ => this.Chunks(chunkTexts));
        }

        private async IAsyncEnumerable<EmbeddingChunk> Chunks(IEnumerable<string> texts)
        {
            await Task.CompletedTask;
            foreach (var text in texts)
            {
                if (text == UNREADABLE)
                    throw new IOException("The file is locked by another program.");

                this.ChunksRead++;
                yield return new EmbeddingChunk(text, default, default);
            }
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