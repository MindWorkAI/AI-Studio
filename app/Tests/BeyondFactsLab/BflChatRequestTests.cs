using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Chat;
using AIStudio.Chat.Embedded;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks what a request of a chat of the BeyondFacts Lab carries when it runs in the embedded chat, without asking any
/// provider.
/// </summary>
[TestFixture]
public sealed class BflChatRequestTests
{
    private const AIStudio.Tools.Components LAB = AIStudio.Tools.Components.BEYOND_FACTS_LAB_ASSISTANT;

    private const string SYSTEM_PROMPT = "Der Prompt des Schritts.";

    private string dataDirectory = string.Empty;
    private SettingsManager settingsManager = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;
    private AIStudio.Settings.Provider provider = null!;

    [SetUp]
    public void CreateSettings()
    {
        this.dataDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-beyondfacts-lab-chat-requests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.dataDirectory);

        // The scheme which trusts Europe gives Mistral MEDIUM, the floor of the lab:
        this.settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        this.settingsManager.ConfigurationData.Confidence.ConfidenceScheme = ConfidenceSchemes.TRUST_EUROPE;
        this.previousServiceProvider = Program.SERVICE_PROVIDER;
        this.serviceProvider = new ServiceCollection().AddSingleton(this.settingsManager).BuildServiceProvider();
        Program.SERVICE_PROVIDER = this.serviceProvider;

