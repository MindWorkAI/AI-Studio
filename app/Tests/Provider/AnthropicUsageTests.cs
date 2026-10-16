using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Provider.Anthropic;

namespace AIStudio.Tests.Provider;

/// <summary>
/// Checks that what Anthropic says a request carried is read off the right line of the stream.
/// </summary>
/// <remarks>
/// Anthropic states a usage twice per message: at its start, and cumulatively at its end. Only the
/// start describes the request as it was sent. The end adds whatever a server tool fed back into
/// the same request, which no later request carries -- read that one, and a single web search
/// makes the chat look four times as large as it is.
/// </remarks>
[TestFixture]
public sealed class AnthropicUsageTests
{
    /// <summary>
    /// The opening line of a message, as the streaming documentation shows it.
    /// </summary>
    private const string MESSAGE_START =
        """
        {"type": "message_start", "message": {"id": "msg_1nZdL29xx5MUA1yADyHTEsnR8uuvGzszyY", "type": "message", "role": "assistant", "content": [], "model": "claude-opus-5-5", "stop_reason": null, "stop_sequence": null, "usage": {"input_tokens": 25, "output_tokens": 1}}}
        """;

    [Test]
    public void TheOpeningLineStatesWhatTheRequestCarried()
    {
        var line = Read(MESSAGE_START);

        Assert.Multiple(() =>
        {
            Assert.That(line.GetUsage().IsKnown, Is.True);
            Assert.That(line.GetUsage().PromptTokens, Is.EqualTo(25));

            // It carries no answer, which is why it has to be read before the content check drops it:
            Assert.That(line.ContainsContent(), Is.False);
        });
    }

    [Test]
    public void WhatWasCachedCountsAsWell()
    {
        //
        // With caching, the input tokens are only what comes after the last cache breakpoint. The
        // request carried all three parts.
        //
        var line = Read(
            """
            {"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","content":[],"model":"claude-opus-5-5","usage":{"input_tokens":50,"cache_creation_input_tokens":1000,"cache_read_input_tokens":2000,"output_tokens":1}}}
            """);

        Assert.That(line.GetUsage().PromptTokens, Is.EqualTo(3050));
    }

    [Test]
    public void WithoutItsInputTokensABlockStatesNothing()
    {
        //
        // The cache parts alone are not the request: the part after the breakpoint is missing, and
        // it is the one part every request has.
        //
        var line = Read(
            """
            {"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","content":[],"model":"claude-opus-5-5","usage":{"cache_read_input_tokens":2000}}}
            """);

        Assert.That(line.GetUsage().IsKnown, Is.False);
    }

    [Test]
    public void TheClosingLineOfAMessageWithAWebSearchStatesNothing()
    {
        //
        // The example of the streaming documentation: 2,679 input tokens at the start, 10,682 at
        // the end, the difference being the search results. The end is cumulative, and the next
        // request carries none of what the search added.
        //
        var line = Read(
            """
            {"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"input_tokens":10682,"cache_creation_input_tokens":0,"cache_read_input_tokens":0,"output_tokens":510,"server_tool_use":{"web_search_requests":1}}}
            """);

        Assert.That(line.GetUsage().IsKnown, Is.False);
    }

    [Test]
    public void AnOpeningLineWithoutAUsageStatesNothing()
    {
        var line = Read(
            """
            {"type": "message_start", "message": {"id": "msg_01...", "type": "message", "role": "assistant", "content": [], "model": "claude-opus-5-5", "stop_reason": null, "stop_sequence": null}}
            """);

        Assert.That(line.GetUsage().IsKnown, Is.False);
    }

    [Test]
    public void ALineOfTheAnswerStatesNothing()
    {
        var line = Read(
            """
            {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hi"}}
            """);

        Assert.Multiple(() =>
        {
            Assert.That(line.GetUsage().IsKnown, Is.False);
            Assert.That(line.ContainsContent(), Is.True);
        });
    }

    private static ResponseStreamLine Read(string data) => JsonSerializer.Deserialize<ResponseStreamLine>(data, ProviderJsonOptions.OPTIONS);
}