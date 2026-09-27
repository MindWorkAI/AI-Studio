using System.Runtime.CompilerServices;

using AIStudio.Provider;
using AIStudio.Tools;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.Harness;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks what the tool calling loop puts on screen while a model works through its tools.
/// </summary>
/// <remarks>
/// Two things decide whether this loop behaves: every word the model writes has to arrive, and it
/// has to arrive once. Both used to be free -- the round's text was shown at its end, and there
/// was nothing else it could have come from. Now the text streams out while the round runs and
/// the round still reports it afterwards, so the one thing that must never happen is showing it
/// twice. The other side of the same coin is the preamble a model writes before it calls a tool,
/// which was dropped entirely before and is the reason for this whole change.<br/><br/>
/// The adapter is scripted rather than real: what a provider puts on the wire is checked in the
/// accumulator tests, while this is about the loop in between.
/// </remarks>
[TestFixture]
public sealed class ToolCallingLoopTests
{
    private const string PREAMBLE = "Let me look that up.";
    private const string ANSWER = "Here is the answer.";
    private const string SEPARATOR = "\n\n";
    private const string NO_ANSWER = "did not return a final answer";
    
    [Test]
    public async Task APreambleReachesTheUserAlthoughItsRoundOnlyCalledATool()
    {
        //
        // The regression this whole change is about: a model which says what it is about to do
        // before it does it. That sentence never left the provider layer.
        //
        var adapter = new ScriptedAdapter(
            [Text(PREAMBLE), Completed(PREAMBLE, [Call("call-1")])],
            [Text(ANSWER), Completed(ANSWER)]);

        var written = await Run(adapter);
        
        Assert.That(written, Does.StartWith(PREAMBLE), "What the model says before it calls a tool is the first thing the user reads, not something we keep to ourselves.");
    }
    
    [Test]
    public async Task EveryTextIsWrittenExactlyOnce()
    {
        //
        // The one way this can go wrong: the round reports the same text its deltas already
        // carried, and the answer ends up on screen twice.
        //
        var adapter = new ScriptedAdapter(
            [Text(PREAMBLE), Completed(PREAMBLE, [Call("call-1")])],
            [Text(ANSWER), Completed(ANSWER)]);

        var written = await Run(adapter);
        
        Assert.Multiple(() =>
        {
            Assert.That(Occurrences(written, PREAMBLE), Is.EqualTo(1), "The preamble streamed out; the round reporting it again must not put it on screen a second time.");
            Assert.That(Occurrences(written, ANSWER), Is.EqualTo(1), "The same goes for the final answer, which is where a duplicate would be most visible.");
        });
    }
    
    [Test]
    public async Task OnlyARoundWhichSpeaksGetsASeparator()
    {
        //
        // A round which does nothing but call a tool must not leave a gap behind: the separator
        // belongs between two texts, not after every round.
        //
        var afterSpeaking = await Run(new ScriptedAdapter(
            [Text(PREAMBLE), Completed(PREAMBLE, [Call("call-1")])],
            [Text(ANSWER), Completed(ANSWER)]));

        var afterSilence = await Run(new ScriptedAdapter(
            [Completed(string.Empty, [Call("call-1")])],
            [Text(ANSWER), Completed(ANSWER)]));

        Assert.Multiple(() =>
        {
            Assert.That(afterSpeaking, Is.EqualTo($"{PREAMBLE}{SEPARATOR}{ANSWER}"), "Two texts from two rounds are two paragraphs, not one run-on sentence.");
            Assert.That(afterSilence, Is.EqualTo(ANSWER), "Nothing was said before, so there is nothing to separate from.");
        });
    }
    
