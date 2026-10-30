using System.Runtime.CompilerServices;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.Harness;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;
using AIStudio.Tools.Web;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks that a tool call which needs confirmation runs only once the user confirmed it.
/// </summary>
/// <remarks>
/// A call which slips through without confirmation sends what the model wrote to a third party,
/// which is what the confirmation exists to prevent. A call which waits where nobody can answer
/// hangs the request instead. Both have to be ruled out, and an allowance for the chat has to
/// count only where the organization permits it.<br/><br/>
/// The user is played by the streaming event of the answer, which the loop fires once a call waits.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ToolApprovalTests : ToolRegistryTestBase
{
    private const string RESULT = "The search found three pages.";

    [Test]
    public async Task ADeclinedCallDoesNotRun()
    {
        var (tool, ran) = CountingTool(ToolOutboundData.THIRD_PARTY_QUERIES);
        var run = await this.RunAsync(tool, ToolApprovalDecision.DECLINE, calls: 1);

        Assert.Multiple(() =>
        {
            Assert.That(ran(), Is.Zero, "The user said no, so nothing may leave for the search engine.");
            Assert.That(run.Questions, Is.EqualTo(1));
            Assert.That(run.Adapter.RecordedResults, Is.EqualTo(new[] { ToolApprovalRules.DECLINED_BY_USER }), "The model learns why it got nothing, so it does not simply try again.");
            Assert.That(run.Answer.ToolInvocations.Single().Status, Is.EqualTo(ToolInvocationTraceStatus.DECLINED));
            Assert.That(run.Answer.ToolInvocations.Single().Approval, Is.EqualTo(ToolApprovalDecision.DECLINE), "The answer tells the user that they declined, not that nobody could confirm.");
            Assert.That(run.Answer.ToolInvocations.Single().StatusMessage, Is.Empty, "The message for the model stands as the result already; shown twice, it only clutters the trace.");
            Assert.That(run.Answer.PendingToolApproval, Is.Null, "Nothing waits anymore, so the buttons must go.");
        });
    }

    [Test]
    public async Task AConfirmedCallRunsAndTheNextOneAsksAgain()
    {
        var (tool, ran) = CountingTool(ToolOutboundData.THIRD_PARTY_QUERIES);
        var run = await this.RunAsync(tool, ToolApprovalDecision.RUN, calls: 2);

        Assert.Multiple(() =>
        {
            Assert.That(ran(), Is.EqualTo(2));
            Assert.That(run.Questions, Is.EqualTo(2), "A single confirmation holds for a single call.");
            Assert.That(run.Thread.RuntimeApprovedToolCollectionIds, Is.Empty);
            Assert.That(run.Answer.ToolInvocations.Select(invocation => invocation.Approval), Is.EqualTo(new[] { ToolApprovalDecision.RUN, ToolApprovalDecision.RUN }), "The answer keeps saying that the user allowed each call.");
        });
    }

    [Test]
    public async Task AnAllowanceForTheChatSparesLaterQuestions()
    {
        var (tool, ran) = CountingTool(ToolOutboundData.THIRD_PARTY_QUERIES);
        var run = await this.RunAsync(tool, ToolApprovalDecision.RUN_ALWAYS_IN_THIS_CHAT, calls: 2);

        Assert.Multiple(() =>
        {
            Assert.That(ran(), Is.EqualTo(2));
            Assert.That(run.Questions, Is.EqualTo(1), "Allowed for the chat, the tool runs on without asking.");
            Assert.That(run.Thread.RuntimeApprovedToolCollectionIds, Is.EquivalentTo(new[] { TOOL_ID }));
            Assert.That(run.Answer.ToolInvocations.Select(invocation => invocation.Approval), Has.All.EqualTo(ToolApprovalDecision.RUN_ALWAYS_IN_THIS_CHAT), "Also the call which ran without asking says why it could.");
        });
    }

    [Test]
    public async Task AnAllowanceForTheChatCountsOnlyWhereTheOrganizationPermitsIt()
    {
        var (tool, ran) = CountingTool(ToolOutboundData.THIRD_PARTY_QUERIES);
        var thread = new ChatThread { RuntimeAsksForToolApproval = true };
        thread.RuntimeApprovedToolCollectionIds.Add(TOOL_ID);

        var run = await this.RunAsync(tool, ToolApprovalDecision.RUN_ALWAYS_IN_THIS_CHAT, calls: 2, thread, mayApproveForWholeChat: false);

        Assert.Multiple(() =>
        {
            Assert.That(ran(), Is.EqualTo(2));
            Assert.That(run.Questions, Is.EqualTo(2), "Neither an allowance given before nor one given now spares a question.");
            Assert.That(run.QuestionsOfferedTheAllowance, Is.False, "The button for the allowance must not even appear.");
            Assert.That(run.Thread.RuntimeApprovedToolCollectionIds, Is.EquivalentTo(new[] { TOOL_ID }), "Only the one from before is there, and it does not count.");
        });
    }

    [Test]
    public async Task ACallNobodyCanConfirmDoesNotRun()
    {
        var (tool, ran) = CountingTool(ToolOutboundData.MODEL_CHOSEN_ADDRESSES);
        var run = await this.RunAsync(tool, ToolApprovalDecision.RUN, calls: 1, new ChatThread { RuntimeAsksForToolApproval = false });

        Assert.Multiple(() =>
        {
            Assert.That(ran(), Is.Zero);
            Assert.That(run.Questions, Is.Zero, "Nobody sees this thread, so a question would wait forever.");
            Assert.That(run.Answer.ToolInvocations.Single().Status, Is.EqualTo(ToolInvocationTraceStatus.DECLINED));
            Assert.That(run.Answer.ToolInvocations.Single().Approval, Is.EqualTo(ToolApprovalDecision.NONE), "Nobody declined: the user was never asked.");
            Assert.That(run.Adapter.RecordedResults, Is.EqualTo(new[] { ToolApprovalRules.NOBODY_TO_CONFIRM }));
        });
    }

    [Test]
    public async Task ACallNobodyCanConfirmRunsWhenItsToolWasAllowedBeforehand()
    {
        var (tool, ran) = CountingTool(ToolOutboundData.MODEL_CHOSEN_ADDRESSES);
        var thread = new ChatThread { RuntimeAsksForToolApproval = false };
        thread.RuntimeApprovedToolCollectionIds.Add(TOOL_ID);

        var run = await this.RunAsync(tool, ToolApprovalDecision.DECLINE, calls: 1, thread);

        Assert.Multiple(() =>
        {
            Assert.That(ran(), Is.EqualTo(1), "The user allowed the tool before the batch started.");
            Assert.That(run.Questions, Is.Zero);
        });
    }

    [TestCase(ToolOutboundData.NONE)]
    [TestCase(ToolOutboundData.CONFIGURED_SERVICE)]
    public async Task AToolWhichKeepsItsDataInsideRunsWithoutAsking(ToolOutboundData outboundData)
    {
        var (tool, ran) = CountingTool(outboundData);
        var run = await this.RunAsync(tool, ToolApprovalDecision.DECLINE, calls: 1);

        Assert.Multiple(() =>
        {
            Assert.That(ran(), Is.EqualTo(1), "Searching the wiki or the data sources sends nothing to strangers.");
            Assert.That(run.Questions, Is.Zero);
        });
    }

    [Test]
    public async Task AskingForEveryToolAsksAlsoForAToolWhichKeepsItsDataInside()
    {
        var (tool, ran) = CountingTool(ToolOutboundData.CONFIGURED_SERVICE);
        var run = await this.RunAsync(tool, ToolApprovalDecision.DECLINE, calls: 1, mode: ToolApprovalMode.ASK_FOR_EVERY_TOOL);

        Assert.Multiple(() =>
        {
            Assert.That(ran(), Is.Zero, "The user wants to confirm every call, the search of the wiki included.");
            Assert.That(run.Questions, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task NeverAskingRunsACriticalToolWithoutAsking()
    {
        var (tool, ran) = CountingTool(ToolOutboundData.THIRD_PARTY_QUERIES);
        var run = await this.RunAsync(tool, ToolApprovalDecision.DECLINE, calls: 1, new ChatThread { RuntimeAsksForToolApproval = false }, mode: ToolApprovalMode.NEVER_ASK);

        Assert.Multiple(() =>
        {
            Assert.That(ran(), Is.EqualTo(1), "The user allowed every call which data protection permits, also where nobody could confirm one.");
            Assert.That(run.Questions, Is.Zero);
            Assert.That(run.Answer.ToolInvocations.Single().Approval, Is.EqualTo(ToolApprovalDecision.NONE), "Nobody was asked, so the answer claims no confirmation.");
        });
    }

    [TestCase(ToolApprovalMode.ASK_FOR_CRITICAL_TOOLS, ToolOutboundData.THIRD_PARTY_QUERIES, ExpectedResult = true)]
    [TestCase(ToolApprovalMode.ASK_FOR_CRITICAL_TOOLS, ToolOutboundData.CONFIGURED_SERVICE, ExpectedResult = false)]
    [TestCase(ToolApprovalMode.ASK_FOR_EVERY_TOOL, ToolOutboundData.NONE, ExpectedResult = true)]
    [TestCase(ToolApprovalMode.NEVER_ASK, ToolOutboundData.MODEL_CHOSEN_ADDRESSES, ExpectedResult = false)]
    public bool ARunOutOfSightAsksBeforehandForWhatTheModeWouldAskFor(ToolApprovalMode mode, ToolOutboundData outboundData) =>
        ToolApprovalRules.MayRequireApproval(mode, CountingTool(outboundData).Tool);

    [Test]
    public void StoppingWhileACallWaitsStopsTheRequest()
    {
        var (tool, ran) = CountingTool(ToolOutboundData.THIRD_PARTY_QUERIES);
        using var cancellation = new CancellationTokenSource();
        var answer = new ContentText();
        answer.StreamingEvent = async () =>
        {
            if (answer.PendingToolApproval is { IsAnswered: false })
                await cancellation.CancelAsync();
        };

        var context = this.Context(tool, new ChatThread { RuntimeAsksForToolApproval = true }, answer, mayApproveForWholeChat: true);

        Assert.Multiple(() =>
        {
            Assert.That(async () => await Drain(new ToolCallingLoop(NullLogger<ToolCallingLoop>.Instance).RunAsync(new CallingAdapter(calls: 1), context, cancellation.Token)), Throws.InstanceOf<OperationCanceledException>());
            Assert.That(ran(), Is.Zero);
            Assert.That(answer.PendingToolApproval, Is.Null, "A request left behind would keep showing buttons nobody can use.");
        });
    }

    [Test]
    public void ReadWebPageAsksOnlyForAnAddressTheModelChoseItself()
    {
        var tool = new ReadWebPageTool(null!, null!, null!, NullLogger<ReadWebPageTool>.Instance);
        var thread = new ChatThread();
        thread.RuntimeWebAddressesFromTools.Add(WebAddresses.CreateRequestKey(new Uri("https://wiki.example.org/display/HR/Handbook")));

        using var foundByTheWikiSearch = JsonDocument.Parse("""{"url":"https://wiki.example.org/display/HR/Handbook"}""");
        using var madeUp = JsonDocument.Parse("""{"url":"https://attacker.example/?salary=secret"}""");
        using var broken = JsonDocument.Parse("""{"url":42}""");

        Assert.Multiple(() =>
        {
            Assert.That(tool.RequiresApproval(foundByTheWikiSearch.RootElement, thread), Is.False, "The wiki search runs without asking, and so does opening what it found.");
            Assert.That(tool.RequiresApproval(madeUp.RootElement, thread), Is.True, "The address alone can carry data out.");
            Assert.That(tool.RequiresApproval(broken.RootElement, thread), Is.True, "What cannot be read is asked about.");
        });
    }

    private static (TestTool Tool, Func<int> Ran) CountingTool(ToolOutboundData outboundData)
    {
        var count = 0;
        var tool = new TestTool(Definition(), execute: _ =>
        {
            count++;
            return new ToolExecutionResult { TextContent = RESULT };
        })
        {
            OutboundData = outboundData,
        };

        return (tool, () => count);
    }

    private async Task<Run> RunAsync(TestTool tool, ToolApprovalDecision decision, int calls, ChatThread? thread = null, bool mayApproveForWholeChat = true, ToolApprovalMode mode = ToolApprovalMode.ASK_FOR_CRITICAL_TOOLS)
    {
        thread ??= new ChatThread { RuntimeAsksForToolApproval = true };
        var answer = new ContentText();
        var adapter = new CallingAdapter(calls);
        var questions = 0;
        var offeredTheAllowance = false;
        answer.StreamingEvent = () =>
        {
            if (answer.PendingToolApproval is { IsAnswered: false } request)
            {
                questions++;
                offeredTheAllowance |= request.MayApproveForWholeChat;
                request.Answer(decision);
            }

            return Task.CompletedTask;
        };

        await Drain(new ToolCallingLoop(NullLogger<ToolCallingLoop>.Instance).RunAsync(adapter, this.Context(tool, thread, answer, mayApproveForWholeChat, mode)));
        return new Run(adapter, answer, thread, questions, offeredTheAllowance);
    }

    private ToolCallingLoopContext Context(TestTool tool, ChatThread thread, ContentText answer, bool mayApproveForWholeChat, ToolApprovalMode mode = ToolApprovalMode.ASK_FOR_CRITICAL_TOOLS) => new()
    {
        ChatThread = thread,
        RunnableTools = [(tool.GetDefinition(), tool)],
        ToolExecutor = new ToolExecutor(this.CreateToolSettingsService(), NullLogger<ToolExecutor>.Instance),
        Provider = new NoProvider(),
        CurrentAssistantContent = answer,
        ProviderInstanceName = "Test provider",
        ProviderType = LLMProviders.NONE,
        ModelId = "test-model",
        MayApproveToolsForWholeChat = mayApproveForWholeChat,
        ToolApprovalMode = mode,
    };

    private static async Task Drain(IAsyncEnumerable<ContentStreamChunk> chunks)
    {
        await foreach (var _ in chunks)
        {
        }
    }

    private sealed record Run(CallingAdapter Adapter, ContentText Answer, ChatThread Thread, int Questions, bool QuestionsOfferedTheAllowance);

    /// <summary>
    /// A model which calls the test tool once per round, as many rounds as it is told, and then answers.
    /// </summary>
    private sealed class CallingAdapter(int calls) : IToolCallingProviderAdapter
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
            var currentRound = this.round++;
            IReadOnlyList<ToolCallingRequestedCall> requested = currentRound < calls ? [new ToolCallingRequestedCall($"call-{currentRound}", TOOL_ID, "{}", true)] : [];
            yield return ToolCallingStreamEvent.RoundCompleted(new ToolCallingRound(requested.Count is 0 ? "Here is the answer." : string.Empty, requested, []));
        }

        /// <inheritdoc />
        public void RecordAssistantTurn()
        {
        }

        /// <inheritdoc />
        public void RecordToolResult(string callId, string content, bool isError = false) => this.RecordedResults.Add(content);
    }
}