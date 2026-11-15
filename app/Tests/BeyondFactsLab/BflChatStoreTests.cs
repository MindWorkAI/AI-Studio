using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Chat;
using AIStudio.Chat.Embedded;
using AIStudio.Settings;
using AIStudio.Tools;
using AIStudio.Tools.AssistantSessions;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks that the embedded chats of the BeyondFacts Lab live where the lab keeps its chats, so that every chat stored
/// before stays readable.
/// </summary>
[TestFixture]
public sealed class BflChatStoreTests
{
    private string dataDirectory = string.Empty;

    private string RootDirectory => Path.Combine(this.dataDirectory, BeyondFactsLabStore.ROOT_DIRECTORY_NAME);

    [SetUp]
    public void CreateDataDirectory()
    {
        this.dataDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-beyondfacts-lab-chat-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.dataDirectory);
    }

    [TearDown]
    public void DeleteDataDirectory()
    {
        if (Directory.Exists(this.dataDirectory))
            Directory.Delete(this.dataDirectory, true);
    }

    [Test]
    public async Task AChatStoredBeforeOpensInTheEmbeddedChatWithAllItsMessages()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var location = new BflStepThreadLocation(created.ProjectId, BflStep.KNOWLEDGE);
        var thread = BflThreadFactory.Create(location);
        AddMessage(thread, ChatRole.USER, BflConversationService.OPENING_REQUEST, isHidden: true);
        AddMessage(thread, ChatRole.AI, "Worauf beruht Ihre Expertise?", isHidden: false);
        await store.SaveThreadAsync(location, thread);

        var conversation = await CreateService().GetAsync(new BflChatStore(this.CreateStore(), location), AIStudio.Tools.Components.BEYOND_FACTS_LAB_ASSISTANT, location.ChatId, () => BflThreadFactory.Create(location));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(conversation.IsAvailable, Is.True);
            Assert.That(conversation.Thread!.ChatId, Is.EqualTo(location.ChatId));
            Assert.That(conversation.Thread.Blocks.Select(block => block.HideFromUser), Is.EqualTo(new[] { true, false }), "The hidden request stays hidden.");
            Assert.That(conversation.UserMessageCount, Is.Zero);
            Assert.That(conversation.Thread.RequiredOutboundDataRestriction, Is.EqualTo(BflChatTools.STEP_REQUIREMENT), "The chat keeps the restriction of its step.");
        }
    }

    [Test]
    public async Task AChatOfAStepIsStoredInTheFolderOfTheStep()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var location = new BflStepThreadLocation(created.ProjectId, BflStep.STRATEGY);
        var thread = BflThreadFactory.Create(location);
        AddMessage(thread, ChatRole.USER, "Wer sitzt mit auf dem Podium?", isHidden: false);

        await new BflChatStore(store, location).SaveThreadAsync(thread);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(Path.Combine(this.RootDirectory, created.ProjectId.ToString("D"), "steps", BflStep.STRATEGY.FolderName, "thread.json")), Is.True);
            Assert.That((await store.LoadThreadAsync(location))!.Blocks, Has.Count.EqualTo(1));
            Assert.That(store.ReadAllThreadLocations(), Does.ContainKey(location.ChatId), "The cleanup of the provenance store finds the chat.");
        }
    }

    [Test]
    public async Task TheOnboardingIsStoredInItsOwnFolder()
    {
        var store = this.CreateStore();
        var location = BflOnboardingThreadLocation.INSTANCE;
        var thread = BflThreadFactory.Create(location);
        AddMessage(thread, ChatRole.USER, "Passt das Lab zu meinem Interview?", isHidden: false);

        await new BflChatStore(store, location).SaveThreadAsync(thread);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(Path.Combine(this.RootDirectory, "onboarding", "thread.json")), Is.True);
            Assert.That(store.ReadAllThreadLocations(), Does.ContainKey(location.ChatId));
        }
    }

    [Test]
    public async Task AChatOfAnotherPlaceIsRefused()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var chatStore = new BflChatStore(store, new BflStepThreadLocation(created.ProjectId, BflStep.FRAMEWORK));
        var otherLocation = new BflStepThreadLocation(created.ProjectId, BflStep.STRATEGY);

        using (Assert.EnterMultipleScope())
        {
            await Assert.ThrowsAsync<ArgumentException>(async () => await chatStore.LoadThreadAsync(otherLocation.ChatId));
            await Assert.ThrowsAsync<ArgumentException>(async () => await chatStore.SaveThreadAsync(BflThreadFactory.Create(otherLocation)));
            await Assert.ThrowsAsync<ArgumentException>(async () => await chatStore.DeleteThreadAsync(otherLocation.ChatId));
        }
    }

    [Test]
    public async Task StartingOverDeletesTheFileOfTheChat()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var location = new BflStepThreadLocation(created.ProjectId, BflStep.FRAMEWORK);
        var thread = BflThreadFactory.Create(location);
        AddMessage(thread, ChatRole.USER, "Hallo", isHidden: false);
        await store.SaveThreadAsync(location, thread);

        var service = CreateService();
        var conversation = await service.GetAsync(new BflChatStore(store, location), AIStudio.Tools.Components.BEYOND_FACTS_LAB_ASSISTANT, location.ChatId, () => BflThreadFactory.Create(location));
        var reset = await service.ResetAsync(location.ChatId, () => BflThreadFactory.Create(location));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reset, Is.True);
            Assert.That(await store.LoadThreadAsync(location), Is.Null);
            Assert.That(conversation.Thread!.Blocks, Is.Empty);
            Assert.That(conversation.Thread.RequiredOutboundDataRestriction, Is.EqualTo(BflChatTools.STEP_REQUIREMENT), "The new conversation starts restricted as well.");
        }
    }

    private static void AddMessage(ChatThread thread, ChatRole role, string text, bool isHidden) => thread.Blocks.Add(new ContentBlock
    {
        Time = DateTimeOffset.Now,
        ContentType = ContentType.TEXT,
        Role = role,
        Content = new ContentText { Text = text },
        HideFromUser = isHidden
    });

    private static EmbeddedChatService CreateService() => new(new SettingsManager(NullLogger<SettingsManager>.Instance, null!), new AssistantSessionService(MessageBus.INSTANCE), MessageBus.INSTANCE, NullLogger<EmbeddedChatService>.Instance, (_, _, _, _) => Task.CompletedTask);

    private BeyondFactsLabStore CreateStore() => new(NullLogger<BeyondFactsLabStore>.Instance, new BflStorageOptions { DataDirectory = this.dataDirectory });
}