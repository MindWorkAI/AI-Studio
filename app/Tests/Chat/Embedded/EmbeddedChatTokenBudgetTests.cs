using AIStudio.Chat;
using AIStudio.Chat.Embedded;
using AIStudio.Models;

namespace AIStudio.Tests.Chat.Embedded;

/// <summary>
/// Checks when an embedded chat warns about a full context, and when it stops sending, with the context window shown or
/// hidden.
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

    [TestCase(20_000, 1_000, WINDOW, EmbeddedChatSendNotice.NONE, TestName = "A message which fits needs no notice")]
    [TestCase(27_000, 1_000, WINDOW, EmbeddedChatSendNotice.FULL, TestName = "In a small window the reserve refuses the message before the share asks")]
    [TestCase(950_000, 1_000, 1_000_000, EmbeddedChatSendNotice.NEARLY_FULL, TestName = "In a large window the share asks before the reserve refuses")]
    [TestCase(990_000, 7_000, 1_000_000, EmbeddedChatSendNotice.FULL, TestName = "In a large window the reserve still refuses the last message")]
    public void TheMessageDecidesTheNoticeBeforeSending(int historyTokens, int messageTokens, int window, EmbeddedChatSendNotice expected)
    {
        var tokens = new ConversationTokens { IsKnown = true, HistoryTokens = historyTokens, DraftTokens = messageTokens, Window = ContextWindow.Of(window) };

        Assert.That(EmbeddedChatTokenBudget.NoticeBeforeSending(tokens, showsContextWindow: true), Is.EqualTo(expected));
    }

    [Test]
    public void AnUnknownWindowNeedsNoNotice()
    {
        var tokens = new ConversationTokens { IsKnown = true, HistoryTokens = 500_000, DraftTokens = 1_000, Window = ContextWindow.UNKNOWN };

        Assert.That(EmbeddedChatTokenBudget.NoticeBeforeSending(tokens, showsContextWindow: true), Is.EqualTo(EmbeddedChatSendNotice.NONE), "Nobody wrote the window down, so nobody can say that it is full.");
    }

    [TestCase(950_000, 1_000, EmbeddedChatSendNotice.NONE, TestName = "A hidden window asks nothing before it is full")]
    [TestCase(990_000, 7_000, EmbeddedChatSendNotice.FULL, TestName = "A hidden window still refuses the message which no longer fits")]
    public void AHiddenWindowOnlyRefuses(int historyTokens, int messageTokens, EmbeddedChatSendNotice expected)
    {
        var tokens = new ConversationTokens { IsKnown = true, HistoryTokens = historyTokens, DraftTokens = messageTokens, Window = ContextWindow.Of(1_000_000) };

        Assert.That(EmbeddedChatTokenBudget.NoticeBeforeSending(tokens, showsContextWindow: false), Is.EqualTo(expected), "Asking would name the window, which tells the models apart.");
    }

    [TestCase(10_000, true, EmbeddedChatTokenBudgetState.OK)]
    [TestCase(26_000, true, EmbeddedChatTokenBudgetState.WARNING)]
    [TestCase(30_000, true, EmbeddedChatTokenBudgetState.FULL)]
    [TestCase(10_000, false, EmbeddedChatTokenBudgetState.UNKNOWN)]
    [TestCase(26_000, false, EmbeddedChatTokenBudgetState.UNKNOWN)]
    [TestCase(30_000, false, EmbeddedChatTokenBudgetState.FULL)]
    public void AHiddenWindowShowsOnlyThatItIsFull(int historyTokens, bool showsContextWindow, EmbeddedChatTokenBudgetState expected)
    {
        var tokens = new ConversationTokens { IsKnown = true, HistoryTokens = historyTokens, Window = ContextWindow.Of(WINDOW) };

        Assert.That(EmbeddedChatTokenBudget.Shown(tokens, showsContextWindow), Is.EqualTo(expected));
    }
}