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

    private const string EMBEDDING_ID = "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01";

    [Test]
    public async Task AProviderFailureKeepsTheDocumentsSkippedForGoodInTheStatus()
    {
        var (progress, statuses, _) = CreateProgress(totalDocuments: 2);

        progress.RecordStillUnreadable("/tmp/test-data/scan.pdf", new PermanentIndexingFailureRecord("AAAA", FileExtractionErrorCode.NO_TEXT_EXTRACTED, "No text could be read.", DateTimeOffset.UtcNow));
        await progress.RecordDocumentFailureAsync(Document("/tmp/test-data/report.pdf"), new ProviderRequestException(ProviderRequestFailureReason.INVALID_OR_MISSING_API_KEY, "The API key was rejected."), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(statuses[^1].FailedDocuments, Is.EqualTo(1));
            Assert.That(statuses[^1].LastError, Is.EqualTo("The API key was rejected."), "The provider said what to do, and that is what the user reads.");
            Assert.That(statuses[^1].PermanentlySkippedDocuments, Is.EqualTo(1), "The scanned file is still done. Dropping it from the status made the bar jump back after every provider failure.");
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
            Assert.That(statuses[^1].FailedDocuments, Is.Zero, "A document which cannot be read for a reason of its own is not a failure of the run.");
            Assert.That(statuses[^1].PermanentlySkippedDocuments, Is.EqualTo(1));
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
            Assert.That(statuses[^1].IndexedDocuments, Is.EqualTo(2));
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
            Assert.That(statuses[^1].Failures.Select(failure => failure.DocumentKey), Is.EquivalentTo(["/tmp/test-data/report.pdf"]));
        });
    }

    [Test]
    public async Task APausedRunKeepsItsHashAndLeavesTheStatusToWhatComesNext()
    {
        var (progress, statuses, manifest) = CreateProgress(totalDocuments: 10);

        progress.RecordUnchanged(4);
        await progress.PauseRunAsync("SOURCE-HASH", "test", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(manifest.SourceHash, Is.EqualTo("SOURCE-HASH"), "Without the hash, a mailbox stays out of every search for the hours its first sync takes.");
            Assert.That(statuses, Is.Empty, "The status said the run is over, while the next one is about to carry on.");
        });
    }

    [Test]
    public void EveryStatusOfARunCarriesTheLastSync()
    {
        var (progress, statuses, _) = CreateProgress(totalDocuments: 2);
        var lastSyncUtc = new DateTimeOffset(2026, 10, 1, 7, 0, 0, TimeSpan.Zero);

        progress.LastSyncUtc = lastSyncUtc;
        progress.Publish();
        progress.PublishRunFailure("The server could not be reached.");
        progress.PublishStoredState(workedThrough: true);

        Assert.That(statuses.Select(status => status.LastSyncUtc), Is.All.EqualTo(lastSyncUtc));
    }

    [Test]
    public void AHeldBackRemovalSaysWhatItAsksAbout()
    {
        var (progress, statuses, _) = CreateProgress(totalDocuments: 1_000);

        progress.PublishRunFailure("This sync would remove 250 mails.", DataSourceAttention.MASS_REMOVAL_PENDING, 250);

        Assert.Multiple(() =>
        {
            Assert.That(statuses[^1].State, Is.EqualTo(DataSourceEmbeddingState.FAILED));
            Assert.That(statuses[^1].Attention, Is.EqualTo(DataSourceAttention.MASS_REMOVAL_PENDING));
            Assert.That(statuses[^1].PendingRemovalCount, Is.EqualTo(250), "Agreeing hands back the very number the user was shown.");
            Assert.That(statuses[^1].LastError, Is.EqualTo("This sync would remove 250 mails."));
        });
    }

    [Test]
    public void AStoredStateIsCompletedOnlyWhenARunGotThroughOnce()
    {
        var (progress, statuses, _) = CreateProgress(totalDocuments: 3);

        progress.PublishStoredState(workedThrough: false);
        progress.PublishStoredState(workedThrough: true);

        Assert.That(statuses.Select(status => status.State), Is.EqualTo(new[] { DataSourceEmbeddingState.IDLE, DataSourceEmbeddingState.COMPLETED }));
    }

    [Test]
    public async Task AFailedMailIsCalledByItsSubject()
    {
        var mailbox = new DataSourceMailbox { Num = 2, Id = DATA_SOURCE_ID, Name = "Work", EmbeddingId = EMBEDDING_ID, ConfidenceLevel = ConfidenceLevel.LOW };
        var (progress, statuses, _) = CreateProgress(totalDocuments: 1, mailbox);
        var mail = Document("mail:5f0c2a9e7b14", "Board report due today");

        await progress.RecordDocumentFailureAsync(mail, new IOException("The vector store refused the chunks."), CancellationToken.None);
        await progress.CompleteRunAsync("SOURCE-HASH", "test", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(statuses[^1].Failures[0].DisplayName, Is.EqualTo("Board report due today"), "The key of a mail is a hash, which tells the user nothing.");
            Assert.That(statuses[^1].Failures[0].Reason, Does.Contain("'Board report due today'").And.Not.Contain("file"), "A mail was called a file.");
            Assert.That(statuses[^1].LastError, Does.Not.Contain("file"));
        });
    }

    [Test]
    public async Task AFailedFileKeepsItsName()
    {
        var (progress, statuses, _) = CreateProgress(totalDocuments: 1);

        await progress.RecordDocumentFailureAsync(Document("/tmp/test-data/report.pdf"), new IOException("The file changed."), CancellationToken.None);

        Assert.That(statuses[^1].Failures[0].DisplayName, Is.EqualTo("report.pdf"));
    }

    private static (DocumentRunProgress Progress, List<DataSourceEmbeddingStatus> Statuses, DataSourceEmbeddingManifest Manifest) CreateProgress(int totalDocuments, IIndexedDataSource? indexedDataSource = null)
    {
        var statuses = new List<DataSourceEmbeddingStatus>();
        var manifest = new DataSourceEmbeddingManifest();
        var embeddingProvider = new EmbeddingProvider(1, EMBEDDING_ID, "Test embeddings", LLMProviders.OPEN_AI, new("text-embedding-3-small", "text-embedding-3-small"));
        var dataSource = indexedDataSource ?? new DataSourceLocalDirectory
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

    private static EmbeddingDocument Document(string path) => Document(path, Path.GetFileName(path));

    private static EmbeddingDocument Document(string key, string displayName) => new(
        key,
        new EmbeddingStateFile(IndexedDocumentIds.CreateParentId(DATA_SOURCE_ID, key), key, displayName, displayName, "pdf", "FINGERPRINT", 1024, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 0),
        displayName,
        _ => NoChunks());

    private static async IAsyncEnumerable<EmbeddingChunk> NoChunks()
    {
        await Task.CompletedTask;
        yield break;
    }
}