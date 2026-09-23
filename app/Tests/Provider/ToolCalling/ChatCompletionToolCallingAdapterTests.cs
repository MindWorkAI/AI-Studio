using System.Runtime.CompilerServices;

using AIStudio.Provider;
using AIStudio.Provider.OpenAI;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Provider.ToolCalling;

/// <summary>
/// Checks which round of a tool calling conversation passes on what its request cost.
/// </summary>
/// <remarks>
/// Every round of a tool conversation is a request of its own, and every one of them reports what
/// it cost. Only the first one describes what the next question will be sent after: every later
/// round carries the tool calls and their results on top, none of which is sent again once the
/// answer stands. Passing on the last report instead would put the chat at the size of everything
/// the tools returned, which is the one number a person watching their context window must not
/// see as exact.
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
    private static ChatCompletionToolCallingAdapter<ChatCompletionAPIRequest> Adapter(params string[][] rounds)
    {
        var nextRound = 0;
        return new(
            (_, _, _) => Task.FromResult(new ChatCompletionAPIRequest("model-a", [], true)),
            new TextMessage("You are a helpful assistant.", "system"),
            new Dictionary<string, object>(),
            [],
            [],
            (_, token) => Lines(rounds[nextRound++], token),
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