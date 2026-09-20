using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Provider.Anthropic;

namespace AIStudio.Tests.Provider.ToolCalling;

/// <summary>
/// Checks how a streamed Anthropic message is put back together.
/// </summary>
/// <remarks>
/// The blocks of a message do not only have to be readable afterwards, they have to be sendable:
/// they go back to Anthropic with the next round. A thinking block is the sharp edge -- its
/// signature has to return byte for byte with the text it was made for, or the provider refuses
/// the continuation with a 400 and the whole conversation is stuck.
/// </remarks>
[TestFixture]
public sealed class AnthropicMessageStreamAccumulatorTests
{
    [Test]
    public void ATextBlockIsTheFragmentsItArrivedIn()
    {
        var response = Read(
            """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}""",
            """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Let me "}}""",
            """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"look that "}}""",
            """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"up."}}""",
            """{"type":"content_block_stop","index":0}""",
            """{"type":"message_stop"}""");

        Assert.That(response!.GetTextOutput(), Is.EqualTo("Let me look that up."), "The fragments are joined in order and with nothing in between.");
    }
    
    [Test]
    public void TheTextIsShownWhileItIsBeingWritten()
    {
        var accumulator = new AnthropicMessageStreamAccumulator();
        var shown = string.Concat(Lines(
                """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}""",
                """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hel"}}""",
                """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"lo"}}""")
            .Select(accumulator.Process)
            .Where(part => part.HasContent)
            .Select(part => part.TextDelta));
        
        Assert.That(shown, Is.EqualTo("Hello"), "Each piece of text goes out as it arrives rather than at the end of the block.");
    }
    
    [Test]
    public void ThinkingNeverReachesTheUser()
    {
        //
        // Neither path has ever shown thinking, and making it visible would be a feature of its
        // own rather than something that happens by accident while streaming.
        //
        var accumulator = new AnthropicMessageStreamAccumulator();
        var shown = Lines(
                """{"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":""}}""",
                """{"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"Let me consider this."}}""")
            .Select(accumulator.Process)
            .Any(part => part.HasContent);
        
        Assert.That(shown, Is.False, "What the model thinks stays between it and the next round.");
    }
    
    [Test]
    public void AThinkingBlockKeepsItsSignature()
    {
        //
        // The test which nails down the sharpest risk of this change: text and signature have to
        // come back exactly as they were sent, or the next round is refused.
        //
        var response = Read(
            """{"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":""}}""",
            """{"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"Weighing "}}""",
            """{"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"the options."}}""",
            """{"type":"content_block_delta","index":0,"delta":{"type":"signature_delta","signature":"EqQBCgIYAhIM+abc/DEF=="}}""",
            """{"type":"content_block_stop","index":0}""",
            """{"type":"message_stop"}""");

        var block = response!.Content.Single();
        Assert.Multiple(() =>
        {
            Assert.That(ReadString(block, "type"), Is.EqualTo("thinking"), "The block goes back as the kind it was.");
            Assert.That(ReadString(block, "thinking"), Is.EqualTo("Weighing the options."), "With the thinking it carried.");
            Assert.That(ReadString(block, "signature"), Is.EqualTo("EqQBCgIYAhIM+abc/DEF=="), "And with the signature that was made for exactly that text.");
        });
    }
    
    [Test]
    public void ARedactedThinkingBlockGoesBackUntouched()
    {
        //
        // We cannot read it, which is the very reason we must not rewrite it either.
        //
        var response = Read(
            """{"type":"content_block_start","index":0,"content_block":{"type":"redacted_thinking","data":"EroBCkYIARgCKkBS0mBXJ"}}""",
            """{"type":"content_block_stop","index":0}""",
            """{"type":"message_stop"}""");

        Assert.That(ReadString(response!.Content.Single(), "data"), Is.EqualTo("EroBCkYIARgCKkBS0mBXJ"), "Whatever we do not understand travels on unchanged.");
    }
    
    [Test]
    public void AToolUseCollectsItsArgumentsFromFragments()
    {
        var response = Read(
            """{"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"toolu_1","name":"web_search","input":{}}}""",
            """{"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"query\":"}}""",
            """{"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"\"weather\"}"}}""",
            """{"type":"content_block_stop","index":0}""",
            """{"type":"message_stop"}""");

        var toolUse = response!.GetToolUses().Single();
        Assert.Multiple(() =>
        {
            Assert.That(toolUse.Id, Is.EqualTo("toolu_1"), "The ID comes from the block as it opened.");
            Assert.That(toolUse.Name, Is.EqualTo("web_search"), "So does the name.");
            Assert.That(toolUse.Arguments, Is.EqualTo("""{"query":"weather"}"""), "And the arguments are the fragments joined back together.");
        });
    }
    
