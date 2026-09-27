using System.Runtime.CompilerServices;

using AIStudio.Provider;
using AIStudio.Provider.Anthropic;

namespace AIStudio.Tests.Provider.ToolCalling;

/// <summary>
/// Checks what a round of a tool calling conversation with Anthropic passes on about its cost.
/// </summary>
/// <remarks>
/// Every round is a request of its own, and every one of them states at its start what it carried.
/// The adapter passes each of those on and none of the cumulative ones at the end of a message.
/// Which round describes the conversation is the tool calling loop's decision, checked in
/// ToolCallingLoopTests.
/// </remarks>
[TestFixture]
public sealed class AnthropicToolCallingAdapterTests
{
    [Test]
    public async Task EveryRoundPassesOnWhatItsRequestCarried()
    {
        var adapter = Adapter(
        [
            """{"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","content":[],"usage":{"input_tokens":1200,"output_tokens":1}}}""",
            """{"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"toolu_1","name":"web_search","input":{}}}""",
            """{"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"query\":\"weather\"}"}}""",
            """{"type":"content_block_stop","index":0}""",
            """{"type":"message_delta","delta":{"stop_reason":"tool_use","stop_sequence":null},"usage":{"output_tokens":20}}""",
            """{"type":"message_stop"}""",
        ],
        [
            """{"type":"message_start","message":{"id":"msg_2","type":"message","role":"assistant","content":[],"usage":{"input_tokens":9800,"output_tokens":1}}}""",
            """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}""",
            """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"It is sunny."}}""",
            """{"type":"content_block_stop","index":0}""",
            """{"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"input_tokens":12000,"output_tokens":30}}""",
            """{"type":"message_stop"}""",
        ]);

        var firstRound = await Usages(adapter);

        //
        // What the loop does between two rounds: the model's turn and the tool's result become part
        // of the next request.
        //
        adapter.RecordAssistantTurn();
        adapter.RecordToolResult("toolu_1", "Sunny, 24 degrees.");

        var secondRound = await Usages(adapter);

        Assert.Multiple(() =>
        {
            Assert.That(firstRound, Is.EqualTo(new[] { 1200 }), "The first round states the conversation up to the question.");
            Assert.That(secondRound, Is.EqualTo(new[] { 9800 }), "The second round states its own request, and the cumulative number at its end stays behind.");
        });
    }

    [Test]
    public async Task TheTextStillGoesOutNextToTheUsage()
    {
        var adapter = Adapter(
        [
            """{"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","content":[],"usage":{"input_tokens":1200,"output_tokens":1}}}""",
            """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}""",
            """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hello."}}""",
            """{"type":"content_block_stop","index":0}""",
            """{"type":"message_stop"}""",
        ]);

        var written = new List<string>();
        await foreach (var streamEvent in adapter.ExecuteRoundAsync(null, true))
            if (streamEvent.Delta is { Content.Length: > 0 } delta)
                written.Add(delta.Content);

        Assert.That(written, Is.EqualTo(new[] { "Hello." }));
    }

    /// <summary>
    /// Runs the next round and returns the prompt of every usage it passed on.
    /// </summary>
    private static async Task<List<int>> Usages(AnthropicToolCallingAdapter adapter)
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
    private static AnthropicToolCallingAdapter Adapter(params string[][] rounds)
    {
        var nextRound = 0;
        return new(new Model("claude-test", null), [], "You are a helpful assistant.", 1024, new Dictionary<string, object>(), [], (_, token) => Lines(rounds[nextRound++], token));
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