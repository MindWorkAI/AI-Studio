using System.Reflection;
using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tests.Tools.Databases;
using AIStudio.Tools;
using AIStudio.Tools.DataBackup;
using AIStudio.Tools.Databases.ProvenanceStore;
using AIStudio.Tools.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks when the cleanup of the provenance store at the start reads which chats, see ProvenanceCleanup.
/// </summary>
/// <remarks>
/// The cleanup reads only the chats which changed since its last run. A restore breaks that: it may
/// bring back the chats of one moment and keep the store of another, so the cleanup reads every chat
/// after it, and touches nothing while the runtime has not finished it.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ProvenanceCleanupServiceTests
{
    private static readonly CancellationToken TOKEN = CancellationToken.None;

    private string? previousDataDirectory;
    private TemporaryProvenanceStore store = null!;
    private string testDirectory = string.Empty;

    [SetUp]
    public async Task PrepareDataDirectoryAndStore()
    {
        // The data directory is static state of the whole application, which is why this fixture does not run alongside others:
        this.previousDataDirectory = SettingsManager.DataDirectory;
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-provenance-cleanup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testDirectory);
        SettingsManager.DataDirectory = this.testDirectory;
        WorkspaceBehaviour.TestDataDirectory = this.testDirectory;
        await WorkspaceBehaviour.ForceReloadWorkspaceTreeAsync();

        this.store = await TemporaryProvenanceStore.CreateAsync();
    }

    [TearDown]
    public async Task RestoreApplicationState()
    {
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
    public async Task AChatWhichDidNotChangeIsSkippedAndTheCleanupIsNoted()
    {
        var unchangedChat = await this.StoreUnchangedChatAsync();
        var unreferenced = await this.store.Client.StoreAsync(unchangedChat.ChatId, ProvenanceKind.ATTACHMENT, "of a message removed long ago", null, TOKEN);

        await RunAsync(this.store, RestoreStateWith(report: null));

        var entries = await this.store.Client.GetEntriesAsync([unreferenced!.Value], TOKEN);
        var lastCleanup = await this.store.Client.GetLastCleanupAsync(TOKEN);
        Assert.Multiple(() =>
        {
            Assert.That(entries.Keys, Is.EquivalentTo(new[] { unreferenced.Value }), "Nothing changed in the chat since the last cleanup, so it was not read.");
            Assert.That(lastCleanup, Is.GreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1)), "The next start reads only what changes from now on.");
        });
    }

    [Test]
    public async Task ARestoreAtThisStartHasEveryChatRead()
    {
        var unchangedChat = await this.StoreUnchangedChatAsync();
        var unreferenced = await this.store.Client.StoreAsync(unchangedChat.ChatId, ProvenanceKind.ATTACHMENT, "of a message the restored chat does not have", null, TOKEN);

        await RunAsync(this.store, RestoreStateWith(new(DataRestoreOutcome.APPLIED, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty)));

        var entries = await this.store.Client.GetEntriesAsync([unreferenced!.Value], TOKEN);
        Assert.That(entries, Is.Empty, "The restored chat came from another moment than the store, so the time of the last cleanup tells nothing about it.");
    }

    [Test]
    public async Task AnUnfinishedRestoreKeepsEverything()
    {
        var entry = await this.store.Client.StoreAsync(Guid.NewGuid(), ProvenanceKind.WEB_PAGE, "of a chat the unfinished restore moved aside", null, TOKEN);
        var pendingDirectory = DataRestoreFormat.DirectoryIn(this.testDirectory, DataRestoreFormat.PENDING_DIRECTORY);
        Directory.CreateDirectory(pendingDirectory);
        await File.WriteAllTextAsync(Path.Join(pendingDirectory, DataRestoreFormat.READY_FILE_NAME), string.Empty, TOKEN);
        await File.WriteAllTextAsync(Path.Join(pendingDirectory, DataRestoreFormat.JOURNAL_FILE_NAME), "{}", TOKEN);

        await RunAsync(this.store, RestoreStateWith(report: null));

        var entries = await this.store.Client.GetEntriesAsync([entry!.Value], TOKEN);
        var lastCleanup = await this.store.Client.GetLastCleanupAsync(TOKEN);
        Assert.Multiple(() =>
        {
            Assert.That(entries.Keys, Is.EquivalentTo(new[] { entry.Value }), "The chat may wait elsewhere to be moved back.");
            Assert.That(lastCleanup, Is.Null);
        });
    }

    /// <summary>
    /// Stores a chat whose file was written long before the last cleanup, which is noted as if it ran a minute ago.
    /// </summary>
    private async Task<ChatThread> StoreUnchangedChatAsync()
    {
        var chat = new ChatThread { WorkspaceId = Guid.Empty, ChatId = Guid.NewGuid(), Name = "Unchanged" };
        Assert.That(await WorkspaceBehaviour.StoreChatAsync(chat), Is.True, "The chat could not be stored.");

        File.SetLastWriteTimeUtc(Path.Join(WorkspaceBehaviour.GetChatDirectory(Guid.Empty, chat.ChatId), "thread.json"), DateTime.UtcNow.AddDays(-1));

        // Entries stored afterward count as older than the last cleanup, as if it had kept them:
        await this.store.Client.SetLastCleanupAsync(DateTimeOffset.UtcNow.AddMinutes(1), TOKEN);
        return chat;
    }

    private static async Task RunAsync(TemporaryProvenanceStore store, DataRestoreService dataRestoreService)
    {
        // Only entries stored before the start of the service may be removed, and the test stored them just now:
        await Task.Delay(TimeSpan.FromMilliseconds(10), TOKEN);

        using var service = new ProvenanceCleanupService(store.CreateClientProvider(), dataRestoreService, NullLogger<ProvenanceCleanupService>.Instance);
        await service.StartAsync(TOKEN);
        await service.ExecuteTask!;
    }

    /// <summary>
    /// Makes the restore service as it is once it learned what this start found of a restore.
    /// </summary>
    /// <remarks>
    /// Without its constructor, which needs most services of the app: the cleanup reads only these two members.
    /// </remarks>
    private static DataRestoreService RestoreStateWith(DataRestoreReport? report)
    {
        var dataRestoreService = (DataRestoreService)RuntimeHelpers.GetUninitializedObject(typeof(DataRestoreService));
        var startupStateLoaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        startupStateLoaded.SetResult();
        typeof(DataRestoreService).GetField("startupStateLoaded", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(dataRestoreService, startupStateLoaded);
        typeof(DataRestoreService).GetProperty(nameof(DataRestoreService.Report))!.SetValue(dataRestoreService, report);
        return dataRestoreService;
    }
}