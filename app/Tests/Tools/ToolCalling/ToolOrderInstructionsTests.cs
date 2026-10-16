using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks that the model learns beforehand which tools end the web search of a chat.
/// </summary>
/// <remarks>
/// Once internal content enters a chat, it is no longer offered the web search. A model which does
/// not know that reads its mails or the wiki first and finds the search gone when it needs it. The
/// instructions therefore tell it to search the web first when a task needs both, and they tell it
/// what is left once a chat is restricted.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ToolOrderInstructionsTests : ToolRegistryTestBase
{
    private static readonly Uri WIKI = new("https://wiki.example.org/confluence/");

    [Test]
    public void TheWebSearchNamesEveryToolWhichEndsIt()
    {
        var instructions = new WebSearchTool([], null!, null!, NullLogger<WebSearchTool>.Instance).GetDefinition().SystemPromptInstructions;

        Assert.Multiple(() =>
        {
            foreach (var toolId in new[] { ToolSelectionRules.SEARCH_MAILS_TOOL_ID, ToolSelectionRules.READ_MAIL_TOOL_ID, ToolSelectionRules.COUNT_MAILS_TOOL_ID, ToolSelectionRules.SEARCH_CONFLUENCE_TOOL_ID, ToolSelectionRules.READ_WEB_PAGE_TOOL_ID })
                Assert.That(instructions, Does.Contain($"`{toolId}`"));

            Assert.That(instructions, Does.Contain("search the web first"));
        });
    }

    [Test]
    public void TheMailToolsSayThatTheyMayEndTheWebSearch()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new SearchMailsTool(null!, null!, null!, null!, NullLogger<SearchMailsTool>.Instance).GetDefinition().SystemPromptInstructions, Does.Contain("search the web before you use the mail tools"));
            Assert.That(new CountMailsTool(null!, null!, null!, null!, NullLogger<CountMailsTool>.Instance).GetDefinition().SystemPromptInstructions, Does.Contain("search the web before you use the mail tools"));
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

    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, true)]
    [TestCase(OutboundDataRestriction.UNRESTRICTED, false)]
    public void ReadWebPageSaysSoWhenAnInternalPageEndsTheWebSearch(OutboundDataRestriction internalPageRestriction, bool expected)
    {
        var instructions = ReadWebPageTool.BuildSystemPromptInstructions(FreeAddressChoice.OFF, OutboundDataRestriction.UNRESTRICTED, wiki: null, internalPageRestriction: internalPageRestriction);
        Assert.That(instructions.Contains("search the web before you read such a page", StringComparison.Ordinal), Is.EqualTo(expected));
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

    private ToolResolutionContext ResolutionContext(ChatThread thread)
    {
        var provider = ToolCapableProvider();
        return new()
        {
            Provider = provider,
            Component = AIStudio.Tools.Components.CHAT,
            ProviderConfidence = provider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level,
            ChatThread = thread,
        };
    }
}