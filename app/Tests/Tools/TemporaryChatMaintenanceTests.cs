using System.Reflection;

using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tests.Tools.Databases;
using AIStudio.Tools;
using AIStudio.Tools.Databases;
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
        var databases = new DatabaseClientProvider(null!, NullLoggerFactory.Instance);
        var clients = (Dictionary<DatabaseRole, DatabaseClient>)typeof(DatabaseClientProvider).GetField("clients", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(databases)!;
        clients.Add(DatabaseRole.PROVENANCE_STORE, this.store.Client);

        this.previousServiceProvider = Program.SERVICE_PROVIDER;
        this.serviceProvider = new ServiceCollection().AddSingleton(databases).BuildServiceProvider();
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
        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(WorkspaceBehaviour.GetChatDirectory(Guid.Empty, expiredChat.ChatId)), Is.False, "The expired chat should be gone.");
            Assert.That(Directory.Exists(WorkspaceBehaviour.GetChatDirectory(Guid.Empty, recentChat.ChatId)), Is.True, "The recent chat should stay.");
            Assert.That(entries.Keys, Is.EquivalentTo(new[] { recentEntry.Value }), "What the expired chat received should go with it, right away.");
        });
    }

    /// <summary>
    /// Stores a temporary chat whose file was written the given time ago.
    /// </summary>
    private static async Task<ChatThread> StoreTemporaryChatAsync(string name, TimeSpan age)
    {
        var chat = new ChatThread { WorkspaceId = Guid.Empty, ChatId = Guid.NewGuid(), Name = name };
        Assert.That(await WorkspaceBehaviour.StoreChatAsync(chat), Is.True, $"The chat '{name}' could not be stored.");

        var chatFile = Path.Join(WorkspaceBehaviour.GetChatDirectory(Guid.Empty, chat.ChatId), "thread.json");
        File.SetLastWriteTimeUtc(chatFile, DateTime.UtcNow - age);
        return chat;
    }
}