    [Test]
    public async Task TheLimitMessageOnlyAppearsWhenTheLastRoundSaidNothing()
    {
        //
        // Reaching the limit means the model is asked for a final answer without tools. When it
        // gives one, that answer has already streamed out -- and the message about not having
        // answered has to stay away.
        //
        var answering = await Run(new ScriptedAdapter([..ExhaustTheToolBudget(), [Text(ANSWER), Completed(ANSWER)]]));
        var silent = await Run(new ScriptedAdapter([..ExhaustTheToolBudget(), [Completed(string.Empty)]]));
        
        Assert.Multiple(() =>
        {
            Assert.That(answering, Does.EndWith(ANSWER).And.Not.Contains(NO_ANSWER), "The model answered, so nothing has to be said on its behalf.");
            Assert.That(silent, Does.Contain(NO_ANSWER), "It stayed silent after using up its tools, and silence would look like a hung request.");
        });
    }
    
    [Test]
    public async Task TheNoAnswerMessageOnlyAppearsWhenTheRoundSaidNothing()
    {
        var answering = await Run(new ScriptedAdapter(
            [Completed(string.Empty, [Call("call-1")])],
            [Text(ANSWER), Completed(ANSWER)]));

        var silent = await Run(new ScriptedAdapter(
            [Completed(string.Empty, [Call("call-1")])],
            [Completed(string.Empty)]));
        
        Assert.Multiple(() =>
        {
            Assert.That(answering, Is.EqualTo(ANSWER), "There is an answer, so the fallback message has no place here.");
            Assert.That(silent, Does.Contain(NO_ANSWER), "The tool ran and nothing came of it, which the user has to be told.");
        });
    }
    
    [Test]
    public async Task TheSourcesArriveAlthoughTheFinalTextNoLongerDoes()
    {
        //
        // The last round hands over an empty chunk carrying the sources, because its text went
        // out as deltas. Forget that chunk and the citation links of a web search disappear.
        //
        var source = new Source("Example", "https://example.org/", SourceOrigin.LLM);
        var adapter = new ScriptedAdapter(
            [Completed(string.Empty, [Call("call-1")], [source])],
            [Text(ANSWER), Completed(ANSWER)]);

        var chunks = await Collect(adapter);
        
        Assert.That(chunks.SelectMany(chunk => chunk.Sources).Select(x => x.URL), Does.Contain("https://example.org/"), "The sources of a round reach the caller even when its text does not.");
    }
    
    [Test]
    public async Task ARoundWhichNeverCompletesEndsQuietly()
    {
        //
        // A stream cut off mid-sentence, or a request which failed: the adapter has told the user
        // what went wrong already, so the loop adds nothing of its own.
        //
        var adapter = new ScriptedAdapter([Text(PREAMBLE)]);

        var chunks = await Collect(adapter);
        
        Assert.Multiple(() =>
        {
            Assert.That(string.Concat(chunks.Select(x => x.Content)), Is.EqualTo(PREAMBLE), "What was streamed stays; nothing is taken back.");
            Assert.That(chunks.Select(x => x.Content), Has.None.Contains(NO_ANSWER), "An error message on top of the adapter's own would say the same thing twice.");
        });
    }
    
    [Test]
    public async Task ACallWithoutAnIdEndsTheConversation()
    {
        //
        // The result is correlated by that ID. Inventing one has the next request rejected, so
        // there is nothing to salvage from a round like this.
        //
        var written = await Run(new ScriptedAdapter(
            [Completed(string.Empty, [Call(string.Empty)])],
            [Text(ANSWER), Completed(ANSWER)]));

        Assert.Multiple(() =>
        {
            Assert.That(written, Does.Contain("The tool call was invalid."), "The user learns why the answer stops here.");
            Assert.That(written, Does.Not.Contain(ANSWER), "And the loop does not carry on into a round the provider would refuse.");
        });
    }
    
    [Test]
    public async Task TheModelsTurnIsRecordedOncePerRoundAndBeforeItsResults()
    {
        //
        // The provider has to know about the turn before it is sent results for it, and recording
        // it twice would send the same tool call twice.
        //
        var adapter = new ScriptedAdapter(
            [Text(PREAMBLE), Completed(PREAMBLE, [Call("call-1"), Call("call-2")])],
            [Text(ANSWER), Completed(ANSWER)]);

        await Run(adapter);
        
        Assert.That(adapter.Recordings, Is.EqualTo(new[] { "turn", "result:call-1", "result:call-2" }), "One turn, then its results, in the order the model asked for them.");
    }
    
