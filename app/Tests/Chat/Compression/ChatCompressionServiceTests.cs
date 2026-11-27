using AIStudio.AIOperations.ConversationSummary;
using AIStudio.Chat.Compression;
using AIStudio.Models;

namespace AIStudio.Tests.Chat.Compression;

/// <summary>
/// Checks when the notice before sending offers to compress a chat.
/// </summary>
/// <remarks>
/// A wrong answer here either hides the feature or offers one which leaves the chat as full as before, so that the
/// notice comes again in the new chat.
/// </remarks>
[TestFixture]
public sealed class ChatCompressionServiceTests
{
    [TestCase(32_000, true, 500, ChatCompressionOffer.OFFERED, TestName = "Compressing makes room for an ordinary message")]
    [TestCase(32_000, true, 25_000, ChatCompressionOffer.MESSAGE_TOO_LONG, TestName = "A message which fills the window itself goes along unchanged")]
    [TestCase(32_000, false, 500, ChatCompressionOffer.NOTHING_TO_SUMMARIZE, TestName = "The last exchange alone holds nothing to summarize")]
    [TestCase(8_192, false, 316_000, ChatCompressionOffer.MESSAGE_TOO_LONG, TestName = "A message too long for the model is named before an empty chat")]
    [TestCase(ConversationSummaryBudget.MIN_CONTEXT_WINDOW - 1, true, 10, ChatCompressionOffer.WINDOW_TOO_SMALL, TestName = "A model below the minimum writes no summary")]
    public void WhatSpeaksAgainstCompressingBeforeCounting(int window, bool hasSomethingToSummarize, int messageTokens, ChatCompressionOffer expected)
    {
        Assert.That(ChatCompressionService.OfferBeforeCounting(ContextWindow.Of(window), hasSomethingToSummarize, messageTokens, ChatCompressionOptions.DEFAULT), Is.EqualTo(expected));
    }

    [Test]
    public void WithoutAWindowNothingIsSaidAboutCompressing()
    {
        Assert.That(ChatCompressionService.OfferBeforeCounting(ContextWindow.UNKNOWN, true, 500, ChatCompressionOptions.DEFAULT), Is.EqualTo(ChatCompressionOffer.NONE), "Without a window, AI Studio does not ask before sending at all.");
    }

    [TestCase(32_000, 31_000, 25_000, false, TestName = "Summarizing most of the chat makes room")]
    [TestCase(32_000, 300_000, 2_000, true, TestName = "A large file of the last message stays and fills the window again")]
    [TestCase(32_000, 31_000, 7_000, true, TestName = "The last exchange and the summary together still fill the window")]
    public void WhatCompressingKeepsDecidesWhetherItHelps(int window, int tokensWithMessage, int summarizedTokens, bool expected)
    {
        Assert.That(ChatCompressionService.KeepsTooMuch(window, tokensWithMessage, summarizedTokens, ChatCompressionOptions.DEFAULT), Is.EqualTo(expected), "Compressing takes off only the summarized messages and adds the room for the summary.");
    }

    [Test]
    public void ALowerShareMakesCompressingHelpLessOften()
    {
        var options = ChatCompressionOptions.DEFAULT with { ShareToAsk = 0.5 };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatCompressionService.KeepsTooMuch(32_000, 31_000, 16_000, ChatCompressionOptions.DEFAULT), Is.False, "At 95 percent, the compressed chat stays below the share.");
            Assert.That(ChatCompressionService.KeepsTooMuch(32_000, 31_000, 16_000, options), Is.True, "At 50 percent, the same chat would reach the share again right away.");
        }
    }

    [Test]
    public void AShorterSummaryLeavesMoreRoomForTheMessage()
    {
        var options = ChatCompressionOptions.DEFAULT with { MaxSummaryTokens = 1_024 };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatCompressionService.OfferBeforeCounting(ContextWindow.Of(32_000), true, 23_000, ChatCompressionOptions.DEFAULT), Is.EqualTo(ChatCompressionOffer.MESSAGE_TOO_LONG), "A message and a summary of 8,000 tokens fill the window.");
            Assert.That(ChatCompressionService.OfferBeforeCounting(ContextWindow.Of(32_000), true, 23_000, options), Is.EqualTo(ChatCompressionOffer.OFFERED), "With a summary of at most 1,024 tokens, the message fits.");
        }
    }
}