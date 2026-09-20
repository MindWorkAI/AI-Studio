using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Provider.OpenAI;

namespace AIStudio.Tests.Provider;

/// <summary>
/// Checks that what a provider says a request cost is read off the stream, and asked for.
/// </summary>
/// <remarks>
/// Both halves matter and neither is visible from the other: an OpenAI-compatible provider says
/// nothing about the cost of a streamed request unless the request asks for it, and the line it
/// then sends carries no content, so the reading side has to look for it apart from the text.
/// Get either half wrong and the app silently keeps estimating, which looks exactly like a
/// provider which reports nothing.
/// </remarks>
[TestFixture]
public sealed class ChatCompletionUsageTests
{
    /// <summary>
    /// The last line of a streamed answer at a provider which was asked for the usage.
    /// </summary>
    private const string USAGE_LINE =
        """
        {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1,"model":"gpt-5","choices":[],"usage":{"prompt_tokens":1200,"completion_tokens":345,"total_tokens":1545}}
        """;

    /// <summary>
    /// An ordinary line carrying a piece of the answer.
    /// </summary>
    private const string CONTENT_LINE =
        """
        {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1,"model":"gpt-5","choices":[{"index":0,"delta":{"content":"Hi"}}]}
        """;

    [Test]
    public void TheFinalLineStatesWhatTheRequestCost()
    {
        var line = JsonSerializer.Deserialize<ChatCompletionDeltaStreamLine>(USAGE_LINE, ProviderJsonOptions.OPTIONS);

        Assert.Multiple(() =>
        {
            Assert.That(line!.ContainsUsage(), Is.True);
            Assert.That(line.GetUsage().PromptTokens, Is.EqualTo(1200));
            Assert.That(line.GetUsage().CompletionTokens, Is.EqualTo(345));
            Assert.That(line.GetUsage().TotalTokens, Is.EqualTo(1545));

            //
            // The line which carries the usage carries no answer, which is why it has to be read
            // before the content check drops it:
            //
            Assert.That(line.ContainsContent(), Is.False);
        });
    }

    [Test]
    public void ALineOfTheAnswerStatesNoCost()
    {
        var line = JsonSerializer.Deserialize<ChatCompletionDeltaStreamLine>(CONTENT_LINE, ProviderJsonOptions.OPTIONS);

        Assert.Multiple(() =>
        {
            Assert.That(line!.ContainsUsage(), Is.False);
            Assert.That(line.GetUsage().IsKnown, Is.False);
            Assert.That(line.ContainsContent(), Is.True);
        });
    }

    [Test]
    public void AStreamedRequestAsksForTheUsage()
    {
        var request = new ChatCompletionAPIRequest("gpt-5", [], true);
        var json = JsonSerializer.Serialize(request, ProviderJsonOptions.OPTIONS);

        Assert.That(json, Does.Contain("""
                                       "stream_options":{"include_usage":true}
                                       """));
    }

    [Test]
    public void ARequestWhichIsNotStreamedDoesNot()
    {
        var request = new ChatCompletionAPIRequest("gpt-5", [], false);
        var json = JsonSerializer.Serialize(request, ProviderJsonOptions.OPTIONS);

        Assert.That(json, Does.Not.Contain("stream_options"));
    }

    /// <summary>
    /// A provider which sends the block but fills in nothing usable states nothing.
    /// </summary>
    /// <remarks>
    /// Several OpenAI-compatible servers send an empty or zeroed usage block on every line while
    /// streaming and the real numbers only at the end. Reading a zero as a fact would replace an
    /// estimate with a statement that the conversation costs nothing.
    /// </remarks>
    [Test]
    public void AnEmptyUsageBlockStatesNothing()
    {
        var line = JsonSerializer.Deserialize<ChatCompletionDeltaStreamLine>(
            """
            {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1,"model":"gpt-5","choices":[],"usage":{"prompt_tokens":0,"completion_tokens":0}}
            """, ProviderJsonOptions.OPTIONS);

        Assert.That(line!.ContainsUsage(), Is.False);
    }

    /// <summary>
    /// The line a real LM Studio server sends, taken off the wire.
    /// </summary>
    /// <remarks>
    /// It carries fields the shape above does not name -- the reasoning share of the completion,
    /// among them -- and a server which sends more than we read must not stop us from reading what
    /// we came for.
    /// </remarks>
    [Test]
    public void ARealServerLineIsRead()
    {
        var line = JsonSerializer.Deserialize<ChatCompletionDeltaStreamLine>(
            """
            {"id":"chatcmpl-xb8mn282eff3tiu46xz8t3","object":"chat.completion.chunk","created":1789917744,"model":"google/gemma-4-12b-qat","system_fingerprint":"google/gemma-4-12b-qat","choices":[],"usage":{"prompt_tokens":17,"completion_tokens":116,"total_tokens":133,"completion_tokens_details":{"reasoning_tokens":102}}}
            """, ProviderJsonOptions.OPTIONS);

        Assert.Multiple(() =>
        {
            Assert.That(line!.ContainsUsage(), Is.True);
            Assert.That(line.GetUsage().TotalTokens, Is.EqualTo(133));
        });
    }

    /// <summary>
    /// An answer cut off before it wrote anything still cost its prompt.
    /// </summary>
    [Test]
    public void AnAnswerOfNoTokensIsStillACost()
    {
        var usage = TokenUsage.OfReported(900, 0);

        Assert.Multiple(() =>
        {
            Assert.That(usage.IsKnown, Is.True);
            Assert.That(usage.TotalTokens, Is.EqualTo(900));
        });
    }
}