using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks what the workspace list gets to see after a chat moved into another workspace.
/// </summary>
/// <remarks>
/// The workspace list draws from a cache, which holds the chats of every workspace loaded so far.
/// A move has to take the chat out of the place it came from and put it where it lives now, while
/// everything else in the cache stays as it was. Otherwise, the list shows the chat in its old place,
/// or every workspace empty until the user clicks it.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class WorkspaceTreeCacheTests
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
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-workspace-tree-{Guid.NewGuid():N}");
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
    public async Task AMovedDisappearingChatLeavesTheDisappearingChats()
    {
        var targetId = await CreateLoadedWorkspaceAsync("Projects");
        var chat = await StoreChatAsync(Guid.Empty, "Meeting notes");

        await WorkspaceBehaviour.MoveChatAsync(chat, targetId);

        var snapshot = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
        var target = snapshot.Workspaces.Single(workspace => workspace.WorkspaceId == targetId);
        var expectedPath = Path.Combine(this.testDirectory, "workspaces", targetId.ToString(), chat.ChatId.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.TemporaryChats.Select(entry => entry.ChatId), Does.Not.Contain(chat.ChatId), "The chat lives in a workspace now, so the disappearing chats must not list it any longer.");
            Assert.That(target.ChatsLoaded, Is.True, "The chats of the target workspace were loaded before the move, and they still are.");
            Assert.That(target.Chats.Select(entry => entry.ChatPath), Does.Contain(expectedPath), "The target workspace lists the chat with the directory it lives in now.");
        });
    }

    [Test]
    public async Task MovingAChatKeepsTheChatsOfOtherWorkspacesLoaded()
    {
        var otherId = await CreateLoadedWorkspaceAsync("Archive");
        var otherChat = await StoreChatAsync(otherId, "Old notes");
        var targetId = await CreateLoadedWorkspaceAsync("Projects");
        var chat = await StoreChatAsync(Guid.Empty, "Meeting notes");

        await WorkspaceBehaviour.MoveChatAsync(chat, targetId);

        var other = await GetWorkspaceAsync(otherId);
        Assert.Multiple(() =>
        {
            Assert.That(other.ChatsLoaded, Is.True, "A move must not make the cache forget the chats of a workspace which has nothing to do with it. The list would show that workspace empty.");
            Assert.That(other.Chats.Select(entry => entry.ChatId), Does.Contain(otherChat.ChatId));
        });
    }

    [Test]
    public async Task AChatMovedBetweenWorkspacesLeavesItsFormerWorkspace()
    {
        var sourceId = await CreateLoadedWorkspaceAsync("Archive");
        var targetId = await CreateLoadedWorkspaceAsync("Projects");
        var chat = await StoreChatAsync(sourceId, "Meeting notes");

        await WorkspaceBehaviour.MoveChatAsync(chat, targetId);

        var source = await GetWorkspaceAsync(sourceId);
        var target = await GetWorkspaceAsync(targetId);
        Assert.Multiple(() =>
        {
            Assert.That(source.Chats.Select(entry => entry.ChatId), Does.Not.Contain(chat.ChatId), "The chat left this workspace.");
            Assert.That(target.Chats.Select(entry => entry.ChatId), Does.Contain(chat.ChatId), "The chat arrived in this workspace.");
        });
    }

    [Test]
    public async Task AWorkspaceWithoutLoadedChatsFindsTheMovedChatOnTheDisk()
    {
        var targetResult = await WorkspaceBehaviour.TryCreateWorkspaceAsync("Projects");
        var targetId = targetResult.Workspace.WorkspaceId;

        // As after a start of the app: the workspaces are known, but none of their chats were read yet.
        await WorkspaceBehaviour.ForceReloadWorkspaceTreeAsync();
        var chat = await StoreChatAsync(Guid.Empty, "Meeting notes");

        await WorkspaceBehaviour.MoveChatAsync(chat, targetId);

        var chats = await WorkspaceBehaviour.GetWorkspaceChatsAsync(targetId);
        Assert.That(chats.Select(entry => entry.ChatId), Does.Contain(chat.ChatId), "Loading the chats of the workspace later reads them from the disk, the moved one included.");
    }

    [Test]
    public async Task AChatMovedIntoANewWorkspaceShowsUpThereRightAway()
    {
        var chat = await StoreChatAsync(Guid.Empty, "Meeting notes");

        // As in the dialog for moving a chat, which can create the workspace it moves the chat into:
        var targetResult = await WorkspaceBehaviour.TryCreateWorkspaceAsync("Projects");
        await WorkspaceBehaviour.MoveChatAsync(chat, targetResult.Workspace.WorkspaceId);

        var target = await GetWorkspaceAsync(targetResult.Workspace.WorkspaceId);
        Assert.Multiple(() =>
        {
            Assert.That(target.ChatsLoaded, Is.True, "A workspace created just now has no chats on the disk, so there is nothing left to load.");
            Assert.That(target.Chats.Select(entry => entry.ChatId), Does.Contain(chat.ChatId), "Otherwise, the user has to click the new workspace before the chat shows up.");
        });
    }

    [Test]
    public async Task AWorkspaceUnknownToTheCacheShowsUpAfterTheMove()
    {
        var chat = await StoreChatAsync(Guid.Empty, "Meeting notes");
        var targetId = Guid.NewGuid();

        await WorkspaceBehaviour.MoveChatAsync(chat, targetId);

        var snapshot = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
        var chats = await WorkspaceBehaviour.GetWorkspaceChatsAsync(targetId);
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Workspaces.Select(workspace => workspace.WorkspaceId), Does.Contain(targetId), "The move created the workspace directory, so the list has to show it.");
            Assert.That(chats.Select(entry => entry.ChatId), Does.Contain(chat.ChatId));
        });
    }

    /// <summary>
    /// Creates a workspace the way the app finds it after a start, with its chats loaded.
    /// </summary>
    /// <remarks>
    /// The app reads the workspaces first and their chats afterward, one workspace after the other.
    /// Reloading the cache and then loading the chats of the workspace mimics that.
    /// </remarks>
    /// <param name="name">The name of the workspace.</param>
    /// <returns>The ID of the workspace.</returns>
    private static async Task<Guid> CreateLoadedWorkspaceAsync(string name)
    {
        var result = await WorkspaceBehaviour.TryCreateWorkspaceAsync(name);
        Assert.That(result.Success, Is.True, $"The workspace '{name}' could not be created.");

        await WorkspaceBehaviour.ForceReloadWorkspaceTreeAsync();
        await WorkspaceBehaviour.TryPrefetchRemainingChatsAsync();
        return result.Workspace.WorkspaceId;
    }

    private static async Task<ChatThread> StoreChatAsync(Guid workspaceId, string name)
    {
        var chat = new ChatThread { WorkspaceId = workspaceId, ChatId = Guid.NewGuid(), Name = name };
        Assert.That(await WorkspaceBehaviour.StoreChatAsync(chat), Is.True, $"The chat '{name}' could not be stored.");
        return chat;
    }

    private static async Task<WorkspaceTreeWorkspace> GetWorkspaceAsync(Guid workspaceId)
    {
        var snapshot = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
        return snapshot.Workspaces.Single(workspace => workspace.WorkspaceId == workspaceId);
    }
}