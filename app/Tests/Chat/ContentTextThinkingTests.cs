using AIStudio.Chat;
using AIStudio.Provider;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks when an answer tells the user that the model is thinking.
/// </summary>
/// <remarks>
/// Some providers say that their model reasons, others send nothing to read for a while, and some models write
/// their thoughts into think tags. All of them have to end in the same status, and the status has to go however
/// the stream ends.
/// </remarks>
[TestFixture]
public sealed class ContentTextThinkingTests
{
    private const string MODEL_ID = "test-model";

    [Test]
    public void NothingThinksBeforeTheFirstChunk()
    {
        var answer = new ContentText { InitialRemoteWait = true };
        Assert.That(answer.IsThinking, Is.False, "Until the first chunk, the chat says that it processes the prompt.");
    }

    [Test]
    public void ReasoningLastsUntilTheFirstText()
    {
        var answer = new ContentText { InitialRemoteWait = true };

        Apply(answer, Reasoning());
        var whileReasoning = answer.IsThinking;

        Apply(answer, new ContentStreamChunk(string.Empty, []));
        var afterAChunkWithoutText = answer.IsThinking;

        Apply(answer, new ContentStreamChunk("The answer.", []));
        var afterTheText = answer.IsThinking;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(whileReasoning, Is.True);
            Assert.That(afterAChunkWithoutText, Is.True, "A chunk without text, such as one with the usage, does not end the reasoning.");
            Assert.That(afterTheText, Is.False);
        }
    }

    [Test]
    public void AChunkWithoutTextLetsTheModelThink()
    {
        var answer = new ContentText { InitialRemoteWait = true };
        Apply(answer, new ContentStreamChunk(string.Empty, []));

        Assert.That(answer.IsThinking, Is.True, "A provider which does not say that its model reasons still sends nothing to read yet.");
    }

    [Test]
    public void ThinkTagsLetTheModelThinkUntilTheAnswer()
    {
        var answer = new ContentText { InitialRemoteWait = true };

        Apply(answer, new ContentStreamChunk("<think>Let me", []));
        var whileTheBlockIsOpen = answer.IsThinking;

        Apply(answer, new ContentStreamChunk(" consider this.</think>", []));
        var afterTheBlock = answer.IsThinking;

        Apply(answer, new ContentStreamChunk("\n\n", []));
        var afterTheLineBreaks = answer.IsThinking;

        Apply(answer, new ContentStreamChunk("The answer.", []));
        var afterTheAnswer = answer.IsThinking;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(whileTheBlockIsOpen, Is.True);
            Assert.That(afterTheBlock, Is.True, "A closed think block is no answer yet.");
            Assert.That(afterTheLineBreaks, Is.True, "Neither are the line breaks after it.");
            Assert.That(afterTheAnswer, Is.False);
        }
    }

    [Test]
    public void ReasoningLaterInTheAnswerShowsAgain()
    {
        var answer = new ContentText { InitialRemoteWait = true };

        Apply(answer, new ContentStreamChunk("Let me look that up.", []));
        var whileWriting = answer.IsThinking;

        Apply(answer, Reasoning());
        var whileReasoningAgain = answer.IsThinking;

        Apply(answer, new ContentStreamChunk(" Here it is.", []));
        var afterMoreText = answer.IsThinking;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(whileWriting, Is.False);
            Assert.That(whileReasoningAgain, Is.True, "After a tool, the model may reason again before it goes on writing.");
            Assert.That(afterMoreText, Is.False);
        }
    }

    [Test]
    public void TheEndOfTheStreamEndsTheThinking()
    {
        var answer = new ContentText { InitialRemoteWait = true };
        Apply(answer, Reasoning());

        // The job service of the chat ends a stream this way, without a word about reasoning:
        answer.IsStreaming = false;

        Assert.That(answer.IsThinking, Is.False);
    }

    private static ContentStreamChunk Reasoning() => new(string.Empty, []) { IsReasoning = true };

    private static void Apply(ContentText answer, ContentStreamChunk chunk) => answer.ApplyStreamChunk(chunk, MODEL_ID, blockCount: 2);
}