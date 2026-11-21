using AIStudio.Chat;
using AIStudio.Chat.Embedded;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools;
using AIStudio.Tools.AssistantSessions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Chat.Embedded;

/// <summary>
/// Checks how EmbeddedChatService runs a request, without asking any provider.
/// </summary>
[TestFixture]
public sealed class EmbeddedChatServiceTests
{
    private const AIStudio.Tools.Components COMPONENT = AIStudio.Tools.Components.MODEL_COMPARISON_ASSISTANT;

    private SettingsManager settingsManager = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;
    private AIStudio.Settings.Provider provider = null!;

    [SetUp]
    public void CreateSettings()
    {
        this.settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        this.previousServiceProvider = Program.SERVICE_PROVIDER;
        this.serviceProvider = new ServiceCollection().AddSingleton(this.settingsManager).BuildServiceProvider();
        Program.SERVICE_PROVIDER = this.serviceProvider;

        this.provider = new AIStudio.Settings.Provider(1, Guid.NewGuid().ToString(), "Instance 1", LLMProviders.DEEP_SEEK, new("test-model", null));
    }

    [TearDown]
    public void RestoreApplicationState()
    {
        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        this.serviceProvider.Dispose();
    }

    [Test]
    public async Task AChatWithoutAStoredThreadStartsWithANewOne()
    {
        var service = this.CreateService(Answer("An answer."));
        var store = new InMemoryEmbeddedChatStore();
        var chatId = Guid.NewGuid();

        var conversation = await service.GetAsync(store, COMPONENT, chatId, () => new ChatThread());
        var again = await service.GetAsync(store, COMPONENT, chatId, () => new ChatThread());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(conversation.IsAvailable, Is.True);
            Assert.That(conversation.Thread!.ChatId, Is.EqualTo(chatId), "The new thread gets the ID of the chat.");
            Assert.That(again, Is.SameAs(conversation), "A chat is loaded once.");
        }
    }

    [Test]
    public async Task AChatThisVersionCannotOpenTakesNoMessage()
    {
        var service = this.CreateService(Answer("An answer."));
        var store = new InMemoryEmbeddedChatStore { CannotOpen = true };
        var chatId = Guid.NewGuid();

        var conversation = await service.GetAsync(store, COMPONENT, chatId, () => new ChatThread());
        var sent = await service.SendAsync(chatId, this.Request("A question."));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(conversation.IsAvailable, Is.False);
            Assert.That(sent, Is.False);
            Assert.That(store.SaveCount, Is.Zero, "The stored chat stays as it is.");
        }
    }

    [Test]
    public async Task TheMessageIsStoredBeforeTheModelAnswers()
    {
        var store = new InMemoryEmbeddedChatStore();
        var chatId = Guid.NewGuid();
        var blocksStoredBeforeTheAnswer = -1;
        var service = this.CreateService((_, answer, _, _) =>
        {
            blocksStoredBeforeTheAnswer = store.StoredBlocks(chatId).Count;
            answer.Text = "An answer.";
            return Task.CompletedTask;
        });

        var conversation = await service.GetAsync(store, COMPONENT, chatId, () => new ChatThread());
        var sent = await service.SendAsync(chatId, this.Request("A question."));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent, Is.True);
            Assert.That(blocksStoredBeforeTheAnswer, Is.EqualTo(1), "Only the message was stored when the model began.");
            Assert.That(store.StoredBlocks(chatId).Select(block => block.Role), Is.EqualTo(new[] { ChatRole.USER, ChatRole.AI }));
            Assert.That(conversation.UserMessageCount, Is.EqualTo(1));
            Assert.That(conversation.HasAnswer, Is.True);
            Assert.That(conversation.IsRunning, Is.False);
        }
    }

    [Test]
    public async Task AnEmptyAnswerLeavesOnlyTheMessage()
    {
        var store = new InMemoryEmbeddedChatStore();
        var chatId = Guid.NewGuid();
        var service = this.CreateService(Answer(string.Empty));

        var conversation = await service.GetAsync(store, COMPONENT, chatId, () => new ChatThread());
        var sent = await service.SendAsync(chatId, this.Request("A question."));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent, Is.True, "The message went in, even without an answer.");
            Assert.That(store.StoredBlocks(chatId).Select(block => block.Role), Is.EqualTo(new[] { ChatRole.USER }));
            Assert.That(conversation.HasAnswer, Is.False);
        }
    }

    [Test]
    public async Task ARefusedPreparationSendsNothing()
    {
        var store = new InMemoryEmbeddedChatStore();
        var chatId = Guid.NewGuid();
        var service = this.CreateService(Answer("An answer."));

        var conversation = await service.GetAsync(store, COMPONENT, chatId, () => new ChatThread());
        var sent = await service.SendAsync(chatId, this.Request("A question.") with { PrepareAsync = _ => Task.FromResult(EmbeddedChatPreparation.REFUSED) });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent, Is.False);
            Assert.That(conversation.Thread!.Blocks, Is.Empty);
            Assert.That(store.SaveCount, Is.Zero);
            Assert.That(conversation.IsRunning, Is.False, "A refused request does not block the next one.");
        }
    }

    [Test]
    public async Task WithoutAProviderNothingIsSent()
    {
        var store = new InMemoryEmbeddedChatStore();
        var chatId = Guid.NewGuid();
        var service = this.CreateService(Answer("An answer."));

        var conversation = await service.GetAsync(store, COMPONENT, chatId, () => new ChatThread());
        var sent = await service.SendAsync(chatId, this.Request("A question.") with { Provider = AIStudio.Settings.Provider.NONE });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent, Is.False);
            Assert.That(conversation.Thread!.Blocks, Is.Empty);
        }
    }

    [Test]
    public async Task ASecondMessageWaitsUntilTheFirstIsAnswered()
    {
        var store = new InMemoryEmbeddedChatStore();
        var chatId = Guid.NewGuid();
        var answering = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var service = this.CreateService(async (_, answer, _, _) =>
        {
            answering.SetResult();
            await release.Task;
            answer.Text = "An answer.";
        });

        var conversation = await service.GetAsync(store, COMPONENT, chatId, () => new ChatThread());
        var first = service.SendAsync(chatId, this.Request("A question."));
        await answering.Task;

        var second = await service.SendAsync(chatId, this.Request("Another question."));
        release.SetResult();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second, Is.False);
            Assert.That(await first, Is.True);
            Assert.That(conversation.UserMessageCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task TheRequestMayCarryMoreThanTheStoredChat()
    {
        var store = new InMemoryEmbeddedChatStore();
        var chatId = Guid.NewGuid();
        var blocksSent = -1;
        var service = this.CreateService((_, answer, thread, _) =>
        {
            blocksSent = thread.Blocks.Count;
            answer.Text = "An answer.";
            return Task.CompletedTask;
        });

        await service.GetAsync(store, COMPONENT, chatId, () => new ChatThread());
        var extraBlock = new ContentBlock { Role = ChatRole.USER, ContentType = ContentType.TEXT, Content = new ContentText { Text = "An image description." } };
        var preparation = EmbeddedChatPreparation.READY with { ThreadForRequest = thread => thread with { Blocks = [extraBlock, .. thread.Blocks] } };
        await service.SendAsync(chatId, this.Request("A question.") with { PrepareAsync = _ => Task.FromResult(preparation) });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(blocksSent, Is.EqualTo(3), "The request carried the extra block, the message, and the answer.");
            Assert.That(store.StoredBlocks(chatId), Has.Count.EqualTo(2), "The stored chat never holds the extra block.");
        }
    }

    [Test]
    public async Task AHiddenMessageIsNotCountedAsOneOfTheUser()
    {
        var store = new InMemoryEmbeddedChatStore();
        var chatId = Guid.NewGuid();
        var service = this.CreateService(Answer("Hello, how can I help?"));

        var conversation = await service.GetAsync(store, COMPONENT, chatId, () => new ChatThread());
        await service.SendAsync(chatId, this.Request("Open the conversation.") with { IsHidden = true });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(conversation.UserMessageCount, Is.Zero);
            Assert.That(conversation.HasVisibleMessages, Is.True, "The answer is visible.");
        }
    }

    [Test]
    public async Task StartingOverDeletesTheConversationAndKeepsTheIdOfTheChat()
    {
        var store = new InMemoryEmbeddedChatStore();
        var chatId = Guid.NewGuid();
        var service = this.CreateService(Answer("An answer."));

        var conversation = await service.GetAsync(store, COMPONENT, chatId, () => new ChatThread());
        await service.SendAsync(chatId, this.Request("A question."));
        var reset = await service.ResetAsync(chatId, () => new ChatThread { Name = "Started over" });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reset, Is.True);
            Assert.That(store.IsStored(chatId), Is.False, "The stored conversation is gone.");
            Assert.That(conversation.Thread!.Blocks, Is.Empty);
            Assert.That(conversation.Thread.ChatId, Is.EqualTo(chatId));
            Assert.That(conversation.Thread.Name, Is.EqualTo("Started over"), "The new conversation starts as a new chat does.");
        }
    }

    [Test]
    public async Task AConversationDoesNotStartOverWhileTheModelAnswers()
    {
        var store = new InMemoryEmbeddedChatStore();
        var chatId = Guid.NewGuid();
        var answering = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var service = this.CreateService(async (_, answer, _, _) =>
        {
            answering.SetResult();
            await release.Task;
            answer.Text = "An answer.";
        });

        var conversation = await service.GetAsync(store, COMPONENT, chatId, () => new ChatThread());
        var sending = service.SendAsync(chatId, this.Request("A question."));
        await answering.Task;

        var reset = await service.ResetAsync(chatId, () => new ChatThread());
        release.SetResult();
        await sending;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reset, Is.False);
            Assert.That(conversation.Thread!.Blocks, Has.Count.EqualTo(2), "The question and its answer stay.");
            Assert.That(store.IsStored(chatId), Is.True);
        }
    }

    [Test]
    public async Task AChatThisVersionCannotOpenBecomesAvailableWhenItStartsOver()
    {
        var store = new InMemoryEmbeddedChatStore { CannotOpen = true };
        var chatId = Guid.NewGuid();
        var service = this.CreateService(Answer("An answer."));

        var conversation = await service.GetAsync(store, COMPONENT, chatId, () => new ChatThread());
        var wasAvailable = conversation.IsAvailable;
        var reset = await service.ResetAsync(chatId, () => new ChatThread());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(wasAvailable, Is.False);
            Assert.That(reset, Is.True);
            Assert.That(conversation.IsAvailable, Is.True);
        }
    }

    [Test]
    public async Task AChatWhichWasNeverLoadedDoesNotStartOver()
    {
        var service = this.CreateService(Answer("An answer."));
        Assert.That(await service.ResetAsync(Guid.NewGuid(), () => new ChatThread()), Is.False);
    }

    [Test]
    public async Task AReplacedConversationIsStoredAndKeepsTheIdOfTheChat()
    {
        var store = new InMemoryEmbeddedChatStore();
        var chatId = Guid.NewGuid();
        var service = this.CreateService(Answer("An answer."));

        var conversation = await service.GetAsync(store, COMPONENT, chatId, () => new ChatThread());
        await service.SendAsync(chatId, this.Request("A question."));
        var compressed = new ChatThread { ChatId = Guid.NewGuid(), Blocks = [new ContentBlock { Time = DateTimeOffset.Now, ContentType = ContentType.TEXT, Role = ChatRole.USER, Content = new ContentText { Text = "The summary" } }] };
        var replaced = await service.ReplaceAsync(chatId, compressed);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(replaced, Is.True);
            Assert.That(conversation.Thread, Is.SameAs(compressed), "The chat shows the new conversation.");
            Assert.That(conversation.Thread!.ChatId, Is.EqualTo(chatId), "The chat stays the one of its assistant.");
            Assert.That(store.StoredBlocks(chatId).Select(block => ((ContentText)block.Content!).Text), Is.EqualTo(new[] { "The summary" }), "What the chat shows is on the disk as well.");
        }
    }

    [Test]
    public async Task AConversationIsNotReplacedWhileTheModelAnswers()
    {
        var store = new InMemoryEmbeddedChatStore();
        var chatId = Guid.NewGuid();
        var answering = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var service = this.CreateService(async (_, answer, _, _) =>
        {
            answering.SetResult();
            await release.Task;
            answer.Text = "An answer.";
        });

        var conversation = await service.GetAsync(store, COMPONENT, chatId, () => new ChatThread());
        var sending = service.SendAsync(chatId, this.Request("A question."));
        await answering.Task;

        var replaced = await service.ReplaceAsync(chatId, new ChatThread());
        release.SetResult();
        await sending;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(replaced, Is.False, "The answer would go into a conversation nobody sees anymore.");
            Assert.That(conversation.Thread!.Blocks, Has.Count.EqualTo(2), "The question and its answer stay.");
        }
    }

    private EmbeddedChatService CreateService(Func<EmbeddedChatRequest, ContentText, ChatThread, CancellationToken, Task> answerAsync) => new(this.settingsManager, new AssistantSessionService(MessageBus.INSTANCE), MessageBus.INSTANCE, NullLogger<EmbeddedChatService>.Instance, answerAsync);

    private EmbeddedChatRequest Request(string text) => new() { Provider = this.provider, Message = new ContentText { Text = text } };

    private static Func<EmbeddedChatRequest, ContentText, ChatThread, CancellationToken, Task> Answer(string text) => (_, answer, _, _) =>
    {
        answer.Text = text;
        return Task.CompletedTask;
    };
}