        this.provider = new AIStudio.Settings.Provider(1, Guid.NewGuid().ToString(), "Instance 1", LLMProviders.MISTRAL, new("test-model", null));
    }

    [TearDown]
    public void RestoreApplicationState()
    {
        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        this.serviceProvider.Dispose();
        if (Directory.Exists(this.dataDirectory))
            Directory.Delete(this.dataDirectory, true);
    }

    [Test]
    public async Task AStepAsksWithThePromptOfTheLabAndReadsOnlyWebPagesGivenToIt()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var location = new BflStepThreadLocation(created.ProjectId, BflStep.FRAMEWORK);
        ChatThread? sent = null;
        var service = this.CreateService(thread => sent = thread);

        await service.GetAsync(new BflChatStore(store, location), LAB, location.ChatId, () => BflThreadFactory.Create(location));
        var isSent = await service.SendAsync(location.ChatId, this.Request(location, "Ich spreche auf einem Podium.", new(SYSTEM_PROMPT, [])));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isSent, Is.True);
            Assert.That(sent!.SystemPrompt, Is.EqualTo(SYSTEM_PROMPT));
            Assert.That(sent.RuntimeSelectedToolIds, Is.EquivalentTo(new[] { ToolSelectionRules.READ_WEB_PAGE_TOOL_ID }));
            Assert.That(sent.RuntimeToolsAreAssistantManaged, Is.True);
            Assert.That(sent.RuntimeComponent, Is.EqualTo(LAB));
            Assert.That(sent.DataSourceOptions.DisableDataSources, Is.True);
            Assert.That(sent.RequiredOutboundDataRestriction, Is.EqualTo(BflChatTools.STEP_REQUIREMENT));
            Assert.That((await store.LoadThreadAsync(location))!.Blocks.Select(block => block.Role), Is.EqualTo(new[] { ChatRole.USER, ChatRole.AI }));
        }
    }

    [Test]
    public async Task TheOnboardingAsksWithoutTools()
    {
        var store = this.CreateStore();
        var location = BflOnboardingThreadLocation.INSTANCE;
        ChatThread? sent = null;
        var service = this.CreateService(thread => sent = thread);

        await service.GetAsync(new BflChatStore(store, location), LAB, location.ChatId, () => BflThreadFactory.Create(location));
        await service.SendAsync(location.ChatId, this.Request(location, "Passt das Lab zu meinem Interview?", new(SYSTEM_PROMPT, [])));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent!.RuntimeSelectedToolIds, Is.Empty);
            Assert.That(sent.RequiredOutboundDataRestriction, Is.EqualTo(OutboundDataRequirement.NONE));
            Assert.That(File.Exists(Path.Combine(this.dataDirectory, BeyondFactsLabStore.ROOT_DIRECTORY_NAME, "onboarding", "thread.json")), Is.True);
        }
    }

    [Test]
    public async Task TheImagesOfTheMaterialGoAlongWithTheRequestOnly()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var location = new BflStepThreadLocation(created.ProjectId, BflStep.KNOWLEDGE);
        var image = new FileAttachmentImage("Poster.png", Path.Combine(this.dataDirectory, "poster-copy.png"), 1_024);
        ChatThread? sent = null;
        var service = this.CreateService(thread => sent = thread);

        await service.GetAsync(new BflChatStore(store, location), LAB, location.ChatId, () => BflThreadFactory.Create(location));
        await service.SendAsync(location.ChatId, this.Request(location, BflConversationService.OPENING_REQUEST, new(SYSTEM_PROMPT, [image]), isHidden: true));

        var stored = await store.LoadThreadAsync(location);
        var storedFile = await File.ReadAllTextAsync(Path.Combine(this.dataDirectory, BeyondFactsLabStore.ROOT_DIRECTORY_NAME, created.ProjectId.ToString("D"), "steps", BflStep.KNOWLEDGE.FolderName, "thread.json"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent!.Blocks[0].Content!.FileAttachments, Is.EqualTo(new FileAttachment[] { image }), "The image goes along with the first message.");
            Assert.That(stored!.Blocks[0].Content!.FileAttachments, Is.Empty, "The stored chat never holds an image.");
            Assert.That(stored.Blocks[0].HideFromUser, Is.True);
            Assert.That(storedFile, Does.Not.Contain("poster-copy.png"));
        }
    }

    [Test]
    public async Task AChatStoredBeforeTheLabOfferedToolsIsRestrictedBeforeItsRequest()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var location = new BflStepThreadLocation(created.ProjectId, BflStep.STRATEGY);
        await store.SaveThreadAsync(location, new ChatThread { ChatId = location.ChatId, Name = location.ChatName, Blocks = [] });
        ChatThread? sent = null;
        var service = this.CreateService(thread => sent = thread);

        var conversation = await service.GetAsync(new BflChatStore(store, location), LAB, location.ChatId, () => BflThreadFactory.Create(location));
        var restrictionBefore = conversation.Thread!.RequiredOutboundDataRestriction;
        await service.SendAsync(location.ChatId, this.Request(location, "Wer sitzt mit auf dem Podium?", new(SYSTEM_PROMPT, [])));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(restrictionBefore, Is.EqualTo(OutboundDataRequirement.NONE));
            Assert.That(sent!.RequiredOutboundDataRestriction, Is.EqualTo(BflChatTools.STEP_REQUIREMENT));
            Assert.That((await store.LoadThreadAsync(location))!.RequiredOutboundDataRestriction, Is.EqualTo(BflChatTools.STEP_REQUIREMENT), "The chat stores the restriction.");
        }
    }

    private EmbeddedChatRequest Request(BflThreadLocation location, string text, BflRequestMaterial requestMaterial, bool isHidden = false) => new()
    {
        Provider = this.provider,
        Message = new ContentText { Text = text },
        IsHidden = isHidden,
        PrepareAsync = thread => Task.FromResult(BflConversationService.Prepare(location, this.provider, thread, requestMaterial))
    };

    private EmbeddedChatService CreateService(Action<ChatThread> sent) => new(this.settingsManager, new AssistantSessionService(MessageBus.INSTANCE), MessageBus.INSTANCE, NullLogger<EmbeddedChatService>.Instance, (_, answer, thread, _) =>
    {
        sent(thread);
        answer.Text = "Worum geht es Ihnen?";
        return Task.CompletedTask;
    });

    private BeyondFactsLabStore CreateStore() => new(NullLogger<BeyondFactsLabStore>.Instance, new BflStorageOptions { DataDirectory = this.dataDirectory });
}