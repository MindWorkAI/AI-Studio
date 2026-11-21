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
}