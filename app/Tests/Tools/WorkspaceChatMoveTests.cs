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
        chat.Draft.AddAttachments([draft]);

        await WorkspaceBehaviour.MoveChatAsync(chat, workspaceId, storeUnsavedChanges: true);

        var targetDirectory = Path.Combine(this.testDirectory, "workspaces", workspaceId.ToString(), chatId.ToString());
        var movedDraft = chat.Draft.MediaTranscripts.Single();
        var movedSent = ((ContentText)chat.Blocks.Single().Content!).FileAttachments.Single();
        var stored = JsonSerializer.Deserialize<ChatThread>(await File.ReadAllTextAsync(Path.Combine(targetDirectory, "thread.json")), WorkspaceBehaviour.JSON_OPTIONS)!;

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(draft.FilePath), Is.False, "The move takes the whole chat directory along, so nothing stays at the old path.");
            Assert.That(movedDraft.FilePath, Does.StartWith(targetDirectory), "The draft has to point into the directory the chat lives in now.");
            Assert.That(File.Exists(movedDraft.FilePath), Is.True, "Otherwise the transcript drops out of the message which is still being written.");
            Assert.That(File.Exists(movedSent.FilePath), Is.True, "The transcripts of the sent messages move along, as they always did.");
            Assert.That(stored.Draft.MediaTranscripts.Single().FilePath, Is.EqualTo(movedDraft.FilePath), "Opening the chat again brings the draft back from the stored chat, so that has to know the new path as well.");
        });
    }

    [Test]
    public async Task AFileOutsideTheChatDirectoryKeepsItsPath()
    {
        var chatId = Guid.NewGuid();
        Directory.CreateDirectory(Path.Combine(this.testDirectory, "tempChats", chatId.ToString()));

        var document = FileAttachment.FromPath(Path.Combine(this.testDirectory, "documents", "agenda.pdf"));
        var chat = new ChatThread { ChatId = chatId, Name = "Meeting notes" };
        chat.Draft.AddAttachments([document]);

        await WorkspaceBehaviour.MoveChatAsync(chat, Guid.NewGuid(), storeUnsavedChanges: true);

        Assert.That(chat.Draft.FileAttachments.Single(), Is.SameAs(document), "The move takes only the chat directory along, so a path elsewhere is still right.");
    }

    [Test]
    public async Task TheComposerOfTheOpenChatFollowsTheMove()
    {
        var chatId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var transcript = this.CreateTranscript(chatId, "meeting", 1);
        var document = FileAttachment.FromPath(Path.Combine(this.testDirectory, "documents", "agenda.pdf"));
        var chat = new ChatThread { ChatId = chatId, Name = "Meeting notes" };
        chat.Draft.AddAttachments([transcript]);

        await WorkspaceBehaviour.MoveChatAsync(chat, workspaceId, storeUnsavedChanges: true);

        // What the composer of the open chat holds: the transcript of the draft, and a document from elsewhere:
        var relocatedTranscript = WorkspaceBehaviour.RelocateAttachmentAfterMove(transcript, chatId, Guid.Empty, workspaceId);
        var relocatedDocument = WorkspaceBehaviour.RelocateAttachmentAfterMove(document, chatId, Guid.Empty, workspaceId);

        Assert.Multiple(() =>
        {
            Assert.That(relocatedTranscript, Is.EqualTo(chat.Draft.MediaTranscripts.Single()), "The composer and the draft have to agree, or the composer shows the transcript twice.");
            Assert.That(relocatedTranscript.Exists, Is.True, "The message being written has to go out with the transcript.");
            Assert.That(relocatedTranscript, Is.InstanceOf<ManagedTranscriptAttachment>(), "The chat still manages the transcript, so removing it from the composer still deletes the file.");
            Assert.That(relocatedDocument, Is.SameAs(document), "The move did not touch the document, so it keeps its path.");
        });
    }

    [Test]
    public async Task AChatStoredByHandMovesAsItWasSavedLast()
    {
        var chatId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var sent = this.CreateTranscript(chatId, "kickoff", 1);

        // What the user saved last:
        var saved = new ChatThread { ChatId = chatId, Name = "Meeting notes", Blocks = [UserMessage("Summarize the kickoff", sent)] };
        await File.WriteAllTextAsync(Path.Combine(this.testDirectory, "tempChats", chatId.ToString(), "thread.json"), JsonSerializer.Serialize(saved, WorkspaceBehaviour.JSON_OPTIONS));

        // The open chat, with a message the user sent since, but did not save:
        var chat = new ChatThread { ChatId = chatId, Name = "Meeting notes", Blocks = [UserMessage("Summarize the kickoff", sent), UserMessage("And list the open questions")] };

        await WorkspaceBehaviour.MoveChatAsync(chat, workspaceId, storeUnsavedChanges: false);

        var targetDirectory = Path.Combine(this.testDirectory, "workspaces", workspaceId.ToString(), chatId.ToString());
        var stored = JsonSerializer.Deserialize<ChatThread>(await File.ReadAllTextAsync(Path.Combine(targetDirectory, "thread.json")), WorkspaceBehaviour.JSON_OPTIONS)!;
        var storedTranscript = ((ContentText)stored.Blocks.Single().Content!).FileAttachments.Single();
        var openTranscript = ((ContentText)chat.Blocks.First().Content!).FileAttachments.Single();

        Assert.Multiple(() =>
        {
            Assert.That(stored.Blocks, Has.Count.EqualTo(1), "Whoever stores chats manually decides with the save button what reaches the disk, and moving a chat is no saving.");
            Assert.That(stored.WorkspaceId, Is.EqualTo(workspaceId), "Opening the chat again has to find it in the workspace it was moved to.");
            Assert.That(storedTranscript.FilePath, Does.StartWith(targetDirectory), "The stored chat has to point at its transcripts in the new place.");
            Assert.That(chat.Blocks, Has.Count.EqualTo(2), "Nothing the user did not save may get lost by moving the chat; it stays unsaved in the open chat.");
            Assert.That(chat.WorkspaceId, Is.EqualTo(workspaceId), "The open chat follows the move, so saving it later stores it in the new place.");
            Assert.That(File.Exists(openTranscript.FilePath), Is.True, "The open chat has to point at its transcripts in the new place as well.");
        });
    }

    private static ContentBlock UserMessage(string text, params FileAttachment[] attachments) => new()
    {
        Time = START,
        ContentType = ContentType.TEXT,
        Content = new ContentText { Text = text, FileAttachments = [..attachments] },
        Role = ChatRole.USER,
    };

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
        Directory.CreateDirectory(transcriptDirectory);

        var fileName = $"{stem}-transcript-{number:D4}.md";
        var path = Path.Combine(transcriptDirectory, fileName);
        File.WriteAllText(path, $"# Transcription: {stem}.mp4{Environment.NewLine}{Environment.NewLine}Hello and welcome.");
        return new ManagedTranscriptAttachment(fileName, path, new FileInfo(path).Length, $"{stem}.mp4", false);
    }
}