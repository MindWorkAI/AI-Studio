using AIStudio.Provider;
using AIStudio.Provider.OpenAI;

namespace AIStudio.Tests.Provider.ToolCalling;

/// <summary>
/// Checks how a streamed Responses API call is read back.
/// </summary>
/// <remarks>
/// The API repeats the whole response when it is done, so there is little to reassemble here --
/// but there is one thing to get right: the reasoning items have to return exactly as they came,
/// including the parts we do not understand. The API refuses a continuation whose reasoning is
/// missing, and it would just as surely refuse one we rewrote.
/// </remarks>
[TestFixture]
public sealed class ResponsesStreamAccumulatorTests
{
    private const string REASONING_ITEM = """{"type":"reasoning","id":"rs_1","summary":[],"encrypted_content":"gAAAAAB0aXRs"}""";
    private const string COMPLETED_PREFIX = """{"type":"response.completed","response":{"id":"resp_1","model":"gpt-5","output":[""";
    
    [Test]
    public void TheReasoningItemComesBackWordForWord()
    {
        //
        // The test that nails down the main risk: whatever the reasoning item carries, including
        // fields nobody here knows about, is what goes back on the next request.
        //
        var response = Read(COMPLETED_PREFIX + REASONING_ITEM + "]}}");

        Assert.That(response!.Output.Single().GetRawText(), Is.EqualTo(REASONING_ITEM), "Not a field added, not a field dropped: the item travels on as it arrived.");
    }
    
    [Test]
    public void TheCompletedEventCarriesTheWholeRound()
    {
        var response = Read(COMPLETED_PREFIX + REASONING_ITEM + "," +
                            """{"type":"message","role":"assistant","content":[{"type":"output_text","text":"Here is the answer."}]},""" +
                            """{"type":"function_call","call_id":"call_1","name":"web_search","arguments":"{\"query\":\"weather\"}"}""" +
                            "]}}");

        Assert.Multiple(() =>
        {
            Assert.That(response!.GetTextOutput(), Is.EqualTo("Here is the answer."), "The text of the round is read out of the completed response.");
            Assert.That(response.GetFunctionCalls().Single().CallId, Is.EqualTo("call_1"), "And so are the calls it asked for.");
            Assert.That(response.Output, Has.Count.EqualTo(3), "Every output item is kept, because every one of them goes back.");
        });
    }
    
    [Test]
    public void TheTextIsShownWhileItIsBeingWritten()
    {
        var accumulator = new ResponsesStreamAccumulator();
        var shown = string.Concat(Lines(
                """{"type":"response.output_text.delta","delta":"Let me "}""",
                """{"type":"response.output_text.delta","delta":"look that up."}""")
            .Select(accumulator.Process)
            .Where(part => part.HasContent)
            .Select(part => part.TextDelta));
        
        Assert.That(shown, Is.EqualTo("Let me look that up."), "Each piece of text goes out as it arrives rather than at the end of the round.");
    }
    
    [Test]
    public void AnAnnouncedSourceTravelsWithItsLine()
    {
        var accumulator = new ResponsesStreamAccumulator();
        var part = accumulator.Process(Event("""{"type":"response.output_text.annotation.added","annotation_index":0,"annotation":{"type":"url_citation","title":"Example","url":"https://example.org/"}}"""));
        
        Assert.Multiple(() =>
        {
            Assert.That(part.Sources.Select(x => x.URL), Is.EqualTo(new[] { "https://example.org/" }), "A citation reaches the user as soon as the model makes it.");
            Assert.That(part.TextDelta, Is.Empty, "A line which only announces a source carries no text.");
        });
    }
    
    [Test]
    public void AGatewayWithoutACompletedEventStillGetsARound()
    {
        //
        // Not every gateway in front of this API sends the closing event. The finished output
        // items are enough to put the round back together, reasoning included.
        //
        var response = Read(
            """{"type":"response.output_item.done","output_index":0,"item":""" + REASONING_ITEM + "}",
            """{"type":"response.output_item.done","output_index":1,"item":{"type":"function_call","call_id":"call_1","name":"web_search","arguments":"{}"}}""");

        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null, "A round built from its items is still a round.");
            Assert.That(response!.Output.First().GetRawText(), Is.EqualTo(REASONING_ITEM), "And the reasoning item is as untouched as it would be in the completed event.");
            Assert.That(response.GetFunctionCalls().Single().Name, Is.EqualTo("web_search"), "The call is there to be executed.");
        });
    }
    
    [Test]
    public void TheCompletedEventWinsOverTheCollectedItems()
    {
        //
        // When both arrive, the response the API itself assembled is the one to trust.
        //
        var response = Read(
            """{"type":"response.output_item.done","output_index":0,"item":{"type":"message","role":"assistant","content":[{"type":"output_text","text":"Partial"}]}}""",
            """{"type":"response.completed","response":{"id":"resp_1","model":"gpt-5","output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"Complete"}]}]}}""");

        Assert.That(response!.GetTextOutput(), Is.EqualTo("Complete"), "The closing event is the round, and the collected items were only there in case it never came.");
    }
    
    [Test]
    public void TheCompletedEventStatesWhatTheRequestCost()
    {
        var accumulator = new ResponsesStreamAccumulator();
        var part = accumulator.Process(Event(COMPLETED_PREFIX + REASONING_ITEM + """],"usage":{"input_tokens":2679,"input_tokens_details":{"cached_tokens":0},"output_tokens":510}}}"""));

        Assert.Multiple(() =>
        {
            Assert.That(part.Usage.IsKnown, Is.True, "The closing event is the one line which states what the request cost.");
            Assert.That(part.Usage.PromptTokens, Is.EqualTo(2679));
            Assert.That(part.HasContent, Is.False, "And there is nothing on it to show.");
            Assert.That(accumulator.Build()!.Output, Has.Count.EqualTo(1), "Reading the usage leaves the round as it was.");
        });
    }

    [Test]
    public void ARoundPutBackTogetherFromItsItemsStatesNoCost()
    {
        //
        // The gateway which never sends the closing event never sends the usage either, which
        // leaves the round to the estimate.
        //
        var response = Read("""{"type":"response.output_item.done","output_index":0,"item":{"type":"message","role":"assistant","content":[{"type":"output_text","text":"Partial"}]}}""");

        Assert.That(response!.GetUsage().IsKnown, Is.False);
    }

    [Test]
    public void AStreamWhichSaidNothingIsAFailedRound()
    {
        var response = Read("""{"type":"response.created","response":{"id":"resp_1"}}""");

        Assert.That(response, Is.Null, "Neither a completed response nor a single finished item: there is no round here to continue from.");
    }
    
    private static ResponsesResponse? Read(params string[] data)
    {
        var accumulator = new ResponsesStreamAccumulator();
        foreach (var serverSentEvent in Lines(data))
            accumulator.Process(serverSentEvent);

        return accumulator.Build();
    }
    
    private static IEnumerable<ServerSentEvent> Lines(params string[] data) => data.Select(Event);
    
    private static ServerSentEvent Event(string data) => new($"data: {data}", data);
}