    [Test]
    public async Task ACancelledStreamStopsTheLoopWhereItIs()
    {
        //
        // What the user sees when they press stop. The provider's stream reader ends quietly on
        // a cancellation rather than throwing, so the round reaches its end without completing --
        // which has to leave the text alone and add nothing to it.
        //
        using var cancellation = new CancellationTokenSource();
        var adapter = new ScriptedAdapter([Text(PREAMBLE), Text(ANSWER), Completed(ANSWER)])
        {
            CancelAfterFirstEvent = cancellation,
        };

        var chunks = await Collect(adapter, cancellation.Token);
        
        Assert.Multiple(() =>
        {
            Assert.That(string.Concat(chunks.Select(x => x.Content)), Is.EqualTo(PREAMBLE), "Everything written before the stop stays, and nothing after it arrives.");
            Assert.That(chunks.Select(x => x.Content), Has.None.Contains(NO_ANSWER), "A stop is not a failure to answer, so it is not reported as one.");
        });
    }

    [Test]
    public async Task OnlyTheFirstRoundsUsageReachesTheAnswer()
    {
        //
        // Every round is a request of its own, and every one of them reports what it cost. Only
        // the first one describes what the next question will be sent after: every later round
        // carries the tool calls and their results on top, none of which is sent again once the
        // answer stands.
        //
        var adapter = new ScriptedAdapter(
            [Usage(1200), Completed(string.Empty, [Call("call-1")])],
            [Text(ANSWER), Usage(9800), Completed(ANSWER)]);

        var usages = (await Collect(adapter)).Where(chunk => chunk.Usage.IsKnown).Select(chunk => chunk.Usage.PromptTokens);

        Assert.That(usages, Is.EqualTo(new[] { 1200 }), "The second round's prompt holds the tool result as well, which the next question is not sent with.");
    }

    [Test]
    public async Task ALaterRoundsUsageLeavesItsTextAndNothingElse()
    {
        //
        // Some providers send the usage next to the last piece of text rather than on a line of
        // its own. Dropping the usage must not drop that text, and a delta which carried nothing
        // but the usage must not turn into an empty chunk of its own.
        //
        var withUsage = await Collect(new ScriptedAdapter(
            [Completed(string.Empty, [Call("call-1")])],
            [Usage(9800), Usage(9800, ANSWER), Completed(ANSWER)]));

        var withoutUsage = await Collect(new ScriptedAdapter(
            [Completed(string.Empty, [Call("call-1")])],
            [Text(ANSWER), Completed(ANSWER)]));

        Assert.Multiple(() =>
        {
            Assert.That(withUsage.Select(x => x.Content), Is.EqualTo(withoutUsage.Select(x => x.Content)), "The same chunks arrive as if the round had reported nothing.");
            Assert.That(withUsage.Select(x => x.Usage.IsKnown), Has.None.True, "And none of them carries the usage on.");
        });
    }

    /// <summary>
    /// As many rounds calling one tool each as it takes to use up the tool budget.
    /// </summary>
    private static List<IReadOnlyList<ToolCallingStreamEvent>> ExhaustTheToolBudget() => Enumerable
        .Range(0, ToolSelectionRules.MAX_TOOL_CALLS)
        .Select(IReadOnlyList<ToolCallingStreamEvent> (round) => [Completed(string.Empty, [Call($"call-{round}")])])
        .ToList();
    
    private static ToolCallingStreamEvent Text(string text) => ToolCallingStreamEvent.TextDelta(text);

    private static ToolCallingStreamEvent Usage(int promptTokens, string text = "") => ToolCallingStreamEvent.TextDelta(new ContentStreamChunk(text, [], TokenUsage.Of(promptTokens)));

