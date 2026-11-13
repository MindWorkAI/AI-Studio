using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how the new chat of a place is stored and found again, see ChatThread.IsNewChat.
/// </summary>
/// <remarks>
/// Where chats are stored automatically, the new chat of a place lies on the disk like any other chat,
/// marked as new, so that starting a new chat there opens it again, also after a restart. The list
/// shows it first among the chats of its place, and its first message makes it a chat like any other.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class WorkspaceNewChatStoreTests
{
    private string? previousDataDirectory;
    private string testDirectory = string.Empty;

    [SetUp]
    public async Task PrepareDataDirectory()
    {
        //
        // The data directory and the cache of the workspace tree are static state of the whole
        // application, see WorkspaceChatStoreTests, which is why this fixture does not run alongside others:
        //
        this.previousDataDirectory = SettingsManager.DataDirectory;
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-new-chat-store-{Guid.NewGuid():N}");
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
    public async Task AStoredNewChatIsTheNewChatOfItsWorkspace()
    {
        var workspaceId = await CreateWorkspaceAsync("Projects");
        var newChat = new ChatThread { WorkspaceId = workspaceId, ChatId = Guid.NewGuid(), Name = "Summarize the minutes", IsNewChat = true };

        Assert.That(await WorkspaceBehaviour.StoreChatAsync(newChat), Is.True, "The chat could not be stored.");
        var found = await WorkspaceBehaviour.FindStoredNewChatAsync(workspaceId);
        var listed = (await WorkspaceBehaviour.GetWorkspaceChatsAsync(workspaceId)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.EqualTo(new LoadChat(workspaceId, newChat.ChatId)), "Starting a new chat in the workspace opens this one again.");
            Assert.That(listed.IsNewChat, Is.True, "The list shows it as the new chat of its workspace.");
        }
    }

    [Test]
    public async Task AStoredNewChatOutsideOfWorkspacesIsFoundAsWell()
    {
        var newChat = new ChatThread { WorkspaceId = Guid.Empty, ChatId = Guid.NewGuid(), Name = "Quick question", IsNewChat = true };

        Assert.That(await WorkspaceBehaviour.StoreChatAsync(newChat), Is.True, "The chat could not be stored.");
        var found = await WorkspaceBehaviour.FindStoredNewChatAsync(Guid.Empty);

        Assert.That(found, Is.EqualTo(new LoadChat(Guid.Empty, newChat.ChatId)), "The chats outside of workspaces have their new chat as well.");
    }

    [Test]
    public async Task TheMarkIsStillThereWhenTheListIsReadFromTheDiskAgain()
    {
        var workspaceId = await CreateWorkspaceAsync("Projects");
        var newChat = new ChatThread { WorkspaceId = workspaceId, ChatId = Guid.NewGuid(), Name = "Summarize the minutes", IsNewChat = true };
        await WorkspaceBehaviour.StoreChatAsync(newChat);

        // As after a restart:
        await WorkspaceBehaviour.ForceReloadWorkspaceTreeAsync();
        var listed = (await WorkspaceBehaviour.GetWorkspaceChatsAsync(workspaceId, forceRefresh: true)).Single();
        var loaded = await WorkspaceBehaviour.LoadChatAsync(new LoadChat(workspaceId, newChat.ChatId));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(listed.IsNewChat, Is.True, "The list reads the mark without reading the thread.");
            Assert.That(loaded?.IsNewChat, Is.True, "The thread itself knows it as well.");
        }
    }

    [Test]
    public async Task OnceSentTheChatIsNoNewChatAnyLonger()
    {
        var workspaceId = await CreateWorkspaceAsync("Projects");
        var chat = new ChatThread { WorkspaceId = workspaceId, ChatId = Guid.NewGuid(), Name = "Summarize the minutes", IsNewChat = true };
        await WorkspaceBehaviour.StoreChatAsync(chat);

        chat.IsNewChat = false;
        await WorkspaceBehaviour.StoreChatAsync(chat);
        var found = await WorkspaceBehaviour.FindStoredNewChatAsync(workspaceId);

        await WorkspaceBehaviour.ForceReloadWorkspaceTreeAsync();
        var listed = (await WorkspaceBehaviour.GetWorkspaceChatsAsync(workspaceId, forceRefresh: true)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.Null, "The next new chat of the workspace starts empty.");
            Assert.That(listed.IsNewChat, Is.False, "The mark has to leave the disk as well.");
        }
    }

    [Test]
    public void TheNewChatComesFirstInTheList()
    {
        var older = new WorkspaceTreeChat(Guid.Empty, Guid.NewGuid(), "a", "Older", DateTimeOffset.Now.AddDays(-2), IsTemporary: true);
        var newChat = new WorkspaceTreeChat(Guid.Empty, Guid.NewGuid(), "b", "New", DateTimeOffset.Now.AddDays(-1), IsTemporary: true, IsNewChat: true);
        var newest = new WorkspaceTreeChat(Guid.Empty, Guid.NewGuid(), "c", "Newest", DateTimeOffset.Now, IsTemporary: true);

        var ordered = WorkspaceTreeChat.InListOrder([older, newest, newChat]);

        Assert.That(ordered, Is.EqualTo(new[] { newChat, newest, older }), "The new chat stays on top while the user types, and the other chats follow by their last change.");
    }

    [Test]
    public async Task AWorkspaceIsFoundByItsNameWithoutCreatingOne()
    {
        var workspaceId = await CreateWorkspaceAsync("Projects");

        var found = await WorkspaceBehaviour.FindWorkspaceIdByNameAsync("  projects ");
        var missing = await WorkspaceBehaviour.FindWorkspaceIdByNameAsync("Board");
        var workspaceCount = (await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync()).Workspaces.Count;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.EqualTo(workspaceId), "Names are compared without blanks around them and regardless of case.");
            Assert.That(missing, Is.EqualTo(Guid.Empty));
            Assert.That(workspaceCount, Is.EqualTo(1), "Looking for the new chat of a template's workspace must not create the workspace.");
        }
    }

    [Test]
    public async Task ANewChatTakenOffTheDiskKeepsTheTranscriptsOfItsMessage()
    {
        //
        // A new chat which moves into a place that never stores chats leaves the disk, while it lives
        // on in memory. Storing it took its transcript into its directory, which goes:
        //
        var workspaceId = await CreateWorkspaceAsync("Projects");
        var newChat = new ChatThread { WorkspaceId = workspaceId, ChatId = Guid.NewGuid(), Name = "Transcription: kickoff.mp4", IsNewChat = true };
        newChat.Draft.AddAttachments([await ManagedTranscriptAttachment.CreateStagedAsync("/media/kickoff.mp4", "We start on Monday.")]);
        await WorkspaceBehaviour.StoreChatAsync(newChat);
        var storedTranscript = newChat.Draft.MediaTranscripts.Single();

        var isOffTheDisk = await WorkspaceBehaviour.TakeChatOffDiskAsync(newChat);
        var transcript = newChat.Draft.MediaTranscripts.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(storedTranscript.IsStaged, Is.False, "Storing took the transcript into the directory of the chat.");
            Assert.That(isOffTheDisk, Is.True);
            Assert.That(WorkspaceBehaviour.IsChatExisting(new LoadChat(workspaceId, newChat.ChatId)), Is.False, "A place which never stores keeps nothing on the disk.");
            Assert.That(transcript.IsStaged, Is.True, "The transcript waits in the staging area, from where storing the chat again would take it.");
            Assert.That(File.ReadAllText(transcript.FilePath), Does.Contain("We start on Monday."), "A transcript took a while and may have cost money, so it must not go with the directory.");
        }
    }

    private static async Task<Guid> CreateWorkspaceAsync(string name)
    {
        var result = await WorkspaceBehaviour.TryCreateWorkspaceAsync(name);
        Assert.That(result.Success, Is.True, "The workspace could not be created.");
        return result.Workspace.WorkspaceId;
    }
}