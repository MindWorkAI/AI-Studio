using AIStudio.Provider;
using AIStudio.Tools;
using AIStudio.Provider.OpenAI;

namespace AIStudio.Tests.Provider.ToolCalling;

/// <summary>
/// Checks how a streamed Chat Completions answer is put back together.
/// </summary>
/// <remarks>
/// Seventeen providers share this one path, and they disagree on nearly every detail of it: some
/// send the index with every fragment, some only with the first, some send no index at all, and
/// not all of them close the stream with a "[DONE]". Each of those is one case below, because
/// each of them is one provider whose tool calls would otherwise fall apart.
/// </remarks>
[TestFixture]
public sealed class ChatCompletionToolCallAccumulatorTests
{
    [Test]
    public void ArgumentsSpreadOverManyFragmentsBecomeOneCall()
    {
        var message = Read(
            """{"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"call_1","type":"function","function":{"name":"web_search","arguments":"{\"qu"}}]}}]}""",
            """{"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"function":{"arguments":"ery\":"}}]}}]}""",
            """{"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"function":{"arguments":"\"wea"}}]}}]}""",
            """{"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"function":{"arguments":"ther\""}}]}}]}""",
            """{"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"function":{"arguments":"}"}}]}}]}""",
            "[DONE]");

        var call = message!.ToolCalls!.Single()!;
        Assert.Multiple(() =>
        {
            Assert.That(call.Id, Is.EqualTo("call_1"), "The ID arrived with the first fragment and belongs to the whole call.");
            Assert.That(call.Function!.Name, Is.EqualTo("web_search"), "So does the name.");
            Assert.That(call.Function!.Arguments, Is.EqualTo("""{"query":"weather"}"""), "And the arguments are the fragments in the order they came, joined without anything in between.");
        });
    }
    
    [Test]
    public void TwoCallsWrittenAtTheSameTimeStayApart()
    {
        //
        // Nothing says a model finishes one call before it starts the next, and the index is
        // what keeps the fragments of the two from running into each other.
        //
        var message = Read(
            """{"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"call_a","function":{"name":"web_search","arguments":"{\"query\":"}}]}}]}""",
            """{"choices":[{"index":0,"delta":{"tool_calls":[{"index":1,"id":"call_b","function":{"name":"read_web_page","arguments":"{\"url\":"}}]}}]}""",
            """{"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"function":{"arguments":"\"a\"}"}}]}}]}""",
            """{"choices":[{"index":0,"delta":{"tool_calls":[{"index":1,"function":{"arguments":"\"b\"}"}}]}}]}""");

        Assert.That(message!.ToolCalls!.Select(x => $"{x!.Id}:{x.Function!.Arguments}"), Is.EqualTo(new[]
        {
            """call_a:{"query":"a"}""",
            """call_b:{"url":"b"}""",
        }), "Each call collects its own fragments, whichever order they arrive in.");
    }
    
    [Test]
    public void AProviderWhichSendsNoIndexStillGetsAWholeCall()
    {
        //
        // Some gateways leave the index out once the call is open. What is left to correlate by
        // is the ID, and after that the call which was opened last.
        //
        var message = Read(
            """{"choices":[{"index":0,"delta":{"tool_calls":[{"id":"call_1","function":{"name":"web_search","arguments":"{\"query\":"}}]}}]}""",
            """{"choices":[{"index":0,"delta":{"tool_calls":[{"function":{"arguments":"\"weather\"}"}}]}}]}""");

        var call = message!.ToolCalls!.Single()!;
        Assert.Multiple(() =>
        {
            Assert.That(call.Id, Is.EqualTo("call_1"), "One call, not two: a fragment without an index belongs to the one being written.");
            Assert.That(call.Function!.Arguments, Is.EqualTo("""{"query":"weather"}"""), "And its arguments are complete.");
        });
    }
    
    [Test]
    public void AWholeCallInOneFragmentWorksJustAsWell()
    {
        var message = Read("""{"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"call_1","type":"function","function":{"name":"web_search","arguments":"{\"query\":\"weather\"}"}}]},"finish_reason":"tool_calls"}]}""");

        var call = message!.ToolCalls!.Single()!;
        Assert.That(call.Function!.Arguments, Is.EqualTo("""{"query":"weather"}"""), "Fragmenting is what providers may do, not what they must do.");
    }
    
