using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.SemanticSearch;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;
using AIStudio.Tools.Web;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks where a chat may still send data once Semantic Search found something in its data sources.
/// </summary>
/// <remarks>
/// A data source holds the documents of the user or of their organization. A web search or an
/// address the model writes itself would carry them to a third party, so a search which found
/// something restricts the chat as a mailbox or the wiki does. By default, the chat keeps the links
/// from the chat. A data source which only self-hosted providers may use allows the configured
/// services only: the internal pages it links to stay readable, a public page does not. The search
/// itself needs indexed data sources and is left to a manual test; these tests start from what the
/// passages of a search demand.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class SemanticSearchOutboundDataRestrictionTests : ToolRegistryTestBase
{
    private const string INTRANET_PAGE = "https://intranet.example.org/projects/budget";
    private const string PUBLIC_PAGE = "https://example.org/press/2026-10";
    private const string CHOSEN_BY_THE_MODEL = "https://attacker.example/?budget=2027";

    private static readonly IDataSource PROJECT_FILES = new DataSourceLocalDirectory { Num = 1, Id = "5e2b8d4f-1c7a-4e9b-a3d6-8f4c2e7b1a59", Name = "Project files", MaxMatches = 10 };
    private static readonly IDataSource INTRANET = new DataSourceERI_V1 { Num = 2, Id = "8a3f6c1e-4d9b-4b2a-9e7c-3d6a1f8b5c27", Name = "Intranet", MaxMatches = 10, SecurityPolicy = DataSourceSecurity.SELF_HOSTED, OutboundDataRestriction = OutboundDataRestriction.UNRESTRICTED };
    private static readonly IDataSource PRESS_RELEASES = new DataSourceLocalFile { Num = 3, Id = "2d7c4a9e-6b1f-4c8d-b5a3-7e2f9c4d1b86", Name = "Press releases", MaxMatches = 10, OutboundDataRestriction = OutboundDataRestriction.UNRESTRICTED };

    private static readonly Func<string, bool> ALLOWED_PRIVATE_HOSTS = host => string.Equals(host, "intranet.example.org", StringComparison.OrdinalIgnoreCase);

    [Test]
    public void ASearchRestrictsTheChatToLinksFromTheChatByDefault()
    {
        var requirements = SemanticSearchTool.GetRequirements([PROJECT_FILES, PRESS_RELEASES], [3, 0], OutboundDataRestriction.UNRESTRICTED);
        Assert.That(requirements.OutboundData, Is.EqualTo(new OutboundDataRequirement(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataOrigin.DATA_SOURCE, PROJECT_FILES.Id)), "The chat names the data source, so the user learns why web search is gone.");
    }

    [Test]
    public void ASearchInADataSourceWithoutRestrictionRestrictsNothing()
    {
        var requirements = SemanticSearchTool.GetRequirements([PROJECT_FILES, PRESS_RELEASES], [0, 2], OutboundDataRestriction.UNRESTRICTED);
        Assert.That(requirements.OutboundData, Is.EqualTo(OutboundDataRequirement.NONE), "The project files found nothing, and the press releases may be read by anybody.");
    }

    [Test]
    public void AnOrganizationMinimumTightensWhatASearchDemands()
    {
        var requirements = SemanticSearchTool.GetRequirements([PRESS_RELEASES], [2], OutboundDataRestriction.ONLY_CONFIGURED_SERVICES);
        Assert.That(requirements.OutboundData.Restriction, Is.EqualTo(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES));
    }

    [Test]
    public void AfterAHitWebSearchIsNotOffered()
    {
        IToolImplementation webSearch = new WebSearchTool([], null!, null!, NullLogger<WebSearchTool>.Instance);
        Assert.That(ToolSelectionRules.IsOutboundDataAllowed(ChatAfterASearch(PROJECT_FILES).RequiredOutboundDataRestriction.Restriction, webSearch), Is.False);
    }

    [Test]
    public async Task AfterAHitAWebSearchIsTurnedDownBeforeItRuns()
    {
        this.SettingsManager.ConfigurationData.DataSources.Add(PROJECT_FILES);

        //
        // The request offered the web search while the chat was still unrestricted. Then Semantic
        // Search brought in the passages, and the model calls the web search in the same request:
        //
        var hasRun = false;
        var webSearch = new TestTool(Definition(), execute: _ =>
        {
            hasRun = true;
            return new ToolExecutionResult { TextContent = "Results about the budget." };
        }) { OutboundData = ToolOutboundData.THIRD_PARTY_QUERIES };

        var executor = new ToolExecutor(this.CreateToolSettingsService(), NullLogger<ToolExecutor>.Instance);
        var outcome = await executor.ExecuteAsync("call-1", TOOL_ID, "{}", [(webSearch.GetDefinition(), webSearch)], new NoProvider(), ChatAfterASearch(PROJECT_FILES), order: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hasRun, Is.False, "The content of the project files would already have left AI Studio.");
            Assert.That(outcome.Trace.Status, Is.EqualTo(ToolInvocationTraceStatus.BLOCKED));
            Assert.That(outcome.Content, Does.Contain("Project files"), "The user learns which data source stands in the way.");
        }
    }

    [TestCase(INTRANET_PAGE, true)]
    [TestCase(PUBLIC_PAGE, true)]
    [TestCase(CHOSEN_BY_THE_MODEL, false)]
    public void AfterAHitTheLinksOfTheHitsAreRead(string address, bool expectedAllowed)
    {
        var chat = ChatAfterASearch(PROJECT_FILES);
        var allowed = ReadWebPageTool.IsAllowedByOutboundDataRestriction(new Uri(address), chat.RequiredOutboundDataRestriction.Restriction, chat, wiki: null, ALLOWED_PRIVATE_HOSTS, out _);
        Assert.That(allowed, Is.EqualTo(expectedAllowed), "The model may follow the links of what it found, but it cannot write an address of its own.");
    }

    [TestCase(INTRANET_PAGE, true)]
    [TestCase(PUBLIC_PAGE, false)]
    [TestCase(CHOSEN_BY_THE_MODEL, false)]
    public void AfterAHitInADataSourceForSelfHostedProvidersOnlyInternalPagesAreRead(string address, bool expectedAllowed)
    {
        var chat = ChatAfterASearch(INTRANET);
        var allowed = ReadWebPageTool.IsAllowedByOutboundDataRestriction(new Uri(address), chat.RequiredOutboundDataRestriction.Restriction, chat, wiki: null, ALLOWED_PRIVATE_HOSTS, out _);
        Assert.That(allowed, Is.EqualTo(expectedAllowed), "An internal page the data source links to stays readable, a public one does not, although the hit names it.");
    }

    /// <summary>
    /// A chat in which Semantic Search found passages in the data source, linking to an internal and to a public page.
    /// </summary>
    private static ChatThread ChatAfterASearch(IDataSource dataSource)
    {
        var thread = new ChatThread();
        thread.RuntimeWebAddressesFromTools.Add(WebAddresses.CreateRequestKey(new Uri(INTRANET_PAGE)));
        thread.RuntimeWebAddressesFromTools.Add(WebAddresses.CreateRequestKey(new Uri(PUBLIC_PAGE)));
        thread.RequireOutboundDataRestriction(SemanticSearchTool.GetRequirements([dataSource], [2], OutboundDataRestriction.UNRESTRICTED).OutboundData);
        return thread;
    }
}