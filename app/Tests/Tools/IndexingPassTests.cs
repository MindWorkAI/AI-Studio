using AIStudio.Tools.Services;
using AIStudio.Tools.Services.Indexing;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks when an indexing run carries on the pass of its data source, and when it begins a new one.
/// </summary>
/// <remarks>
/// A pass decides which documents a run leaves alone, because they failed during an earlier step. A
/// run which carries on a pass it should not have left documents unindexed which would work now. A
/// run which begins anew when it should not have reads every failed document once more, with a cloud
/// provider for real money, and tells the user about the same failure once more.
/// </remarks>
[TestFixture]
public sealed class IndexingPassTests
{
    private static readonly DataSourceRunSignature SIGNATURE = new("EMBEDDING-SIGNATURE", "SELECTION");

    [Test]
    public void TheNextStepCarriesOnThePass()
    {
        var pass = new IndexingPass(SIGNATURE);

        //
        // A request during a step merges into the next step, which then runs for the reason of
        // that request:
        //
        using (Assert.EnterMultipleScope())
        {
            foreach (var refreshMode in new[] { DataSourceEmbeddingRefreshMode.CONTINUATION, DataSourceEmbeddingRefreshMode.WATCHER_HASH_CHECK, DataSourceEmbeddingRefreshMode.INTERVAL_CHECK })
                Assert.That(IndexingPass.ContinueOrBegin(pass, refreshMode, SIGNATURE), Is.SameAs(pass), $"RefreshMode={refreshMode}");
        }
    }

    [Test]
    public void AManualRetryBeginsANewPass()
    {
        var pass = new IndexingPass(SIGNATURE);

        Assert.That(IndexingPass.ContinueOrBegin(pass, DataSourceEmbeddingRefreshMode.MANUAL_RETRY, SIGNATURE), Is.Not.SameAs(pass), "The user asked for every failed document to be tried again.");
    }

    [Test]
    public void OtherEmbeddingSettingsBeginANewPass()
    {
        var pass = new IndexingPass(SIGNATURE);
        var otherSignature = new DataSourceRunSignature("OTHER-EMBEDDING-SIGNATURE", "SELECTION");

        var nextPass = IndexingPass.ContinueOrBegin(pass, DataSourceEmbeddingRefreshMode.CONTINUATION, otherSignature);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nextPass, Is.Not.SameAs(pass), "What failed with the old embedding provider may well work with the new one.");
            Assert.That(nextPass.RunSignature, Is.EqualTo(otherSignature));
        }
    }

    [Test]
    public void AnotherSelectionBeginsANewPass()
    {
        var pass = new IndexingPass(SIGNATURE);
        var otherSelection = SIGNATURE with { SelectionSignature = "OTHER-SELECTION" };

        Assert.That(IndexingPass.ContinueOrBegin(pass, DataSourceEmbeddingRefreshMode.CONTINUATION, otherSelection), Is.Not.SameAs(pass), "The data source selects other documents, so the run goes through it anew.");
    }

    [Test]
    public void ARunWithoutAPassBeginsOne() =>
        Assert.That(IndexingPass.ContinueOrBegin(null, DataSourceEmbeddingRefreshMode.CONTINUATION, SIGNATURE).RunSignature, Is.EqualTo(SIGNATURE));
}