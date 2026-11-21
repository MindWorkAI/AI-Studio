using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Provider.OpenAI;

using AnthropicResponseStreamLine = AIStudio.Provider.Anthropic.ResponseStreamLine;

namespace AIStudio.Tests.Provider;

/// <summary>
/// Checks that the lines of a stream without tools say when the model reasons, so that the chat can tell the user.
/// </summary>
/// <remarks>
/// The stream with tools reads the same lines through its accumulators, which have tests of their own. Both have to
/// agree: whether a chat may use tools must not change what the user reads while the model thinks.
/// </remarks>
[TestFixture]
public sealed class ReasoningSignalTests
{
    [Test]
    public void AThinkingDeltaOfAnthropicSaysThatTheModelReasons()
    {
        var line = ReadAnthropic("""{"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"Let me consider this."}}""");
        var chunk = line.GetContent();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(line.ContainsContent(), Is.True, "The line has to reach the chat, although it carries no text.");
            Assert.That(chunk.IsReasoning, Is.True);
            Assert.That(chunk.Content, Is.Empty, "The thinking itself does not show.");
        }
    }

    [Test]
    public void ATextDeltaOfAnthropicIsNoReasoning()
    {
        var chunk = ReadAnthropic("""{"type":"content_block_delta","index":1,"delta":{"type":"text_delta","text":"The answer."}}""").GetContent();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chunk.IsReasoning, Is.False);
            Assert.That(chunk.Content, Is.EqualTo("The answer."));
        }
    }

    [Test]
    public void ReasoningContentOfAChatCompletionSaysThatTheModelReasons()
    {
        var chunk = ReadChatCompletion("""{"id":"chatcmpl-1","object":"chat.completion.chunk","created":1,"model":"deepseek-reasoner","choices":[{"index":0,"delta":{"reasoning_content":"Thinking about it."}}]}""");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chunk.IsReasoning, Is.True);
            Assert.That(chunk.Content, Is.Empty, "The reasoning itself does not show.");
        }
    }

    [Test]
    public void ContentOfAChatCompletionIsNoReasoning()
    {
        var chunk = ReadChatCompletion("""{"id":"chatcmpl-1","object":"chat.completion.chunk","created":1,"model":"deepseek-reasoner","choices":[{"index":0,"delta":{"content":"The answer."}}]}""");
        Assert.That(chunk.IsReasoning, Is.False);
    }

    private static AnthropicResponseStreamLine ReadAnthropic(string data) => JsonSerializer.Deserialize<AnthropicResponseStreamLine>(data, ProviderJsonOptions.OPTIONS);

    private static ContentStreamChunk ReadChatCompletion(string data) => JsonSerializer.Deserialize<ChatCompletionDeltaStreamLine>(data, ProviderJsonOptions.OPTIONS)!.GetContent();
}