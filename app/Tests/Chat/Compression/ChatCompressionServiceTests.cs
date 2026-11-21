using AIStudio.Chat;
using AIStudio.Chat.Compression;
using AIStudio.Models;

namespace AIStudio.Tests.Chat.Compression;

/// <summary>
/// Checks which models may compress a chat at all, and when a summary is too long.
/// </summary>
/// <remarks>
/// Below the minimum, the compress button stays off and the notice before sending offers no
/// compressing, so a wrong answer here either hides the feature or offers one which always fails.
/// </remarks>
[TestFixture]
public sealed class ChatCompressionServiceTests
{
    [Test]
    public void AWindowBelowTheMinimumCannotSummarize() => Assert.That(ChatCompressionService.CanSummarizeWith(ContextWindow.Of(ChatCompressionService.MIN_CONTEXT_WINDOW - 1)), Is.False);

    [Test]
    public void AWindowOfTheMinimumCanSummarize() => Assert.That(ChatCompressionService.CanSummarizeWith(ContextWindow.Of(ChatCompressionService.MIN_CONTEXT_WINDOW)), Is.True);

    [Test]
    public void AnUnknownWindowCanSummarize() => Assert.That(ChatCompressionService.CanSummarizeWith(ContextWindow.UNKNOWN), Is.True);

    [TestCase(8_192, 8_192, false, TestName = "A summary of the length asked for stays")]
    [TestCase(12_288, 8_192, false, TestName = "A summary up to the tolerance stays")]
    [TestCase(12_289, 8_192, true, TestName = "A summary beyond the tolerance is shortened")]
    [TestCase(100_000, 0, false, TestName = "Without a known room nothing is shortened")]
    public void ASummaryIsShortenedOnlyBeyondTheTolerance(int summaryTokens, int tokensForTheSummary, bool expected)
    {
        Assert.That(ChatCompressionService.IsTooLong(summaryTokens, tokensForTheSummary), Is.EqualTo(expected), "Models cannot count, so a summary somewhat longer than asked for is fine.");
    }

    [Test]
    public void CompressingMakesRoomForAnOrdinaryMessage() => Assert.That(ChatCompressionService.MakesRoomFor(ChatWithTwoExchanges(), 0, ContextWindow.Of(32_000), 500), Is.True);

    [Test]
    public void CompressingCannotMakeRoomForAMessageWhichFillsTheWindowItself()
    {
        Assert.That(ChatCompressionService.MakesRoomFor(ChatWithTwoExchanges(), 0, ContextWindow.Of(32_000), 25_000), Is.False, "The message goes along unchanged, so next to the summary it would fill the window again.");
    }

    [Test]
    public void CompressingNeedsAKnownAndLargeEnoughWindow()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatCompressionService.MakesRoomFor(ChatWithTwoExchanges(), 0, ContextWindow.UNKNOWN, 500), Is.False, "Without a window, nothing can be said about the room.");
            Assert.That(ChatCompressionService.MakesRoomFor(ChatWithTwoExchanges(), 0, ContextWindow.Of(ChatCompressionService.MIN_CONTEXT_WINDOW - 1), 10), Is.False, "The model is too small to write a summary.");
        }
    }

    [Test]
    public void CompressingNeedsSomethingToSummarize()
    {
        var chat = new ChatThread
        {
            Blocks = [Block(ChatRole.USER, "Only question", 1), Block(ChatRole.AI, "Only answer", 2)],
        };

        Assert.That(ChatCompressionService.MakesRoomFor(chat, 0, ContextWindow.Of(32_000), 500), Is.False, "The new chat keeps the last exchange word for word, so there is nothing to summarize.");
    }

    private static ChatThread ChatWithTwoExchanges() => new()
    {
        Blocks =
        [
            Block(ChatRole.USER, "First question", 1),
            Block(ChatRole.AI, "First answer", 2),
            Block(ChatRole.USER, "Second question", 3),
            Block(ChatRole.AI, "Second answer", 4),
        ],
    };

    private static ContentBlock Block(ChatRole role, string text, int minute) => new()
    {
        Time = new DateTimeOffset(2026, 11, 21, 10, minute, 0, TimeSpan.Zero),
        ContentType = ContentType.TEXT,
        Content = new ContentText { Text = text },
        Role = role,
    };
}