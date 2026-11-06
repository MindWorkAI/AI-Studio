using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tests.Tools.Databases;
using AIStudio.Tools;
using AIStudio.Tools.Databases.ProvenanceStore;
using AIStudio.Tools.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that the maintenance of the temporary chats takes what a chat received along with the chat.
/// </summary>
/// <remarks>
/// What a chat received, e.g., an attached document or a web page, lies in the provenance store. The
/// user chose how long temporary chats stay, and that holds for their contents as well: they must
/// not stay until the next start, which may be weeks away.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class TemporaryChatMaintenanceTests
{
    private static readonly CancellationToken TOKEN = CancellationToken.None;

    private string? previousDataDirectory;
    private IServiceProvider previousServiceProvider = null!;
    private ServiceProvider serviceProvider = null!;
    private TemporaryProvenanceStore store = null!;
    private string testDirectory = string.Empty;

    [SetUp]
    public async Task PrepareDataDirectoryAndStore()
    {
        // The data directory and the services are static state of the whole application, which is why this fixture does not run alongside others:
        this.previousDataDirectory = SettingsManager.DataDirectory;
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-temporary-chat-maintenance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testDirectory);
        SettingsManager.DataDirectory = this.testDirectory;
        WorkspaceBehaviour.TestDataDirectory = this.testDirectory;
        await WorkspaceBehaviour.ForceReloadWorkspaceTreeAsync();

        this.store = await TemporaryProvenanceStore.CreateAsync();
        this.previousServiceProvider = Program.SERVICE_PROVIDER;
        this.serviceProvider = new ServiceCollection().AddSingleton(this.store.CreateClientProvider()).BuildServiceProvider();
        Program.SERVICE_PROVIDER = this.serviceProvider;
    }

    [TearDown]
    public async Task RestoreApplicationState()
    {
        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        await this.serviceProvider.DisposeAsync();
        await this.store.DisposeAsync();

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

    [Test]
    public async Task AnExpiredTemporaryChatTakesWhatItReceivedAlong()
    {
        var expiredChat = await StoreTemporaryChatAsync("Expired", TimeSpan.FromDays(10));
        var recentChat = await StoreTemporaryChatAsync("Recent", TimeSpan.FromDays(1));
        var expiredEntry = await this.store.Client.StoreAsync(expiredChat.ChatId, ProvenanceKind.WEB_PAGE, "a page the expired chat read", null, TOKEN);
        var recentEntry = await this.store.Client.StoreAsync(recentChat.ChatId, ProvenanceKind.ATTACHMENT, "a document the recent chat read", null, TOKEN);

        var settings = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        settings.ConfigurationData.Workspace.StorageTemporaryMaintenancePolicy = WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_7_DAYS;
        await new TemporaryChatService(NullLogger<TemporaryChatService>.Instance, settings).StartMaintenanceAsync();

        var entries = await this.store.Client.GetEntriesAsync([expiredEntry!.Value, recentEntry!.Value], TOKEN);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Directory.Exists(WorkspaceBehaviour.GetChatDirectory(Guid.Empty, expiredChat.ChatId)), Is.False, "The expired chat should be gone.");
            Assert.That(Directory.Exists(WorkspaceBehaviour.GetChatDirectory(Guid.Empty, recentChat.ChatId)), Is.True, "The recent chat should stay.");
            Assert.That(entries.Keys, Is.EquivalentTo(new[] { recentEntry.Value }), "What the expired chat received should go with it, right away.");
        }
    }

    [Test]
    public async Task ChatsInWorkspacesStayWhileTheyHaveNoChoiceOfTheirOwn()
    {
        var workspaceId = await WorkspaceBehaviour.ResolveOrCreateWorkspaceIdByNameAsync("Projects");
        var oldChat = await StoreTemporaryChatAsync("Old", TimeSpan.FromDays(400), workspaceId);

        var settings = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        settings.ConfigurationData.Workspace.StorageTemporaryMaintenancePolicy = WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_7_DAYS;
        await new TemporaryChatService(NullLogger<TemporaryChatService>.Instance, settings).StartMaintenanceAsync();

        Assert.That(Directory.Exists(WorkspaceBehaviour.GetChatDirectory(workspaceId, oldChat.ChatId)), Is.True, "Settings written before chats in workspaces had a choice of their own keep them, as before.");
    }

    [Test]
    public async Task ChatsInWorkspacesGoByTheirOwnDuration()
    {
        var workspaceId = await WorkspaceBehaviour.ResolveOrCreateWorkspaceIdByNameAsync("Projects");
        var expiredWorkspaceChat = await StoreTemporaryChatAsync("Expired", TimeSpan.FromDays(10), workspaceId);
        var recentWorkspaceChat = await StoreTemporaryChatAsync("Recent", TimeSpan.FromDays(1), workspaceId);
        var oldDisappearingChat = await StoreTemporaryChatAsync("Old", TimeSpan.FromDays(400));

        var settings = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        settings.ConfigurationData.Workspace.SetChoice(isInWorkspace: false, ChatStorageChoice.STORE_INDEFINITELY);
        settings.ConfigurationData.Workspace.SetChoice(isInWorkspace: true, ChatStorageChoice.STORE_TEMPORARILY);
        settings.ConfigurationData.Workspace.SetDuration(isInWorkspace: true, WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_7_DAYS);
        await new TemporaryChatService(NullLogger<TemporaryChatService>.Instance, settings).StartMaintenanceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Directory.Exists(WorkspaceBehaviour.GetChatDirectory(workspaceId, expiredWorkspaceChat.ChatId)), Is.False, "The chat in the workspace stayed unchanged longer than the workspaces keep chats.");
            Assert.That(Directory.Exists(WorkspaceBehaviour.GetChatDirectory(workspaceId, recentWorkspaceChat.ChatId)), Is.True, "The recent chat in the workspace should stay.");
            Assert.That(Directory.Exists(WorkspaceBehaviour.GetChatDirectory(Guid.Empty, oldDisappearingChat.ChatId)), Is.True, "Chats outside of workspaces follow their own choice, which keeps them.");
        }
    }

    [Test]
    public async Task AChatSavedByHandStays()
    {
        var oldChat = await StoreTemporaryChatAsync("Saved", TimeSpan.FromDays(400));

        var settings = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        settings.ConfigurationData.Workspace.StorageTemporaryMaintenancePolicy = WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_7_DAYS;
        settings.ConfigurationData.Workspace.StorageBehavior = WorkspaceStorageBehavior.STORE_CHATS_MANUALLY;
        await new TemporaryChatService(NullLogger<TemporaryChatService>.Instance, settings).StartMaintenanceAsync();

        Assert.That(Directory.Exists(WorkspaceBehaviour.GetChatDirectory(Guid.Empty, oldChat.ChatId)), Is.True, "Whoever stores chats by clicking save keeps them until they delete them.");
    }

    /// <summary>
    /// Stores a chat whose file was written the given time ago.
    /// </summary>
    /// <param name="name">The name of the chat.</param>
    /// <param name="age">How long ago the chat changed last.</param>
    /// <param name="workspaceId">The workspace of the chat, or none for a disappearing chat.</param>
    private static async Task<ChatThread> StoreTemporaryChatAsync(string name, TimeSpan age, Guid workspaceId = default)
    {
        var chat = new ChatThread { WorkspaceId = workspaceId, ChatId = Guid.NewGuid(), Name = name };
        Assert.That(await WorkspaceBehaviour.StoreChatAsync(chat), Is.True, $"The chat '{name}' could not be stored.");

        var chatFile = Path.Join(WorkspaceBehaviour.GetChatDirectory(workspaceId, chat.ChatId), "thread.json");
        File.SetLastWriteTimeUtc(chatFile, DateTime.UtcNow - age);
        return chat;
    }
}