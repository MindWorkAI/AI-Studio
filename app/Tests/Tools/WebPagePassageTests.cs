using AIStudio.Chat;
using AIStudio.Tests.Tools.Databases;
using AIStudio.Tools;
using AIStudio.Tools.Databases.ProvenanceStore;
using AIStudio.Tools.RAG;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks the passage a web page gets, which lets the user read under its source what the AI read of it.
/// </summary>
/// <remarks>
/// The web tools list every page they read as a source. Without a passage, the source only linked
/// to the page as it is now, which may well differ from what the AI read, or be gone.
/// </remarks>
[TestFixture]
public sealed class WebPagePassageTests
{
    private const string PAGE_URL = "https://example.org/budget-2026";

    private static readonly Source PAGE_SOURCE = new("Budget 2026", PAGE_URL, SourceOrigin.TOOL);

    [Test]
    public void ThePassageIsListedUnderItsSource()
    {
        var passage = PAGE_SOURCE.ToWebPagePassage("# Budget 2026\nThe budget grows by 4 %.");

        var found = new[] { passage }.SortBySource().GetPassagesOf(PAGE_SOURCE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.EqualTo(new[] { passage }), "Only a passage under its source gets the eye next to it.");
            Assert.That(passage.DataSourceName, Is.EqualTo("Budget 2026"), "The dialog of the passage is named after the page.");
            Assert.That(passage.Text, Is.EqualTo("# Budget 2026\nThe budget grows by 4 %."));
            Assert.That(passage.Type, Is.EqualTo(RetrievalContentType.TEXT_WEBSITE));
        }
    }

    [Test]
    public void TheSourceKeepsTheTitleOfThePage()
    {
        List<Source> sources = [PAGE_SOURCE];

        sources.NamePassagesInTitles([PAGE_SOURCE.ToWebPagePassage("The budget grows by 4 %.")]);

        Assert.That(sources.Single().Title, Is.EqualTo("Budget 2026"), "A page names no pages or lines, so there is nothing to add to its title.");
    }

    [Test]
    public void TheSameContentReadTwiceIsOnePassage()
    {
        List<SourcePassage> passages = [PAGE_SOURCE.ToWebPagePassage("The budget grows by 4 %.")];

        passages.MergePassages([PAGE_SOURCE.ToWebPagePassage("The budget grows by 4 %.")]);

        Assert.That(passages, Has.Count.EqualTo(1), "A search and a read of the same page may return the same content, which the AI read once.");
    }

    [Test]
    public async Task ThePageIsStoredAsAWebPage()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var answer = new ContentText { Text = "The budget grows by 4 %.", SourcePassages = [PAGE_SOURCE.ToWebPagePassage("The budget grows by 4 %.")] };

        await answer.StoreReceivedContentAsync(store.Client, Guid.NewGuid());
        var provenanceId = answer.SourcePassages.Single().ProvenanceId!.Value;
        var entries = await store.Client.GetEntriesAsync([provenanceId], CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entries[provenanceId].Kind, Is.EqualTo(ProvenanceKind.WEB_PAGE), "The information page counts it among the web pages.");
            Assert.That(entries[provenanceId].Content, Is.EqualTo("The budget grows by 4 %."));
        }
    }
}