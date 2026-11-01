using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which indexing outcomes deserve attention in one row and in the navigation overview.
/// </summary>
[TestFixture]
public sealed class DataSourceEmbeddingHealthTests
{
    [Test]
    public void AUsableDataSourceStaysHealthyWhenSomeSelectedFilesCannotBeRead()
    {
        var status = Status(indexedDocuments: 2, totalDocuments: 3, permanentlySkippedDocuments: 1);

        Assert.That(status.Health, Is.EqualTo(DataSourceEmbeddingHealth.HEALTHY));
    }

    [Test]
    public void ADataSourceWithOnlySkippedSelectedFilesNeedsAttention()
    {
        var status = Status(indexedDocuments: 0, totalDocuments: 2, permanentlySkippedDocuments: 2);

        Assert.That(status.Health, Is.EqualTo(DataSourceEmbeddingHealth.WARNING));
    }

    [TestCase(DataSourceEmbeddingIgnoreReason.TRANSCRIPTION_PROVIDER_MISSING)]
    [TestCase(DataSourceEmbeddingIgnoreReason.TRANSCRIPTION_PROVIDER_NOT_ALLOWED)]
    public void ATranscriptionProviderWarningRemainsActionableWhenOtherFilesWereIndexed(DataSourceEmbeddingIgnoreReason reason)
    {
        var status = Status(indexedDocuments: 1, totalDocuments: 2) with
        {
            IgnoredFiles =
            [
                new DataSourceEmbeddingIgnoredFile("/data/interview.mp3", reason),
            ],
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(status.Health, Is.EqualTo(DataSourceEmbeddingHealth.WARNING));
            Assert.That(status.NeedsTranscriptionProvider, Is.True, "The embedding page offers the way to the settings only then.");
        }
    }

    [Test]
    public void AnIgnoredImageKeepsAUsableDataSourceHealthy()
    {
        var status = Status(indexedDocuments: 1, totalDocuments: 2) with
        {
            IgnoredFiles =
            [
                new DataSourceEmbeddingIgnoredFile("/data/photo.png", DataSourceEmbeddingIgnoreReason.IMAGE_NOT_SUPPORTED),
            ],
        };

        Assert.That(status.Health, Is.EqualTo(DataSourceEmbeddingHealth.HEALTHY));
    }

    [Test]
    public void PatternExclusionsDoNotChangeHealth()
    {
        var status = Status(indexedDocuments: 0, totalDocuments: 0) with
        {
            FilePatternExclusions =
            [
                new DataSourceEmbeddingFilePatternGroup(".pdf", 12),
            ],
        };

        Assert.That(status.Health, Is.EqualTo(DataSourceEmbeddingHealth.HEALTHY));
    }

    [Test]
    public void AFailedDocumentMakesTheDataSourceAnError()
    {
        var status = Status(indexedDocuments: 2, totalDocuments: 3, failedDocuments: 1);

        Assert.That(status.Health, Is.EqualTo(DataSourceEmbeddingHealth.ERROR));
    }

    [Test]
    public void AnAggregateWarningDoesNotPretendTheRunFailed()
    {
        var healthy = Status("healthy", indexedDocuments: 2, totalDocuments: 3, permanentlySkippedDocuments: 1);
        var warning = Status("warning", indexedDocuments: 0, totalDocuments: 1, permanentlySkippedDocuments: 1);

        var overview = DataSourceEmbeddingOverview.Create([healthy, warning]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(overview.State, Is.EqualTo(DataSourceEmbeddingState.COMPLETED));
            Assert.That(overview.Health, Is.EqualTo(DataSourceEmbeddingHealth.WARNING));
            Assert.That(overview.AttentionDataSources, Is.EqualTo(1));
        }
    }

    [Test]
    public void ActiveWorkKeepsItsActivityWhileTheOverviewRetainsExistingHealth()
    {
        var queued = Status("queued", DataSourceEmbeddingState.QUEUED);
        var running = Status("running", DataSourceEmbeddingState.RUNNING, indexedDocuments: 1, totalDocuments: 3);
        var warning = Status("warning", indexedDocuments: 0, totalDocuments: 1, permanentlySkippedDocuments: 1);
        var error = Status("error", indexedDocuments: 0, totalDocuments: 1, failedDocuments: 1);

        var overview = DataSourceEmbeddingOverview.Create([queued, warning, running, error]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(overview.State, Is.EqualTo(DataSourceEmbeddingState.RUNNING), "The sync icon must remain visible while work is underway.");
            Assert.That(overview.Health, Is.EqualTo(DataSourceEmbeddingHealth.ERROR), "The existing error must reappear as soon as the active run settles.");
            Assert.That(overview.AttentionDataSources, Is.EqualTo(2));
            Assert.That(overview.IndexedDocuments, Is.EqualTo(1), "Running progress belongs to the active data source, not the aggregate.");
            Assert.That(overview.TotalDocuments, Is.EqualTo(3));
        }
    }

    [Test]
    public void SettledOverviewUsesTheHighestHealthAndAggregatesFailureCounts()
    {
        var warning = Status("warning", indexedDocuments: 0, totalDocuments: 1, permanentlySkippedDocuments: 1);
        var firstError = Status("first-error", indexedDocuments: 2, totalDocuments: 3, failedDocuments: 1);
        var secondError = Status("second-error", indexedDocuments: 3, totalDocuments: 5, failedDocuments: 2);

        var overview = DataSourceEmbeddingOverview.Create([warning, firstError, secondError]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(overview.State, Is.EqualTo(DataSourceEmbeddingState.FAILED));
            Assert.That(overview.Health, Is.EqualTo(DataSourceEmbeddingHealth.ERROR));
            Assert.That(overview.AttentionDataSources, Is.EqualTo(3));
            Assert.That(overview.FailedDocuments, Is.EqualTo(3));
        }
    }

    private static DataSourceEmbeddingStatus Status(
        int indexedDocuments,
        int totalDocuments,
        int failedDocuments = 0,
        int permanentlySkippedDocuments = 0) =>
        Status("source", DataSourceEmbeddingState.COMPLETED, indexedDocuments, totalDocuments, failedDocuments, permanentlySkippedDocuments);

    private static DataSourceEmbeddingStatus Status(
        string id,
        DataSourceEmbeddingState state = DataSourceEmbeddingState.COMPLETED,
        int indexedDocuments = 0,
        int totalDocuments = 0,
        int failedDocuments = 0,
        int permanentlySkippedDocuments = 0) =>
        new(
            id,
            id,
            DataSourceType.LOCAL_DIRECTORY,
            state,
            totalDocuments,
            indexedDocuments,
            failedDocuments,
            string.Empty,
            string.Empty,
            [],
            permanentlySkippedDocuments);
}
