using AIStudio.Chat;
using AIStudio.Chat.Embedded;

namespace AIStudio.Tests.Chat.Embedded;

/// <summary>
/// Checks when an embedded chat sends the message it opens with, and when it offers to let the model begin.
/// </summary>
[TestFixture]
public sealed class EmbeddedChatOpeningTests
{
    private static readonly ContentText OPENING_MESSAGE = new() { Text = "What is new today?" };

    private const string START_REQUEST = "Open the conversation with your first message.";

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

    [Test]
    public void AnEmptyChatOffersToBegin()
    {
        Assert.That(EmbeddedChatOpening.OffersStart(START_REQUEST, Conversation()), Is.True);
    }

    [Test]
    public void AChatWithoutStartRequestOffersNothing()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(EmbeddedChatOpening.OffersStart(null, Conversation()), Is.False);
            Assert.That(EmbeddedChatOpening.OffersStart(" ", Conversation()), Is.False);
        }
    }

    [Test]
    public void AChatWhoseConversationBeganOffersNothing()
    {
        var conversation = Conversation();
        conversation.Thread!.Blocks.Add(new ContentBlock { Role = ChatRole.AI, ContentType = ContentType.TEXT, Content = new ContentText { Text = "Hello, what do you prepare?" } });

        Assert.That(EmbeddedChatOpening.OffersStart(START_REQUEST, conversation), Is.False);
    }

    [Test]
    public void AChatWithOnlyAHiddenRequestStillOffersToBegin()
    {
        // E.g., the model failed to answer the request which lets it begin:
        var conversation = Conversation();
        conversation.Thread!.Blocks.Add(new ContentBlock { Role = ChatRole.USER, ContentType = ContentType.TEXT, Content = new ContentText { Text = START_REQUEST }, HideFromUser = true });

        Assert.That(EmbeddedChatOpening.OffersStart(START_REQUEST, conversation), Is.True);
    }

    [Test]
    public void AChatWhichLoadsRunsOrCannotBeOpenedOffersNothing()
    {
        var running = Conversation();
        running.IsRunning = true;
        var unavailable = Conversation();
        unavailable.Thread = null;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(EmbeddedChatOpening.OffersStart(START_REQUEST, null), Is.False);
            Assert.That(EmbeddedChatOpening.OffersStart(START_REQUEST, running), Is.False);
            Assert.That(EmbeddedChatOpening.OffersStart(START_REQUEST, unavailable), Is.False);
        }
    }

    private static EmbeddedConversation Conversation() => new()
    {
        ChatId = Guid.NewGuid(),
        Component = AIStudio.Tools.Components.MODEL_COMPARISON_ASSISTANT,
        Store = new InMemoryEmbeddedChatStore(),
        Thread = new ChatThread(),
    };
}