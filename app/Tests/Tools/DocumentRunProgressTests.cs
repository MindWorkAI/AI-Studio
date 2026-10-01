using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Databases.VectorStore;
using AIStudio.Tools.Services;
using AIStudio.Tools.Services.Indexing;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks what an indexing run tells the user about the documents of a data source.
/// </summary>
/// <remarks>
/// Every kind of data source reports its documents through the same progress, so whatever it
/// gets wrong shows up on every row of the embedding page at once. The stores are the ones which
/// stand in for an unavailable database: the progress has to cope with a store which refuses to
/// clean up, because a document which failed must never fail the run on top of it.
/// </remarks>
[TestFixture]
public sealed class DocumentRunProgressTests
{
    private const string DATA_SOURCE_ID = "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11";

    [Test]
    public async Task AProviderFailureKeepsTheDocumentsSkippedForGoodInTheStatus()
    {
        var (progress, statuses, _) = CreateProgress(totalDocuments: 2);

        progress.RecordStillUnreadable("/tmp/test-data/scan.pdf", new PermanentIndexingFailureRecord("AAAA", FileExtractionErrorCode.NO_TEXT_EXTRACTED, "No text could be read.", DateTimeOffset.UtcNow));
        await progress.RecordDocumentFailureAsync(Document("/tmp/test-data/report.pdf"), new ProviderRequestException(ProviderRequestFailureReason.INVALID_OR_MISSING_API_KEY, "The API key was rejected."), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(statuses[^1].FailedFiles, Is.EqualTo(1));
            Assert.That(statuses[^1].LastError, Is.EqualTo("The API key was rejected."), "The provider said what to do, and that is what the user reads.");
            Assert.That(statuses[^1].PermanentlySkippedFiles, Is.EqualTo(1), "The scanned file is still done. Dropping it from the status made the bar jump back after every provider failure.");
        });
    }

    [Test]
    public async Task ADocumentWhichCannotBeReadIsMarkedUnderItsStoredPath()
    {
        var (progress, statuses, manifest) = CreateProgress(totalDocuments: 1);
        var document = Document("/tmp/test-data/scan.pdf");

        await progress.RecordDocumentFailureAsync(document, new FileExtractionException(FileExtractionErrorCode.NO_TEXT_EXTRACTED, "No text."), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(manifest.PermanentFailures.Keys, Is.EquivalentTo([document.State.AbsolutePath]), "The mark is what keeps the document from being read again until it changes.");
            Assert.That(manifest.PermanentFailures[document.State.AbsolutePath].Fingerprint, Is.EqualTo(document.State.Fingerprint), "The fingerprint decides when the document deserves another attempt.");
            Assert.That(statuses[^1].FailedFiles, Is.Zero, "A document which cannot be read for a reason of its own is not a failure of the run.");
            Assert.That(statuses[^1].PermanentlySkippedFiles, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ARunWhichOnlySkippedDocumentsForGoodIsCompleted()
    {
        var (progress, statuses, _) = CreateProgress(totalDocuments: 3);

        progress.RecordUnchanged(2);
        progress.RecordStillUnreadable("/tmp/test-data/scan.pdf", new PermanentIndexingFailureRecord("AAAA", FileExtractionErrorCode.NO_TEXT_EXTRACTED, "No text could be read.", DateTimeOffset.UtcNow));
        await progress.CompleteRunAsync("SOURCE-HASH", "test", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(statuses[^1].State, Is.EqualTo(DataSourceEmbeddingState.COMPLETED), "Nothing is left to try, so the data source must not ask for attention forever.");
            Assert.That(statuses[^1].IndexedFiles, Is.EqualTo(2));
            Assert.That(statuses[^1].ProgressPercent, Is.EqualTo(100));
            Assert.That(statuses[^1].Failures, Has.Count.EqualTo(1), "The stored reason keeps its place in the list, so the user still sees why.");
        });
    }

    [Test]
    public async Task ARunWithAFailedDocumentNeedsAttention()
    {
        var (progress, statuses, _) = CreateProgress(totalDocuments: 1);

        await progress.RecordDocumentFailureAsync(Document("/tmp/test-data/report.pdf"), new IOException("The file changed."), CancellationToken.None);
        await progress.CompleteRunAsync("SOURCE-HASH", "test", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(statuses[^1].State, Is.EqualTo(DataSourceEmbeddingState.FAILED));
            Assert.That(statuses[^1].LastError, Is.Not.Empty, "A failed run says why, or at least where to look.");
            Assert.That(statuses[^1].Failures.Select(failure => failure.FilePath), Is.EquivalentTo(["/tmp/test-data/report.pdf"]));
        });
    }

    private static (DocumentRunProgress Progress, List<DataSourceEmbeddingStatus> Statuses, DataSourceEmbeddingManifest Manifest) CreateProgress(int totalDocuments)
    {
        var statuses = new List<DataSourceEmbeddingStatus>();
        var manifest = new DataSourceEmbeddingManifest();
        var embeddingProvider = new EmbeddingProvider(1, "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "Test embeddings", LLMProviders.OPEN_AI, new("text-embedding-3-small", "text-embedding-3-small"));
        var dataSource = new DataSourceLocalDirectory
        {
            Num = 1,
            Id = DATA_SOURCE_ID,
            Name = "Test data",
            Type = DataSourceType.LOCAL_DIRECTORY,
            EmbeddingId = embeddingProvider.Id,
            ConfidenceLevel = ConfidenceLevel.LOW,
            Path = "/tmp/test-data",
        };

        var context = new IndexedRunContext(
            dataSource,
            embeddingProvider,
            embeddingProvider.CreateProvider(),
            new NoVectorStoreClient("Test vector store", "Not available in tests."),
            new NoIndexStoreClient("Test index store", "Not available in tests."),
            manifest,
            new SettingsManager(NullLogger<SettingsManager>.Instance, null!),
            statuses.Add,
            NullLogger.Instance);

        return (new DocumentRunProgress(context, totalDocuments, 0, string.Empty, [], NullLogger.Instance), statuses, manifest);
    }

    private static EmbeddingDocument Document(string path) => new(
        path,
        new EmbeddingStateFile(IndexedDocumentIds.CreateParentId(DATA_SOURCE_ID, path), path, Path.GetFileName(path), Path.GetFileName(path), "pdf", "FINGERPRINT", 1024, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 0),
        Path.GetFileName(path),
        _ => NoChunks());

    private static async IAsyncEnumerable<EmbeddingChunk> NoChunks()
    {
        await Task.CompletedTask;
        yield break;
    }
}