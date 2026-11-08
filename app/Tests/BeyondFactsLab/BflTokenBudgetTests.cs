using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Chat;
using AIStudio.Models;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks when the BeyondFacts Lab warns about a full context, and when it stops sending.
/// </summary>
[TestFixture]
public sealed class BflTokenBudgetTests
{
    private const int WINDOW = 32_000;

    [Test]
    public void ACountWhichFailedTellsNothing()
    {
        Assert.That(BflTokenBudget.Of(ConversationTokens.UNAVAILABLE), Is.EqualTo(BflTokenBudgetState.UNKNOWN));
    }

    [Test]
    public void AModelWithoutAStatementAboutItsWindowTellsNothing()
    {
        var tokens = new ConversationTokens { IsKnown = true, HistoryTokens = 500_000, Window = ContextWindow.UNKNOWN };

        Assert.That(BflTokenBudget.Of(tokens), Is.EqualTo(BflTokenBudgetState.UNKNOWN), "The lab cannot know when such a model is full, so it never stops sending.");
    }

    [TestCase(0, BflTokenBudgetState.OK)]
    [TestCase(25_599, BflTokenBudgetState.OK)]
    [TestCase(25_600, BflTokenBudgetState.WARNING)]
    [TestCase(WINDOW - BflTokenBudget.RESERVE_TOKENS, BflTokenBudgetState.WARNING)]
    [TestCase(WINDOW - BflTokenBudget.RESERVE_TOKENS + 1, BflTokenBudgetState.FULL)]
    [TestCase(WINDOW * 2, BflTokenBudgetState.FULL)]
    public void TheShareOfTheWindowDecides(int historyTokens, BflTokenBudgetState expected)
    {
        var tokens = new ConversationTokens { IsKnown = true, HistoryTokens = historyTokens, Window = ContextWindow.Of(WINDOW) };

        Assert.That(BflTokenBudget.Of(tokens), Is.EqualTo(expected));
    }

    [Test]
    public void TheMessageBeingWrittenCountsAsWell()
    {
        var tokens = new ConversationTokens { IsKnown = true, HistoryTokens = 20_000, DraftTokens = 6_000, Window = ContextWindow.Of(WINDOW) };

        Assert.That(BflTokenBudget.Of(tokens), Is.EqualTo(BflTokenBudgetState.WARNING));
    }
}