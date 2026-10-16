using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks where a chat may still send data once it searched the Confluence wiki.
/// </summary>
/// <remarks>
/// What the wiki returns is internal to the organization. A web search or an address the model
/// writes itself would carry it to a third party, so a search restricts the chat as a mailbox
/// does. The level is the one Read Web Page applies to every internal page, so a search and the
/// wiki pages opened after it restrict the chat alike. By default, the chat keeps the links from
/// the chat and the wiki itself, and an organization can tighten that to the wiki alone. The
/// search itself needs a real Confluence and is left to a manual test; these tests start from what
/// a successful search demands.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ConfluenceOutboundDataRestrictionTests : ToolRegistryTestBase
{
    private const string WIKI = "https://wiki.example.org/confluence/";
    private const string WIKI_PAGE = "https://wiki.example.org/confluence/display/TEAM/Budget";
    private const string PASTED = "https://example.org/newsletter/2026-10";
    private const string CHOSEN_BY_THE_MODEL = "https://attacker.example/?wiki=budget-2027";

    private static readonly Uri WIKI_URL = new(WIKI);

    private static readonly Func<string, bool> NO_PRIVATE_HOSTS = _ => false;

    [Test]
    public async Task ASearchRestrictsTheChatToLinksFromTheChatByDefault()
    {
        var requirement = ConfluenceSearchTool.GetOutboundDataRequirement(await ReadWebPageTool.ReadInternalPageOutboundDataRestrictionAsync(this.CreateToolSettingsService()));
        Assert.That(requirement, Is.EqualTo(new OutboundDataRequirement(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataOrigin.CONFLUENCE_WIKI, string.Empty)), "The chat names the wiki, so the user learns why web search is gone.");
    }

    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)]
    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT)]
    [TestCase(OutboundDataRestriction.UNRESTRICTED)]
    public async Task ASearchTakesTheLevelOfInternalPages(OutboundDataRestriction configured)
    {
        this.SettingsManager.ConfigurationData.Tools.Settings[ToolSelectionRules.READ_WEB_PAGE_TOOL_ID] = new() { ["internalPageOutboundDataRestriction"] = configured.ToString() };

        var restriction = await ReadWebPageTool.ReadInternalPageOutboundDataRestrictionAsync(this.CreateToolSettingsService());

        Assert.That(restriction, Is.EqualTo(configured), "One setting of Read Web Page decides for the search and for the wiki pages opened after it.");
    }

    [Test]
    public void TheSettingOffersEveryLevel()
    {
        var values = ToolSettingsOptionSources.GetValues(ToolSettingsOptionSources.OUTBOUND_DATA_RESTRICTION);
        Assert.That(values, Is.EquivalentTo(Enum.GetNames<OutboundDataRestriction>()), "The values are the names an organization writes into its configuration.");
    }

    [Test]
    public void AfterASearchWebSearchIsNotOffered()
    {
        IToolImplementation webSearch = new WebSearchTool([], null!, null!, NullLogger<WebSearchTool>.Instance);
        Assert.That(ToolSelectionRules.IsOutboundDataAllowed(ChatAfterASearch().RequiredOutboundDataRestriction.Restriction, webSearch), Is.False);
    }

    [Test]
    public async Task AfterASearchAWebSearchIsTurnedDownBeforeItRuns()
    {
        //
        // The request offered the web search while the chat was still unrestricted. Then the
        // wiki search brought in its results, and the model calls the web search in the same request:
        //
        var hasRun = false;
        var webSearch = new TestTool(Definition(), execute: _ =>
        {
            hasRun = true;
            return new ToolExecutionResult { TextContent = "Results about the budget." };
        }) { OutboundData = ToolOutboundData.THIRD_PARTY_QUERIES };

        var executor = new ToolExecutor(this.CreateToolSettingsService(), NullLogger<ToolExecutor>.Instance);
        var outcome = await executor.ExecuteAsync("call-1", TOOL_ID, "{}", [(webSearch.GetDefinition(), webSearch)], new NoProvider(), ChatAfterASearch(), order: 1);

        Assert.Multiple(() =>
        {
            Assert.That(hasRun, Is.False, "The wiki content would already have left AI Studio.");
            Assert.That(outcome.Trace.Status, Is.EqualTo(ToolInvocationTraceStatus.BLOCKED));
            Assert.That(outcome.Content, Does.Contain("Confluence wiki"));
        });
    }

    [TestCase(WIKI_PAGE, true)]
    [TestCase(PASTED, true)]
    [TestCase(CHOSEN_BY_THE_MODEL, false)]
    public void AfterASearchWikiPagesAndLinksFromTheChatAreRead(string address, bool expectedAllowed)
    {
        var allowed = ReadWebPageTool.IsAllowedByOutboundDataRestriction(new Uri(address), ChatAfterASearch().RequiredOutboundDataRestriction.Restriction, ChatAfterASearch(), WIKI_URL, NO_PRIVATE_HOSTS, out _);
        Assert.That(allowed, Is.EqualTo(expectedAllowed), "The model may follow the wiki's results and the user's links, but it cannot write an address of its own.");
    }

    [TestCase(WIKI_PAGE, true)]
    [TestCase(PASTED, false)]
    [TestCase(CHOSEN_BY_THE_MODEL, false)]
    public void AfterASearchForConfiguredServicesOnlyOnlyTheWikiIsRead(string address, bool expectedAllowed)
    {
        var chat = ChatAfterASearch(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES);
        var allowed = ReadWebPageTool.IsAllowedByOutboundDataRestriction(new Uri(address), chat.RequiredOutboundDataRestriction.Restriction, chat, WIKI_URL, NO_PRIVATE_HOSTS, out _);
        Assert.That(allowed, Is.EqualTo(expectedAllowed), "Not even an address the user pasted is read.");
    }

    private static ChatThread ChatAfterASearch(OutboundDataRestriction internalPageRestriction = OutboundDataRestriction.ONLY_LINKS_FROM_CHAT)
    {
        var thread = new ChatThread
        {
            Blocks =
            [
                new ContentBlock
                {
                    Time = new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero),
                    ContentType = ContentType.TEXT,
                    Content = new ContentText { Text = $"Our wiki has the budget, and {PASTED} has the background. What changed?" },
                    Role = ChatRole.USER,
                },
            ],
        };

        thread.RequireOutboundDataRestriction(ConfluenceSearchTool.GetOutboundDataRequirement(internalPageRestriction));
        return thread;
    }
}