    [Test]
    public void TextAndAToolCallInTheSameRoundBothSurvive()
    {
        //
        // The preamble case on the wire: the model says what it is going to do and then does it.
        //
        var accumulator = new ChatCompletionToolCallAccumulator();
        var shown = string.Concat(Lines(
                """{"choices":[{"index":0,"delta":{"content":"Let me look "}}]}""",
                """{"choices":[{"index":0,"delta":{"content":"that up."}}]}""",
                """{"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"call_1","function":{"name":"web_search","arguments":"{}"}}]}}]}""")
            .Select(accumulator.Process)
            .Where(part => part.HasContent)
            .Select(part => part.TextDelta));

        var message = accumulator.Build();
        Assert.Multiple(() =>
        {
            Assert.That(shown, Is.EqualTo("Let me look that up."), "The text goes out while it is being written, in the pieces it arrives in.");
            Assert.That(message!.Content, Is.EqualTo("Let me look that up."), "And the same text goes back to the provider as what the model said.");
            Assert.That(message.ToolCalls!.Single()!.Id, Is.EqualTo("call_1"), "The tool call of that round is there as well.");
        });
    }
    
    [Test]
    public void ContentSentAsPartsIsReadAsText()
    {
        // Some gateways send the content the way a request carries it, as a list of parts:
        var message = Read("""{"choices":[{"index":0,"delta":{"content":[{"type":"text","text":"Hello"}]}}]}""");

        Assert.That(message!.Content, Is.EqualTo("Hello"), "A provider which sends parts instead of a string is still sending text.");
    }
    
    [Test]
    public void ReasoningTravelsSeparatelyFromTheAnswer()
    {
        var message = Read(
            """{"choices":[{"index":0,"delta":{"reasoning_content":"Thinking about it."}}]}""",
            """{"choices":[{"index":0,"delta":{"content":"The answer."}}]}""");

        Assert.Multiple(() =>
        {
            Assert.That(message!.ReasoningContent, Is.EqualTo("Thinking about it."), "Reasoning is kept, because the next request is charged for it.");
            Assert.That(message.Content, Is.EqualTo("The answer."), "And it is not mixed into the answer.");
        });
    }
    
    [Test]
    public void AToolWhichTakesNothingGetsAnEmptyObject()
    {
        //
        // A parameterless tool is called without a single argument fragment, while the very same
        // call carries an empty object when it is not streamed. Handing on the empty string here
        // would have every one of those calls rejected as invalid.
        //
        var withoutAnyFragment = Read("""{"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"call_1","function":{"name":"get_time"}}]}}]}""");
        var withAnEmptyFragment = Read("""{"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"call_1","function":{"name":"get_time","arguments":""}}]}}]}""");

        Assert.Multiple(() =>
        {
            Assert.That(withoutAnyFragment!.ToolCalls!.Single()!.Function!.Arguments, Is.EqualTo("{}"), "No fragment at all is a call without arguments, not a broken one.");
            Assert.That(withAnEmptyFragment!.ToolCalls!.Single()!.Function!.Arguments, Is.EqualTo("{}"), "And neither is the empty fragment some providers send instead.");
        });
    }
    
    [Test]
    public void ARoundWithoutTextHasNoContentAtAll()
    {
        //
        // An empty string in place of the missing content is rejected by some providers, so the
        // field has to be absent exactly as it is in a non-streamed answer.
        //
        var message = Read("""{"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"call_1","function":{"name":"web_search","arguments":"{}"}}]}}]}""");

        Assert.Multiple(() =>
        {
            Assert.That(message!.RawContent, Is.Null, "No text means no content field.");
            Assert.That(message.Content, Is.Null, "Which is what the adapter reads as an answer without words.");
        });
    }
    
    [Test]
    public void AStreamWhichSaidNothingIsAFailedRound()
    {
        Assert.That(new ChatCompletionToolCallAccumulator().Build(), Is.Null, "A request that failed leaves no lines behind, and a round without a message ends the loop without a second error message.");
    }
    
    [Test]
    public void SourcesOfTheProviderTravelWithTheirLine()
    {
        //
        // Perplexity puts its search results next to the text rather than on a line of their own,
        // which is why the sources are read through the provider's own types.
        //
        var accumulator = new ChatCompletionToolCallAccumulator(_ => [new Source("Example", "https://example.org/", SourceOrigin.LLM)]);
        var part = accumulator.Process(Event("""{"choices":[{"index":0,"delta":{"content":"Hello"}}]}"""));
        
        Assert.That(part.Sources.Select(x => x.URL), Is.EqualTo(new[] { "https://example.org/" }), "Whatever the provider announced on that line reaches the user with it.");
    }
    
    private static ChatCompletionResponseMessage? Read(params string[] data)
    {
        var accumulator = new ChatCompletionToolCallAccumulator();
        foreach (var serverSentEvent in Lines(data))
            accumulator.Process(serverSentEvent);

        return accumulator.Build();
    }
    
    private static IEnumerable<ServerSentEvent> Lines(params string[] data) => data.Select(Event);
    
    private static ServerSentEvent Event(string data) => new($"data: {data}", data);
}