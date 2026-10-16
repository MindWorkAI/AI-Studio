using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks which tools a chat may still use once it read from a mailbox.
/// </summary>
/// <remarks>
/// Every argument a model writes may carry the mail it read. A web search would send it to a
/// search engine, a web page address to whoever runs the server. A chat restricted by its mailbox
/// must therefore neither be offered such a tool nor run it, while the services configured in
/// AI Studio stay allowed on every level.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ToolOutboundDataTests : ToolRegistryTestBase
{
    private const string MAILBOX_ID = "6e3a9d2f-1b7c-4f8e-a5d4-9c2b7e1f3a6d";

    [TestCase(OutboundDataRestriction.UNRESTRICTED, ToolOutboundData.NONE, true)]
    [TestCase(OutboundDataRestriction.UNRESTRICTED, ToolOutboundData.CONFIGURED_SERVICE, true)]
    [TestCase(OutboundDataRestriction.UNRESTRICTED, ToolOutboundData.THIRD_PARTY_QUERIES, true)]
    [TestCase(OutboundDataRestriction.UNRESTRICTED, ToolOutboundData.MODEL_CHOSEN_ADDRESSES, true)]
    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, ToolOutboundData.NONE, true)]
    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, ToolOutboundData.CONFIGURED_SERVICE, true)]
    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, ToolOutboundData.THIRD_PARTY_QUERIES, false)]
    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, ToolOutboundData.MODEL_CHOSEN_ADDRESSES, false)]
    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, ToolOutboundData.NONE, true)]
    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, ToolOutboundData.CONFIGURED_SERVICE, true)]
    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, ToolOutboundData.THIRD_PARTY_QUERIES, false)]
    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, ToolOutboundData.MODEL_CHOSEN_ADDRESSES, false)]
    public void WhereAToolSendsDataDecidesWhetherItMayRun(OutboundDataRestriction restriction, ToolOutboundData outboundData, bool expected)
    {
        var tool = new TestTool(Definition()) { OutboundData = outboundData };
        Assert.That(ToolSelectionRules.IsOutboundDataAllowed(restriction, tool), Is.EqualTo(expected));
    }

    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT)]
    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)]
    public void AToolWhichKeepsToTheRestrictionItselfMayRun(OutboundDataRestriction restriction)
    {
        var tool = new TestTool(Definition()) { OutboundData = ToolOutboundData.MODEL_CHOSEN_ADDRESSES, EnforcesOutboundDataRestriction = true };
        Assert.That(ToolSelectionRules.IsOutboundDataAllowed(restriction, tool), Is.True, "Read Web Page tells the addresses of the chat from others on its own.");
    }

    [Test]
    public void ALevelThisVersionDoesNotKnowCountsAsStrict()
    {
        var tool = new TestTool(Definition()) { OutboundData = ToolOutboundData.THIRD_PARTY_QUERIES };
        Assert.That(ToolSelectionRules.IsOutboundDataAllowed((OutboundDataRestriction)99, tool), Is.False);
    }

    [Test]
    public void AToolWhichSaysNothingCountsAsTheMostOpenKind()
    {
        IToolImplementation tool = new SilentTool();
        Assert.Multiple(() =>
        {
            Assert.That(tool.OutboundData, Is.EqualTo(ToolOutboundData.MODEL_CHOSEN_ADDRESSES));
            Assert.That(tool.EnforcesOutboundDataRestriction, Is.False);
        });
    }

    [Test]
    public async Task ARestrictedChatIsNotOfferedAToolWhichGoesTooFar()
    {
        var webSearch = new TestTool(Definition("web_search_test")) { OutboundData = ToolOutboundData.THIRD_PARTY_QUERIES };
        var wiki = new TestTool(Definition("wiki_search_test")) { OutboundData = ToolOutboundData.CONFIGURED_SERVICE };
        var registry = this.CreateRegistry(webSearch, wiki);

        var restricted = await this.OfferedToolIds(registry, RestrictedChat(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES));
        var unrestricted = await this.OfferedToolIds(registry, new ChatThread());

        Assert.Multiple(() =>
        {
            Assert.That(restricted, Is.EqualTo(new[] { "wiki_search_test" }), "The configured wiki stays allowed, the web search is left out.");
            Assert.That(unrestricted, Is.EquivalentTo(new[] { "web_search_test", "wiki_search_test" }), "A chat which read no mailbox gets every tool it selected.");
        });
    }

    [TestCase(OutboundDataRestriction.UNRESTRICTED)]
    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT)]
    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)]
    public async Task TheToolsCountedForARequestAreTheOnesItOffers(OutboundDataRestriction restriction)
    {
        //
        // The token count below the message field and the tools a chat records for its request
        // both come from FilterToolIdsForProvider. A tool which the request leaves out, but which
        // the count still counts, makes the number wrong in exactly the chats which read mails:
        //
        var webSearch = new TestTool(Definition("web_search_test")) { OutboundData = ToolOutboundData.THIRD_PARTY_QUERIES };
        var wiki = new TestTool(Definition("wiki_search_test")) { OutboundData = ToolOutboundData.CONFIGURED_SERVICE };
        var registry = this.CreateRegistry(webSearch, wiki);
        var thread = restriction is OutboundDataRestriction.UNRESTRICTED ? new ChatThread() : RestrictedChat(restriction);

        var counted = registry.FilterToolIdsForProvider(ToolCapableProvider(), ["web_search_test", "wiki_search_test"], thread.RequiredOutboundDataRestriction.Restriction);
        var offered = await this.OfferedToolIds(registry, thread);

        Assert.That(counted, Is.EquivalentTo(offered));
    }

    [Test]
    public async Task AToolWhichKeepsToTheRestrictionItselfIsOffered()
    {
        var readWebPage = new TestTool(Definition()) { OutboundData = ToolOutboundData.MODEL_CHOSEN_ADDRESSES, EnforcesOutboundDataRestriction = true };

        var offered = await this.OfferedToolIds(this.CreateRegistry(readWebPage), RestrictedChat(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT));

        Assert.That(offered, Is.EqualTo(new[] { TOOL_ID }));
    }

    [Test]
    public async Task ACallWhichGoesTooFarIsTurnedDownBeforeTheToolRuns()
    {
        this.SettingsManager.ConfigurationData.Mailboxes.Add(new DataSourceMailbox { Id = MAILBOX_ID, Name = "Work mailbox" });
        var hasRun = false;
        var webSearch = new TestTool(Definition(), execute: _ =>
        {
            hasRun = true;
            return new ToolExecutionResult { TextContent = "Results about the budget." };
        }) { OutboundData = ToolOutboundData.THIRD_PARTY_QUERIES };

        //
        // The request offered the tool while the chat was still unrestricted. Then a mail tool
        // brought in the mail, and the model calls the web search with it in the same request:
        //
        var executor = new ToolExecutor(this.CreateToolSettingsService(), NullLogger<ToolExecutor>.Instance);
        var outcome = await executor.ExecuteAsync("call-1", TOOL_ID, "{}", [(webSearch.GetDefinition(), webSearch)], new NoProvider(), RestrictedChat(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT), order: 1);

        Assert.Multiple(() =>
        {
            Assert.That(hasRun, Is.False, "The mail would already have left AI Studio.");
            Assert.That(outcome.Trace.Status, Is.EqualTo(ToolInvocationTraceStatus.BLOCKED));
            Assert.That(outcome.Content, Does.Contain("Work mailbox"), "The user learns which mailbox stands in the way.");
            Assert.That(outcome.Content, Does.Contain(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT.GetName()));
        });
    }

    [Test]
    public void AMailboxRemovedSinceIsNotNamed()
    {
        var requirement = new OutboundDataRequirement(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, MAILBOX_ID);
        var message = requirement.GetToolBlockedMessage([new DataSourceMailbox { Id = "another-mailbox", Name = "Private mailbox" }]);

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain("removed since"));
            Assert.That(message, Does.Not.Contain("Private mailbox"), "Another mailbox did not restrict the chat.");
            Assert.That(message, Does.Contain(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES.GetName()), "The restriction stays, so the user still learns why.");
        });
    }

    private static ChatThread RestrictedChat(OutboundDataRestriction restriction)
    {
        var thread = new ChatThread();
        thread.RequireOutboundDataRestriction(new(restriction, MAILBOX_ID));
        return thread;
    }

    private async Task<List<string>> OfferedToolIds(ToolRegistry registry, ChatThread thread)
    {
        var provider = ToolCapableProvider();
        var context = new ToolResolutionContext
        {
            Provider = provider,
            Component = AIStudio.Tools.Components.CHAT,
            ProviderConfidence = provider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level,
            ChatThread = thread,
        };

        var runnableTools = await registry.GetRunnableToolsAsync(context, registry.GetAllDefinitions().Select(x => x.Id), mayRunTools: true);
        return runnableTools.Select(x => x.Definition.Id).ToList();
    }

    /// <summary>
    /// A tool which states nothing about where it sends data.
    /// </summary>
    private sealed class SilentTool : IToolImplementation
    {
        public string ImplementationKey => "silent_tool";

        public IReadOnlySet<string> SensitiveTraceArgumentNames { get; } = new HashSet<string>(StringComparer.Ordinal);

        public ToolDefinition GetDefinition() => Definition("silent_tool");

        public Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, ToolExecutionContext context, CancellationToken token = default) => Task.FromResult(new ToolExecutionResult());
    }
}