using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks how a selection turns into the collections which run, and those into their tools.
/// </summary>
/// <remarks>
/// Every tool selection in the app passes through this, and so do the audit of an assistant plugin
/// and its security card. Whatever it adds is therefore what the user and the audit get to see, so it
/// must neither add a tool nobody asked for nor keep adding each time it runs.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ToolSelectionRulesTests : ToolRegistryTestBase
{
    private const string SEARCH_CONFLUENCE = ToolSelectionRules.SEARCH_CONFLUENCE_TOOL_ID;
    private const string READ_WEB_PAGE = ToolSelectionRules.READ_WEB_PAGE_TOOL_ID;
    private const string WEB_SEARCH = ToolSelectionRules.WEB_SEARCH_TOOL_ID;
    private const string SEMANTIC_SEARCH = ToolSelectionRules.SEMANTIC_SEARCH_TOOL_ID;
    private const string SEARCH_MAILS = ToolSelectionRules.SEARCH_MAILS_TOOL_ID;
    private const string READ_MAIL = ToolSelectionRules.READ_MAIL_TOOL_ID;
    private const string COUNT_MAILS = ToolSelectionRules.COUNT_MAILS_TOOL_ID;
    private const string MAILBOXES = ToolSelectionRules.MAILBOXES_COLLECTION_ID;

    [TestCase(SEARCH_MAILS)]
    [TestCase(READ_MAIL)]
    [TestCase(COUNT_MAILS)]
    public void AToolOfACollectionSelectsTheWholeCollection(string toolId)
    {
        var registry = this.CreateAppRegistry();

        Assert.Multiple(() =>
        {
            Assert.That(registry.NormalizeSelection([toolId]), Is.EquivalentTo(new[] { MAILBOXES }), "A selection stored before the tool joined its collection selects the whole collection.");
            Assert.That(registry.ExpandSelection([toolId]), Is.EquivalentTo(new[] { SEARCH_MAILS, READ_MAIL, COUNT_MAILS }), "The model sees each tool of the collection on its own.");
        });
    }

    [Test]
    public void SearchConfluenceBringsReadWebPageAlong()
    {
        Assert.That(this.CreateAppRegistry().NormalizeSelection([SEARCH_CONFLUENCE]), Is.EquivalentTo(new[] { SEARCH_CONFLUENCE, READ_WEB_PAGE }), "The search only finds pages; without Read Web Page the model could not open a single result.");
    }

    [TestCase(READ_WEB_PAGE)]
    [TestCase(WEB_SEARCH)]
    public void OtherToolsBringNothingAlong(string toolId)
    {
        Assert.That(this.CreateAppRegistry().NormalizeSelection([toolId]), Is.EquivalentTo(new[] { toolId }), "Only the search of the wiki depends on another tool. The reader in particular does not pull a search in.");
    }

    [Test]
    public void SemanticSearchIsNeverPartOfASelection()
    {
        Assert.That(this.CreateAppRegistry().NormalizeSelection([SEMANTIC_SEARCH, WEB_SEARCH]), Is.EquivalentTo(new[] { WEB_SEARCH }), "Semantic Search offers itself from the data sources of a chat. A template or a plugin naming it would put a tool on the security card that the selection has no say over.");
    }

    [Test]
    public void AnUnknownToolStaysInTheSelection()
    {
        var registry = this.CreateAppRegistry();

        Assert.Multiple(() =>
        {
            Assert.That(registry.NormalizeSelection(["tool_of_a_plugin"]), Is.EquivalentTo(new[] { "tool_of_a_plugin" }), "The tool may arrive with a plugin installed later.");
            Assert.That(registry.ExpandSelection(["tool_of_a_plugin"]), Is.EquivalentTo(new[] { "tool_of_a_plugin" }));
            Assert.That(registry.IsKnown("tool_of_a_plugin"), Is.False);
            Assert.That(registry.IsKnown(MAILBOXES), Is.True, "An assistant may name a collection.");
        });
    }

    [Test]
    public void NormalizingTwiceChangesNothing()
    {
        var registry = this.CreateAppRegistry();
        var once = registry.NormalizeSelection([SEARCH_CONFLUENCE, WEB_SEARCH, READ_MAIL]);

        Assert.That(registry.NormalizeSelection(once), Is.EquivalentTo(once), "The selection fields normalize whatever they receive, including a selection they normalized themselves a moment ago.");
    }

    [Test]
    public void ATwiceSelectedCollectionRunsOnce()
    {
        var registry = this.CreateAppRegistry();

        Assert.Multiple(() =>
        {
            Assert.That(registry.NormalizeSelection([WEB_SEARCH, WEB_SEARCH]), Has.Count.EqualTo(1));
            Assert.That(registry.NormalizeSelection([MAILBOXES, SEARCH_MAILS, READ_MAIL]), Has.Count.EqualTo(1), "The collection and two of its tools are the same selection.");
        });
    }

    [Test]
    public void TheSelectionPassedInStaysUntouched()
    {
        HashSet<string> selected = [SEARCH_CONFLUENCE, READ_MAIL];
        this.CreateAppRegistry().NormalizeSelection(selected);

        Assert.That(selected, Is.EquivalentTo(new[] { SEARCH_CONFLUENCE, READ_MAIL }), "The caller's set, such as the tools of a stored chat template, must not change behind its back.");
    }

    [Test]
    public async Task TheCatalogShowsACollectionAsOneEntry()
    {
        var catalog = await this.CreateAppRegistry().GetCatalogAsync(AIStudio.Tools.Components.CHAT);
        var mailboxes = catalog.Single(item => item.Id == MAILBOXES);

        Assert.Multiple(() =>
        {
            Assert.That(catalog.Select(item => item.Id), Is.EquivalentTo(new[] { MAILBOXES, WEB_SEARCH, READ_WEB_PAGE, SEARCH_CONFLUENCE }), "Semantic Search is nobody's to select, and the mail tools are one entry.");
            Assert.That(mailboxes.Tools.Select(tool => tool.Definition.Id), Is.EqualTo(new[] { SEARCH_MAILS, READ_MAIL, COUNT_MAILS }), "In the order the collection lists them.");
        });
    }

    [Test]
    public async Task APreselectionNamesTheCollection()
    {
        Assert.That(await this.CreateAppRegistry().FilterSelectableToolIdsAsync(AIStudio.Tools.Components.CHAT, [COUNT_MAILS]), Is.EquivalentTo(new[] { MAILBOXES }), "A launcher naming one mail tool opens the chat with the collection selected.");
    }

    [Test]
    public void TheTokenCountCountsEveryToolOfACollection()
    {
        var counted = this.CreateAppRegistry().FilterToolIdsForProvider(ToolCapableProvider(), [MAILBOXES], OutboundDataRestriction.UNRESTRICTED);

        Assert.That(counted, Is.EquivalentTo(new[] { SEARCH_MAILS, READ_MAIL, COUNT_MAILS }), "The model is offered each tool on its own, and each costs tokens.");
    }

    [Test]
    public void TheDefaultsNameTheCollection()
    {
        var registry = this.CreateAppRegistry();
        this.SettingsManager.ConfigurationData.Tools.DefaultToolIdsByComponent[nameof(AIStudio.Tools.Components.CHAT)] = [SEARCH_MAILS, SEMANTIC_SEARCH];

        Assert.That(registry.GetDefaultToolIds(AIStudio.Tools.Components.CHAT), Is.EquivalentTo(new[] { MAILBOXES }), "Defaults stored before the collection existed preselect it.");
    }

    /// <summary>
    /// The tools of the app as far as selecting them goes, with the mailbox collection.
    /// </summary>
    private ToolRegistry CreateAppRegistry() => this.CreateRegistry(
        [new MailboxToolCollection()],
        new TestTool(Definition(WEB_SEARCH)),
        new TestTool(Definition(READ_WEB_PAGE)),
        new TestTool(Definition(SEARCH_CONFLUENCE)),
        new TestTool(Definition(SEMANTIC_SEARCH, activation: ToolActivation.CONTEXT)),
        new TestTool(Definition(SEARCH_MAILS)),
        new TestTool(Definition(READ_MAIL)),
        new TestTool(Definition(COUNT_MAILS)));
}