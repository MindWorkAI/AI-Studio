using System.Runtime.CompilerServices;

using AIStudio.Provider;
using AIStudio.Provider.OpenAI;

namespace AIStudio.Tests.Provider.ToolCalling;

/// <summary>
/// Checks what a round of a tool calling conversation with the Responses API passes on about its cost.
/// </summary>
/// <remarks>
/// Every round is a request of its own, and every one of them states at its end what it cost -- as
/// long as OpenAI ran no hosted tool in it. Which round describes the conversation is the tool
/// calling loop's decision, checked in ToolCallingLoopTests.
/// </remarks>
[TestFixture]
public sealed class ResponsesToolCallingAdapterTests
{
    [Test]
    public async Task EveryRoundPassesOnWhatItsRequestCost()
    {
        var adapter = Adapter(
        [
            """{"type":"response.output_item.done","output_index":0,"item":{"type":"function_call","call_id":"call_1","name":"web_search","arguments":"{\"query\":\"weather\"}"}}""",
            """{"type":"response.completed","response":{"id":"resp_1","model":"gpt-5","output":[{"type":"function_call","call_id":"call_1","name":"web_search","arguments":"{\"query\":\"weather\"}"}],"usage":{"input_tokens":1200,"output_tokens":20}}}""",
        ],
        [
            """{"type":"response.output_text.delta","delta":"It is sunny."}""",
            """{"type":"response.completed","response":{"id":"resp_2","model":"gpt-5","output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"It is sunny."}]}],"usage":{"input_tokens":9800,"output_tokens":30}}}""",
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
            Assert.That(firstRound, Is.EqualTo(new[] { 1200 }), "The first round states the conversation up to the question.");
            Assert.That(secondRound, Is.EqualTo(new[] { 9800 }), "The second round states its own request.");
        });
    }

    [Test]
    public async Task ARoundWithAHostedWebSearchPassesOnNothingButItsText()
    {
        var adapter = Adapter(
        [
            """{"type":"response.output_text.delta","delta":"It is sunny."}""",
            """{"type":"response.completed","response":{"id":"resp_1","model":"gpt-5","output":[{"type":"web_search_call","id":"ws_1","status":"completed"},{"type":"message","role":"assistant","content":[{"type":"output_text","text":"It is sunny."}]}],"usage":{"input_tokens":12000,"output_tokens":30}}}""",
        ]);

        var usages = new List<int>();
        var written = new List<string>();
        await foreach (var streamEvent in adapter.ExecuteRoundAsync(null, true))
        {
            if (streamEvent.Delta is not { } delta)
                continue;

            if (delta.Usage.IsKnown)
                usages.Add(delta.Usage.PromptTokens);

            if (delta.Content.Length > 0)
                written.Add(delta.Content);
        }

        Assert.Multiple(() =>
        {
            Assert.That(usages, Is.Empty, "What the search found is part of the number, and no later request carries it.");
            Assert.That(written, Is.EqualTo(new[] { "It is sunny." }), "The answer itself goes out as always.");
        });
    }

    /// <summary>
    /// Runs the next round and returns the prompt of every usage it passed on.
    /// </summary>
    private static async Task<List<int>> Usages(ResponsesToolCallingAdapter adapter)
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
    private static ResponsesToolCallingAdapter Adapter(params string[][] rounds)
    {
        var nextRound = 0;
        return new(new Model("gpt-5", null), [], new Dictionary<string, object>(), [], [], (_, token) => Lines(rounds[nextRound++], token));
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