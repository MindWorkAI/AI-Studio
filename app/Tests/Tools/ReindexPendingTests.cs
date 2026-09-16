using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks when a data source counts as waiting for its index to be rebuilt.
/// </summary>
/// <remarks>
/// This decides whether the data source selection greys a row out. Two mistakes are possible and
/// both are bad in their own way: calling a rebuild finished lets the user pick a data source which
/// finds nothing and answers without their data, while calling a healthy data source unusable locks
/// a row for good. The stored signature alone cannot tell the two apart, because it is written back
/// the moment the old index is discarded -- the stored hash of the data source is what closes that
/// gap, since it only appears once a run has worked through everything.
/// </remarks>
[TestFixture]
public sealed class ReindexPendingTests
{
    private const string CURRENT_SIGNATURE = "v1|openai|text-embedding-3-small|512|100";

    [Test]
    public void ADataSourceWhichWasNeverIndexedIsWaiting()
    {
        Assert.That(
            DataSourceEmbeddingService.IsIndexAwaitingRebuild(null, CURRENT_SIGNATURE, null),
            Is.True,
            "Nothing is stored about this data source, so there is nothing to search in it.");
    }

    [Test]
    public void AnotherEmbeddingConfigurationMeansWaiting()
    {
        var indexState = new DataSourceIndexState("openai", "v1|openai|text-embedding-3-large|512|100", "source-hash", 1536);

        Assert.That(
            DataSourceEmbeddingService.IsIndexAwaitingRebuild(indexState, CURRENT_SIGNATURE, DataSourceEmbeddingState.COMPLETED),
            Is.True,
            "The stored vectors belong to another embedding configuration and are discarded by the next run, so they are of no use now either.");
    }

    [Test]
    public void AFinishedIndexIsNotWaiting()
    {
        var indexState = new DataSourceIndexState("openai", CURRENT_SIGNATURE, "source-hash", 1536);

        Assert.That(
            DataSourceEmbeddingService.IsIndexAwaitingRebuild(indexState, CURRENT_SIGNATURE, DataSourceEmbeddingState.COMPLETED),
            Is.False,
            "A run has worked through the whole data source since the index was last discarded.");
    }

    [Test]
    public void CatchingUpWithChangedFilesIsNotWaiting()
    {
        var indexState = new DataSourceIndexState("openai", CURRENT_SIGNATURE, "source-hash", 1536);

        Assert.That(
            DataSourceEmbeddingService.IsIndexAwaitingRebuild(indexState, CURRENT_SIGNATURE, DataSourceEmbeddingState.RUNNING),
            Is.False,
            "An ordinary run leaves the stored hash in place: everything indexed before is still there and still searchable.");
    }

    [Test]
    public void ARebuildInProgressIsWaiting()
    {
        //
        // What a reset leaves behind: the row was written anew with the current signature, and the
        // hash of the data source is empty until a run has been through all of it.
        //
        var indexState = new DataSourceIndexState("openai", CURRENT_SIGNATURE, string.Empty, 0);

        Assert.That(
            DataSourceEmbeddingService.IsIndexAwaitingRebuild(indexState, CURRENT_SIGNATURE, DataSourceEmbeddingState.RUNNING),
            Is.True,
            "The signature matches again, but no run has finished since the vectors were thrown away.");
    }

    [Test]
    public void AFailedRunIsNotWaiting()
    {
        var indexState = new DataSourceIndexState("openai", CURRENT_SIGNATURE, string.Empty, 1536);

        Assert.That(
            DataSourceEmbeddingService.IsIndexAwaitingRebuild(indexState, CURRENT_SIGNATURE, DataSourceEmbeddingState.FAILED),
            Is.False,
            "Whatever the failed run managed to index is searchable, and the embeddings page already names the problem.");
    }
}