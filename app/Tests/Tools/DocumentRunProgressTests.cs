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

    private static readonly DateTimeOffset NOW = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);

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
        var outcome = await progress.PauseRunAsync("SOURCE-HASH", "test", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(IndexedRunOutcome.MORE_TO_DO));
            Assert.That(manifest.SourceHash, Is.EqualTo("SOURCE-HASH"), "Without the hash, a mailbox stays out of every search for the hours its first sync takes.");
            Assert.That(statuses, Is.Empty, "The status said the run is over, while the next one is about to carry on.");
        });
    }

    [Test]
    public async Task APausedRunWithoutAHashLeavesTheStoredOneAsItIs()
    {
        var (progress, statuses, manifest) = CreateProgress(totalDocuments: 10);
        manifest.SourceHash = "HASH-OF-THE-LAST-COMPLETE-RUN";

        progress.RecordUnchanged(4);
        var outcome = await progress.PauseRunAsync(null, "test", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(IndexedRunOutcome.MORE_TO_DO));
            Assert.That(manifest.SourceHash, Is.EqualTo("HASH-OF-THE-LAST-COMPLETE-RUN"), "The hash says that a run got through the whole folder. A half-indexed folder would count as complete, and the next run would skip it.");
            Assert.That(statuses, Is.Empty);
        });
    }

    [Test]
    public async Task APauseWithoutAnythingIndexedEndsTheRunWithItsFailures()
    {
        var (progress, statuses, manifest) = CreateProgress(totalDocuments: 10);
        var providerDown = new ProviderRequestException(ProviderRequestFailureReason.PROVIDER_UNAVAILABLE, "The embedding provider cannot be reached.");

        await progress.RecordDocumentFailureAsync(Document("/tmp/test-data/a.pdf"), providerDown, CancellationToken.None);
        await progress.RecordDocumentFailureAsync(Document("/tmp/test-data/b.pdf"), providerDown, CancellationToken.None);
        var outcome = await progress.PauseRunAsync(null, "test", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(IndexedRunOutcome.DONE), "The next run would fail all the same, and the one after it, without end.");
            Assert.That(statuses[^1].State, Is.EqualTo(DataSourceEmbeddingState.FAILED), "The run is over, so its row has to say so, and why.");
            Assert.That(statuses[^1].LastError, Is.EqualTo("The embedding provider cannot be reached."));
            Assert.That(manifest.SourceHash, Is.Empty, "A run which stopped halfway did not get through the folder.");
        });
    }

    [Test]
    public async Task InputsWhichCouldNotBeLookedAtDoNotEndAPausedRun()
    {
        var (progress, statuses, _) = CreateProgress(totalDocuments: 10, failedInputs: 1);

        // A step which spent its share on a single scanned document without a text layer:
        await progress.RecordDocumentFailureAsync(Document("/tmp/test-data/scan.pdf"), new FileExtractionException(FileExtractionErrorCode.NO_TEXT_EXTRACTED, "No text."), CancellationToken.None);
        var outcome = await progress.PauseRunAsync(null, "test", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(IndexedRunOutcome.MORE_TO_DO), "A locked subfolder fails in every run, however much the run gets done otherwise, and the scanned document is never read again. Ending here left the rest of the folder unindexed.");
            Assert.That(statuses.Select(status => status.State), Has.None.EqualTo(DataSourceEmbeddingState.FAILED));
        });
    }

    [Test]
    public void TheShareCountsFromTheFirstQuestion()
    {
        var (progress, _, _) = CreateProgress(totalDocuments: 10, share: new RunShare(TimeSpan.FromMinutes(5)));

        var first = progress.HasDoneItsShare(NOW);
        var shortlyBefore = progress.HasDoneItsShare(NOW + TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        var atTheEnd = progress.HasDoneItsShare(NOW + TimeSpan.FromMinutes(5));

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.False, "However long finding the documents took, every run reads at least one. Otherwise, a folder whose files take longer to list than the share would never get anywhere.");
            Assert.That(shortlyBefore, Is.False);
            Assert.That(atTheEnd, Is.True);
        });
    }

    [Test]
    public async Task TheShareEndsAfterItsDocumentsWhetherIndexingThemWorkedOrNot()
    {
        var (progress, _, _) = CreateProgress(totalDocuments: 10, share: new RunShare(RunShare.DEFAULT_DURATION, 3));
        progress.HasDoneItsShare(NOW);

        await progress.RecordDocumentIndexedAsync(Document("/tmp/test-data/a.pdf"), 4, true, CancellationToken.None);
        await progress.RecordDocumentFailureAsync(Document("/tmp/test-data/b.pdf"), new IOException("The file changed."), CancellationToken.None);
        var afterTwo = progress.HasDoneItsShare(NOW);
        await progress.RecordDocumentFailureAsync(Document("/tmp/test-data/scan.pdf"), new FileExtractionException(FileExtractionErrorCode.NO_TEXT_EXTRACTED, "No text."), CancellationToken.None);
        var afterThree = progress.HasDoneItsShare(NOW);

        Assert.Multiple(() =>
        {
            Assert.That(afterTwo, Is.False);
            Assert.That(afterThree, Is.True, "Reading a document costs its time, whether indexing it worked or not.");
        });
    }

    [Test]
    public void DocumentsWhichWereNotReadDoNotCountTowardsTheShare()
    {
        var (progress, _, _) = CreateProgress(totalDocuments: 1_000, share: new RunShare(RunShare.DEFAULT_DURATION, 3));

        progress.RecordUnchanged(900);
        progress.RecordStillUnreadable("/tmp/test-data/scan.pdf", new PermanentIndexingFailureRecord("AAAA", FileExtractionErrorCode.NO_TEXT_EXTRACTED, "No text could be read.", DateTimeOffset.UtcNow));

        Assert.That(progress.HasDoneItsShare(NOW), Is.False, "Leaving a document alone costs next to nothing. Counting it would end the step of a mailbox after its first few mails.");
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

    private static (DocumentRunProgress Progress, List<DataSourceEmbeddingStatus> Statuses, DataSourceEmbeddingManifest Manifest) CreateProgress(int totalDocuments, IIndexedDataSource? indexedDataSource = null, RunShare? share = null, int failedInputs = 0)
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
            share ?? new RunShare(RunShare.DEFAULT_DURATION),
            new SettingsManager(NullLogger<SettingsManager>.Instance, null!),
            statuses.Add,
            NullLogger.Instance);

        return (new DocumentRunProgress(context, totalDocuments, failedInputs, string.Empty, [], NullLogger.Instance), statuses, manifest);
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