    private static ToolCallingStreamEvent Completed(string text, IReadOnlyList<ToolCallingRequestedCall>? calls = null, IReadOnlyList<ISource>? sources = null)
        => ToolCallingStreamEvent.RoundCompleted(new ToolCallingRound(text, calls ?? [], sources ?? []));
    
    private static ToolCallingRequestedCall Call(string callId) => new(callId, "some_tool", "{}", true);
    
    private static async Task<string> Run(ScriptedAdapter adapter) => string.Concat((await Collect(adapter)).Select(chunk => chunk.Content));
    
    private static async Task<List<ContentStreamChunk>> Collect(ScriptedAdapter adapter, CancellationToken token = default)
    {
        var loop = new ToolCallingLoop(NullLogger<ToolCallingLoop>.Instance);
        var chunks = new List<ContentStreamChunk>();
        await foreach (var chunk in loop.RunAsync(adapter, CreateContext(), token))
            chunks.Add(chunk);

        return chunks;
    }
    
    /// <summary>
    /// A context which needs nothing of the application around it.
    /// </summary>
    /// <remarks>
    /// Without an assistant message, every UI call of the context returns right away, which is
    /// what keeps the service provider out of these tests. The tool executor gets no settings
    /// service for the same reason: with no runnable tools, every call ends as blocked long
    /// before any setting is read.
    /// </remarks>
    private static ToolCallingLoopContext CreateContext() => new()
    {
        ChatThread = new(),
        RunnableTools = [],
        ToolExecutor = new(null!, NullLogger<ToolExecutor>.Instance),
        Provider = new NoProvider(),
        CurrentAssistantContent = null,
        ProviderInstanceName = "Test provider",
        ProviderType = LLMProviders.NONE,
        ModelId = "test-model",
    };
    
    private static int Occurrences(string text, string part)
    {
        var count = 0;
        for (var index = text.IndexOf(part, StringComparison.Ordinal); index >= 0; index = text.IndexOf(part, index + part.Length, StringComparison.Ordinal))
            count++;

        return count;
    }
    
    /// <summary>
    /// An adapter which plays back a script of events, one list per round.
    /// </summary>
    private sealed class ScriptedAdapter(params IReadOnlyList<ToolCallingStreamEvent>[] rounds) : IToolCallingProviderAdapter
    {
        private readonly Queue<IReadOnlyList<ToolCallingStreamEvent>> remainingRounds = new(rounds);
        
        /// <summary>
        /// When set, the run is cancelled right after the first event of the first round, the way
        /// a user pressing stop cancels one.
        /// </summary>
        public CancellationTokenSource? CancelAfterFirstEvent { get; init; }
        
        /// <summary>
        /// What the loop recorded, in the order it did.
        /// </summary>
        public List<string> Recordings { get; } = [];
        
        /// <inheritdoc />
        public IReadOnlyList<string> RecordedRequestTexts => [];
        
        /// <inheritdoc />
        public async IAsyncEnumerable<ToolCallingStreamEvent> ExecuteRoundAsync(string? finalResponseInstruction, bool includeTools, [EnumeratorCancellation] CancellationToken token = default)
        {
            await Task.Yield();
            if (this.remainingRounds.Count is 0)
                yield break;

            foreach (var streamEvent in this.remainingRounds.Dequeue())
            {
                //
                // Ending rather than throwing, which is what the shared stream reader does when a
                // cancellation reaches it: it stops reading lines and lets the round end without
                // its completed event.
                //
                if (token.IsCancellationRequested)
                    yield break;

                yield return streamEvent;
                this.CancelAfterFirstEvent?.Cancel();
            }
        }
        
        /// <inheritdoc />
        public void RecordAssistantTurn() => this.Recordings.Add("turn");
        
        /// <inheritdoc />
        public void RecordToolResult(string callId, string content, bool isError = false) => this.Recordings.Add($"result:{callId}");
    }
}