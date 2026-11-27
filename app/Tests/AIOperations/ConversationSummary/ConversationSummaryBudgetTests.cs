using AIStudio.AIOperations.ConversationSummary;
using AIStudio.Models;

namespace AIStudio.Tests.AIOperations.ConversationSummary;

/// <summary>
/// Checks which models may write a summary at all, and when a summary is too long.
/// </summary>
/// <remarks>
/// A wrong answer here either hides compressing a chat or lets a summary take the room the next part or the new chat
/// needs.
/// </remarks>
[TestFixture]
public sealed class ConversationSummaryBudgetTests
{
    [Test]
    public void AWindowBelowTheMinimumCannotSummarize() => Assert.That(ConversationSummaryBudget.CanSummarizeWith(ContextWindow.Of(ConversationSummaryBudget.MIN_CONTEXT_WINDOW - 1)), Is.False);

    [Test]
    public void AWindowOfTheMinimumCanSummarize() => Assert.That(ConversationSummaryBudget.CanSummarizeWith(ContextWindow.Of(ConversationSummaryBudget.MIN_CONTEXT_WINDOW)), Is.True);

    [Test]
    public void AnUnknownWindowCanSummarize() => Assert.That(ConversationSummaryBudget.CanSummarizeWith(ContextWindow.UNKNOWN), Is.True);

    [TestCase(8_192, 8_192, false, TestName = "A summary of the length asked for stays")]
    [TestCase(12_288, 8_192, false, TestName = "A summary up to the tolerance stays")]
    [TestCase(12_289, 8_192, true, TestName = "A summary beyond the tolerance is shortened")]
    [TestCase(100_000, 0, false, TestName = "Without a known room nothing is shortened")]
    public void ASummaryIsShortenedOnlyBeyondTheTolerance(int summaryTokens, int tokensForTheSummary, bool expected)
    {
        Assert.That(ConversationSummaryBudget.IsTooLong(summaryTokens, tokensForTheSummary), Is.EqualTo(expected), "Models cannot count, so a summary somewhat longer than asked for is fine.");
    }

    [TestCase(32_000, 8_192, 8_000, TestName = "A quarter of the window")]
    [TestCase(100_000, 8_192, 8_192, TestName = "At most what the settings allow")]
    [TestCase(100_000, 16_384, 16_384, TestName = "A larger maximum lets a large window keep more")]
    [TestCase(2_000, 8_192, ConversationSummaryBudget.MIN_TOKENS_FOR_THE_SUMMARY, TestName = "At least the room for a summary at all")]
    [TestCase(100_000, 500, ConversationSummaryBudget.MIN_TOKENS_FOR_THE_SUMMARY, TestName = "A maximum below the least room counts as the least room")]
    public void TheRoomForTheSummaryFollowsTheWindowAndTheSettings(int windowTokens, int maxSummaryTokens, int expected)
    {
        Assert.That(ConversationSummaryBudget.TokensForTheSummary(windowTokens, maxSummaryTokens), Is.EqualTo(expected));
    }
}