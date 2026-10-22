using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Provider.OpenAI;

namespace AIStudio.Tests.Provider;

/// <summary>
/// Checks that what OpenAI says a Responses API call cost is read off the completed event, and only
/// believed when it describes the request.
/// </summary>
/// <remarks>
/// The completed event states the input of the whole response. That is the request as it was sent,
/// unless OpenAI ran a hosted tool along the way: what such a tool found is charged as input of the
/// same response, and no later request carries it. Believe the number then, and a single web search
/// makes the chat look several times as large as it is.
/// </remarks>
[TestFixture]
public sealed class ResponsesUsageTests
{
    private const string COMPLETED_PREFIX = """{"type":"response.completed","sequence_number":42,"response":{"id":"resp_1","object":"response","status":"completed","model":"gpt-5","output":[""";
    private const string MESSAGE_ITEM = """{"id":"msg_1","type":"message","status":"completed","role":"assistant","content":[{"type":"output_text","text":"Hi there!","annotations":[]}]}""";
    private const string USAGE_SUFFIX = ""","usage":{"input_tokens":2006,"input_tokens_details":{"cached_tokens":1920},"output_tokens":300,"output_tokens_details":{"reasoning_tokens":120},"total_tokens":2306}}}""";

    [Test]
    public void TheCompletedEventStatesWhatTheRequestCarried()
    {
        //
        // The cached tokens are a share of the input tokens, not an addition to them: the request
        // carried 2,006 tokens, 1,920 of which came out of the cache.
        //
        var line = Read(COMPLETED_PREFIX + MESSAGE_ITEM + "]" + USAGE_SUFFIX);

        Assert.Multiple(() =>
        {
            Assert.That(line.GetUsage().IsKnown, Is.True);
            Assert.That(line.GetUsage().PromptTokens, Is.EqualTo(2006));
        });
    }

    [Test]
    public void ReasoningAndFunctionCallsLeaveTheInputAlone()
    {
        //
        // Both are output the model wrote itself. Neither makes OpenAI add anything to the input,
        // and the function call's result comes back in the next request, which states its own.
        //
        var line = Read(COMPLETED_PREFIX +
                        """{"type":"reasoning","id":"rs_1","summary":[],"encrypted_content":"gAAAAAB0aXRs"},""" +
                        """{"type":"function_call","call_id":"call_1","name":"web_search","arguments":"{\"query\":\"weather\"}"}""" +
                        "]" + USAGE_SUFFIX);

        Assert.That(line.GetUsage().PromptTokens, Is.EqualTo(2006));
    }

    [Test]
    public void AHostedWebSearchStatesNothing()
    {
        //
        // OpenAI searched on its own, and what it found is part of the input tokens. The next
        // request carries none of it.
        //
        var line = Read(COMPLETED_PREFIX +
                        """{"type":"web_search_call","id":"ws_1","status":"completed","action":{"type":"search","query":"weather"}},""" +
                        MESSAGE_ITEM +
                        "]" + USAGE_SUFFIX);

        Assert.That(line.GetUsage().IsKnown, Is.False);
    }

    [Test]
    public void AnOutputItemNobodyKnowsStatesNothing()
    {
        //
        // A hosted tool OpenAI adds later is treated like the web search until somebody checked
        // what it does to the input.
        //
        var line = Read(COMPLETED_PREFIX +
                        """{"type":"future_tool_call","id":"ft_1","status":"completed"},""" +
                        MESSAGE_ITEM +
                        "]" + USAGE_SUFFIX);

        Assert.That(line.GetUsage().IsKnown, Is.False);
    }

    [Test]
    public void ACompletedEventWithoutAUsageStatesNothing()
    {
        var line = Read(COMPLETED_PREFIX + MESSAGE_ITEM + "]}}");

        Assert.That(line.GetUsage().IsKnown, Is.False);
    }

    private static ResponsesCompletedStreamLine Read(string data) => JsonSerializer.Deserialize<ResponsesCompletedStreamLine>(data, ProviderJsonOptions.OPTIONS)!;
}