using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks where the transcripts of a chat point after the chat moved into a workspace.
/// </summary>
/// <remarks>
/// Moving a chat moves its whole directory, transcripts included. Every reference to a transcript
/// therefore needs its new path: the ones of the sent messages, and the ones of the message the user
/// is still writing. A reference left behind points at a directory which no longer exists, and the
/// transcript silently drops out of the message.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class WorkspaceChatMoveTests
{
    private static readonly DateTimeOffset START = new(2026, 10, 11, 10, 0, 0, TimeSpan.Zero);

    private string? previousDataDirectory;
    private string testDirectory = string.Empty;

    [SetUp]
    public void PrepareDataDirectory()
    {
        //
        // The data directory is static state of the whole application, which is why this fixture
        // does not run alongside others. The chats live below it, and so do their transcripts.
        //
        this.previousDataDirectory = SettingsManager.DataDirectory;
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-chat-move-{Guid.NewGuid():N}");
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
    public async Task TheTranscriptsOfTheDraftMoveAlongWithTheChat()
    {
        var chatId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var sent = this.CreateTranscript(chatId, "kickoff", 1);
        var draft = this.CreateTranscript(chatId, "meeting", 2);
        var chat = new ChatThread
        {
            ChatId = chatId,
            Name = "Meeting notes",
            PendingMediaTranscripts = [draft],
            Blocks =
            [
                new ContentBlock
                {
                    Time = START,
                    ContentType = ContentType.TEXT,
                    Content = new ContentText { Text = "Summarize the kickoff", FileAttachments = [sent] },
                    Role = ChatRole.USER,
                },
            ],
        };

        await WorkspaceBehaviour.MoveChatAsync(chat, workspaceId);

        var targetDirectory = Path.Combine(this.testDirectory, "workspaces", workspaceId.ToString(), chatId.ToString());
        var movedDraft = chat.PendingMediaTranscripts.Single();
        var movedSent = ((ContentText)chat.Blocks.Single().Content!).FileAttachments.Single();
        var stored = JsonSerializer.Deserialize<ChatThread>(await File.ReadAllTextAsync(Path.Combine(targetDirectory, "thread.json")), WorkspaceBehaviour.JSON_OPTIONS)!;

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(draft.FilePath), Is.False, "The move takes the whole chat directory along, so nothing stays at the old path.");
            Assert.That(movedDraft.FilePath, Does.StartWith(targetDirectory), "The draft has to point into the directory the chat lives in now.");
            Assert.That(File.Exists(movedDraft.FilePath), Is.True, "Otherwise the transcript drops out of the message which is still being written.");
            Assert.That(File.Exists(movedSent.FilePath), Is.True, "The transcripts of the sent messages move along, as they always did.");
            Assert.That(stored.PendingMediaTranscripts.Single().FilePath, Is.EqualTo(movedDraft.FilePath), "Opening the chat again brings the draft back from the stored chat, so that has to know the new path as well.");
        });
    }

    [Test]
    public async Task ATranscriptOutsideTheChatDirectoryKeepsItsPath()
    {
        var chatId = Guid.NewGuid();
        Directory.CreateDirectory(Path.Combine(this.testDirectory, "tempChats", chatId.ToString()));

        // A transcript which is still waiting in the staging area, because its chat was not stored yet when it was made:
        var stagingDirectory = Path.Combine(this.testDirectory, "media-staging", Guid.NewGuid().ToString("N"));
        var staged = WriteTranscript(stagingDirectory, $"{Guid.NewGuid():N}.md", "meeting", isStaged: true);
        var chat = new ChatThread { ChatId = chatId, Name = "Meeting notes", PendingMediaTranscripts = [staged] };

        await WorkspaceBehaviour.MoveChatAsync(chat, Guid.NewGuid());

        Assert.That(chat.PendingMediaTranscripts.Single().FilePath, Is.EqualTo(staged.FilePath), "The move takes only the chat directory along, so a path elsewhere is still right.");
    }

    /// <summary>
    /// Writes a transcript where a temporary chat keeps its transcripts.
    /// </summary>
    /// <param name="chatId">The chat which owns the transcript.</param>
    /// <param name="stem">The name of the media file, without its extension.</param>
    /// <param name="number">The number of the transcript within the chat.</param>
    /// <returns>The transcript, as the chat attaches it to a message.</returns>
    private ManagedTranscriptAttachment CreateTranscript(Guid chatId, string stem, int number)
    {
        var transcriptDirectory = Path.Combine(this.testDirectory, "tempChats", chatId.ToString(), "attachments", "transcripts");
        return WriteTranscript(transcriptDirectory, $"{stem}-transcript-{number:D4}.md", stem, isStaged: false);
    }

    private static ManagedTranscriptAttachment WriteTranscript(string directory, string fileName, string stem, bool isStaged)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, $"# Transcription: {stem}.mp4{Environment.NewLine}{Environment.NewLine}Hello and welcome.");
        return new ManagedTranscriptAttachment(fileName, path, new FileInfo(path).Length, $"{stem}.mp4", isStaged);
    }
}