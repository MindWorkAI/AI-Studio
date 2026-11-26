using AIStudio.Tools.Services.Indexing;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how the changes a watcher reports are counted instead of logged one by one.
/// </summary>
[TestFixture]
public sealed class WatcherChangeTallyTests
{
    private const string DATA_SOURCE = "72022e84-a70c-45dd-b717-78377d07cc45";

    private const string OTHER_DATA_SOURCE = "4f2a6c1e-8b3d-4e5f-9a7c-1d2e3f4a5b6c";

    [Test]
    public void OnlyTheFirstChangeOfABurstOpensAWindow()
    {
        var tally = new WatcherChangeTally();

        Assert.That(tally.Count(DATA_SOURCE, "/repo/target/a.rlib", isRelevant: false), Is.True);
        Assert.That(tally.Count(DATA_SOURCE, "/repo/target/b.rlib", isRelevant: false), Is.False);
        Assert.That(tally.Count(DATA_SOURCE, "/repo/README.md", isRelevant: true), Is.False);
    }

    [Test]
    public void ClosingTheWindowReturnsTheFurtherChanges()
    {
        var tally = new WatcherChangeTally();
        tally.Count(DATA_SOURCE, "/repo/target/first.rlib", isRelevant: false);
        for (var file = 0; file < 5; file++)
            tally.Count(DATA_SOURCE, $"/repo/target/{file}.rlib", isRelevant: false);

        tally.Count(DATA_SOURCE, "/repo/README.md", isRelevant: true);
        tally.Count(DATA_SOURCE, "/repo/Überblick ä ß 日本.md", isRelevant: true);

        var counts = tally.Close(DATA_SOURCE);

        Assert.That(counts.Ignored, Is.EqualTo(5), "The change which opened the window was counted as well.");
        Assert.That(counts.Relevant, Is.EqualTo(2));
        Assert.That(counts.Total, Is.EqualTo(7));
        Assert.That(counts.LastPath, Is.EqualTo("/repo/Überblick ä ß 日本.md"));
    }

    [Test]
    public void AfterClosingTheNextChangeOpensANewWindow()
    {
        var tally = new WatcherChangeTally();
        tally.Count(DATA_SOURCE, "/repo/a.md", isRelevant: true);
        tally.Count(DATA_SOURCE, "/repo/b.md", isRelevant: true);
        tally.Close(DATA_SOURCE);

        Assert.That(tally.Count(DATA_SOURCE, "/repo/c.md", isRelevant: true), Is.True);
        Assert.That(tally.Close(DATA_SOURCE), Is.EqualTo(WatcherChangeCounts.NONE), "The counts of the closed window were carried over.");
    }

    [Test]
    public void EveryDataSourceHasAWindowOfItsOwn()
    {
        var tally = new WatcherChangeTally();
        tally.Count(DATA_SOURCE, "/repo/a.md", isRelevant: true);

        Assert.That(tally.Count(OTHER_DATA_SOURCE, "/mail/b.md", isRelevant: true), Is.True, "A change of one data source joined the window of another.");

        tally.Count(OTHER_DATA_SOURCE, "/mail/c.md", isRelevant: false);
        Assert.That(tally.Close(DATA_SOURCE).Total, Is.Zero);
        Assert.That(tally.Close(OTHER_DATA_SOURCE).Total, Is.EqualTo(1));
    }

    [Test]
    public void ClosingAWindowWhichIsNotOpenCountsNothing()
    {
        var tally = new WatcherChangeTally();

        Assert.That(tally.Close(DATA_SOURCE), Is.EqualTo(WatcherChangeCounts.NONE));
    }
}