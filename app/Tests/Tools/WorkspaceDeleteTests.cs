using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks when a workspace may be deleted together with its chats.
/// </summary>
/// <remarks>
/// The work on a busy chat, an answer or a transcript, stores the chat once more when it is done. Had
/// its workspace been deleted meanwhile, that brings the workspace back as an unnamed one. A busy chat
/// therefore keeps the whole workspace, while the chats of other workspaces have no say in it.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class WorkspaceDeleteTests
{
    private string? previousDataDirectory;
    private string testDirectory = string.Empty;

    [SetUp]
    public async Task PrepareDataDirectory()
    {
        //
        // The data directory is static state of the whole application, which is why this fixture
        // does not run alongside others. The workspace behaviour keeps the roots of the workspaces
        // and of the temporary chats fixed, as the app never changes its data directory, so it gets
        // told about the one of this test separately. The cache is static as well, so it starts
        // over from what the new data directory holds: nothing.
        //
        this.previousDataDirectory = SettingsManager.DataDirectory;
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-workspace-delete-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testDirectory);
        SettingsManager.DataDirectory = this.testDirectory;
        WorkspaceBehaviour.TestDataDirectory = this.testDirectory;

        await WorkspaceBehaviour.ForceReloadWorkspaceTreeAsync();
    }

    [TearDown]
    public void RemoveDataDirectory()
    {
        SettingsManager.DataDirectory = this.previousDataDirectory;
        WorkspaceBehaviour.TestDataDirectory = null;

        // Whoever reads the cache next must not get the workspaces of this test:
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
    public async Task ABusyChatKeepsTheWholeWorkspace()
    {
        var workspaceId = await CreateWorkspaceAsync("Projects");
        var busyChat = await StoreChatAsync(workspaceId, "Meeting notes");
        var idleChat = await StoreChatAsync(workspaceId, "Old notes");

        var deleted = await WorkspaceBehaviour.DeleteWorkspaceAsync(workspaceId, chatId => chatId == busyChat.ChatId);

        var snapshot = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(deleted, Is.False, "The caller has to learn that the workspace is still there.");
            Assert.That(WorkspaceBehaviour.IsChatExisting(new LoadChat(workspaceId, busyChat.ChatId)), Is.True, "The answer or the transcript still needs the chat it is made for.");
            Assert.That(WorkspaceBehaviour.IsChatExisting(new LoadChat(workspaceId, idleChat.ChatId)), Is.True, "The user asked for the whole workspace to go, not for some of its chats.");
            Assert.That(snapshot.Workspaces.Select(workspace => workspace.WorkspaceId), Does.Contain(workspaceId), "The workspace list keeps showing the workspace, which still exists.");
        }
    }

    [Test]
    public async Task AWorkspaceWithoutBusyChatsIsGone()
    {
        var workspaceId = await CreateWorkspaceAsync("Projects");
        var chat = await StoreChatAsync(workspaceId, "Meeting notes");

        var deleted = await WorkspaceBehaviour.DeleteWorkspaceAsync(workspaceId, _ => false);

        var snapshot = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(deleted, Is.True);
            Assert.That(WorkspaceBehaviour.IsWorkspaceExisting(workspaceId), Is.False, "The workspace has to be gone from the disk, its chats included.");
            Assert.That(WorkspaceBehaviour.IsChatExisting(new LoadChat(workspaceId, chat.ChatId)), Is.False);
            Assert.That(snapshot.Workspaces.Select(workspace => workspace.WorkspaceId), Does.Not.Contain(workspaceId), "The workspace list must not show a workspace which is gone.");
        }
    }

    [Test]
    public async Task ABusyChatInAnotherWorkspaceDoesNotKeepTheWorkspace()
    {
        var workspaceId = await CreateWorkspaceAsync("Projects");
        await StoreChatAsync(workspaceId, "Meeting notes");
        var otherId = await CreateWorkspaceAsync("Archive");
        var busyChat = await StoreChatAsync(otherId, "Old notes");

        var deleted = await WorkspaceBehaviour.DeleteWorkspaceAsync(workspaceId, chatId => chatId == busyChat.ChatId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deleted, Is.True, "Only the chats of the workspace being deleted count.");
            Assert.That(WorkspaceBehaviour.IsWorkspaceExisting(workspaceId), Is.False);
            Assert.That(WorkspaceBehaviour.IsChatExisting(new LoadChat(otherId, busyChat.ChatId)), Is.True, "The other workspace has nothing to do with it and stays as it is.");
        }
    }

    [Test]
    public async Task AWorkspaceWhichIsNotThereCountsAsDeleted()
    {
        var deleted = await WorkspaceBehaviour.DeleteWorkspaceAsync(Guid.NewGuid(), _ => false);

        Assert.That(deleted, Is.True, "There is nothing left to delete, so the caller may go on.");
    }

    [Test]
    public void TheDisappearingChatsAlwaysExist()
    {
        // The temporary chats belong to no workspace, so a new chat among them asks with the empty ID, and it has to be stored:
        Assert.That(WorkspaceBehaviour.IsWorkspaceExisting(Guid.Empty), Is.True);
    }

    private static async Task<Guid> CreateWorkspaceAsync(string name)
    {
        var result = await WorkspaceBehaviour.TryCreateWorkspaceAsync(name);
        Assert.That(result.Success, Is.True, $"The workspace '{name}' could not be created.");
        return result.Workspace.WorkspaceId;
    }

    private static async Task<ChatThread> StoreChatAsync(Guid workspaceId, string name)
    {
        var chat = new ChatThread { WorkspaceId = workspaceId, ChatId = Guid.NewGuid(), Name = name };
        Assert.That(await WorkspaceBehaviour.StoreChatAsync(chat), Is.True, $"The chat '{name}' could not be stored.");
        return chat;
    }
}