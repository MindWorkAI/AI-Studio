using AIStudio.Chat;
using AIStudio.Chat.Embedded;
using AIStudio.Models;

namespace AIStudio.Tests.Chat.Embedded;

/// <summary>
/// Checks when an embedded chat which shows the context window warns about a full context, and when it stops sending.
/// </summary>
[TestFixture]
public sealed class EmbeddedChatTokenBudgetTests
{
    private const int WINDOW = 32_000;

    [Test]
    public void ACountWhichFailedTellsNothing()
    {
        Assert.That(EmbeddedChatTokenBudget.Of(ConversationTokens.UNAVAILABLE), Is.EqualTo(EmbeddedChatTokenBudgetState.UNKNOWN));
    }

    [Test]
    public void AModelWithoutAStatementAboutItsWindowTellsNothing()
    {
        var tokens = new ConversationTokens { IsKnown = true, HistoryTokens = 500_000, Window = ContextWindow.UNKNOWN };

        Assert.That(EmbeddedChatTokenBudget.Of(tokens), Is.EqualTo(EmbeddedChatTokenBudgetState.UNKNOWN), "The chat cannot know when such a model is full, so it never stops sending.");
    }

    [TestCase(0, EmbeddedChatTokenBudgetState.OK)]
    [TestCase(25_599, EmbeddedChatTokenBudgetState.OK)]
    [TestCase(25_600, EmbeddedChatTokenBudgetState.WARNING)]
    [TestCase(WINDOW - EmbeddedChatTokenBudget.RESERVE_TOKENS, EmbeddedChatTokenBudgetState.WARNING)]
    [TestCase(WINDOW - EmbeddedChatTokenBudget.RESERVE_TOKENS + 1, EmbeddedChatTokenBudgetState.FULL)]
    [TestCase(WINDOW * 2, EmbeddedChatTokenBudgetState.FULL)]
    public void TheShareOfTheWindowDecides(int historyTokens, EmbeddedChatTokenBudgetState expected)
    {
        var tokens = new ConversationTokens { IsKnown = true, HistoryTokens = historyTokens, Window = ContextWindow.Of(WINDOW) };

        Assert.That(EmbeddedChatTokenBudget.Of(tokens), Is.EqualTo(expected));
    }

    [Test]
    public void TheMessageBeingWrittenCountsAsWell()
    {
        var tokens = new ConversationTokens { IsKnown = true, HistoryTokens = 20_000, DraftTokens = 6_000, Window = ContextWindow.Of(WINDOW) };

        Assert.That(EmbeddedChatTokenBudget.Of(tokens), Is.EqualTo(EmbeddedChatTokenBudgetState.WARNING));
    }
}