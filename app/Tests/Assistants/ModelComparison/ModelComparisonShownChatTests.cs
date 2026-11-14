using AIStudio.Assistants.ModelComparison;
using AIStudio.Provider;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks which chat of a task the user works in, which model answers there, and whether it starts with the first
/// message of the first chat.
/// </summary>
[TestFixture]
public sealed class ModelComparisonShownChatTests
{
    private static readonly DateTimeOffset BEGUN = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    private static readonly AIStudio.Settings.Provider FIRST_PROVIDER = new(0, "11111111-1111-1111-1111-111111111111", "First", LLMProviders.OPEN_AI, new Model("model-a", null));

    private static readonly AIStudio.Settings.Provider SECOND_PROVIDER = new(1, "22222222-2222-2222-2222-222222222222", "Second", LLMProviders.MISTRAL, new Model("model-b", null));

    [TestCase(true, "First")]
    [TestCase(false, "Second")]
    public void TheFirstChatHasTheModelDrawnForItAndNoOpeningMessage(bool firstModelIsChatOne, string expectedProvider)
    {
        var task = ModelComparisonSeriesTask.Begin(BEGUN, firstModelIsChatOne);
        var chat = ModelComparisonShownChat.Of(task, FIRST_PROVIDER, SECOND_PROVIDER);

        Assert.That(chat, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(chat!.ChatId, Is.EqualTo(task.ChatOne.ChatId));
            Assert.That(chat.Provider.InstanceName, Is.EqualTo(expectedProvider));
            Assert.That(chat.OpensWithFirstMessage, Is.False, "The first chat starts with whatever the user writes.");
        }
    }

    [TestCase(true, "Second")]
    [TestCase(false, "First")]
    public void TheSecondChatHasTheOtherModelAndStartsWithTheFirstMessage(bool firstModelIsChatOne, string expectedProvider)
    {
        var task = ModelComparisonSeriesTask.Begin(BEGUN, firstModelIsChatOne);
        task.FinishFirstChat(BEGUN.AddMinutes(1), BEGUN.AddMinutes(5), 1, "What is new today?");
        var chat = ModelComparisonShownChat.Of(task, FIRST_PROVIDER, SECOND_PROVIDER);

        Assert.That(chat, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(chat!.ChatId, Is.EqualTo(task.ChatTwo.ChatId));
            Assert.That(chat.Provider.InstanceName, Is.EqualTo(expectedProvider), "The second chat must never go to the model of the first one.");
            Assert.That(chat.OpensWithFirstMessage, Is.True, "Without the first message, the second model would never answer the task.");
        }
    }

    [Test]
    public void ATaskWaitingForTheDecisionShowsNoChat()
    {
        var task = ModelComparisonSeriesTask.Begin(BEGUN, firstModelIsChatOne: true);
        task.FinishFirstChat(BEGUN.AddMinutes(1), BEGUN.AddMinutes(5), 1, "What is new today?");
        task.FinishSecondChat(BEGUN.AddMinutes(6), BEGUN.AddMinutes(9), 1);

        Assert.That(ModelComparisonShownChat.Of(task, FIRST_PROVIDER, SECOND_PROVIDER), Is.Null);
    }
}