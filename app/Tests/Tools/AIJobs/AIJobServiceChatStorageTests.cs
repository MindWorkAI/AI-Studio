using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.AIJobs;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.AIJobs;

/// <summary>
/// Checks when the AI job of a chat stores that chat.
/// </summary>
/// <remarks>
/// The job stores the chat while the answer is written, so that a chat being answered survives when the
/// user goes elsewhere. That is what storing chats automatically means. Whoever stores chats manually
/// decides with the save button when a chat reaches the disk, and whoever disabled the workspaces wants
/// no chat on the disk at all. The job of such a chat must leave the disk alone: neither a new chat may
/// appear there, nor may a saved one take on what the user did not save.<br/><br/>
///
/// The jobs here run without a provider, so they end right away, the way a failed request ends. That
/// is enough: every job ends through the same last checkpoint, and the first one is taken before the
/// provider is asked anything.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class AIJobServiceChatStorageTests
{
    private static readonly TimeSpan WAIT_LIMIT = TimeSpan.FromSeconds(10);

    private string? previousDataDirectory;
    private string testDirectory = string.Empty;
    private readonly List<IMessageBusReceiver> registeredReceivers = [];

    [SetUp]
    public async Task PrepareDataDirectory()
    {
        //
        // The data directory, the workspace cache, and the message bus are static state of the whole
        // application, which is why this fixture does not run alongside others. See WorkspaceChatStoreTests
        // for why the workspace behaviour gets told about the data directory separately.
        //
        this.previousDataDirectory = SettingsManager.DataDirectory;
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-ai-job-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testDirectory);
        SettingsManager.DataDirectory = this.testDirectory;
        WorkspaceBehaviour.TestDataDirectory = this.testDirectory;

        await WorkspaceBehaviour.ForceReloadWorkspaceTreeAsync();
    }

    [TearDown]
    public void RemoveDataDirectory()
    {
        foreach (var receiver in this.registeredReceivers)
            MessageBus.INSTANCE.Unregister(receiver);

        this.registeredReceivers.Clear();
        SettingsManager.DataDirectory = this.previousDataDirectory;
        WorkspaceBehaviour.TestDataDirectory = null;

        // Whoever reads the cache next must not get the chats of this test:
        WorkspaceBehaviour.InvalidateWorkspaceTreeCache();

        try
        {
            Directory.Delete(this.testDirectory, true);
        }
        catch (IOException)
        {
            // A temporary directory we could not remove says nothing about the code under test.
        }
    }

    [TestCase(WorkspaceStorageBehavior.DISABLE_WORKSPACES)]
    [TestCase(WorkspaceStorageBehavior.STORE_CHATS_MANUALLY)]
    public async Task ANewChatStaysOffTheDiskWhileChatsAreNotStoredAutomatically(WorkspaceStorageBehavior storageBehavior)
    {
        var chat = NewChat("What is the capital of France?");

        await this.AnswerAsync(chat, storageBehavior);

        // Reading the workspace tree creates the directory of the temporary chats, so only what it holds tells:
        var temporaryChats = Path.Combine(this.testDirectory, "tempChats");
        var temporaryChatEntries = Directory.Exists(temporaryChats) ? Directory.EnumerateFileSystemEntries(temporaryChats).ToList() : [];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(WorkspaceBehaviour.IsChatExisting(new LoadChat(chat.WorkspaceId, chat.ChatId)), Is.False, "Sending a message is no reason to store a chat the user did not save, or which they want kept nowhere.");
            Assert.That(temporaryChatEntries, Is.Empty, "A chat outside the workspaces would end up among the temporary chats, where nobody sees it while the workspaces are disabled.");
        }
    }

    [Test]
    public async Task AChatSavedByHandKeepsItsSavedStateUntilTheNextSave()
    {
        var chat = NewChat("What is the capital of France?");
        Assert.That(await WorkspaceBehaviour.StoreChatAsync(chat), Is.True, "The chat could not be saved.");

        // A message sent after saving, which the user did not save yet:
        AddUserMessage(chat, "And the one of Italy?");
        await this.AnswerAsync(chat, WorkspaceStorageBehavior.STORE_CHATS_MANUALLY);

        var stored = await ReadStoredChatAsync(chat);
        Assert.That(stored.Blocks, Has.Count.EqualTo(1), "Only saving puts the new message on the disk; until then, the chat on the disk is the one the user saved.");
    }

    [Test]
    public async Task AnAnsweredChatIsStoredWhileChatsAreStoredAutomatically()
    {
        var chat = NewChat("What is the capital of France?");

        await this.AnswerAsync(chat, WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY);

        Assert.That(WorkspaceBehaviour.IsChatExisting(new LoadChat(chat.WorkspaceId, chat.ChatId)), Is.True, "Storing chats automatically means the chat reaches the disk with its first message.");
        var stored = await ReadStoredChatAsync(chat);
        Assert.That(stored.Blocks.Select(block => block.Content).OfType<ContentText>().Select(text => text.Text), Does.Contain("What is the capital of France?"), "The stored chat has to hold the message which was sent.");
    }

    /// <summary>
    /// Lets the AI job answer the given chat, like sending a message does, and waits until the job is done.
    /// </summary>
    /// <param name="chat">The chat, with the message to answer as its last block.</param>
    /// <param name="storageBehavior">How the user wants their chats stored.</param>
    private async Task AnswerAsync(ChatThread chat, WorkspaceStorageBehavior storageBehavior)
    {
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        settingsManager.ConfigurationData.Workspace.StorageBehavior = storageBehavior;

        var aiText = new ContentText { InitialRemoteWait = true };
        chat.Blocks.Add(new ContentBlock
        {
            Time = DateTimeOffset.Now,
            ContentType = ContentType.TEXT,
            Role = ChatRole.AI,
            Content = aiText,
        });

        // The bus announces the end of a job only after its last checkpoint, so nothing is stored afterward:
        var finishedJob = new FinishedJobReceiver(chat.ChatId);
        MessageBus.INSTANCE.RegisterComponent(finishedJob);
        MessageBus.INSTANCE.ApplyFilters(finishedJob, [], [Event.AI_JOB_FINISHED]);
        this.registeredReceivers.Add(finishedJob);

        var aiJobService = new AIJobService(settingsManager, MessageBus.INSTANCE, NullLogger<AIJobService>.Instance);
        var snapshot = await aiJobService.TryStartChatGenerationAsync(new ChatGenerationRequest
        {
            ChatThread = chat,
            AIText = aiText,
            LastUserPrompt = chat.Blocks.Last(block => block.Role is ChatRole.USER).Content,
            ProviderSettings = AIStudio.Settings.Provider.NONE,
        });

        Assert.That(snapshot, Is.Not.Null, "The AI job did not start.");
        await finishedJob.Finished.WaitAsync(WAIT_LIMIT);
    }

    private static ChatThread NewChat(string message)
    {
        var chat = new ChatThread { WorkspaceId = Guid.Empty, ChatId = Guid.NewGuid(), Name = "Capitals" };
        AddUserMessage(chat, message);
        return chat;
    }

    private static void AddUserMessage(ChatThread chat, string message) => chat.Blocks.Add(new ContentBlock
    {
        Time = DateTimeOffset.Now,
        ContentType = ContentType.TEXT,
        Role = ChatRole.USER,
        Content = new ContentText { Text = message },
    });

    private static async Task<ChatThread> ReadStoredChatAsync(ChatThread chat)
    {
        var chatPath = Path.Combine(WorkspaceBehaviour.GetChatDirectory(chat.WorkspaceId, chat.ChatId), "thread.json");
        return JsonSerializer.Deserialize<ChatThread>(await File.ReadAllTextAsync(chatPath), WorkspaceBehaviour.JSON_OPTIONS)!;
    }

    /// <summary>
    /// Learns from the message bus when the job of one chat is done.
    /// </summary>
    /// <param name="chatId">The chat whose job to wait for.</param>
    private sealed class FinishedJobReceiver(Guid chatId) : IMessageBusReceiver
    {
        private readonly TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Finished => this.finished.Task;

        public Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
        {
            if (triggeredEvent is Event.AI_JOB_FINISHED && data is AIJobSnapshot snapshot && snapshot.SubjectId == chatId)
                this.finished.TrySetResult();

            return Task.CompletedTask;
        }

        public Task<MessageBusAnswer<TResult>> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data) => Task.FromResult(MessageBusAnswer<TResult>.NONE);
    }
}