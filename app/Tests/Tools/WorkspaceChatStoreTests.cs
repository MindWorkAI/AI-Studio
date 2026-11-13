using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks where the transcripts of a chat end up when the chat is stored.
/// </summary>
/// <remarks>
/// A transcript made before its chat was stored waits in the staging area, which every start of AI
/// Studio empties. Storing the chat therefore takes each such transcript into the chat directory: the
/// ones of the sent messages, and the ones of the message the user is still writing. Leaving a new
/// chat stores it together with that message, so a transcript left behind would be gone after the next
/// start, while the draft still pointed at it.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class WorkspaceChatStoreTests
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
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-chat-store-{Guid.NewGuid():N}");
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
    public async Task TheStagedTranscriptOfTheDraftMovesIntoTheChat()
    {
        var workspace = await WorkspaceBehaviour.TryCreateWorkspaceAsync("Projects");
        Assert.That(workspace.Success, Is.True, "The workspace could not be created.");

        // A new chat, which was not stored yet when the transcript of its draft was made:
        var chat = new ChatThread { WorkspaceId = workspace.Workspace.WorkspaceId, ChatId = Guid.NewGuid(), Name = "Meeting notes" };
        var staged = await ManagedTranscriptAttachment.CreateStagedAsync(Path.Combine(this.testDirectory, "meeting.mp4"), "Hello and welcome.");
        chat.Draft.AddAttachments([staged]);

        Assert.That(await WorkspaceBehaviour.StoreChatAsync(chat), Is.True, "The chat could not be stored.");

        var chatDirectory = WorkspaceBehaviour.GetChatDirectory(chat.WorkspaceId, chat.ChatId);
        var transcript = chat.Draft.MediaTranscripts.Single();
        var stored = JsonSerializer.Deserialize<ChatThread>(await File.ReadAllTextAsync(Path.Combine(chatDirectory, "thread.json")), WorkspaceBehaviour.JSON_OPTIONS)!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(transcript.FilePath, Does.StartWith(chatDirectory), "The next start empties the staging area, but leaves the chat directory alone.");
            Assert.That(File.Exists(transcript.FilePath), Is.True, "Otherwise the transcript drops out of the message which is still being written.");
            Assert.That(transcript.IsStaged, Is.False, "The chat owns the transcript now, so deleting the chat deletes its file as well.");
            Assert.That(File.Exists(staged.FilePath), Is.False, "The transcript moves, so there is only one file to own.");
            Assert.That(stored.Draft.MediaTranscripts.Single().FilePath, Is.EqualTo(transcript.FilePath), "Opening the chat again brings the draft back from the stored chat, so that has to know the new path as well.");
        }
    }
}