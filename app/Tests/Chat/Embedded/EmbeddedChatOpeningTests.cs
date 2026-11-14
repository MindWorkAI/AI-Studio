using AIStudio.Chat;
using AIStudio.Chat.Embedded;

namespace AIStudio.Tests.Chat.Embedded;

/// <summary>
/// Checks when an embedded chat sends the message it opens with.
/// </summary>
[TestFixture]
public sealed class EmbeddedChatOpeningTests
{
    private static readonly ContentText OPENING_MESSAGE = new() { Text = "What is new today?" };

    [Test]
    public void AnEmptyChatSendsItsOpeningMessage()
    {
        Assert.That(EmbeddedChatOpening.SendsNow(OPENING_MESSAGE, Conversation(), wasSent: false), Is.True);
    }

    [Test]
    public void AChatWithoutOpeningMessageSendsNothing()
    {
        Assert.That(EmbeddedChatOpening.SendsNow(null, Conversation(), wasSent: false), Is.False);
    }

    [Test]
    public void AChatSendsItsOpeningMessageOnlyOnce()
    {
        // Until the request adds the message to the chat, the chat still looks empty:
        Assert.That(EmbeddedChatOpening.SendsNow(OPENING_MESSAGE, Conversation(), wasSent: true), Is.False);
    }

    [Test]
    public void AChatInWhichTheUserWroteSendsNothing()
    {
        var conversation = Conversation();
        conversation.Thread!.Blocks.Add(new ContentBlock { Role = ChatRole.USER, ContentType = ContentType.TEXT, Content = new ContentText { Text = "My own question." } });

        Assert.That(EmbeddedChatOpening.SendsNow(OPENING_MESSAGE, conversation, wasSent: false), Is.False);
    }

    [Test]
    public void AChatWhichLoadsOrCannotBeOpenedSendsNothing()
    {
        var unavailable = Conversation();
        unavailable.Thread = null;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(EmbeddedChatOpening.SendsNow(OPENING_MESSAGE, null, wasSent: false), Is.False, "While the chat loads, the message waits.");
            Assert.That(EmbeddedChatOpening.SendsNow(OPENING_MESSAGE, unavailable, wasSent: false), Is.False);
        }
    }

    [Test]
    public void AChatInWhichTheModelAnswersSendsNothing()
    {
        var conversation = Conversation();
        conversation.IsRunning = true;

        Assert.That(EmbeddedChatOpening.SendsNow(OPENING_MESSAGE, conversation, wasSent: false), Is.False);
    }

    private static EmbeddedConversation Conversation() => new()
    {
        ChatId = Guid.NewGuid(),
        Component = AIStudio.Tools.Components.MODEL_COMPARISON_ASSISTANT,
        Store = new InMemoryEmbeddedChatStore(),
        Thread = new ChatThread(),
    };
}