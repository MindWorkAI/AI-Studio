using System.Runtime.CompilerServices;
using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Provider.OpenAI;
using AIStudio.Tools.ToolCallingSystem.Harness;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Provider.ToolCalling;

/// <summary>
/// Checks what a round of a tool calling conversation asks for, and what it passes on.
/// </summary>
/// <remarks>
/// Every round of a tool conversation is a request of its own, and every one of them reports what
/// it cost. The adapter passes on each report as it arrives, the line without choices included.
/// Which of them describes the conversation is not its decision: that is the tool calling loop's,
/// which knows which round this is, and is checked in ToolCallingLoopTests.
///
/// What a round asks for is one tool call at a time, wherever the provider lets it ask: a provider
/// which rejects the question fails the whole request, so it is not asked at all.
///
/// What a round sends back of the model's calls is what the provider accepts. A broken call is
/// answered all the same, but with a name and arguments the provider takes, since vLLM rejects the
/// whole request over a single call in its history whose name breaks the rule for function names
/// or whose arguments are not JSON.
/// </remarks>
[TestFixture]
public sealed class ChatCompletionToolCallingAdapterTests
{
    private const string FIRST_ROUND_USAGE = """{"choices":[],"usage":{"prompt_tokens":1200,"completion_tokens":20}}""";

    private const string SECOND_ROUND_USAGE = """{"choices":[],"usage":{"prompt_tokens":9800,"completion_tokens":150}}""";

    [Test]
    public async Task EveryRoundPassesOnWhatItsRequestCost()
    {
        var adapter = Adapter(
        [
            """{"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"call_1","type":"function","function":{"name":"web_search","arguments":"{\"query\":\"weather\"}"}}]}}]}""",
            FIRST_ROUND_USAGE,
            "[DONE]",
        ],
        [
            """{"choices":[{"index":0,"delta":{"content":"It is sunny."}}]}""",
            SECOND_ROUND_USAGE,
            "[DONE]",
        ]);

        var firstRound = await Usages(adapter);

        //
        // What the loop does between two rounds: the model's turn and the tool's result become part
        // of the next request.
        //
        adapter.RecordAssistantTurn();
        adapter.RecordToolResult("call_1", "Sunny, 24 degrees.");

        var secondRound = await Usages(adapter);

        Assert.Multiple(() =>
        {
            Assert.That(firstRound, Is.EqualTo(new[] { 1200 }), "The first round reports the conversation up to the question.");
            Assert.That(secondRound, Is.EqualTo(new[] { 9800 }), "The second round reports its own request, tool result included, and leaves it to the loop to drop.");
        });
    }

    [Test]
    public async Task ARoundWithoutToolCallsPassesItOnAsWell()
    {
        //
        // Offering tools does not mean the model uses them. Then the first round is the only one,
        // and its report is as good as the one of a request which offered none.
        //
        var adapter = Adapter(
        [
            """{"choices":[{"index":0,"delta":{"content":"Hello."}}]}""",
            FIRST_ROUND_USAGE,
            "[DONE]",
        ]);

        Assert.That(await Usages(adapter), Is.EqualTo(new[] { 1200 }));
    }

    [Test]
    public async Task ARoundWhichOffersToolsAsksForOneCallAtATime()
    {
        Assert.That(await SentRequest(mayAskForSequentialToolCalls: true, includeTools: true), Does.Contain("\"parallel_tool_calls\":false"));
    }

    [Test]
    public async Task AProviderWhichRejectsTheQuestionIsNotAskedIt()
    {
        //
        // Hugging Face answers the question with a bad request. Its models may then ask for several
        // calls at once, which the loop works through one by one anyway.
        //
        Assert.That(await SentRequest(mayAskForSequentialToolCalls: false, includeTools: true), Does.Not.Contain("parallel_tool_calls"));
    }

    [Test]
    public async Task ARoundWithoutToolsDoesNotAskAboutToolCalls()
    {
        Assert.That(await SentRequest(mayAskForSequentialToolCalls: true, includeTools: false), Does.Not.Contain("parallel_tool_calls"));
    }

    [Test]
    public async Task AMalformedFunctionNameIsAnsweredButNeverSentBack()
    {
        //
        // What a model behind vLLM once returned. With this name in the history, vLLM rejected the
        // next request, because it breaks the rule for function names:
        //
        const string MALFORMED_NAME = "1,2,3,4,5,6,7,8,9,10,11,12,13,14,15";

        var (call, nextRequest) = await AnswerOneCall(MALFORMED_NAME, "{}", "The tool call was invalid.");

        Assert.Multiple(() =>
        {
            Assert.That(call.IsValid, Is.False, "The call is answered as invalid instead of being run.");
            Assert.That(nextRequest, Does.Not.Contain(MALFORMED_NAME), "The provider would reject the whole request over this name.");
            Assert.That(nextRequest, Does.Contain("\"name\":\"invalid_tool_call\""), "The call itself still goes back, or its result would answer a call the provider does not know.");
        });
    }

    [Test]
    public async Task AWellFormedUnknownNameGoesBackUnchanged()
    {
        //
        // A name which merely misses a tool is a name the provider accepts. Such a call stays valid,
        // so that the executor answers it with which tool is not available: that tells the model
        // more than an invalid call does.
        //
        var (call, nextRequest) = await AnswerOneCall("web_lookup", "{}", "Tool 'web_lookup' is not available.");

        Assert.Multiple(() =>
        {
            Assert.That(call.IsValid, Is.True);
            Assert.That(call.ToolName, Is.EqualTo("web_lookup"));
            Assert.That(nextRequest, Does.Contain("\"name\":\"web_lookup\""));
        });
    }

