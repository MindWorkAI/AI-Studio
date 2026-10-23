using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.SemanticSearch;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks that the model learns beforehand which tools end the web search of a chat.
/// </summary>
/// <remarks>
/// Once internal content enters a chat, it is no longer offered the web search. A model which does
/// not know that reads its mails or the wiki first and finds the search gone when it needs it. The
/// instructions of the tools which bring such content in therefore tell it to search the web first
/// when a task needs both, and they tell it what is left once a chat is restricted.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ToolOrderInstructionsTests : ToolRegistryTestBase
{
    private static readonly Uri WIKI = new("https://wiki.example.org/confluence/");

    [Test]
    public void TheWebSearchLeavesTheOrderToTheToolsWhichEndIt()
    {
        var instructions = new WebSearchTool([], null!, null!, NullLogger<WebSearchTool>.Instance).GetDefinition().SystemPromptInstructions;

        Assert.Multiple(() =>
        {
            foreach (var toolId in new[] { ToolSelectionRules.SEARCH_MAILS_TOOL_ID, ToolSelectionRules.READ_MAIL_TOOL_ID, ToolSelectionRules.COUNT_MAILS_TOOL_ID, ToolSelectionRules.SEARCH_CONFLUENCE_TOOL_ID, ToolSelectionRules.SEMANTIC_SEARCH_TOOL_ID })
                Assert.That(instructions, Does.Not.Contain($"`{toolId}`"), "The web search would name tools the request may not offer, and each of these words the rule itself.");
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task TheMailSearchSaysSoWhenTheRequestOffersTheWebSearch(bool offersWebSearch)
    {
        var tool = new SearchMailsTool(null!, null!, null!, null!, NullLogger<SearchMailsTool>.Instance);
        string[] candidates = offersWebSearch ? [ToolSelectionRules.SEARCH_MAILS_TOOL_ID, ToolSelectionRules.WEB_SEARCH_TOOL_ID] : [ToolSelectionRules.SEARCH_MAILS_TOOL_ID];

        var instructions = await tool.ResolveSystemPromptInstructionsAsync(tool.GetDefinition(), this.ResolutionContext(new ChatThread(), candidates));

        Assert.That(instructions.Contains("search the web before you use the mail tools", StringComparison.Ordinal), Is.EqualTo(offersWebSearch));
    }

    [Test]
    public void TheMailSearchSpeaksForTheWholeMailboxCollection()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new SearchMailsTool(null!, null!, null!, null!, NullLogger<SearchMailsTool>.Instance).GetDefinition().SystemPromptInstructions, Does.Contain("search the web before you use the mail tools"), "The token count assumes a request which offers the web search.");
            Assert.That(new CountMailsTool(null!, null!, null!, null!, NullLogger<CountMailsTool>.Instance).GetDefinition().SystemPromptInstructions, Does.Not.Contain("web_search"), "The collection is always offered together, so the rule would stand twice in the same request.");
        });
    }

    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, true)]
    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, true)]
    [TestCase(OutboundDataRestriction.UNRESTRICTED, false)]
    public async Task TheConfluenceSearchSaysSoWhenItEndsTheWebSearch(OutboundDataRestriction internalPageRestriction, bool expected)
    {
        this.SettingsManager.ConfigurationData.Tools.Settings[ToolSelectionRules.READ_WEB_PAGE_TOOL_ID] = new() { ["internalPageOutboundDataRestriction"] = internalPageRestriction.ToString() };
        var tool = new ConfluenceSearchTool(null!, null!, this.CreateToolSettingsService());

        var instructions = await tool.ResolveSystemPromptInstructionsAsync(tool.GetDefinition(), this.ResolutionContext(new ChatThread()));

        Assert.That(instructions.Contains("search the web before you search the wiki", StringComparison.Ordinal), Is.EqualTo(expected), "Without a restriction, the search leaves the web search in place, and the rule would only cost the model a detour.");
    }

    [Test]
    public async Task TheConfluenceSearchLeavesTheOrderOutWithoutAWebSearch()
    {
        var tool = new ConfluenceSearchTool(null!, null!, this.CreateToolSettingsService());

        var instructions = await tool.ResolveSystemPromptInstructionsAsync(tool.GetDefinition(), this.ResolutionContext(new ChatThread(), ToolSelectionRules.SEARCH_CONFLUENCE_TOOL_ID, ToolSelectionRules.READ_WEB_PAGE_TOOL_ID));

        Assert.That(instructions, Does.Not.Contain("web_search"), "The request does not offer the web search, so a rule about it would only confuse the model.");
    }

    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, true, true)]
    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, true, true)]
    [TestCase(OutboundDataRestriction.UNRESTRICTED, true, false)]
    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, false, false)]
    public async Task TheSemanticSearchSaysSoWhenItEndsTheWebSearch(OutboundDataRestriction restriction, bool offersWebSearch, bool expected)
    {
        var dataSource = new DataSourceLocalDirectory { Id = "6f1a8c3e-9d2b-4e7a-b4c5-1e8d3f6a9c72", Name = "Project files", OutboundDataRestriction = restriction };
        this.SettingsManager.ConfigurationData.DataSources.Add(dataSource);
        var chat = new ChatThread { DataSourceOptions = new() { PreselectedDataSourceIds = [dataSource.Id] } };
        var tool = new SemanticSearchTool(this.SettingsManager, null!, null!, null!, NullLogger<SemanticSearchTool>.Instance);
        string[] candidates = offersWebSearch ? [ToolSelectionRules.SEMANTIC_SEARCH_TOOL_ID, ToolSelectionRules.WEB_SEARCH_TOOL_ID] : [ToolSelectionRules.SEMANTIC_SEARCH_TOOL_ID];

        var instructions = await tool.ResolveSystemPromptInstructionsAsync(tool.GetDefinition(), this.ResolutionContext(chat, candidates));

        Assert.That(instructions.Contains("search the web before you search the data sources", StringComparison.Ordinal), Is.EqualTo(expected), "Without a restriction, a search leaves the web search in place, and the rule would only cost the model a detour.");
    }

    [Test]
    public void OnlyTheDataSourcesTheModelMaySearchCount()
    {
        var open = new DataSourceLocalFile { Id = "3c9e1a7f-2b5d-4f8c-a6e1-9d4b7c2a5f13", Name = "Press releases", OutboundDataRestriction = OutboundDataRestriction.UNRESTRICTED };
        var closed = new DataSourceLocalDirectory { Id = "7b4d2f9a-5e1c-4a6b-8d3f-2c9a6e1b4d58", Name = "Project files" };
        var settings = new Data { DataSources = [open, closed] };
        var selected = new ChatThread { DataSourceOptions = new() { PreselectedDataSourceIds = [open.Id] } };
        var automatic = new ChatThread { DataSourceOptions = new() { AutomaticDataSourceSelection = true } };

        Assert.Multiple(() =>
        {
            Assert.That(SemanticSearchTool.EndsWebSearch(this.ResolutionContext(selected), settings), Is.False, "The project files are not selected, so the model cannot find anything in them.");
            Assert.That(SemanticSearchTool.EndsWebSearch(this.ResolutionContext(automatic), settings), Is.True, "With the automatic selection, the model may search the project files as well.");
        });
    }

    [Test]
    public void TheSemanticSearchCountsWithTheRule()
    {
        var instructions = new SemanticSearchTool(null!, null!, null!, null!, null!).GetDefinition().SystemPromptInstructions;
        Assert.That(instructions, Does.Contain("search the web before you search the data sources"), "The token count assumes a request which offers the web search and a data source which ends it.");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ReadWebPageSaysSoWhenAnInternalPageEndsTheWebSearch(bool internalPageEndsWebSearch)
    {
        var instructions = ReadWebPageTool.BuildSystemPromptInstructions(FreeAddressChoice.OFF, OutboundDataRestriction.UNRESTRICTED, WIKI, internalPageEndsWebSearch: internalPageEndsWebSearch);
        Assert.That(instructions.Contains("search the web before you read such a page", StringComparison.Ordinal), Is.EqualTo(internalPageEndsWebSearch));
    }

    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, true, true, false, true, TestName = "An internal page of the wiki ends the web search")]
    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, true, false, true, true, TestName = "An internal page on an allowed private host ends the web search")]
    [TestCase(OutboundDataRestriction.UNRESTRICTED, true, true, true, false, TestName = "Without a restriction, an internal page leaves the web search in place")]
    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, false, true, true, false, TestName = "A request without a web search has none to lose")]
    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, true, false, false, false, TestName = "Without a wiki and without allowed private hosts, no internal page can be read")]
    public void ReadWebPageWarnsOnlyWhenAnInternalPageCanEndTheWebSearch(OutboundDataRestriction internalPageRestriction, bool offersWebSearch, bool hasWiki, bool hasAllowedPrivateHosts, bool expected)
    {
        var endsWebSearch = ReadWebPageTool.InternalPageEndsWebSearch(internalPageRestriction, offersWebSearch, hasWiki ? WIKI : null, hasAllowedPrivateHosts ? new[] { "intranet.example.org" } : null);
        Assert.That(endsWebSearch, Is.EqualTo(expected));
    }

    [TestCase("intranet.example.org", true, true)]
    [TestCase("intranet.example.org", false, false)]
    [TestCase("", true, false)]
    public async Task ARequestWarnsOfTheEndOfTheWebSearchOnlyWhenItCanHappen(string allowedPrivateHosts, bool offersWebSearch, bool expected)
    {
        this.SettingsManager.ConfigurationData.Tools.Settings[ToolSelectionRules.READ_WEB_PAGE_TOOL_ID] = new() { ["allowedPrivateHosts"] = allowedPrivateHosts };
        var tool = new ReadWebPageTool(null!, null!, this.CreateToolSettingsService(), NullLogger<ReadWebPageTool>.Instance);
        string[] candidates = offersWebSearch ? [ToolSelectionRules.READ_WEB_PAGE_TOOL_ID, ToolSelectionRules.WEB_SEARCH_TOOL_ID] : [ToolSelectionRules.READ_WEB_PAGE_TOOL_ID];

        var instructions = await tool.ResolveSystemPromptInstructionsAsync(tool.GetDefinition(), this.ResolutionContext(new ChatThread(), candidates));

        Assert.That(instructions.Contains("search the web before you read such a page", StringComparison.Ordinal), Is.EqualTo(expected));
    }

    [Test]
    public async Task EveryToolLearnsWhichOthersTheRequestMayOffer()
    {
        var webSearch = new TestTool(Definition("web_search_test")) { OutboundData = ToolOutboundData.THIRD_PARTY_QUERIES };
        var wiki = new TestTool(Definition("wiki_search_test")) { OutboundData = ToolOutboundData.CONFIGURED_SERVICE };
        var registry = this.CreateRegistry(webSearch, wiki);
        var restrictedChat = new ChatThread();
        restrictedChat.RequireOutboundDataRestriction(new(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataOrigin.CONFLUENCE_WIKI, string.Empty));

        await registry.GetRunnableToolsAsync(this.ResolutionContext(new ChatThread()), ["web_search_test", "wiki_search_test"], mayRunTools: true);
        var unrestricted = wiki.LastInstructionsContext?.CandidateToolIds;
        await registry.GetRunnableToolsAsync(this.ResolutionContext(restrictedChat), ["web_search_test", "wiki_search_test"], mayRunTools: true);
        var restricted = wiki.LastInstructionsContext?.CandidateToolIds;

        Assert.Multiple(() =>
        {
            Assert.That(unrestricted, Is.EquivalentTo(new[] { "web_search_test", "wiki_search_test" }));
            Assert.That(restricted, Is.EquivalentTo(new[] { "wiki_search_test" }), "The chat may no longer search the web, so no tool may tell the model to search it first.");
        });
    }

    [Test]
    public void ARestrictedChatDoesNotRepeatTheOrder()
    {
        var instructions = ReadWebPageTool.BuildSystemPromptInstructions(FreeAddressChoice.OFF, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, WIKI);
        Assert.That(instructions, Does.Not.Contain("search the web before"), "The web search is gone already.");
    }

    [Test]
    public void TheRulesOfARestrictedChatNameTheAllowedPrivateHosts()
    {
        var configuredServices = ReadWebPageTool.BuildSystemPromptInstructions(FreeAddressChoice.ON, OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, WIKI, ["intranet.example.org", "*.corp.example.org"]);
        var linksFromChat = ReadWebPageTool.BuildSystemPromptInstructions(FreeAddressChoice.ON, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, wiki: null, ["intranet.example.org"]);

        Assert.Multiple(() =>
        {
            Assert.That(configuredServices, Does.Contain($"only reads pages of the wiki at {WIKI} or on the internal hosts intranet.example.org, *.corp.example.org, whatever the rules above allow"));
            Assert.That(linksFromChat, Does.Contain("or a page on the internal hosts intranet.example.org, whatever the rules above allow"), "Without a wiki, the allowed hosts are the addresses the model may still choose.");
            Assert.That(configuredServices, Does.Contain("This chat holds internal content").And.Not.Contain("content of e-mails"), "The restriction may come from the wiki or an internal page as well.");
        });
    }

    [Test]
    public async Task ARequestNamesTheAllowedPrivateHostsOfTheSettings()
    {
        this.SettingsManager.ConfigurationData.Tools.Settings[ToolSelectionRules.READ_WEB_PAGE_TOOL_ID] = new() { ["allowedPrivateHosts"] = "intranet.example.org, *.corp.example.org", ["freeAddressChoice"] = "ON" };
        var tool = new ReadWebPageTool(null!, null!, this.CreateToolSettingsService(), NullLogger<ReadWebPageTool>.Instance);
        var chat = new ChatThread();
        chat.RequireOutboundDataRestriction(new(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, OutboundDataOrigin.INTERNAL_WEB_PAGE, "intranet.example.org"));

        var instructions = await tool.ResolveSystemPromptInstructionsAsync(tool.GetDefinition(), this.ResolutionContext(chat));

        Assert.That(instructions, Does.Contain("only reads pages on the internal hosts intranet.example.org, *.corp.example.org, whatever the rules above allow"));
    }

    [Test]
    public void WithoutAWikiTheRulesNameNoWikiSearch()
    {
        var withoutWiki = ReadWebPageTool.BuildSystemPromptInstructions(FreeAddressChoice.OFF, OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, wiki: null, ["intranet.example.org"]);
        var withWiki = ReadWebPageTool.BuildSystemPromptInstructions(FreeAddressChoice.OFF, OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, WIKI, ["intranet.example.org"]);

        Assert.Multiple(() =>
        {
            Assert.That(withoutWiki, Does.Contain("only reads pages on the internal hosts intranet.example.org whose URL appears word for word in this conversation, and refuses every other URL."));
            Assert.That(withoutWiki, Does.Not.Contain("wiki search"), "There is no wiki whose hits could be meant.");
            Assert.That(withWiki, Does.Contain("whose URL appears word for word in this conversation, such as the hits of a wiki search, and refuses every other URL."));
        });
    }

    /// <param name="thread">The chat the request continues.</param>
    /// <param name="candidateToolIds">The tools the request may offer; when left out, unknown, so every tool may be there.</param>
    private ToolResolutionContext ResolutionContext(ChatThread thread, params string[] candidateToolIds)
    {
        var provider = ToolCapableProvider();
        return new()
        {
            Provider = provider,
            Component = AIStudio.Tools.Components.CHAT,
            ProviderConfidence = provider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level,
            ChatThread = thread,
            CandidateToolIds = candidateToolIds.Length > 0 ? candidateToolIds.ToHashSet(StringComparer.Ordinal) : null,
        };
    }
}