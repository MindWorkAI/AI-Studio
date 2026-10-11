using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks when an indexing run stops because the embedding settings of its data source changed.
/// </summary>
/// <remarks>
/// A run resolves its data source and its embedding provider once, when it starts, and may then run
/// for hours. A run which carries on after a change of those settings embeds for an index the next
/// run throws away, at a cloud embedding provider for real money. Worse, it keeps sending documents
/// to a provider which was deleted or may no longer read them. Stopping a run for nothing is not free
/// either: everything it did so far is done again.
/// </remarks>
[TestFixture]
public sealed class OutdatedRunTests
{
    private static readonly DataSourceRunSignature RUN_SIGNATURE = new("2|b0a4c4d2|OPEN_AI|text-embedding-3-small|512|100");

    private static readonly DataSourceRunSignature OTHER_SIGNATURE = new("2|b0a4c4d2|OPEN_AI|text-embedding-3-large|512|100");

    [Test]
    public void ARunWhichIsStillPreparingCarriesOn()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DataSourceEmbeddingService.IsRunOutdated(null, OTHER_SIGNATURE), Is.False, "The run has not resolved its embedding provider yet, so it has embedded nothing so far.");
            Assert.That(DataSourceEmbeddingService.IsRunOutdated(null, DataSourceRunSignature.NONE), Is.False, "The run has not resolved its embedding provider yet, so it has sent nothing anywhere so far.");
        });
    }

    [Test]
    public void ARunForTheCurrentSettingsCarriesOn()
    {
        var sameSignature = new DataSourceRunSignature(RUN_SIGNATURE.EmbeddingSignature);
        Assert.That(DataSourceEmbeddingService.IsRunOutdated(RUN_SIGNATURE, sameSignature), Is.False, "Nothing the vectors depend on changed, so stopping the run would only repeat its work.");
    }

    [Test]
    public void ARunForOtherSettingsStops()
    {
        Assert.That(DataSourceEmbeddingService.IsRunOutdated(RUN_SIGNATURE, OTHER_SIGNATURE), Is.True, "The next run throws away whatever this run embeds.");
    }

    [Test]
    public void ARunWhichWouldNotStartAnymoreStops()
    {
        Assert.That(DataSourceEmbeddingService.IsRunOutdated(RUN_SIGNATURE, DataSourceRunSignature.NONE), Is.True, "The embedding provider is gone or may no longer read the data source, so the run must not send it anything else.");
    }

    [Test]
    public void AnAllowedProviderGetsARunWithTheEmbeddingSignature()
    {
        var dataSource = DataSource(ConfidenceLevel.LOW);
        var embeddingProvider = TestEmbeddingProvider();

        Assert.That(
            DataSourceEmbeddingService.GetRunSignature(dataSource, embeddingProvider, ConfidenceLevel.MEDIUM).EmbeddingSignature,
            Is.EqualTo(DataSourceEmbeddingService.BuildEmbeddingSignature(dataSource, embeddingProvider)),
            "A run counts as outdated exactly when the stored index would be thrown away.");
    }

    [Test]
    public void ChangingTheConfidenceOfAnAllowedProviderKeepsTheRun()
    {
        var dataSource = DataSource(ConfidenceLevel.LOW);
        var embeddingProvider = TestEmbeddingProvider();

        Assert.That(
            DataSourceEmbeddingService.GetRunSignature(dataSource, embeddingProvider, ConfidenceLevel.HIGH),
            Is.EqualTo(DataSourceEmbeddingService.GetRunSignature(dataSource, embeddingProvider, ConfidenceLevel.MEDIUM)),
            "The provider may still read the data source, and the confidence level changes no vector.");
    }

    [Test]
    public void AProviderBelowTheLevelOfTheDataSourceGetsNoRun()
    {
        var dataSource = DataSource(ConfidenceLevel.HIGH);

        Assert.That(DataSourceEmbeddingService.GetRunSignature(dataSource, TestEmbeddingProvider(), ConfidenceLevel.LOW), Is.EqualTo(DataSourceRunSignature.NONE), "A run must not send the documents of the data source to a provider which may not read them.");
    }

    [Test]
    public void NoProviderGetsARunForAMailboxWithoutAValidLevel()
    {
        var mailbox = new DataSourceMailbox { ConfidenceLevel = ConfidenceLevel.NONE };

        Assert.That(DataSourceEmbeddingService.GetRunSignature(mailbox, TestEmbeddingProvider(), ConfidenceLevel.HIGH), Is.EqualTo(DataSourceRunSignature.NONE), "The run asked the rule of the other data sources, which lets every provider read a source without a level.");
    }

    private static DataSourceLocalDirectory DataSource(ConfidenceLevel confidenceLevel) => new()
    {
        Num = 1,
        Id = "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11",
        Name = "Test data",
        Description = "Documents used by the tests.",
        Type = DataSourceType.LOCAL_DIRECTORY,
        EmbeddingId = "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01",
        MaxChunkTokenLength = 512,
        ChunkOverlapTokenLength = 100,
        ConfidenceLevel = confidenceLevel,
        Path = "/tmp/test-data",
    };

    private static EmbeddingProvider TestEmbeddingProvider() =>
        new(1, "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "Test embeddings", LLMProviders.OPEN_AI, new("text-embedding-3-small", "text-embedding-3-small"));
}