    [Test]
    public async Task BrokenArgumentsAreAnsweredButNeverSentBack()
    {
        //
        // Up to v0.28, vLLM parses the arguments of every call in the history, and a single one
        // which is not JSON fails the whole request:
        //
        const string BROKEN_ARGUMENTS = """{"query": "weather in Berl""";

        var (call, nextRequest) = await AnswerOneCall("web_search", BROKEN_ARGUMENTS, "The tool call was invalid.");

        Assert.Multiple(() =>
        {
            Assert.That(call.IsValid, Is.False, "The call is answered as invalid instead of being run.");
            Assert.That(nextRequest, Does.Not.Contain("weather in Berl"), "The provider would reject the whole request over these arguments.");
            Assert.That(nextRequest, Does.Contain("\"arguments\":\"{}\""), "The call itself still goes back, with arguments the provider can read.");
            Assert.That(nextRequest, Does.Contain("\"name\":\"web_search\""), "A valid name goes back as it came, even when the arguments next to it are broken.");
        });
    }

    /// <summary>
    /// Runs one round and returns the request it sent, as it goes over the wire.
    /// </summary>
    private static async Task<string> SentRequest(bool mayAskForSequentialToolCalls, bool includeTools)
    {
        ChatCompletionAPIRequest? sent = null;
        var adapter = Adapter(mayAskForSequentialToolCalls, request => sent = request, ["[DONE]"]);
        await foreach (var _ in adapter.ExecuteRoundAsync(null, includeTools))
        {
        }

        return JsonSerializer.Serialize(sent, ProviderJsonOptions.OPTIONS);
    }

    /// <summary>
    /// Runs the next round and returns the prompt of every usage it passed on.
    /// </summary>
    private static async Task<List<int>> Usages(ChatCompletionToolCallingAdapter<ChatCompletionAPIRequest> adapter)
    {
        var usages = new List<int>();
        await foreach (var streamEvent in adapter.ExecuteRoundAsync(null, true))
            if (streamEvent.Delta is { Usage.IsKnown: true } delta)
                usages.Add(delta.Usage.PromptTokens);

        return usages;
    }

    /// <summary>
    /// Runs a round in which the model calls one tool, answers the call, and runs the next round.
    /// </summary>
    /// <param name="functionName">The name the model writes into its call.</param>
    /// <param name="arguments">The arguments the model writes into its call.</param>
    /// <param name="result">The result the call is answered with.</param>
    /// <returns>The call as the loop sees it, and the request of the next round as it goes over the wire.</returns>
    private static async Task<(ToolCallingRequestedCall Call, string NextRequest)> AnswerOneCall(string functionName, string arguments, string result)
    {
        var toolCallLine = JsonSerializer.Serialize(new
        {
            Choices = new[] { new { Index = 0, Delta = new { ToolCalls = new[] { new { Index = 0, Id = "call_1", Type = "function", Function = new { Name = functionName, Arguments = arguments } } } } } },
        }, ProviderJsonOptions.OPTIONS);

        var sent = new List<ChatCompletionAPIRequest>();
        var adapter = Adapter(true, sent.Add,
        [
            toolCallLine,
            "[DONE]",
        ],
        [
            """{"choices":[{"index":0,"delta":{"content":"Here is the answer."}}]}""",
            "[DONE]",
        ]);

        ToolCallingRequestedCall? call = null;
        await foreach (var streamEvent in adapter.ExecuteRoundAsync(null, true))
            if (streamEvent.Round is { } round)
                call = round.Calls.Single();

        Assert.That(call, Is.Not.Null, "The first round has to end with the call.");
        var answeredCall = call!;

        // What the loop does between two rounds:
        adapter.RecordAssistantTurn();
        adapter.RecordToolResult(answeredCall.CallId, result, isError: true);

        await foreach (var _ in adapter.ExecuteRoundAsync(null, true))
        {
        }

        return (answeredCall, JsonSerializer.Serialize(sent[^1], ProviderJsonOptions.OPTIONS));
    }

    /// <summary>
    /// Builds an adapter whose requests are answered by the given rounds, one after another.
    /// </summary>
    private static ChatCompletionToolCallingAdapter<ChatCompletionAPIRequest> Adapter(params string[][] rounds) => Adapter(true, _ => { }, rounds);

    /// <summary>
    /// Builds an adapter whose requests are answered by the given rounds, and which hands every
    /// request it sends to the given observer.
    /// </summary>
    private static ChatCompletionToolCallingAdapter<ChatCompletionAPIRequest> Adapter(bool mayAskForSequentialToolCalls, Action<ChatCompletionAPIRequest> sent, params string[][] rounds)
    {
        var nextRound = 0;
        return new(
            (_, _, tools) => Task.FromResult(new ChatCompletionAPIRequest("model-a", [], true) { Tools = tools }),
            new TextMessage("You are a helpful assistant.", "system"),
            new Dictionary<string, object>(),
            [],
            mayAskForSequentialToolCalls,
            [],
            (request, token) =>
            {
                sent(request);
                return Lines(rounds[nextRound++], token);
            },
            _ => [],
            NullLogger.Instance);
    }

    private static async IAsyncEnumerable<ServerSentEvent> Lines(string[] data, [EnumeratorCancellation] CancellationToken token = default)
    {
        foreach (var line in data)
        {
            token.ThrowIfCancellationRequested();
            yield return new ServerSentEvent($"data: {line}", line);
        }

        await Task.CompletedTask;
    }
}