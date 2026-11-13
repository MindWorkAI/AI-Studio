using AIStudio.Chat;
using AIStudio.Settings;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks that discarding a chat which lives in memory takes the transcripts along which only it knows.
/// </summary>
/// <remarks>
/// A chat in memory keeps the transcripts of its audio and video files in the staging area. When the
/// chat is gone for good, e.g., since the user started a new chat in a place which never stores its
/// chats, nothing of it should stay on the disk. The files of the user stay where they are.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ChatsInMemoryDiscardTests
{
    private string? previousDataDirectory;
    private string testDirectory = string.Empty;

    [SetUp]
    public void PrepareDataDirectory()
    {
        // The data directory is static state of the whole application, which is why this fixture does not run alongside others:
        this.previousDataDirectory = SettingsManager.DataDirectory;
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-chats-in-memory-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testDirectory);
        SettingsManager.DataDirectory = this.testDirectory;
    }

    [TearDown]
    public void RemoveDataDirectory()
    {
        SettingsManager.DataDirectory = this.previousDataDirectory;
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
    public async Task DiscardingAChatDeletesTheTranscriptsOnlyItKnows()
    {
        var transcriptOfTheMessage = await ManagedTranscriptAttachment.CreateStagedAsync(Path.Combine(this.testDirectory, "kickoff.mp4"), "Welcome to the kickoff.");
        var transcriptOfTheDraft = await ManagedTranscriptAttachment.CreateStagedAsync(Path.Combine(this.testDirectory, "meeting.mp4"), "Hello and welcome.");
        var fileOfTheUser = Path.Combine(this.testDirectory, "notes.txt");
        await File.WriteAllTextAsync(fileOfTheUser, "Notes of the user");

        var chat = new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = Guid.Empty };
        chat.Blocks.Add(new ContentBlock
        {
            Time = DateTimeOffset.Now,
            ContentType = ContentType.TEXT,
            Role = ChatRole.USER,
            Content = new ContentText { Text = "Summarize the kickoff", FileAttachments = [transcriptOfTheMessage] },
        });
        chat.Draft.AddAttachments([transcriptOfTheDraft, FileAttachment.FromPath(fileOfTheUser)]);

        var chatsInMemory = new ChatsInMemory();
        chatsInMemory.Keep(chat);
        chatsInMemory.Discard(chat);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chatsInMemory.Find(Guid.Empty), Is.Null, "The chat is gone from memory.");
            Assert.That(File.Exists(transcriptOfTheMessage.FilePath), Is.False, "The transcript of a sent message belongs to the chat alone.");
            Assert.That(File.Exists(transcriptOfTheDraft.FilePath), Is.False, "So does the transcript of the message being written.");
            Assert.That(File.Exists(fileOfTheUser), Is.True, "A file of the user is no part of the chat and stays where it is.");
        }
    }
}