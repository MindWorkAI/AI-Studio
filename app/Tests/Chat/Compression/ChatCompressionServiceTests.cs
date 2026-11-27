using AIStudio.AIOperations.ConversationSummary;
using AIStudio.Chat.Compression;
using AIStudio.Models;

namespace AIStudio.Tests.Chat.Compression;

/// <summary>
/// Checks when the notice before sending offers to compress a chat.
/// </summary>
/// <remarks>
/// A wrong answer here either hides the feature or offers one which leaves the chat as full as before, so that the
/// notice comes again in the new chat. Another model may write the summary than the one of the chat, so two windows
/// count: the one of the writer decides whether it can write a summary at all, the one of the chat what fits.
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
        var contextWindow = ContextWindow.Of(window);

        Assert.That(ChatCompressionService.OfferBeforeCounting(contextWindow, contextWindow, hasSomethingToSummarize, messageTokens, ChatCompressionOptions.DEFAULT), Is.EqualTo(expected), "Here, the model of the chat writes the summary itself.");
    }

    [Test]
    public void WithoutAWindowNothingIsSaidAboutCompressing()
    {
        Assert.That(ChatCompressionService.OfferBeforeCounting(ContextWindow.UNKNOWN, ContextWindow.Of(32_000), true, 500, ChatCompressionOptions.DEFAULT), Is.EqualTo(ChatCompressionOffer.NONE), "Without a window, AI Studio does not ask before sending at all.");
    }

    [Test]
    public void ALargeWriterSummarizesTheChatOfASmallModel()
    {
        var smallChatWindow = ContextWindow.Of(4_096);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatCompressionService.OfferBeforeCounting(smallChatWindow, smallChatWindow, true, 500, ChatCompressionOptions.DEFAULT), Is.EqualTo(ChatCompressionOffer.WINDOW_TOO_SMALL), "Alone, the small model cannot write a summary.");
            Assert.That(ChatCompressionService.OfferBeforeCounting(smallChatWindow, ContextWindow.Of(128_000), true, 500, ChatCompressionOptions.DEFAULT), Is.EqualTo(ChatCompressionOffer.OFFERED), "A model with a large window writes the summary, and the small model continues with it.");
        }
    }

    [Test]
    public void WithoutAWriterNothingCanBeCompressed()
    {
        Assert.That(ChatCompressionService.OfferBeforeCounting(ContextWindow.Of(32_000), null, true, 500, ChatCompressionOptions.DEFAULT), Is.EqualTo(ChatCompressionOffer.WINDOW_TOO_SMALL), "When no model may write the summary, the notice says which window it needs.");
    }

    [Test]
    public void TheSummaryHasToFitIntoTheWindowOfTheChat()
    {
        var chatWindow = ContextWindow.Of(10_000);

        // The writer would keep 8,192 tokens for the summary, the chat only 2,500, and 6,900 + 2,500 stays below 9,500:
        Assert.That(ChatCompressionService.OfferBeforeCounting(chatWindow, ContextWindow.Of(128_000), true, 6_900, ChatCompressionOptions.DEFAULT), Is.EqualTo(ChatCompressionOffer.OFFERED), "The room for the summary follows the smaller of both windows.");
    }

    [TestCase(32_000, 31_000, 25_000, false, TestName = "Summarizing most of the chat makes room")]
    [TestCase(32_000, 300_000, 2_000, true, TestName = "A large file of the last message stays and fills the window again")]
    [TestCase(32_000, 31_000, 7_000, true, TestName = "The last exchange and the summary together still fill the window")]
    public void WhatCompressingKeepsDecidesWhetherItHelps(int window, int tokensWithMessage, int summarizedTokens, bool expected)
    {
        var tokensForTheSummary = ConversationSummaryBudget.TokensForTheSummary(window, ChatCompressionOptions.DEFAULT.MaxSummaryTokens);

        Assert.That(ChatCompressionService.KeepsTooMuch(window, tokensWithMessage, summarizedTokens, tokensForTheSummary, ChatCompressionOptions.DEFAULT.ShareToAsk), Is.EqualTo(expected), "Compressing takes off only the summarized messages and adds the room for the summary.");
    }

    [Test]
    public void ALowerShareMakesCompressingHelpLessOften()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatCompressionService.KeepsTooMuch(32_000, 31_000, 16_000, 8_000, 0.95), Is.False, "At 95 percent, the compressed chat stays below the share.");
            Assert.That(ChatCompressionService.KeepsTooMuch(32_000, 31_000, 16_000, 8_000, 0.5), Is.True, "At 50 percent, the same chat would reach the share again right away.");
        }
    }

    [Test]
    public void AShorterSummaryLeavesMoreRoomForTheMessage()
    {
        var options = ChatCompressionOptions.DEFAULT with { MaxSummaryTokens = 1_024 };
        var window = ContextWindow.Of(32_000);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatCompressionService.OfferBeforeCounting(window, window, true, 23_000, ChatCompressionOptions.DEFAULT), Is.EqualTo(ChatCompressionOffer.MESSAGE_TOO_LONG), "A message and a summary of 8,000 tokens fill the window.");
            Assert.That(ChatCompressionService.OfferBeforeCounting(window, window, true, 23_000, options), Is.EqualTo(ChatCompressionOffer.OFFERED), "With a summary of at most 1,024 tokens, the message fits.");
        }
    }
}