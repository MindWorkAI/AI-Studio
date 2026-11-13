using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks what the row of a data source shows while it waits for its next run.
/// </summary>
/// <remarks>
/// A large data source is indexed in steps of a few minutes and waits in the queue between them.
/// Its row must keep the progress and the failures of the steps so far, or the bar jumps back with
/// every step and the failures vanish for minutes. What belongs to a run going on must go, though:
/// a row which names a document while nothing runs, or offers a decision the next run may no longer
/// ask for, tells the user something which is not true.
/// </remarks>
[TestFixture]
public sealed class QueuedStatusTests
{
    private static readonly DateTimeOffset NOW = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);

    private static readonly DataSourceEmbeddingFailure FAILURE = new("/tmp/test-data/report.pdf", "The file could not be read.", NOW);

    [Test]
    public void AQueuedDataSourceKeepsWhatItsRowShowedOfTheRunsSoFar()
    {
        var status = RunningStatus();

        var queued = DataSourceEmbeddingService.AsQueued(status, DataSource());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(queued.State, Is.EqualTo(DataSourceEmbeddingState.QUEUED));
            Assert.That(queued.TotalDocuments, Is.EqualTo(status.TotalDocuments));
            Assert.That(queued.IndexedDocuments, Is.EqualTo(status.IndexedDocuments));
            Assert.That(queued.FailedDocuments, Is.EqualTo(status.FailedDocuments));
            Assert.That(queued.PermanentlySkippedDocuments, Is.EqualTo(status.PermanentlySkippedDocuments), "Dropping them made the bar jump back.");
            Assert.That(queued.ProgressPercent, Is.EqualTo(status.ProgressPercent), "The bar must not jump back between two steps of the same data source.");
            Assert.That(queued.Failures, Is.EqualTo(new[] { FAILURE }));
            Assert.That(queued.IgnoredFiles, Is.EqualTo(status.IgnoredFiles));
            Assert.That(queued.FilePatternExclusions, Is.EqualTo(status.FilePatternExclusions));
            Assert.That(queued.LastError, Is.EqualTo(status.LastError), "The sentence about the data source as a whole belongs next to its failures.");
            Assert.That(queued.LastSyncUtc, Is.EqualTo(NOW));
        }
    }

    [Test]
    public void AQueuedDataSourceForgetsWhatBelongsToARunGoingOn()
    {
        var queued = DataSourceEmbeddingService.AsQueued(RunningStatus(), DataSource());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(queued.CurrentDocument, Is.Empty, "Nothing is being read while the data source waits.");
            Assert.That(queued.CurrentDocumentBlock, Is.Null);
            Assert.That(queued.CurrentDocumentPage, Is.Null);
            Assert.That(queued.Attention, Is.EqualTo(DataSourceAttention.NONE), "The next run asks the user anew when the question still applies.");
            Assert.That(queued.PendingRemovalCount, Is.Null, "The next run asks the user anew, with the number it finds then.");
            Assert.That(queued.VectorStoreUnreadable, Is.False);
        }
    }

    [Test]
    public void AQueuedDataSourceShowsItsCurrentName() =>
        Assert.That(DataSourceEmbeddingService.AsQueued(RunningStatus(), DataSource()).DataSourceName, Is.EqualTo("Test data"), "The data source may have been renamed since its last run.");

    private static DataSourceEmbeddingStatus RunningStatus() => new(
        "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11",
        "Old name",
        DataSourceType.LOCAL_DIRECTORY,
        DataSourceEmbeddingState.RUNNING,
        1200,
        400,
        3,
        "/tmp/test-data/notes.docx",
        "Some files could not be read.",
        [FAILURE],
        PermanentlySkippedDocuments: 7,
        CurrentDocumentBlock: 2,
        CurrentDocumentPage: 5,
        VectorStoreUnreadable: true,
        Attention: DataSourceAttention.MASS_REMOVAL_PENDING,
        PendingRemovalCount: 42,
        LastSyncUtc: NOW)
    {
        IgnoredFiles = [new("/tmp/test-data/interview.mp3", DataSourceEmbeddingIgnoreReason.TRANSCRIPTION_PROVIDER_MISSING)],
        FilePatternExclusions = [new(".png", 5)],
    };

    private static DataSourceLocalDirectory DataSource() => new()
    {
        Num = 1,
        Id = "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11",
        Name = "Test data",
        Description = "Documents used by the tests.",
        Type = DataSourceType.LOCAL_DIRECTORY,
        EmbeddingId = "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01",
        MaxChunkTokenLength = 512,
        ChunkOverlapTokenLength = 100,
        Path = "/tmp/test-data",
    };
}