    [Test]
    public void AToolWithoutArgumentsGetsAnEmptyObject()
    {
        //
        // Anthropic sends no fragment at all for a tool which takes nothing, and the input field
        // has to be an object either way.
        //
        var response = Read(
            """{"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"toolu_1","name":"get_time","input":{}}}""",
            """{"type":"content_block_stop","index":0}""",
            """{"type":"message_stop"}""");

        Assert.That(response!.GetToolUses().Single().Arguments, Is.EqualTo("{}"), "An empty object is what an empty call looks like on the wire.");
    }
    
    [Test]
    public void ArgumentsWhichNeverParsedMakeTheCallInvalidWhileTheBlockStaysWellFormed()
    {
        //
        // Two things have to be true at once here: the provider gets a block it accepts, and the
        // call is rejected rather than run with arguments the model never finished writing.
        //
        var response = Read(
            """{"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"toolu_1","name":"web_search","input":{}}}""",
            """{"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"query\":\"wea"}}""",
            """{"type":"content_block_stop","index":0}""",
            """{"type":"message_stop"}""");

        var toolUse = response!.GetToolUses().Single();
        Assert.Multiple(() =>
        {
            Assert.That(toolUse.Arguments, Is.EqualTo("""{"query":"wea"""), "The call carries what actually arrived, which no tool executor will accept.");
            Assert.That(ReadRawText(response.Content.Single(), "input"), Is.EqualTo("{}"), "While the block going back to Anthropic carries an object, because anything else would be refused.");
        });
    }
    
    [Test]
    public void ABlockWhoseClosingEventNeverCameIsStillFinished()
    {
        var response = Read(
            """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}""",
            """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hello"}}""",
            """{"type":"message_stop"}""");

        Assert.That(response!.GetTextOutput(), Is.EqualTo("Hello"), "The end of the message ends every block it still has open.");
    }
    
    [Test]
    public void BlocksComeBackInTheOrderTheyWereIndexed()
    {
        //
        // Interleaved on purpose: what decides the order is the index, not the moment a block
        // happened to be closed.
        //
        var response = Read(
            """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}""",
            """{"type":"content_block_start","index":1,"content_block":{"type":"tool_use","id":"toolu_1","name":"web_search","input":{}}}""",
            """{"type":"content_block_stop","index":1}""",
            """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"First"}}""",
            """{"type":"content_block_stop","index":0}""",
            """{"type":"message_stop"}""");

        Assert.That(response!.Content.Select(block => ReadString(block, "type")), Is.EqualTo(new[] { "text", "tool_use" }), "The order of a message is the order of its indices.");
    }
    
    [Test]
    public void AMessageWhichOnlyEndedWithAStopReasonCountsAsFinished()
    {
        //
        // Not every gateway closes with the message stop event, so the stop reason ends the
        // message as well.
        //
        var response = Read(
            """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}""",
            """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hello"}}""",
            """{"type":"content_block_stop","index":0}""",
            """{"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":5}}""");

        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null, "A message with a stop reason is a message which ended.");
            Assert.That(response!.StopReason, Is.EqualTo("end_turn"), "And the reason it ended travels with it.");
        });
    }
    
    [Test]
    public void AStreamCutOffMidSentenceIsAFailedRound()
    {
        //
        // No stop event and no stop reason: whatever was streamed stays on screen, but there is
        // no round to continue from.
        //
        var response = Read(
            """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}""",
            """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hel"}}""");

        Assert.That(response, Is.Null, "An unfinished message is not handed on as if it were finished.");
    }
    
    private static AnthropicResponse? Read(params string[] data)
    {
        var accumulator = new AnthropicMessageStreamAccumulator();
        foreach (var serverSentEvent in Lines(data))
            accumulator.Process(serverSentEvent);

        return accumulator.Build();
    }
    
    private static IEnumerable<ServerSentEvent> Lines(params string[] data) => data.Select(Event);
    
    private static ServerSentEvent Event(string data) => new($"data: {data}", data);
    
    private static string ReadString(JsonElement block, string propertyName) => block.TryGetProperty(propertyName, out var property) ? property.GetString() ?? string.Empty : string.Empty;
    
    private static string ReadRawText(JsonElement block, string propertyName) => block.TryGetProperty(propertyName, out var property) ? property.GetRawText() : string.Empty;
}