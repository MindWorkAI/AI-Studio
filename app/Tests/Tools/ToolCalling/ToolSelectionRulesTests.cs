using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks how a selection of tools turns into the set which actually runs.
/// </summary>
/// <remarks>
/// Every tool selection in the app passes through this, and so do the audit of an assistant plugin
/// and its security card. Whatever it adds is therefore what the user and the audit get to see, so it
/// must neither add a tool nobody asked for nor keep adding each time it runs.
/// </remarks>
[TestFixture]
public sealed class ToolSelectionRulesTests
{
    private const string SEARCH_CONFLUENCE = ToolSelectionRules.SEARCH_CONFLUENCE_TOOL_ID;
    private const string READ_WEB_PAGE = ToolSelectionRules.READ_WEB_PAGE_TOOL_ID;
    private const string WEB_SEARCH = ToolSelectionRules.WEB_SEARCH_TOOL_ID;
    private const string SEMANTIC_SEARCH = ToolSelectionRules.SEMANTIC_SEARCH_TOOL_ID;

    [Test]
    public void SearchConfluenceBringsReadWebPageAlong()
    {
        Assert.That(ToolSelectionRules.NormalizeSelection([SEARCH_CONFLUENCE]), Is.EquivalentTo(new[] { SEARCH_CONFLUENCE, READ_WEB_PAGE }), "The search only finds pages; without Read Web Page the model could not open a single result.");
    }

    [TestCase(READ_WEB_PAGE)]
    [TestCase(WEB_SEARCH)]
    public void OtherToolsBringNothingAlong(string toolId)
    {
        Assert.That(ToolSelectionRules.NormalizeSelection([toolId]), Is.EquivalentTo(new[] { toolId }), "Only Search Confluence depends on another tool. Read Web Page in particular does not pull the search in.");
    }

    [Test]
    public void SemanticSearchIsNeverPartOfASelection()
    {
        Assert.That(ToolSelectionRules.NormalizeSelection([SEMANTIC_SEARCH, WEB_SEARCH]), Is.EquivalentTo(new[] { WEB_SEARCH }), "Semantic Search offers itself from the data sources of a chat. A template or a plugin naming it would put a tool on the security card that the selection has no say over.");
    }

    [Test]
    public void NormalizingTwiceChangesNothing()
    {
        var once = ToolSelectionRules.NormalizeSelection([SEARCH_CONFLUENCE, WEB_SEARCH]);

        Assert.That(ToolSelectionRules.NormalizeSelection(once), Is.EquivalentTo(once), "The selection fields normalize whatever they receive, including a selection they normalized themselves a moment ago.");
    }

    [Test]
    public void ATwiceSelectedToolRunsOnce()
    {
        Assert.That(ToolSelectionRules.NormalizeSelection([WEB_SEARCH, WEB_SEARCH]), Has.Count.EqualTo(1));
    }

    [Test]
    public void TheSelectionPassedInStaysUntouched()
    {
        HashSet<string> selected = [SEARCH_CONFLUENCE];
        ToolSelectionRules.NormalizeSelection(selected);

        Assert.That(selected, Is.EquivalentTo(new[] { SEARCH_CONFLUENCE }), "The caller's set, such as the tools of a stored chat template, must not change behind its back.");
    }
}