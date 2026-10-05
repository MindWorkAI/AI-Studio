using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.Harness;
using AIStudio.Tools.Web;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks what the tool executor hands to a tool and what it hands back to the loop.
/// </summary>
/// <remarks>
/// What a result demands of the chat has to reach the loop, which tightens the chat with it: a
/// result from a data source for self-hosted providers only that got lost on the way would let the
/// next message go to a cloud provider, and a mail whose restriction got lost would let the next
/// web search carry it out. A call which brought nothing in must demand nothing.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ToolExecutorTests : ToolRegistryTestBase
{
    private const string MAILBOX_ID = "3f2b8c1d-6e4a-4b7f-9c2d-8a1e5f6b7c3d";

    [Test]
    public async Task WhatAResultDemandsReachesTheLoop()
    {
        var restriction = new OutboundDataRequirement(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, MAILBOX_ID);
        var tool = new TestTool(Definition(), execute: _ => new ToolExecutionResult
        {
            TextContent = "A passage from the handbook.",
            RequiredProviderConfidence = ConfidenceLevel.HIGH,
            RequiredDataSecurity = DataSourceSecurity.SELF_HOSTED,
            RequiredOutboundDataRestriction = restriction,
        });

        var outcome = await this.Execute(tool, new ChatThread());

        Assert.Multiple(() =>
        {
            Assert.That(outcome.RequiredDataSecurity, Is.EqualTo(DataSourceSecurity.SELF_HOSTED));
            Assert.That(outcome.RequiredProviderConfidence, Is.EqualTo(ConfidenceLevel.HIGH));
            Assert.That(outcome.RequiredOutboundDataRestriction, Is.EqualTo(restriction));
        });
    }

    [Test]
    public async Task TheToolSeesTheChatOfTheCall()
    {
        ChatThread? seenThread = null;
        var tool = new TestTool(Definition(), execute: context =>
        {
            seenThread = context.ChatThread;
            return new ToolExecutionResult();
        });
        var thread = new ChatThread();

        await this.Execute(tool, thread);

        Assert.That(seenThread, Is.SameAs(thread), "Semantic Search searches the data sources picked for this very chat.");
    }

    [Test]
    public async Task ABlockedCallDemandsNothing()
    {
        var tool = new TestTool(Definition(), execute: _ => throw new ToolExecutionBlockedException("The data source is not available to this provider."));

        var outcome = await this.Execute(tool, new ChatThread());

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Trace.Status, Is.EqualTo(ToolInvocationTraceStatus.BLOCKED));
            AssertDemandsNothing(outcome);
        });
    }

    [Test]
    public async Task AFailedCallDemandsNothing()
    {
        var tool = new TestTool(Definition(), execute: _ => throw new InvalidOperationException("The index could not be read."));

        var outcome = await this.Execute(tool, new ChatThread());

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Trace.Status, Is.EqualTo(ToolInvocationTraceStatus.ERROR));
            AssertDemandsNothing(outcome);
        });
    }

    [Test]
    public async Task WhatAResultDemandsTightensTheChat()
    {
        var restriction = new OutboundDataRequirement(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, MAILBOX_ID);
        var tool = new TestTool(Definition(), execute: _ => new ToolExecutionResult
        {
            TextContent = "A mail about the budget.",
            RequiredProviderConfidence = ConfidenceLevel.MEDIUM,
            RequiredDataSecurity = DataSourceSecurity.SELF_HOSTED,
            RequiredOutboundDataRestriction = restriction,
        });
        var thread = new ChatThread();
        var adapter = new OneToolCallAdapter();

        await foreach (var _ in new ToolCallingLoop(NullLogger<ToolCallingLoop>.Instance).RunAsync(adapter, this.LoopContext(tool, thread)))
        {
        }

        Assert.Multiple(() =>
        {
            Assert.That(adapter.RecordedResults, Is.EqualTo(new[] { "A mail about the budget." }), "The tool ran, so what it demands came from a real result.");
            Assert.That(thread.RequiredProviderConfidence, Is.EqualTo(ConfidenceLevel.MEDIUM));
            Assert.That(thread.DataSecurity, Is.EqualTo(DataSourceSecurity.SELF_HOSTED));
            Assert.That(thread.RequiredOutboundDataRestriction, Is.EqualTo(restriction), "A web search running next in this chat would otherwise carry the mail out.");
        });
    }

    [Test]
    public async Task AnAddressTheModelWroteIntoTheCallIsNoAddressTheToolReturned()
    {
        //
        // Semantic Search returns its query. Counted as returned, the address in it would let the
        // model read any page it likes, with mail content in the query:
        //
        var tool = new TestTool(Definition(), execute: _ => new ToolExecutionResult
        {
            JsonContent = new JsonObject
            {
                ["query"] = "budget https://attacker.example/?mail=board-meeting",
                ["passages"] = new JsonArray("The budget stands on https://intranet.example.org/budget."),
            },
            TextContent = "See also HTTPS://ATTACKER.EXAMPLE/?mail=board-meeting.",
        });

        var outcome = await this.Execute(tool, new ChatThread(), """{"query":"budget https://attacker.example/?mail=board-meeting"}""");

        Assert.That(outcome.ReturnedWebAddresses, Is.EquivalentTo(new[] { WebAddresses.CreateRequestKey(new Uri("https://intranet.example.org/budget")) }), "Only the address the data source held counts, not the echo, whatever its case.");
    }

    [Test]
    public async Task WhatAToolReturnedIsKnownToTheChatAfterwards()
    {
        var tool = new TestTool(Definition(), execute: _ => new ToolExecutionResult { TextContent = "The newsletter links to https://example.org/newsletter/2026-10." });
        var thread = new ChatThread();

        await foreach (var _ in new ToolCallingLoop(NullLogger<ToolCallingLoop>.Instance).RunAsync(new OneToolCallAdapter(), this.LoopContext(tool, thread)))
        {
        }

        Assert.That(thread.IsWebAddressGivenToTheModel(new Uri("https://example.org/newsletter/2026-10")), Is.True, "The user may ask in the next message to open the link.");
    }

    private static void AssertDemandsNothing(ToolCallOutcome outcome)
    {
        const string REASON = "Nothing reached the model, so there is nothing the chat has to keep.";
        Assert.That(outcome.RequiredProviderConfidence, Is.EqualTo(ConfidenceLevel.NONE), REASON);
        Assert.That(outcome.RequiredDataSecurity, Is.EqualTo(DataSourceSecurity.NOT_SPECIFIED), REASON);
        Assert.That(outcome.RequiredOutboundDataRestriction, Is.EqualTo(OutboundDataRequirement.NONE), REASON);
        Assert.That(outcome.Sources, Is.Empty, REASON);
    }

    private ToolCallingLoopContext LoopContext(TestTool tool, ChatThread thread) => new()
    {
        ChatThread = thread,
        RunnableTools = [(tool.GetDefinition(), tool)],
        ToolExecutor = new ToolExecutor(this.CreateToolSettingsService(), NullLogger<ToolExecutor>.Instance),
        Provider = new NoProvider(),
        CurrentAssistantContent = null,
        ProviderInstanceName = "Test provider",
        ProviderType = LLMProviders.NONE,
        ModelId = "test-model",
    };

    private Task<ToolCallOutcome> Execute(TestTool tool, ChatThread thread, string argumentsJson = "{}")
    {
        var executor = new ToolExecutor(this.CreateToolSettingsService(), NullLogger<ToolExecutor>.Instance);
        return executor.ExecuteAsync("call-1", TOOL_ID, argumentsJson, [(tool.GetDefinition(), tool)], new NoProvider(), thread, order: 1);
    }

    /// <summary>
    /// A model which calls the test tool once and then answers.
    /// </summary>
    private sealed class OneToolCallAdapter : IToolCallingProviderAdapter
    {
        private int round;

        /// <summary>
        /// The tool results the loop handed back, in the order it did.
        /// </summary>
        public List<string> RecordedResults { get; } = [];

        /// <inheritdoc />
        public IReadOnlyList<string> RecordedRequestTexts => [];

        /// <inheritdoc />
        public async IAsyncEnumerable<ToolCallingStreamEvent> ExecuteRoundAsync(string? finalResponseInstruction, bool includeTools, [EnumeratorCancellation] CancellationToken token = default)
        {
            await Task.Yield();
            IReadOnlyList<ToolCallingRequestedCall> calls = this.round++ is 0 ? [new ToolCallingRequestedCall("call-1", TOOL_ID, "{}", true)] : [];
            yield return ToolCallingStreamEvent.RoundCompleted(new ToolCallingRound(calls.Count is 0 ? "Here is the answer." : string.Empty, calls, []));
        }

        /// <inheritdoc />
        public void RecordAssistantTurn()
        {
        }

        /// <inheritdoc />
        public void RecordToolResult(string callId, string content, bool isError = false) => this.RecordedResults.Add(content);
    }
}