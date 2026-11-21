using AIStudio.Chat.Compression;
using AIStudio.Models;

namespace AIStudio.Tests.Chat.Compression;

/// <summary>
/// Checks which models may compress a chat at all, when the notice before sending offers it, and when a summary is
/// too long.
/// </summary>
/// <remarks>
/// A wrong answer here either hides the feature or offers one which leaves the chat as full as before, so that the
/// notice comes again in the new chat.
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

    [TestCase(32_000, true, 500, ChatCompressionOffer.OFFERED, TestName = "Compressing makes room for an ordinary message")]
    [TestCase(32_000, true, 25_000, ChatCompressionOffer.MESSAGE_TOO_LONG, TestName = "A message which fills the window itself goes along unchanged")]
    [TestCase(32_000, false, 500, ChatCompressionOffer.NOTHING_TO_SUMMARIZE, TestName = "The last exchange alone holds nothing to summarize")]
    [TestCase(8_192, false, 316_000, ChatCompressionOffer.MESSAGE_TOO_LONG, TestName = "A message too long for the model is named before an empty chat")]
    [TestCase(ChatCompressionService.MIN_CONTEXT_WINDOW - 1, true, 10, ChatCompressionOffer.WINDOW_TOO_SMALL, TestName = "A model below the minimum writes no summary")]
    public void WhatSpeaksAgainstCompressingBeforeCounting(int window, bool hasSomethingToSummarize, int messageTokens, ChatCompressionOffer expected)
    {
        Assert.That(ChatCompressionService.OfferBeforeCounting(ContextWindow.Of(window), hasSomethingToSummarize, messageTokens), Is.EqualTo(expected));
    }

    [Test]
    public void WithoutAWindowNothingIsSaidAboutCompressing()
    {
        Assert.That(ChatCompressionService.OfferBeforeCounting(ContextWindow.UNKNOWN, true, 500), Is.EqualTo(ChatCompressionOffer.NONE), "Without a window, AI Studio does not ask before sending at all.");
    }

    [TestCase(32_000, 31_000, 25_000, false, TestName = "Summarizing most of the chat makes room")]
    [TestCase(32_000, 300_000, 2_000, true, TestName = "A large file of the last message stays and fills the window again")]
    [TestCase(32_000, 31_000, 7_000, true, TestName = "The last exchange and the summary together still fill the window")]
    public void WhatCompressingKeepsDecidesWhetherItHelps(int window, int tokensWithMessage, int summarizedTokens, bool expected)
    {
        Assert.That(ChatCompressionService.KeepsTooMuch(window, tokensWithMessage, summarizedTokens), Is.EqualTo(expected), "Compressing takes off only the summarized messages and adds the room for the summary.");
    }
}