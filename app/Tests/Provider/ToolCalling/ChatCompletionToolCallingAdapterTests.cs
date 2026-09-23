using System.Runtime.CompilerServices;
using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Provider.OpenAI;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Provider.ToolCalling;

/// <summary>
/// Checks what a round of a tool calling conversation asks for, and what it passes on.
/// </summary>
/// <remarks>
/// Every round of a tool conversation is a request of its own, and every one of them reports what
/// it cost. Only the first one describes what the next question will be sent after: every later
/// round carries the tool calls and their results on top, none of which is sent again once the
/// answer stands. Passing on the last report instead would put the chat at the size of everything
/// the tools returned, which is the one number a person watching their context window must not
/// see as exact.
///
/// What a round asks for is one tool call at a time, wherever the provider lets it ask: a provider
/// which rejects the question fails the whole request, so it is not asked at all.
/// </remarks>
[TestFixture]
public sealed class ChatCompletionToolCallingAdapterTests
{
    private const string FIRST_ROUND_USAGE = """{"choices":[],"usage":{"prompt_tokens":1200,"completion_tokens":20}}""";

    private const string SECOND_ROUND_USAGE = """{"choices":[],"usage":{"prompt_tokens":9800,"completion_tokens":150}}""";

    [Test]
    public async Task OnlyTheFirstRoundPassesOnWhatItsRequestCost()
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
            Assert.That(firstRound, Is.EqualTo(new[] { 1200 }), "The first round's prompt is the conversation up to the question.");
            Assert.That(secondRound, Is.Empty, "The second round's prompt holds the tool result as well, which the next question is not sent with.");
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